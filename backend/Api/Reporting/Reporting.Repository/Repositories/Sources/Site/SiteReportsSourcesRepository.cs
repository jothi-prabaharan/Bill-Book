using System;
using System.Collections.Generic;
using System.Linq;
using Reporting.Entity.Enums;
using Reporting.Repository.ReadModels;
using Shared.Kernel.Apps;

namespace Reporting.Repository.Repositories.Sources.Site;

// -----------------------------------------------------------------------------
// Site reports (TK-176): laborer attendance, material reconciliation, profit.
// The queries are written as functions of IQueryables so they can be tested
// over lists.
// -----------------------------------------------------------------------------

public static class SiteReports
{
    public static IQueryable<SiteLaborerAttendanceRow> LaborerAttendance(
        IQueryable<SiteAttendanceRead> attendances,
        IQueryable<SiteJobRead> sites,
        IQueryable<SiteLaborerRead> laborers,
        IQueryable<SiteTradeRead> trades,
        DateOnly? start = null,
        DateOnly? end = null)
    {
        var att = attendances.Where(a =>
            (start == null || a.WorkDate >= start) &&
            (end == null || a.WorkDate <= end));

        return att
            .Join(sites, a => a.SiteId, s => s.SiteId, (a, s) => new { a, s })
            .Join(laborers, x => x.a.LaborerId, l => l.LaborerId, (x, l) => new { x.a, x.s, l })
            .Join(trades, x => x.l.TradeId, t => t.TradeId, (x, t) => new SiteLaborerAttendanceRow
            {
                Id = x.a.AttendanceId,
                SiteName = x.s.SiteName,
                TradeName = t.TradeName,
                LaborerName = x.l.FullName,
                WorkDate = x.a.WorkDate,
                DaysClaimed = x.a.DayFraction,
                DaysApproved = x.a.Status == 3 ? x.a.DayFraction : 0m,
                OtHoursClaimed = x.a.OtHoursClaimed,
                OtHoursApproved = x.a.OtHoursApproved,
                ApprovalStatus = x.a.Status == 1 ? "Checked In" :
                                 x.a.Status == 2 ? "Submitted" :
                                 x.a.Status == 3 ? "Approved" : "Rejected",
                BillingStatus = x.a.SiteBillLineId != null ? "Billed" : "Unbilled",
            });
    }

    public static IQueryable<SiteMaterialReconciliationRow> MaterialReconciliation(
        IQueryable<SiteStockBalanceRead> balances,
        IQueryable<SiteJobRead> sites,
        IQueryable<ItemRead> items,
        DateOnly? asOf = null)
    {
        return balances
            .Join(sites, b => b.SiteId, s => s.SiteId, (b, s) => new { b, s })
            .Join(items, x => x.b.ItemId, i => i.ItemId, (x, i) => new SiteMaterialReconciliationRow
            {
                Id = x.b.BalanceId,
                SiteName = x.s.SiteName,
                ItemName = i.ItemName,
                DispatchedQty = x.b.DispatchedQty,
                UsedQty = x.b.UsedQty,
                ReturnedQty = x.b.ReturnedQty,
                OnSiteQty = x.b.OnSiteQty,
                UnbilledQty = x.b.UnbilledUsedQty,
                BilledQty = x.b.BilledQty,
            });
    }

