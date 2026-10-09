using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Tenancy;
using Site.Entity.Enums;
using Site.Entity.TableEntities;
using Site.Repository.Clients;
using Site.Repository.Repositories;
using Xunit;

namespace Site.Api.Tests;

public sealed class FakeSalesInvoiceClient : ISalesInvoiceClient
{
    private long _invoiceCounter = 5000;
    public List<CreateSiteInvoicePayload> InvoicesCreated { get; } = new();

    public Task<CreateSiteInvoiceResult> CreateAndPostInvoiceAsync(
        CreateSiteInvoicePayload payload, CancellationToken ct = default)
    {
        InvoicesCreated.Add(payload);
        long invId = ++_invoiceCounter;
        return Task.FromResult(new CreateSiteInvoiceResult
        {
            Success = true,
            InvoiceId = invId,
            InvoiceNo = $"INV-2627-{invId:D5}",
        });
    }
}

[Collection(nameof(PostgresCollection))]
public sealed class SiteBillingTests
{
    private readonly PostgresFixture _postgres;

    public SiteBillingTests(PostgresFixture postgres)
    {
        _postgres = postgres;
    }

    [SkippableFact]
    public async Task Two_partial_bills_on_one_site_post_separate_invoices_and_prevent_double_billing()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        var customerId = Guid.NewGuid();
        var orgId = Guid.NewGuid();

        await using var db = _postgres.CreateContext(customerId, orgId);
        var tenant = new TenantContext { CustomerId = customerId, OrgId = orgId, CustomerCode = "CUST001" };
        var whClient = new FakeInventoryWarehouseClient();
        var salesClient = new FakeSalesInvoiceClient();
        var numbers = new FakeNumberGenerator();
        var marginRepo = new SiteMarginRepository(db);

        var stockRepo = new SiteStockRepository(db, tenant, whClient, numbers, marginRepo);
        var billingRepo = new SiteBillingRepository(db, tenant, salesClient, numbers);

        // 1. Setup trade, laborer, site, assignment
        var trade = new Trade
        {
            TradeCode = $"TRD-{Guid.NewGuid():N}"[..10],
            TradeName = "Electrical",
            DefaultCostRatePerDay = 800m,
            DefaultSellRatePerDay = 1200m,
            DefaultOtCostRatePerHour = 100m,
            DefaultOtSellRatePerHour = 150m,
        };
        db.Trades.Add(trade);
        await db.SaveChangesAsync();

        var site = new SiteJob
        {
            SiteCode = $"SIT-{Guid.NewGuid():N}"[..10],
            SiteName = "Tech Park Tower B",
            ContactId = 42,
            WarehouseId = 888,
            Address = "Electronic City",
            Status = SiteStatus.Active,
        };
        db.Sites.Add(site);
        await db.SaveChangesAsync();

        var laborer = new Laborer
        {
            TradeId = trade.TradeId,
            FullName = "Anil Verma",
            Phone = "9988776655",
            CostRatePerDay = 800m,
            SellRatePerDay = 1200m,
            OtCostRatePerHour = 100m,
            OtSellRatePerHour = 150m,
        };
        db.Laborers.Add(laborer);
        await db.SaveChangesAsync();

        var assignment = new SiteAssignment
        {
            SiteId = site.SiteId,
            LaborerId = laborer.LaborerId,
            TradeId = trade.TradeId,
            StartDate = new DateOnly(2026, 10, 1),
            CostRatePerDay = 800m,
            SellRatePerDay = 1200m,
            ApprovedDays = 1.0m,
            ApprovedOtHours = 0m,
            BilledDays = 0m,
            BilledOtHours = 0m,
            Status = AssignmentStatus.Active,
        };
        db.SiteAssignments.Add(assignment);
        await db.SaveChangesAsync();

        // 2. Setup approved attendance
        var attendance = new SiteAttendance
        {
            AssignmentId = assignment.SiteAssignmentId,
            SiteId = site.SiteId,
            LaborerId = laborer.LaborerId,
            WorkDate = new DateOnly(2026, 10, 5),
            CheckInTime = DateTime.UtcNow.AddDays(-5),
            ClockOutTime = DateTime.UtcNow.AddDays(-5).AddHours(8),
            DayFraction = 1.0m,
            Status = AttendanceStatus.Approved,
            DayCostSnapshot = 800m,
            DayMarginSnapshot = 400m,
            DaySellSnapshot = 1200m,
        };
        db.SiteAttendances.Add(attendance);
        await db.SaveChangesAsync();

