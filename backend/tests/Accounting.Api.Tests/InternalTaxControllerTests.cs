using Accounting.Api.Controllers;
using Accounting.Api.Services;
using Accounting.Entity.TableEntities;
using Accounting.Repository;
using Accounting.Repository.SeedData;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Shared.Kernel.Tax;
using Shared.Kernel.Tenancy;
using Xunit;

namespace Accounting.Api.Tests;

/// <summary>
/// <c>GET internal/tax/rates</c> answers for the branch the request names.
///
/// Sales and Purchase call it with the internal key and no token, so nothing
/// but the query can tell Accounting which branch is asking. Before TK-06 the
/// route read no branch at all, the query filter saw no tenant, and every
/// invoice resolved its GST against an empty list.
///
/// The context is registered the way the host builds it — from the request's
/// <see cref="TenantContext"/> — so the controller has to set the tenant before
/// it resolves anything, which is the thing being tested.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class InternalTaxControllerTests
{
    private static readonly DateOnly From = new(2026, 4, 1);

    private readonly PostgresFixture _postgres;

    public InternalTaxControllerTests(PostgresFixture postgres) => _postgres = postgres;

    private async Task SeedRatesAsync(Guid customerId, Guid orgId)
    {
        await using AccountingDbContext db = _postgres.CreateContext(customerId, orgId);

        List<TaxMaster> rates = [.. TaxMasterSeed.Build(orgId, From)];
        db.TaxMasters.AddRange(rates);
        await db.SaveChangesAsync();

        foreach (TaxMaster rate in rates)
        {
            rate.TaxGroupId = rate.TaxMasterId;
        }

        await db.SaveChangesAsync();
    }

    /// <summary>A controller over a fresh tenant, as one request would see it.</summary>
    private (InternalTaxController Controller, ServiceProvider Services) Controller()
    {
        var tenant = new TenantContext();

        var services = new ServiceCollection();
        services.AddSingleton(tenant);
        services.AddScoped(_ => _postgres.CreateContext(tenant));
        services.AddScoped<SubAccountService>();
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<TaxMasterService>();

        ServiceProvider provider = services.BuildServiceProvider();
        return (new InternalTaxController(tenant, provider), provider);
    }

    [SkippableFact]
    public async Task A_seeded_branch_gets_its_rates_back()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        Guid customerId = Guid.NewGuid();
        Guid orgId = Guid.NewGuid();
        await SeedRatesAsync(customerId, orgId);

        (InternalTaxController controller, ServiceProvider services) = Controller();
        await using ServiceProvider _ = services;

        IActionResult result = await controller.Rates(customerId, orgId, From.AddDays(10), CancellationToken.None);

        List<TaxRate> rates = Assert.IsType<List<TaxRate>>(Assert.IsType<OkObjectResult>(result).Value);
        Assert.Equal(TaxMasterSeed.Build(orgId, From).Count, rates.Count);
        Assert.Contains(rates, r => r.TaxSystemName == "GST18" && r.TotalRate == 18m && r.CgstRate == 9m);
    }

    [SkippableFact]
    public async Task Another_branch_sees_none_of_them()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        Guid customerId = Guid.NewGuid();
        await SeedRatesAsync(customerId, Guid.NewGuid());

        (InternalTaxController controller, ServiceProvider services) = Controller();
        await using ServiceProvider _ = services;

        IActionResult result = await controller.Rates(customerId, Guid.NewGuid(), From.AddDays(10), CancellationToken.None);

        Assert.Empty(Assert.IsType<List<TaxRate>>(Assert.IsType<OkObjectResult>(result).Value));
    }

    [Fact]
    public async Task A_request_that_names_no_branch_is_refused_rather_than_answered_empty()
    {
        var tenant = new TenantContext();
        var controller = new InternalTaxController(tenant, new ServiceCollection().BuildServiceProvider());

        IActionResult noCustomer = await controller.Rates(Guid.Empty, Guid.NewGuid(), From, CancellationToken.None);
        IActionResult noOrg = await controller.Rates(Guid.NewGuid(), Guid.Empty, From, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(noCustomer);
        Assert.IsType<BadRequestObjectResult>(noOrg);
        Assert.Null(tenant.OrgId);
    }
}
