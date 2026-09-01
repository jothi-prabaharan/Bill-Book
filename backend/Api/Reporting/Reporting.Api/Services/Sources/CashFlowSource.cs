using System.Linq.Expressions;
using Reporting.Entity.Enums;
using Reporting.Repository;

namespace Reporting.Api.Services.Sources;

public sealed class CashFlowSource : ReportSource<CashFlowRow>
{
    public override string ReportKey => "cash-flow";

    public override string Title => "Cash Flow";

    public override ReportModule Module => ReportModule.Accounting;

    public override string RequiredPermission => "accounting.view";

    public override IReadOnlyList<ReportParameter> Parameters =>
    [
        new() { Name = "from", Label = "From", DataType = ColumnDataType.Date },
        new() { Name = "to", Label = "To", DataType = ColumnDataType.Date },
    ];

    public override IReadOnlyList<ReportColumn> Columns =>
    [
        ReportColumn.Of<CashFlowRow, DateOnly>(
            "ledgerDate", ColumnDataType.Date, r => r.LedgerDate),

        ReportColumn.Of<CashFlowRow, string>(
            "transactionTypeCode", ColumnDataType.Text, r => r.TransactionTypeCode, groupable: true),

        ReportColumn.Of<CashFlowRow, string>(
            "accountName", ColumnDataType.Text, r => r.AccountName, groupable: true),

        ReportColumn.Of<CashFlowRow, decimal>(
            "cashIn", ColumnDataType.Money, r => r.CashIn, AggregateFunction.Sum),

        ReportColumn.Of<CashFlowRow, decimal>(
            "cashOut", ColumnDataType.Money, r => r.CashOut, AggregateFunction.Sum),

        ReportColumn.Of<CashFlowRow, long>(
            "ledgerId", ColumnDataType.Number, r => r.LedgerId, filterable: false),
    ];

    protected override IQueryable<CashFlowRow> Build(
        ReportParameters parameters, ReportingDbContext db)
    {
        DateOnly? start = parameters.Date("from");
        DateOnly? end = parameters.Date("to");

        return from l in db.Ledger
               join a in db.Accounts on l.AccountId equals a.AccountId
               where a.IsBank
                   && (start == null || l.LedgerDate >= start)
                   && (end == null || l.LedgerDate <= end)
               select new CashFlowRow
               {
                   LedgerId = l.LedgerId,
                   LedgerDate = l.LedgerDate,
                   TransactionTypeCode = l.TransactionTypeCode,
                   AccountName = a.AccountName,
                   CashIn = l.DebitAmountBase,
                   CashOut = l.CreditAmountBase,
               };
    }

    protected override LambdaExpression DefaultOrder =>
        (Expression<Func<CashFlowRow, long>>)(r => r.LedgerId);
}

public sealed class CashFlowRow
{
    public long LedgerId { get; set; }

    public DateOnly LedgerDate { get; set; }

    public string TransactionTypeCode { get; set; } = null!;

    public string AccountName { get; set; } = null!;

    public decimal CashIn { get; set; }

    public decimal CashOut { get; set; }
}
