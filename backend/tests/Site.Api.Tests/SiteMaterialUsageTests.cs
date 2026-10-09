using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Shared.Kernel.Tenancy;
using Site.Entity.Enums;
using Site.Entity.TableEntities;
using Site.Repository.Repositories;
using Xunit;

namespace Site.Api.Tests;

[Collection(nameof(PostgresCollection))]
public sealed class SiteMaterialUsageTests
{
    private readonly PostgresFixture _postgres;

    public SiteMaterialUsageTests(PostgresFixture postgres)
    {
        _postgres = postgres;
    }

    [SkippableFact]
    public async Task Material_usage_approval_updates_used_and_unbilled_quantities()
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
            SiteName = "Bridge Rehabilitation",
            Address = "NH 44",
            Status = SiteStatus.Active,
            WarehouseId = 601,
        };
        db.Sites.Add(site);
        await db.SaveChangesAsync();

        long centralWarehouseId = 10;
        long itemId = 301; // Cement bags

        // 2. Dispatch 3 bags to site
        await repo.DispatchAsync(new DispatchStockRequest(
            site.SiteId,
            centralWarehouseId,
            new DateOnly(2026, 10, 10),
            new List<DispatchStockLineDto> { new(itemId, 3m, 400m) },
            "Dispatch 3 bags cement"));

        var initialBalance = await repo.GetBalanceAsync(site.SiteId, itemId);
        Assert.NotNull(initialBalance);
        Assert.Equal(3m, initialBalance.OnSiteQty);
        Assert.Equal(0m, initialBalance.UsedQty);
        Assert.Equal(0m, initialBalance.UnbilledUsedQty);

        // 3. Submitting usage of 4 bags (more than 3 on site) MUST be refused
        var exSubmit = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            repo.SubmitUsageAsync(new SubmitMaterialUsageRequest(
                site.SiteId,
                new DateOnly(2026, 10, 11),
                new List<MaterialUsageLineDto> { new(itemId, 4m, 400m) },
                "Excessive usage")));

        Assert.Contains("Only 3", exSubmit.Message);

        // 4. Submit usage for 2 bags
        var usageDoc = await repo.SubmitUsageAsync(new SubmitMaterialUsageRequest(
            site.SiteId,
            new DateOnly(2026, 10, 11),
            new List<MaterialUsageLineDto> { new(itemId, 2m, 400m) },
            "Used 2 bags for pier reinforcement"));

        Assert.NotNull(usageDoc);
        Assert.Equal(SiteStockDocumentType.Usage, usageDoc.DocumentType);
        Assert.Equal(SiteStockDocumentStatus.Submitted, usageDoc.Status);

        // Before approval, on-site balance is still 3, used is 0
        var balanceBeforeApproval = await repo.GetBalanceAsync(site.SiteId, itemId);
        Assert.NotNull(balanceBeforeApproval);
        Assert.Equal(3m, balanceBeforeApproval.OnSiteQty);
        Assert.Equal(0m, balanceBeforeApproval.UsedQty);

        // 5. Admin approves usage
        var approvedDoc = await repo.ApproveUsageAsync(usageDoc.DocumentId);
        Assert.NotNull(approvedDoc);
        Assert.Equal(SiteStockDocumentStatus.Approved, approvedDoc.Status);

        // Done when: 3 dispatched and 2 used leaves 1 on site and 2 unbilled
        var balanceAfterApproval = await repo.GetBalanceAsync(site.SiteId, itemId);
        Assert.NotNull(balanceAfterApproval);
        Assert.Equal(1m, balanceAfterApproval.OnSiteQty);
        Assert.Equal(2m, balanceAfterApproval.UsedQty);
        Assert.Equal(2m, balanceAfterApproval.UnbilledUsedQty);
    }

    [SkippableFact]
    public async Task Material_usage_rejection_leaves_balances_untouched()
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

        var site = new SiteJob
        {
            SiteCode = $"SIT-{Guid.NewGuid():N}"[..10],
            SiteName = "Highway Lighting",
            Address = "Outer Ring Road",
            Status = SiteStatus.Active,
            WarehouseId = 602,
        };
        db.Sites.Add(site);
        await db.SaveChangesAsync();

        long centralWarehouseId = 10;
        long itemId = 302;

        await repo.DispatchAsync(new DispatchStockRequest(
            site.SiteId,
            centralWarehouseId,
            new DateOnly(2026, 10, 10),
            new List<DispatchStockLineDto> { new(itemId, 5m, 100m) },
            "Dispatch 5 items"));

        var usageDoc = await repo.SubmitUsageAsync(new SubmitMaterialUsageRequest(
            site.SiteId,
            new DateOnly(2026, 10, 11),
            new List<MaterialUsageLineDto> { new(itemId, 2m, 100m) },
            "Submitted erroneous usage"));

        // Reject usage
        var rejectedDoc = await repo.RejectUsageAsync(usageDoc.DocumentId, "Work cancelled by client");
        Assert.Equal(SiteStockDocumentStatus.Void, rejectedDoc.Status);
        Assert.Contains("Rejected: Work cancelled by client", rejectedDoc.Remarks ?? string.Empty);

        // Balances remain untouched
        var finalBalance = await repo.GetBalanceAsync(site.SiteId, itemId);
        Assert.NotNull(finalBalance);
        Assert.Equal(5m, finalBalance.OnSiteQty);
        Assert.Equal(0m, finalBalance.UsedQty);
        Assert.Equal(0m, finalBalance.UnbilledUsedQty);
    }
}
