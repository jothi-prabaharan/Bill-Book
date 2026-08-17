using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Reporting.Entity.Enums;
using Reporting.Repository;

namespace Reporting.Api.Services.Sources;

public sealed class GeneralLedgerSummarySource : ReportSource<GeneralLedgerSummaryRow>
{
    public override string ReportKey => "general-ledger-summary";

    public override string Title => "General Ledger Summary";

    public override ReportModule Module => ReportModule.Accounting;

    public override string RequiredPermission => "accounting.view";

    public override bool ForcesSortOrder => true;

    public override IReadOnlyList<ReportParameter> Parameters =>
    [
        new() { Name = "from", Label = "From", DataType = ColumnDataType.Date },
        new() { Name = "to", Label = "To", DataType = ColumnDataType.Date },
    ];

    public override IReadOnlyList<ReportColumn> Columns =>
    [
        ReportColumn.Of<GeneralLedgerSummaryRow, string>(
            "account", ColumnDataType.Text, r => r.Account, groupable: true),

        ReportColumn.Of<GeneralLedgerSummaryRow, string>(
            "accountCode", ColumnDataType.Text, r => r.AccountCode, groupable: true),

        ReportColumn.Of<GeneralLedgerSummaryRow, decimal>(
            "openingBalance", ColumnDataType.Money, r => r.OpeningBalance, AggregateFunction.Sum),

        ReportColumn.Of<GeneralLedgerSummaryRow, decimal>(
            "debit", ColumnDataType.Money, r => r.Debit, AggregateFunction.Sum),

        ReportColumn.Of<GeneralLedgerSummaryRow, decimal>(
            "credit", ColumnDataType.Money, r => r.Credit, AggregateFunction.Sum),

        ReportColumn.Of<GeneralLedgerSummaryRow, decimal>(
            "netMovement", ColumnDataType.Money, r => r.NetMovement, AggregateFunction.Sum),

        ReportColumn.Of<GeneralLedgerSummaryRow, decimal>(
            "closing", ColumnDataType.Money, r => r.Closing, AggregateFunction.Sum),

        ReportColumn.Of<GeneralLedgerSummaryRow, long>(
            "accountId", ColumnDataType.Number, r => r.AccountId, filterable: false),
    ];

    protected override IQueryable<GeneralLedgerSummaryRow> Build(
        ReportParameters parameters, ReportingDbContext db)
    {
        DateOnly? start = parameters.Date("from");
        DateOnly? end = parameters.Date("to");

        return from a in db.Accounts
               where a.IsActive
               let openingLegs = db.Ledger.Where(l => l.AccountId == a.AccountId && (start == null || l.LedgerDate < start))
               let periodLegs = db.Ledger.Where(l => l.AccountId == a.AccountId && (start == null || l.LedgerDate >= start) && (end == null || l.LedgerDate <= end))
               select new GeneralLedgerSummaryRow
               {
                   AccountId = a.AccountId,
                   AccountCode = a.AccountCode,
                   Account = a.AccountName,
                   OpeningBalance = openingLegs.Sum(l => l.DebitAmountBase) - openingLegs.Sum(l => l.CreditAmountBase),
                   Debit = periodLegs.Sum(l => l.DebitAmountBase),
                   Credit = periodLegs.Sum(l => l.CreditAmountBase),
                   NetMovement = periodLegs.Sum(l => l.DebitAmountBase) - periodLegs.Sum(l => l.CreditAmountBase),
                   Closing = (openingLegs.Sum(l => l.DebitAmountBase) - openingLegs.Sum(l => l.CreditAmountBase)) + (periodLegs.Sum(l => l.DebitAmountBase) - periodLegs.Sum(l => l.CreditAmountBase)),
               };
    }

    protected override LambdaExpression DefaultOrder =>
        (Expression<Func<GeneralLedgerSummaryRow, long>>)(r => r.AccountId);

    protected override async Task<decimal?> OpeningBalanceAsync(
        IQueryable<GeneralLedgerSummaryRow> rowsBeforeThisPage, CancellationToken ct) =>
        await rowsBeforeThisPage.SumAsync(r => r.OpeningBalance, ct);
}

public sealed class GeneralLedgerSummaryRow
{
    public long AccountId { get; set; }
    public string AccountCode { get; set; } = null!;
    public string Account { get; set; } = null!;
    public decimal OpeningBalance { get; set; }
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
    public decimal NetMovement { get; set; }
    public decimal Closing { get; set; }
}
