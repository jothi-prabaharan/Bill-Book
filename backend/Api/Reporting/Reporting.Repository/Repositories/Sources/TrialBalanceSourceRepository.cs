using System.Linq.Expressions;
using Reporting.Entity.Enums;
using Reporting.Repository;
using Reporting.Repository.ReadModels;

namespace Reporting.Repository.Repositories;

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
public sealed class TrialBalanceSourceRepository : ReportSource<TrialBalanceRow>
{
    private readonly BatchedNameResolverRepository _resolver;

    public TrialBalanceSourceRepository(BatchedNameResolverRepository resolver)
    {
        _resolver = resolver;
    }

    public override string ReportKey => "trial-balance";

    public override string Title => "Trial Balance";

    public override ReportModule Module => ReportModule.Accounting;

    public override string RequiredPermission => "accounting.view";

    /// <summary>
    /// Both dates are required (D-67, TK-215), so an export, a saved view and a
    /// scheduled email take a period as the page does.
    /// </summary>
    public override IReadOnlyList<ReportParameterRepository> Parameters =>
    [
        new() { Name = "from", Label = "From", DataType = ColumnDataType.Date, IsRequired = true },
        new() { Name = "to", Label = "To", DataType = ColumnDataType.Date, IsRequired = true },
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
    /// One row per active account, with the ledger summed beneath it. The period
    /// is required: a run without both dates, or with the end before the start,
    /// is refused, which the endpoint answers as a 400 with the message.
    ///
    /// <b>Accounts with no postings are included</b>, at zero. A trial balance that
    /// silently omits an empty account hides the one thing somebody checking a new
    /// chart of accounts is looking for — that the account exists and has not been
    /// posted to.
    /// </summary>
    protected override IQueryable<TrialBalanceRow> Build(
        ReportParameters parameters, ReportingDbContext db)
    {
        (DateOnly start, DateOnly end) = PeriodOf(parameters);

        return Rows(db.Accounts, db.Ledger, start, end);
    }

    /// <summary>
    /// The period asked for. A missing date, or an end before the start, is
    /// refused with a message the endpoint passes on. Pure.
    /// </summary>
    public static (DateOnly Start, DateOnly End) PeriodOf(ReportParameters parameters)
    {
        // Not named `from` and `to`: `from` is a query-expression keyword and
        // cannot be a local inside one.
        DateOnly start = parameters.Date("from")
            ?? throw new ReportQueryException("From date is required.");
        DateOnly end = parameters.Date("to")
            ?? throw new ReportQueryException("To date is required.");

        if (end < start)
        {
            throw new ReportQueryException("To date cannot be before the from date.");
        }

        return (start, end);
    }

    /// <summary>
    /// The rows over the sets rather than the context, so the arithmetic can be
    /// tested over lists.
    ///
    /// <b>The ledger is summed by account first and the accounts joined to the
    /// sums.</b> A sub-query per account, projected once for the debits and again
    /// for the credits, is what answered 500 here: EF cannot translate it, and
    /// the whole report failed on a branch with any postings. This is one grouped
    /// query, joined once, as TK-207 rewrote the budget reports.
    ///
    /// <b>Two queries joined end to end, not a left join.</b> The accounts with a
    /// posting in the period take their sums from the join; the accounts with none
    /// follow at nothing. A left join with a null check inside its projection
    /// translates for the rows but not for the totals the runner sums over it, so
    /// it passed its own tests and answered 500 against PostgreSQL. The typed
    /// <c>0m</c> is what lets Postgres sum over the union.
    /// </summary>
    public static IQueryable<TrialBalanceRow> Rows(
        IQueryable<AccountRead> accounts, IQueryable<JournalLedgerRead> ledger, DateOnly start, DateOnly end)
    {
        IQueryable<JournalLedgerRead> inPeriod = ledger.Where(l => l.LedgerDate >= start && l.LedgerDate <= end);

        IQueryable<TrialBalanceAccountSum> sums = inPeriod
            .GroupBy(l => l.AccountId)
            .Select(g => new TrialBalanceAccountSum
            {
                AccountId = g.Key,
                Debit = g.Sum(l => l.DebitAmountBase),
                Credit = g.Sum(l => l.CreditAmountBase),
            });

        IQueryable<TrialBalanceRow> posted =
            from a in accounts
            where a.IsActive
            join s in sums on a.AccountId equals s.AccountId
            select new TrialBalanceRow
            {
                AccountId = a.AccountId,
                AccountCode = a.AccountCode,
                AccountName = a.AccountName,
                AccountTypeId = a.AccountTypeId,
                Debit = s.Debit,
                Credit = s.Credit,
                CurrentBalance = s.Debit - s.Credit,
            };

        IQueryable<TrialBalanceRow> idle =
            from a in accounts
            where a.IsActive && !inPeriod.Any(l => l.AccountId == a.AccountId)
            select new TrialBalanceRow
            {
                AccountId = a.AccountId,
                AccountCode = a.AccountCode,
                AccountName = a.AccountName,
                AccountTypeId = a.AccountTypeId,
                Debit = 0m,
                Credit = 0m,
                CurrentBalance = 0m,
            };

        return posted.Concat(idle);
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

/// <summary>One account's debits and credits over the period, before it is joined to the account.</summary>
public sealed class TrialBalanceAccountSum
{
    public long AccountId { get; set; }

    public decimal Debit { get; set; }

    public decimal Credit { get; set; }
}
