using System.Linq.Expressions;
using Reporting.Entity.Enums;
using Reporting.Repository;

namespace Reporting.Api.Services.Sources;

/// <summary>
/// The trial balance: every account with its debits, its credits and the balance
/// between them.
///
/// <b>The second worked example, and it is here to show aggregation.</b> Account
/// Movement returns one row per ledger leg; this returns one row per <i>account</i>,
/// with the ledger summed underneath it. Reports of that shape — General Ledger
/// Summary, Bank Summary, Inventory Item Summary — are copied from this one rather
/// than from Account Movement.
///
/// A page already exists at <c>accounting-ui/trial-balance</c>. It stays: it
/// carries the balanced/unbalanced banner, which is a page's job rather than a
/// grid's, and the two agreeing is a useful check on this report.
/// </summary>
public sealed class TrialBalanceSource : ReportSource<TrialBalanceRow>
{
    private readonly BatchedNameResolver _resolver;

    public TrialBalanceSource(BatchedNameResolver resolver)
    {
        _resolver = resolver;
    }

    public override string ReportKey => "trial-balance";

    public override string Title => "Trial Balance";

    public override ReportModule Module => ReportModule.Accounting;

    public override string RequiredPermission => "accounting.view";

    public override IReadOnlyList<ReportParameter> Parameters =>
    [
        new() { Name = "from", Label = "From", DataType = ColumnDataType.Date },
        new() { Name = "to", Label = "To", DataType = ColumnDataType.Date },
    ];

    public override IReadOnlyList<ReportColumn> Columns =>
    [
        ReportColumn.Of<TrialBalanceRow, string>(
            "accountCode", ColumnDataType.Text, r => r.AccountCode),

        ReportColumn.Of<TrialBalanceRow, string>(
            "accountName", ColumnDataType.Text, r => r.AccountName, groupable: true),

        // Asset, Liability, Equity, Income or Expense. It lives in
        // `mst.AccountTypes` — another database — so the id travels on the row and
        // the name is filled in after the page is fetched, batched, the way every
        // cross-database name on these reports is.
        ReportColumn.Of<TrialBalanceRow, string?>(
            "accountType", ColumnDataType.Enum, r => r.AccountType, groupable: true),

        ReportColumn.Of<TrialBalanceRow, decimal>(
            "debit", ColumnDataType.Money, r => r.Debit, AggregateFunction.Sum),

        ReportColumn.Of<TrialBalanceRow, decimal>(
            "credit", ColumnDataType.Money, r => r.Credit, AggregateFunction.Sum),

        // Debits minus credits. Summed rather than left alone because the total of
        // this column across every account is the number that says the books
        // balance: zero, or something is wrong.
        ReportColumn.Of<TrialBalanceRow, decimal>(
            "currentBalance", ColumnDataType.Money, r => r.CurrentBalance,
            AggregateFunction.Sum),

        // The source list calls this CAAccountID. It is an internal key, carried so
        // a row can link through to the account ledger and seeded as hidden so it
        // is never offered in the column chooser.
        ReportColumn.Of<TrialBalanceRow, long>(
            "accountId", ColumnDataType.Number, r => r.AccountId, filterable: false),
    ];

    /// <summary>
    /// One row per account, with the ledger summed beneath it.
    ///
    /// <b>Accounts with no postings are included</b>, at zero. A trial balance that
    /// silently omits an empty account hides the one thing somebody checking a new
    /// chart of accounts is looking for — that the account exists and has not been
    /// posted to.
    /// </summary>
    protected override IQueryable<TrialBalanceRow> Build(
        ReportParameters parameters, ReportingDbContext db)
    {
        // Not named `from` and `to`: `from` is a query-expression keyword and
        // cannot be a local inside one.
        DateOnly? start = parameters.Date("from");
        DateOnly? end = parameters.Date("to");

        return from a in db.Accounts
               where a.IsActive
               let legs = db.Ledger.Where(l =>
                   l.AccountId == a.AccountId
                   && (start == null || l.LedgerDate >= start)
                   && (end == null || l.LedgerDate <= end))
               select new TrialBalanceRow
               {
                   AccountId = a.AccountId,
                   AccountCode = a.AccountCode,
                   AccountName = a.AccountName,
                   AccountTypeId = a.AccountTypeId,
                   Debit = legs.Sum(l => l.DebitAmountBase),
                   Credit = legs.Sum(l => l.CreditAmountBase),
                   CurrentBalance =
                       legs.Sum(l => l.DebitAmountBase) - legs.Sum(l => l.CreditAmountBase),
               };
    }

    protected override LambdaExpression DefaultOrder =>
        (Expression<Func<TrialBalanceRow, long>>)(r => r.AccountId);

    /// <summary>
    /// One call for the whole page, not one per row. There are five account types
    /// and they never change, so the resolver caches them — but a per-row lookup
    /// would still be five hundred awaits on a five-hundred-account chart.
    /// </summary>
    protected override async Task FormatRowsAsync(
        IReadOnlyList<TrialBalanceRow> page, CancellationToken ct)
    {
        Dictionary<int, string> types = await _resolver.GetAccountTypeNamesAsync(ct);

        foreach (TrialBalanceRow row in page)
        {
            if (types.TryGetValue(row.AccountTypeId, out string? typeName))
            {
                row.AccountType = typeName;
            }
        }
    }
}

/// <summary>One account's totals.</summary>
public sealed class TrialBalanceRow
{
    public long AccountId { get; set; }

    public string AccountCode { get; set; } = null!;

    public string AccountName { get; set; } = null!;

    /// <summary>Resolved to <see cref="AccountType"/> after the page is fetched.</summary>
    public int AccountTypeId { get; set; }

    public string? AccountType { get; set; }

    public decimal Debit { get; set; }

    public decimal Credit { get; set; }

    public decimal CurrentBalance { get; set; }
}