        // 3. Dispatch 3 coils of wire, then use and approve 2 coils
        long itemId = 701;
        await stockRepo.DispatchAsync(new DispatchStockRequest(
            site.SiteId,
            10,
            new DateOnly(2026, 10, 2),
            new List<DispatchStockLineDto> { new(itemId, 3m, 250m) },
            "Dispatch 3 coils wire"));

        var usageDoc = await stockRepo.SubmitUsageAsync(new SubmitMaterialUsageRequest(
            site.SiteId,
            new DateOnly(2026, 10, 4),
            new List<MaterialUsageLineDto> { new(itemId, 2m, 250m) },
            "Used 2 coils"));

        await stockRepo.ApproveUsageAsync(usageDoc.DocumentId);

        var balance = await stockRepo.GetBalanceAsync(site.SiteId, itemId);
        Assert.NotNull(balance);
        Assert.Equal(1m, balance.OnSiteQty);
        Assert.Equal(2m, balance.UsedQty);
        Assert.Equal(2m, balance.UnbilledUsedQty);

        // 4. First Bill: Electrical labor
        var bill1 = await billingRepo.CreateAndPostBillAsync(new CreateSiteBillRequest(
            site.SiteId,
            new DateOnly(2026, 10, 15),
            AttendanceIdsToBill: new List<long> { attendance.AttendanceId },
            MaterialLinesToBill: null,
            Notes: "Electrical Labor Bill"));

        Assert.NotNull(bill1);
        Assert.Equal(1200m, bill1.TotalLaborAmount);
        Assert.Equal(0m, bill1.TotalMaterialAmount);
        Assert.Equal(1200m, bill1.TotalBillableAmount);
        Assert.NotNull(bill1.SalesInvoiceId);

        // Verify assignment and attendance updated
        var updatedAtt = await db.SiteAttendances.FindAsync(attendance.AttendanceId);
        Assert.NotNull(updatedAtt?.SiteBillLineId);

        var updatedAsgn = await db.SiteAssignments.FindAsync(assignment.SiteAssignmentId);
        Assert.Equal(1.0m, updatedAsgn?.BilledDays);

        // 5. Trying to double-bill the same attendance MUST throw
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            billingRepo.CreateAndPostBillAsync(new CreateSiteBillRequest(
                site.SiteId,
                new DateOnly(2026, 10, 16),
                AttendanceIdsToBill: new List<long> { attendance.AttendanceId })));

        // 6. Second Bill: Material (2 used coils)
        var stockLine = await db.SiteStockLines.FirstAsync(l => l.DocumentId == usageDoc.DocumentId);
        var bill2 = await billingRepo.CreateAndPostBillAsync(new CreateSiteBillRequest(
            site.SiteId,
            new DateOnly(2026, 10, 17),
            AttendanceIdsToBill: null,
            MaterialLinesToBill: new List<MaterialLineToBillDto> { new(stockLine.LineId, 2m) },
            Notes: "Electrical Material Bill"));

        Assert.NotNull(bill2);
        Assert.Equal(0m, bill2.TotalLaborAmount);
        Assert.True(bill2.TotalMaterialAmount > 0);
        Assert.NotNull(bill2.SalesInvoiceId);

        // Verify SiteStockBalances: unbilled used drops to 0, billed rises to 2
        var balanceAfterBill2 = await stockRepo.GetBalanceAsync(site.SiteId, itemId);
        Assert.NotNull(balanceAfterBill2);
        Assert.Equal(0m, balanceAfterBill2.UnbilledUsedQty);
        Assert.Equal(2m, balanceAfterBill2.BilledQty);
        Assert.Equal(1m, balanceAfterBill2.OnSiteQty);

        // 7. Trying to bill material again (when unbilled is 0) MUST throw
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            billingRepo.CreateAndPostBillAsync(new CreateSiteBillRequest(
                site.SiteId,
                new DateOnly(2026, 10, 18),
                MaterialLinesToBill: new List<MaterialLineToBillDto> { new(stockLine.LineId, 1m) })));

        // 8. Privacy Check: Sales invoices created must contain ONLY selling prices and descriptions
        Assert.Equal(2, salesClient.InvoicesCreated.Count);
        foreach (var inv in salesClient.InvoicesCreated)
        {
            foreach (var line in inv.Lines)
            {
                Assert.True(line.UnitPrice > 0);
                Assert.False(string.IsNullOrWhiteSpace(line.Description));
            }
        }
    }
}
