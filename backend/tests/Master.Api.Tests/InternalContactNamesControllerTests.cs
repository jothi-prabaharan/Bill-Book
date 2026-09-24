using Master.Api.Controllers;
using Master.Entity.TableEntities;
using Master.Repository;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Shared.Kernel.Documents;
using Shared.Kernel.Tenancy;
using Xunit;

namespace Master.Api.Tests;

/// <summary>
/// <c>POST internal/contacts/names</c> resolves names in the branch the request
/// names, or in the caller's own when it forwards a token.
///
/// Sales and Purchase send only the internal key, so before TK-06 the route read
/// <c>con.Contacts</c> with no tenant, and every document list showed ids where
/// the contact names should have been. Customer forwards the user's token as
/// well and sends no branch in the body; that path must keep working.
///
/// The context is registered the way the host builds it, from the request's
/// <see cref="TenantContext"/>, so the controller must set the tenant before it
/// resolves the context — which is the thing being tested.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class InternalContactNamesControllerTests
{
    private readonly PostgresFixture _postgres;

    public InternalContactNamesControllerTests(PostgresFixture postgres) => _postgres = postgres;

    private async Task<long> SeedContactAsync(Guid customerId, Guid orgId)
    {
        await using ContactsDbContext db = _postgres.CreateContext(customerId, orgId);

        var contact = new Contact
        {
            ContactCode = "C001",
            DisplayName = "Named over the wire",
            CurrencyCode = "INR",
            IsCustomer = true,
        };

        db.Contacts.Add(contact);
        await db.SaveChangesAsync(CancellationToken.None);
        return contact.ContactId;
    }

    private (InternalContactNamesController Controller, ServiceProvider Services) Controller(TenantContext tenant)
    {
        var services = new ServiceCollection();
        services.AddScoped(_ => _postgres.CreateContext(tenant));

        ServiceProvider provider = services.BuildServiceProvider();
        return (new InternalContactNamesController(tenant, provider), provider);
    }

    [SkippableFact]
    public async Task A_branch_named_in_the_body_gets_its_names()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        Guid customerId = Guid.NewGuid();
        Guid orgId = Guid.NewGuid();
        long contactId = await SeedContactAsync(customerId, orgId);

        (InternalContactNamesController controller, ServiceProvider services) = Controller(new TenantContext());
        await using ServiceProvider _ = services;

        IActionResult result = await controller.Names(
            new NameLookupRequest { Ids = [contactId], CustomerId = customerId, OrgId = orgId },
            CancellationToken.None);

        List<NamedRef> names = Assert.IsType<List<NamedRef>>(Assert.IsType<OkObjectResult>(result).Value);
        NamedRef name = Assert.Single(names);
        Assert.Equal(new NamedRef(contactId, "C001", "Named over the wire"), name);
    }

    [SkippableFact]
    public async Task A_forwarded_token_still_works_with_no_branch_in_the_body()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        Guid customerId = Guid.NewGuid();
        Guid orgId = Guid.NewGuid();
        long contactId = await SeedContactAsync(customerId, orgId);

        // What TenantMiddleware leaves behind when the caller forwards a token.
        (InternalContactNamesController controller, ServiceProvider services) =
            Controller(new TenantContext { CustomerId = customerId, OrgId = orgId });
        await using ServiceProvider _ = services;

        IActionResult result = await controller.Names(
            new NameLookupRequest { Ids = [contactId] }, CancellationToken.None);

        Assert.Single(Assert.IsType<List<NamedRef>>(Assert.IsType<OkObjectResult>(result).Value));
    }

    [SkippableFact]
    public async Task Another_branch_gets_nothing_back()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        Guid customerId = Guid.NewGuid();
        long contactId = await SeedContactAsync(customerId, Guid.NewGuid());

        (InternalContactNamesController controller, ServiceProvider services) = Controller(new TenantContext());
        await using ServiceProvider _ = services;

        IActionResult result = await controller.Names(
            new NameLookupRequest { Ids = [contactId], CustomerId = customerId, OrgId = Guid.NewGuid() },
            CancellationToken.None);

        Assert.Empty(Assert.IsType<List<NamedRef>>(Assert.IsType<OkObjectResult>(result).Value));
    }

    [Fact]
    public async Task No_branch_anywhere_is_a_400()
    {
        var controller = new InternalContactNamesController(
            new TenantContext(), new ServiceCollection().BuildServiceProvider());

        IActionResult result = await controller.Names(
            new NameLookupRequest { Ids = [1] }, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task A_body_that_disagrees_with_the_token_is_a_403()
    {
        var tenant = new TenantContext { CustomerId = Guid.NewGuid(), OrgId = Guid.NewGuid() };
        Guid tokenOrg = tenant.OrgId!.Value;
        var controller = new InternalContactNamesController(tenant, new ServiceCollection().BuildServiceProvider());

        IActionResult result = await controller.Names(
            new NameLookupRequest { Ids = [1], CustomerId = tenant.CustomerId!.Value, OrgId = Guid.NewGuid() },
            CancellationToken.None);

        Assert.IsType<ForbidResult>(result);
        Assert.Equal(tokenOrg, tenant.OrgId);
    }
}