    public static IQueryable<SiteProfitSummaryRow> ProfitSummary(
        IQueryable<SiteBillingInvoiceRead> bills,
        IQueryable<SiteJobRead> sites,
        IQueryable<SiteBillingInvoiceLineRead> billLines,
        IQueryable<SiteAttendanceRead> attendances,
        IQueryable<SiteStockLineRead> stockLines,
        DateOnly? start = null,
        DateOnly? end = null)
    {
        var filteredBills = bills.Where(b =>
            (start == null || b.BillingDate >= start) &&
            (end == null || b.BillingDate <= end));

        var rawBills = filteredBills
            .Join(sites, b => b.SiteId, s => s.SiteId, (b, s) => new
            {
                b.SiteBillingInvoiceId,
                b.SiteId,
                s.SiteName,
                b.BillingNumber,
                b.BillingDate,
                b.TotalLaborAmount,
                b.TotalMaterialAmount,
                b.TotalBillableAmount,
            });

        return rawBills.Select(x => new
        {
            x.SiteBillingInvoiceId,
            x.SiteName,
            x.BillingNumber,
            x.BillingDate,
            x.TotalLaborAmount,
            x.TotalMaterialAmount,
            x.TotalBillableAmount,
            LaborCost = billLines
                .Where(l => l.SiteBillingInvoiceId == x.SiteBillingInvoiceId && l.AttendanceId != null)
                .Join(attendances, l => l.AttendanceId, a => a.AttendanceId, (l, a) => new { l, a })
                .Sum(la => (la.a.DayFraction * la.a.DayCostSnapshot) + (la.a.OtHoursApproved * la.a.OtCostSnapshot)),
            MaterialCost = billLines
                .Where(l => l.SiteBillingInvoiceId == x.SiteBillingInvoiceId && l.UsageLineId != null)
                .Join(stockLines, l => l.UsageLineId, sl => sl.LineId, (l, sl) => new { l, sl })
                .Sum(lsl => lsl.l.Quantity * (lsl.sl.UnitCost ?? 0m)),
        })
        .Select(x => new SiteProfitSummaryRow
        {
            Id = x.SiteBillingInvoiceId,
            SiteName = x.SiteName,
            BillingNumber = x.BillingNumber,
            BillingDate = x.BillingDate,
            LaborCost = x.LaborCost,
            LaborSell = x.TotalLaborAmount,
            LaborMargin = x.TotalLaborAmount - x.LaborCost,
            MaterialCost = x.MaterialCost,
            MaterialSell = x.TotalMaterialAmount,
            MaterialMargin = x.TotalMaterialAmount - x.MaterialCost,
            TotalCost = x.LaborCost + x.MaterialCost,
            TotalSell = x.TotalBillableAmount,
            TotalProfit = x.TotalBillableAmount - (x.LaborCost + x.MaterialCost),
        });
    }
}

// 1. Laborer Attendance Source
public class SiteLaborerAttendanceRow
{
    public long Id { get; set; }
    public string SiteName { get; set; } = null!;
    public string TradeName { get; set; } = null!;
    public string LaborerName { get; set; } = null!;
    public DateOnly WorkDate { get; set; }
    public decimal DaysClaimed { get; set; }
    public decimal DaysApproved { get; set; }
    public decimal OtHoursClaimed { get; set; }
    public decimal OtHoursApproved { get; set; }
    public string ApprovalStatus { get; set; } = null!;
    public string BillingStatus { get; set; } = null!;
}

public sealed class SiteLaborerAttendanceSource : ReportSource<SiteLaborerAttendanceRow>
{
    public override string ReportKey => "site-attendance";
    public override string Title => "Site Laborer Attendance";
    public override ReportModule Module => ReportModule.Site;
    public override AddOn RequiredAddOn => AddOn.SiteServices;
    public override string RequiredPermission => "site.view";

    public override IReadOnlyList<ReportParameterRepository> Parameters =>
    [
        new() { Name = "from", Label = "From", DataType = ColumnDataType.Date, IsRequired = true },
        new() { Name = "to", Label = "To", DataType = ColumnDataType.Date, IsRequired = true },
    ];

    protected override System.Linq.Expressions.LambdaExpression DefaultOrder => (System.Linq.Expressions.Expression<Func<SiteLaborerAttendanceRow, DateOnly>>)(r => r.WorkDate);

