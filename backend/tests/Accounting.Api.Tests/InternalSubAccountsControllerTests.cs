using Accounting.Api.Controllers;
using Accounting.Api.Services;
using Accounting.Entity.Enums;
using Accounting.Entity.Models;
using Accounting.Repository;
using Accounting.Repository.SeedData;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shared.Kernel.Tenancy;
using Xunit;

namespace Accounting.Api.Tests;

/// <summary>
/// <c>POST internal/sub-accounts/provision</c> writes into the branch the request
/// names (TK-17).
///
/// Master seeds each branch's walk-in customer with no user token to forward,
/// and before TK-17 a token was the only way this route learned the branch — so
/// the walk-in's sub-ledger went nowhere and a walk-in sale could never post.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class InternalSubAccountsControllerTests
{
    private readonly PostgresFixture _postgres;

    public InternalSubAccountsControllerTests(PostgresFixture postgres) => _postgres = postgres;

    private async Task SeedChartAsync(Guid customerId, Guid orgId)
    {
        await using AccountingDbContext db = _postgres.CreateContext(customerId, orgId);
        db.Accounts.AddRange(ChartOfAccountsSeed.Build(orgId));
        await db.SaveChangesAsync();
    }

    private (InternalSubAccountsController Controller, ServiceProvider Services) Controller(TenantContext tenant)
    {
        var services = new ServiceCollection();
        services.AddSingleton(tenant);
        services.AddScoped(_ => _postgres.CreateContext(tenant));
        services.AddScoped<SubAccountService>();

        ServiceProvider provider = services.BuildServiceProvider();
        return (new InternalSubAccountsController(tenant, provider), provider);
    }

    private static ProvisionSubAccountsRequest Contact(long contactId, Guid customerId, Guid orgId) => new()
    {
        ReferenceType = SubAccountReferenceType.Contact,
        ReferenceId = contactId,
        Name = "Walk-in Customer",
        CustomerId = customerId,
        OrgId = orgId,
    };

    [SkippableFact]
    public async Task A_branch_named_in_the_body_gets_the_contacts_six_sub_accounts()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        Guid customerId = Guid.NewGuid();
        Guid orgId = Guid.NewGuid();
        await SeedChartAsync(customerId, orgId);

        (InternalSubAccountsController controller, ServiceProvider services) = Controller(new TenantContext());
        await using ServiceProvider _ = services;

        IActionResult result = await controller.Provision(Contact(901, customerId, orgId), CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);

        await using AccountingDbContext db = _postgres.CreateContext(customerId, orgId);
        Assert.Equal(6, await db.SubAccounts.CountAsync(
            s => s.ReferenceType == SubAccountReferenceType.Contact && s.ReferenceId == 901));
    }

    [Fact]
    public async Task No_branch_anywhere_is_a_400()
    {
        var controller = new InternalSubAccountsController(new TenantContext(), new ServiceCollection().BuildServiceProvider());

        IActionResult result = await controller.Provision(Contact(1, Guid.Empty, Guid.Empty), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task A_body_naming_another_branch_than_the_token_is_forbidden()
    {
        var tenant = new TenantContext { CustomerId = Guid.NewGuid(), OrgId = Guid.NewGuid() };
        var controller = new InternalSubAccountsController(tenant, new ServiceCollection().BuildServiceProvider());

        IActionResult result = await controller.Provision(
            Contact(1, tenant.CustomerId!.Value, Guid.NewGuid()), CancellationToken.None);

        Assert.IsType<ForbidResult>(result);
    }
}
