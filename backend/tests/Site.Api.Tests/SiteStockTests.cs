using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Shared.Kernel.Numbering;
using Shared.Kernel.Tenancy;
using Site.Entity.Enums;
using Site.Entity.TableEntities;
using Site.Repository.Clients;
using Site.Repository.Repositories;
using Xunit;

namespace Site.Api.Tests;

public sealed class FakeInventoryWarehouseClient : IInventoryWarehouseClient
{
    public bool ShouldSucceed { get; set; } = true;
    public List<TransferStockPayload> Transfers { get; } = new();

    public Task<long?> CreateSiteWarehouseAsync(
        Guid customerId, Guid orgId, long siteId, string siteCode, string siteName, CancellationToken ct = default)
        => Task.FromResult<long?>(999);

    public Task<bool> TransferStockAsync(
        Guid customerId,
        Guid orgId,
        long fromWarehouseId,
        long toWarehouseId,
        long itemId,
        decimal quantity,
        string? notes = null,
        CancellationToken ct = default)
    {
        if (!ShouldSucceed) return Task.FromResult(false);

        Transfers.Add(new TransferStockPayload
        {
            CustomerId = customerId,
            OrgId = orgId,
            FromWarehouseId = fromWarehouseId,
            ToWarehouseId = toWarehouseId,
            ItemId = itemId,
            Quantity = quantity,
            Notes = notes,
        });

        return Task.FromResult(true);
    }
}

public sealed class FakeNumberGenerator : INumberGenerator
{
    private int _counter = 1;

    public Task<NumberAllocation> NextAsync(string seriesCode, DateOnly onDate, CancellationToken ct)
    {
        return Task.FromResult(new NumberAllocation(1, _counter, $"{seriesCode}-{_counter++:D4}"));
    }

    public Task<string?> PeekAsync(string seriesCode, DateOnly onDate, CancellationToken ct)
    {
        return Task.FromResult<string?>($"{seriesCode}-0001");
    }
}

[Collection(nameof(PostgresCollection))]
public sealed class SiteStockTests
{
    private readonly PostgresFixture _postgres;

    public SiteStockTests(PostgresFixture postgres)
    {
        _postgres = postgres;
    }

    [SkippableFact]
    public async Task Dispatch_and_return_updates_balances_and_refuses_excessive_return()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        var customerId = Guid.NewGuid();
        var orgId = Guid.NewGuid();

        await using var db = _postgres.CreateContext(customerId, orgId);
        var tenant = new TenantContext { CustomerId = customerId, OrgId = orgId, CustomerCode = "CUST001" };
        var client = new FakeInventoryWarehouseClient();
        var numbers = new FakeNumberGenerator();
        var marginRepo = new SiteMarginRepository(db);
        var repo = new SiteStockRepository(db, tenant, client, numbers, marginRepo);

        // 1. Create a site with linked site warehouse
        var site = new SiteJob
        {
            SiteCode = $"SIT-{Guid.NewGuid():N}"[..10],
            SiteName = "Metro Station Project",
            Address = "MG Road",
            Status = SiteStatus.Active,
            WarehouseId = 501, // Linked site warehouse
        };
        db.Sites.Add(site);
        await db.SaveChangesAsync();

        long centralWarehouseId = 10;
        long itemId = 205; // Coils

        // 2. Dispatch 3 coils from central warehouse to site
        var dispatchDoc = await repo.DispatchAsync(new DispatchStockRequest(
            site.SiteId,
            centralWarehouseId,
            new DateOnly(2026, 10, 10),
            new List<DispatchStockLineDto>
            {
                new(itemId, 3m, 500m)
            },
            "Initial dispatch for wiring"));

        Assert.NotNull(dispatchDoc);
        Assert.Equal(SiteStockDocumentType.Dispatch, dispatchDoc.DocumentType);

        var balanceAfterDispatch = await repo.GetBalanceAsync(site.SiteId, itemId);
        Assert.NotNull(balanceAfterDispatch);
        Assert.Equal(3m, balanceAfterDispatch.DispatchedQty);
        Assert.Equal(3m, balanceAfterDispatch.OnSiteQty);
        Assert.Equal(0m, balanceAfterDispatch.ReturnedQty);

        // 3. Return 1 coil back to central
        var returnDoc = await repo.ReturnAsync(new ReturnStockRequest(
            site.SiteId,
            centralWarehouseId,
            new DateOnly(2026, 10, 11),
            new List<ReturnStockLineDto>
            {
                new(itemId, 1m, 500m)
            },
            "Returned 1 excess coil"));

        Assert.NotNull(returnDoc);
        Assert.Equal(SiteStockDocumentType.Return, returnDoc.DocumentType);

        var balanceAfterReturn = await repo.GetBalanceAsync(site.SiteId, itemId);
        Assert.NotNull(balanceAfterReturn);
        Assert.Equal(3m, balanceAfterReturn.DispatchedQty);
        Assert.Equal(1m, balanceAfterReturn.ReturnedQty);
        Assert.Equal(2m, balanceAfterReturn.OnSiteQty);

        // 4. Returning 3 coils when only 2 are on site MUST be refused
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            repo.ReturnAsync(new ReturnStockRequest(
                site.SiteId,
                centralWarehouseId,
                new DateOnly(2026, 10, 12),
                new List<ReturnStockLineDto>
                {
                    new(itemId, 3m, 500m)
                },
                "Attempt to return more than is on site")));

        Assert.Contains("Only 2", ex.Message);

        // Verify balance was untouched by failed return
        var balanceFinal = await repo.GetBalanceAsync(site.SiteId, itemId);
        Assert.NotNull(balanceFinal);
        Assert.Equal(2m, balanceFinal.OnSiteQty);
        Assert.Equal(1m, balanceFinal.ReturnedQty);
    }
}