    public override IReadOnlyList<ReportColumn> Columns =>
    [
        ReportColumn.Of<SiteLaborerAttendanceRow, DateOnly>("workDate", ColumnDataType.Date, r => r.WorkDate),
        ReportColumn.Of<SiteLaborerAttendanceRow, string>("siteName", ColumnDataType.Text, r => r.SiteName, groupable: true),
        ReportColumn.Of<SiteLaborerAttendanceRow, string>("tradeName", ColumnDataType.Text, r => r.TradeName, groupable: true),
        ReportColumn.Of<SiteLaborerAttendanceRow, string>("laborerName", ColumnDataType.Text, r => r.LaborerName, groupable: true),
        ReportColumn.Of<SiteLaborerAttendanceRow, decimal>("daysClaimed", ColumnDataType.Quantity, r => r.DaysClaimed, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<SiteLaborerAttendanceRow, decimal>("daysApproved", ColumnDataType.Quantity, r => r.DaysApproved, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<SiteLaborerAttendanceRow, decimal>("otHoursClaimed", ColumnDataType.Quantity, r => r.OtHoursClaimed, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<SiteLaborerAttendanceRow, decimal>("otHoursApproved", ColumnDataType.Quantity, r => r.OtHoursApproved, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<SiteLaborerAttendanceRow, string>("approvalStatus", ColumnDataType.Text, r => r.ApprovalStatus, groupable: true),
        ReportColumn.Of<SiteLaborerAttendanceRow, string>("billingStatus", ColumnDataType.Text, r => r.BillingStatus, groupable: true),
        ReportColumn.Of<SiteLaborerAttendanceRow, long>("id", ColumnDataType.Number, r => r.Id, filterable: false),
    ];

    protected override IQueryable<SiteLaborerAttendanceRow> Build(ReportParameters parameters, ReportingDbContext db) =>
        SiteReports.LaborerAttendance(db.SiteAttendances, db.SiteJobs, db.SiteLaborers, db.SiteTrades, parameters.Date("from"), parameters.Date("to"));
}

// 2. Material Reconciliation Source
public class SiteMaterialReconciliationRow
{
    public long Id { get; set; }
    public string SiteName { get; set; } = null!;
    public string ItemName { get; set; } = null!;
    public decimal DispatchedQty { get; set; }
    public decimal UsedQty { get; set; }
    public decimal ReturnedQty { get; set; }
    public decimal OnSiteQty { get; set; }
    public decimal UnbilledQty { get; set; }
    public decimal BilledQty { get; set; }
}

public sealed class SiteMaterialReconciliationSource : ReportSource<SiteMaterialReconciliationRow>
{
    public override string ReportKey => "site-material-reconciliation";
    public override string Title => "Site Material Reconciliation";
    public override ReportModule Module => ReportModule.Site;
    public override AddOn RequiredAddOn => AddOn.SiteServices;
    public override string RequiredPermission => "site.view";

    public override IReadOnlyList<ReportParameterRepository> Parameters =>
    [
        new() { Name = "asOf", Label = "As at", DataType = ColumnDataType.Date, IsRequired = true },
    ];

    protected override System.Linq.Expressions.LambdaExpression DefaultOrder => (System.Linq.Expressions.Expression<Func<SiteMaterialReconciliationRow, string>>)(r => r.SiteName);

    public override IReadOnlyList<ReportColumn> Columns =>
    [
        ReportColumn.Of<SiteMaterialReconciliationRow, string>("siteName", ColumnDataType.Text, r => r.SiteName, groupable: true),
        ReportColumn.Of<SiteMaterialReconciliationRow, string>("itemName", ColumnDataType.Text, r => r.ItemName, groupable: true),
        ReportColumn.Of<SiteMaterialReconciliationRow, decimal>("dispatchedQty", ColumnDataType.Quantity, r => r.DispatchedQty, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<SiteMaterialReconciliationRow, decimal>("usedQty", ColumnDataType.Quantity, r => r.UsedQty, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<SiteMaterialReconciliationRow, decimal>("returnedQty", ColumnDataType.Quantity, r => r.ReturnedQty, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<SiteMaterialReconciliationRow, decimal>("onSiteQty", ColumnDataType.Quantity, r => r.OnSiteQty, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<SiteMaterialReconciliationRow, decimal>("unbilledQty", ColumnDataType.Quantity, r => r.UnbilledQty, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<SiteMaterialReconciliationRow, decimal>("billedQty", ColumnDataType.Quantity, r => r.BilledQty, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<SiteMaterialReconciliationRow, long>("id", ColumnDataType.Number, r => r.Id, filterable: false),
    ];

    protected override IQueryable<SiteMaterialReconciliationRow> Build(ReportParameters parameters, ReportingDbContext db) =>
        SiteReports.MaterialReconciliation(db.SiteStockBalances, db.SiteJobs, db.Items, parameters.Date("asOf"));
}

// 3. Site Profit Summary Source
public class SiteProfitSummaryRow
{
    public long Id { get; set; }
    public string SiteName { get; set; } = null!;
    public string BillingNumber { get; set; } = null!;
    public DateOnly BillingDate { get; set; }
    public decimal LaborCost { get; set; }
    public decimal LaborSell { get; set; }
    public decimal LaborMargin { get; set; }
    public decimal MaterialCost { get; set; }
    public decimal MaterialSell { get; set; }
    public decimal MaterialMargin { get; set; }
    public decimal TotalCost { get; set; }
    public decimal TotalSell { get; set; }
    public decimal TotalProfit { get; set; }
}

public sealed class SiteProfitSummarySource : ReportSource<SiteProfitSummaryRow>
{
    public override string ReportKey => "site-profit-summary";
    public override string Title => "Site Profit Summary";
    public override ReportModule Module => ReportModule.Site;
    public override AddOn RequiredAddOn => AddOn.SiteServices;
    public override string RequiredPermission => "site.cost.view";

    public override IReadOnlyList<ReportParameterRepository> Parameters =>
    [
        new() { Name = "from", Label = "From", DataType = ColumnDataType.Date, IsRequired = true },
        new() { Name = "to", Label = "To", DataType = ColumnDataType.Date, IsRequired = true },
    ];

    protected override System.Linq.Expressions.LambdaExpression DefaultOrder => (System.Linq.Expressions.Expression<Func<SiteProfitSummaryRow, DateOnly>>)(r => r.BillingDate);

    public override IReadOnlyList<ReportColumn> Columns =>
    [
        ReportColumn.Of<SiteProfitSummaryRow, string>("siteName", ColumnDataType.Text, r => r.SiteName, groupable: true),
        ReportColumn.Of<SiteProfitSummaryRow, string>("billingNumber", ColumnDataType.Text, r => r.BillingNumber),
        ReportColumn.Of<SiteProfitSummaryRow, DateOnly>("billingDate", ColumnDataType.Date, r => r.BillingDate),
        ReportColumn.Of<SiteProfitSummaryRow, decimal>("laborCost", ColumnDataType.Money, r => r.LaborCost, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<SiteProfitSummaryRow, decimal>("laborSell", ColumnDataType.Money, r => r.LaborSell, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<SiteProfitSummaryRow, decimal>("laborMargin", ColumnDataType.Money, r => r.LaborMargin, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<SiteProfitSummaryRow, decimal>("materialCost", ColumnDataType.Money, r => r.MaterialCost, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<SiteProfitSummaryRow, decimal>("materialSell", ColumnDataType.Money, r => r.MaterialSell, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<SiteProfitSummaryRow, decimal>("materialMargin", ColumnDataType.Money, r => r.MaterialMargin, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<SiteProfitSummaryRow, decimal>("totalCost", ColumnDataType.Money, r => r.TotalCost, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<SiteProfitSummaryRow, decimal>("totalSell", ColumnDataType.Money, r => r.TotalSell, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<SiteProfitSummaryRow, decimal>("totalProfit", ColumnDataType.Money, r => r.TotalProfit, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<SiteProfitSummaryRow, long>("id", ColumnDataType.Number, r => r.Id, filterable: false),
    ];

    protected override IQueryable<SiteProfitSummaryRow> Build(ReportParameters parameters, ReportingDbContext db) =>
        SiteReports.ProfitSummary(db.SiteBillingInvoices, db.SiteJobs, db.SiteBillingInvoiceLines, db.SiteAttendances, db.SiteStockLines, parameters.Date("from"), parameters.Date("to"));
}
