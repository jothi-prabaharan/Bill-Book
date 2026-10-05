using Master.Api.Controllers;
using Master.Entity.TableEntities;
using Master.Repository;
using Master.Repository.Repositories;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Shared.Kernel.Contacts;
using Shared.Kernel.Tenancy;
using Xunit;

namespace Master.Api.Tests;

/// <summary>The branch's walk-in customer for another service (TK-148).</summary>
[Collection(nameof(PostgresCollection))]
public sealed class InternalWalkInContactTests
{
    private readonly PostgresFixture _postgres;

    public InternalWalkInContactTests(PostgresFixture postgres) => _postgres = postgres;

    private async Task<IActionResult> AskAsync(Guid customerId, Guid orgId)
    {
        var services = new ServiceCollection();
        var tenant = new TenantContext();
        services.AddScoped(_ => _postgres.CreateContext(tenant));
        await using ServiceProvider provider = services.BuildServiceProvider();
        return await new InternalContactLookupController(tenant, provider).WalkIn(new ContactLookupRequest { CustomerId = customerId, OrgId = orgId }, default);
    }

    [SkippableFact]
    public async Task The_walk_in_customer_is_found_by_its_reserved_code()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);
        Guid customerId = Guid.NewGuid(), orgId = Guid.NewGuid();

        long walkInId;
        await using (ContactsDbContext db = _postgres.CreateContext(customerId, orgId))
        {
            db.Contacts.Add(new Contact { ContactCode = "C001", DisplayName = "Someone", CurrencyCode = "INR", IsCustomer = true });
            Contact walkIn = new() { ContactCode = ContactRepository.WalkInCode, DisplayName = "Walk-in Customer", CurrencyCode = "INR", IsCustomer = true };
            db.Contacts.Add(walkIn);
            await db.SaveChangesAsync();
            walkInId = walkIn.ContactId;
        }

        WalkInContactResponse found = Assert.IsType<WalkInContactResponse>(Assert.IsType<OkObjectResult>(await AskAsync(customerId, orgId)).Value);
        Assert.Equal(walkInId, found.ContactId);
    }

    [SkippableFact]
    public async Task A_branch_without_one_answers_not_found()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        Assert.IsType<NotFoundResult>(await AskAsync(Guid.NewGuid(), Guid.NewGuid()));
    }
}
