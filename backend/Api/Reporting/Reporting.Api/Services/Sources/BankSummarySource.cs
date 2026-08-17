using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Reporting.Entity.Enums;
using Reporting.Repository;

namespace Reporting.Api.Services.Sources;

public sealed class BankSummarySource : ReportSource<BankSummaryRow>
{
    private readonly BatchedNameResolver _resolver;

    public BankSummarySource(BatchedNameResolver resolver)
    {
        _resolver = resolver;
    }

    public override string ReportKey => "bank-summary";

    public override string Title => "Bank Summary";

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
        ReportColumn.Of<BankSummaryRow, string>(
            "accountName", ColumnDataType.Text, r => r.AccountName, groupable: true),

        ReportColumn.Of<BankSummaryRow, string>(
            "accountType", ColumnDataType.Text, r => r.AccountType, groupable: true),

        ReportColumn.Of<BankSummaryRow, string>(
            "currencyCode", ColumnDataType.Text, r => r.CurrencyCode, groupable: true),

        ReportColumn.Of<BankSummaryRow, decimal>(
            "openingBalance", ColumnDataType.Money, r => r.OpeningBalance, AggregateFunction.Sum),

        ReportColumn.Of<BankSummaryRow, decimal>(
            "received", ColumnDataType.Money, r => r.Received, AggregateFunction.Sum),

        ReportColumn.Of<BankSummaryRow, decimal>(
            "spent", ColumnDataType.Money, r => r.Spent, AggregateFunction.Sum),

        ReportColumn.Of<BankSummaryRow, decimal>(
            "closingBalance", ColumnDataType.Money, r => r.ClosingBalance, AggregateFunction.Sum),

        ReportColumn.Of<BankSummaryRow, decimal>(
            "bankRevaluation", ColumnDataType.Money, r => r.BankRevaluation, AggregateFunction.Sum),

        ReportColumn.Of<BankSummaryRow, long>(
            "bankAccountId", ColumnDataType.Number, r => r.BankAccountId, filterable: false),
    ];

    protected override IQueryable<BankSummaryRow> Build(
        ReportParameters parameters, ReportingDbContext db)
    {
        DateOnly? start = parameters.Date("from");
        DateOnly? end = parameters.Date("to");

        return from b in db.BankAccounts
               join a in db.Accounts on b.LedgerAccountId equals a.AccountId
               where a.IsActive
               let openingLegs = db.Ledger.Where(l => l.AccountId == a.AccountId && (start == null || l.LedgerDate < start))
               let periodLegs = db.Ledger.Where(l => l.AccountId == a.AccountId && (start == null || l.LedgerDate >= start) && (end == null || l.LedgerDate <= end))
               select new BankSummaryRow
               {
                   BankAccountId = b.BankAccountId,
                   AccountId = a.AccountId,
                   AccountTypeId = a.AccountTypeId,
                   AccountName = a.AccountName,
                   CurrencyCode = b.CurrencyCode,
                   // For a bank account (asset), Debit is Received, Credit is Spent
                   OpeningBalance = openingLegs.Sum(l => l.DebitAmountBase) - openingLegs.Sum(l => l.CreditAmountBase),
                   Received = periodLegs.Sum(l => l.DebitAmountBase),
                   Spent = periodLegs.Sum(l => l.CreditAmountBase),
                   ClosingBalance = (openingLegs.Sum(l => l.DebitAmountBase) - openingLegs.Sum(l => l.CreditAmountBase)) + 
                                    (periodLegs.Sum(l => l.DebitAmountBase) - periodLegs.Sum(l => l.CreditAmountBase)),
                   // Bank Revaluation is usually computed differently, but for now we set it to 0 as it might be complex or unsupported directly in this simple report
                   BankRevaluation = 0m,
               };
    }

    protected override LambdaExpression DefaultOrder =>
        (Expression<Func<BankSummaryRow, long>>)(r => r.BankAccountId);

    protected override async Task<decimal?> OpeningBalanceAsync(
        IQueryable<BankSummaryRow> rowsBeforeThisPage, CancellationToken ct) =>
        await rowsBeforeThisPage.SumAsync(r => r.OpeningBalance, ct);

    protected override async Task FormatRowsAsync(IReadOnlyList<BankSummaryRow> page, CancellationToken ct)
    {
        Dictionary<int, string> types = await _resolver.GetAccountTypeNamesAsync(ct);
        foreach (var row in page)
        {
            if (types.TryGetValue(row.AccountTypeId, out string? typeName))
            {
                row.AccountType = typeName;
            }
        }
    }
}

public sealed class BankSummaryRow
{
    public long BankAccountId { get; set; }
    public long AccountId { get; set; }
    public int AccountTypeId { get; set; }
    public string AccountName { get; set; } = null!;
    public string AccountType { get; set; } = string.Empty;
    public string CurrencyCode { get; set; } = null!;
    public decimal OpeningBalance { get; set; }
    public decimal Received { get; set; }
    public decimal Spent { get; set; }
    public decimal ClosingBalance { get; set; }
    public decimal BankRevaluation { get; set; }
}
