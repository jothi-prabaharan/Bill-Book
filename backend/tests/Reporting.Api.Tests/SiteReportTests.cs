using Reporting.Api.Services;
using Reporting.Repository.Repositories.Sources.Site;
using Reporting.Repository.ReadModels;
using Xunit;

namespace Reporting.Api.Tests;

/// <summary>
/// The site reports (TK-176), over lists.
///
/// <b>What this proves and what it does not.</b> It proves the arithmetic:
/// how attendances and material usages calculate margins and roll up. It does
/// not prove the queries translate, since LINQ to Objects will run anything, so
/// the reports still need running against a real <c>sit</c> schema.
/// </summary>
public sealed class SiteReportTests
{
    private static readonly DateOnly Day1 = new(2026, 10, 1);
    
    private static readonly List<SiteJobRead> Sites =
    [
        new() { SiteId = 1, SiteCode = "S1", SiteName = "Site 1" },
    ];

    private static readonly List<SiteTradeRead> Trades =
    [
        new() { TradeId = 1, TradeName = "Electrical" },
    ];

    private static readonly List<SiteLaborerRead> Laborers =
    [
        new() { LaborerId = 1, TradeId = 1, FullName = "Asha" },
    ];

    private static readonly List<ItemRead> Items =
    [
        new() { ItemId = 10, ItemName = "Wire", ItemCode = "W1" },
    ];

    [Fact]
    public void Attendance_rolls_up_days_and_approval_status()
    {
        List<SiteAttendanceRead> attendances =
        [
            new() { AttendanceId = 1, SiteId = 1, LaborerId = 1, WorkDate = Day1, DayFraction = 1m, Status = 3, OtHoursClaimed = 2m, OtHoursApproved = 1m },
            new() { AttendanceId = 2, SiteId = 1, LaborerId = 1, WorkDate = Day1.AddDays(1), DayFraction = 0.5m, Status = 2 }, // Submitted, not approved
        ];

        var rows = SiteReports.LaborerAttendance(
            attendances.AsQueryable(),
            Sites.AsQueryable(),
            Laborers.AsQueryable(),
            Trades.AsQueryable()).ToList();

        Assert.Equal(2, rows.Count);
        
        var approved = rows.Single(r => r.Id == 1);
        Assert.Equal(1m, approved.DaysClaimed);
        Assert.Equal(1m, approved.DaysApproved); // Status 3 -> Approved
        Assert.Equal("Approved", approved.ApprovalStatus);
        Assert.Equal(2m, approved.OtHoursClaimed);
        Assert.Equal(1m, approved.OtHoursApproved);

        var pending = rows.Single(r => r.Id == 2);
        Assert.Equal(0.5m, pending.DaysClaimed);
        Assert.Equal(0m, pending.DaysApproved); // Status 2 -> Not Approved
        Assert.Equal("Submitted", pending.ApprovalStatus);
    }

    [Fact]
    public void MaterialReconciliation_links_item_names()
    {
        List<SiteStockBalanceRead> balances =
        [
            new() { BalanceId = 1, SiteId = 1, ItemId = 10, DispatchedQty = 50m, UsedQty = 20m, ReturnedQty = 5m, OnSiteQty = 25m, UnbilledUsedQty = 5m, BilledQty = 15m },
        ];

        var rows = SiteReports.MaterialReconciliation(
            balances.AsQueryable(),
            Sites.AsQueryable(),
            Items.AsQueryable()).ToList();

        Assert.Single(rows);
        var row = rows.Single();
        Assert.Equal("Wire", row.ItemName);
        Assert.Equal("Site 1", row.SiteName);
        Assert.Equal(50m, row.DispatchedQty);
        Assert.Equal(20m, row.UsedQty);
        Assert.Equal(15m, row.BilledQty);
    }

    [Fact]
    public void ProfitSummary_computes_cost_from_snapshots()
    {
        List<SiteBillingInvoiceRead> bills =
        [
            new() { SiteBillingInvoiceId = 1, SiteId = 1, BillingNumber = "SBL-1", BillingDate = Day1, TotalLaborAmount = 2000m, TotalMaterialAmount = 1500m, TotalBillableAmount = 3500m },
        ];

        List<SiteBillingInvoiceLineRead> billLines =
        [
            new() { SiteBillingInvoiceLineId = 1, SiteBillingInvoiceId = 1, AttendanceId = 1, Quantity = 1m },
            new() { SiteBillingInvoiceLineId = 2, SiteBillingInvoiceId = 1, UsageLineId = 1, Quantity = 10m },
        ];

        List<SiteAttendanceRead> attendances =
        [
            new() { AttendanceId = 1, DayFraction = 1m, DayCostSnapshot = 1000m, OtHoursApproved = 2m, OtCostSnapshot = 200m }, // Total Labor Cost: 1000 + 400 = 1400
        ];

        List<SiteStockLineRead> stockLines =
        [
            new() { LineId = 1, UnitCost = 100m }, // BillLine qty = 10. Total Material Cost = 10 * 100 = 1000
        ];

        var rows = SiteReports.ProfitSummary(
            bills.AsQueryable(),
            Sites.AsQueryable(),
            billLines.AsQueryable(),
            attendances.AsQueryable(),
            stockLines.AsQueryable()).ToList();

        Assert.Single(rows);
        var row = rows.Single();
        Assert.Equal("Site 1", row.SiteName);
        Assert.Equal("SBL-1", row.BillingNumber);
        
        Assert.Equal(1400m, row.LaborCost);
        Assert.Equal(2000m, row.LaborSell);
        Assert.Equal(600m, row.LaborMargin);

        Assert.Equal(1000m, row.MaterialCost);
        Assert.Equal(1500m, row.MaterialSell);
        Assert.Equal(500m, row.MaterialMargin);

        Assert.Equal(2400m, row.TotalCost);
        Assert.Equal(3500m, row.TotalSell);
        Assert.Equal(1100m, row.TotalProfit);
    }
}
