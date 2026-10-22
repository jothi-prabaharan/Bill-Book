using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Reporting.Entity.Enums;
using Reporting.Repository;
using Reporting.Repository.ReadModels;
using Shared.Kernel.Numbering;

namespace Reporting.Repository.Repositories;

/// <summary>
/// The General Ledger Summary: every active account's opening balance, debits,
/// credits and closing balance for a period, in debit terms (a credit balance is
/// negative).
///
/// <b>The period is required</b> (D-67, TK-216), so an export, a saved view and a
/// scheduled email take one as the page does. <b>Opening follows the page and the
/// Trial Balance:</b> a balance-sheet account opens at everything before the
/// period, an income or expense account at what was posted since its financial
/// year began, so an account's closing here is its closing on the General ledger
/// page.
///
/// <b>The ledger is summed by account first.</b> A sub-query per account, once for
/// the opening and once for the period and each projected several times, is what
/// answered 500 here. Two queries are joined end to end, accounts with postings and
/// accounts with none, because a left join with a null check translates for the
/// rows and not for the totals the runner sums over it.
/// </summary>
public sealed class GeneralLedgerSummarySourceRepository : ReportSource<GeneralLedgerSummaryRow>
{
    // mst.AccountTypes ids: contractual, as ChartOfAccountsSeed has them.
    private const int Income = 4;
    private const int Expense = 5;

    private readonly IFinancialYearProvider _financialYear;

    /// <summary>The first day of the financial year the period starts in, set by <see cref="PrepareAsync"/> for this request.</summary>
    private DateOnly _yearStart;

    public GeneralLedgerSummarySourceRepository(IFinancialYearProvider financialYear) => _financialYear = financialYear;

    public override string ReportKey => "general-ledger-summary";

    public override string Title => "General Ledger Summary";

    public override ReportModule Module => ReportModule.Accounting;

    public override string RequiredPermission => "accounting.view";

    public override bool ForcesSortOrder => true;

    public override IReadOnlyList<ReportParameterRepository> Parameters =>
    [
        new() { Name = "from", Label = "From", DataType = ColumnDataType.Date, IsRequired = true },
        new() { Name = "to", Label = "To", DataType = ColumnDataType.Date, IsRequired = true },
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

    protected override async Task PrepareAsync(ReportParameters parameters, ReportingDbContext db, CancellationToken ct)
    {
        (DateOnly start, _) = TrialBalanceSourceRepository.PeriodOf(parameters);
        _yearStart = PortalAccountRepository.FinancialYearStart(start, await _financialYear.GetStartMonthAsync(ct));
    }

    protected override IQueryable<GeneralLedgerSummaryRow> Build(
        ReportParameters parameters, ReportingDbContext db)
    {
        (DateOnly start, DateOnly end) = TrialBalanceSourceRepository.PeriodOf(parameters);

        return Rows(db.Accounts, db.Ledger, start, end, _yearStart);
    }

    /// <summary>
    /// The rows over the sets rather than the context, so the arithmetic can be
    /// tested over lists. One grouped pass over the ledger up to the end of the
    /// period; the typed <c>0m</c> is what lets Postgres sum over the union.
    /// </summary>
    public static IQueryable<GeneralLedgerSummaryRow> Rows(
        IQueryable<AccountRead> accounts,
        IQueryable<JournalLedgerRead> ledger,
        DateOnly start,
        DateOnly end,
        DateOnly yearStart)
    {
        IQueryable<JournalLedgerRead> upToEnd = ledger.Where(l => l.LedgerDate <= end);

        IQueryable<GeneralLedgerAccountSum> sums = upToEnd
            .GroupBy(l => l.AccountId)
            .Select(g => new GeneralLedgerAccountSum
            {
                AccountId = g.Key,
                NetBefore = g.Sum(l => l.LedgerDate < start ? l.DebitAmountBase - l.CreditAmountBase : 0m),
                NetBeforeYear = g.Sum(l => l.LedgerDate < yearStart ? l.DebitAmountBase - l.CreditAmountBase : 0m),
                Debit = g.Sum(l => l.LedgerDate >= start ? l.DebitAmountBase : 0m),
                Credit = g.Sum(l => l.LedgerDate >= start ? l.CreditAmountBase : 0m),
            });

        IQueryable<GeneralLedgerSummaryRow> posted =
            from a in accounts
            where a.IsActive
            join s in sums on a.AccountId equals s.AccountId
            let opening = a.AccountTypeId == Income || a.AccountTypeId == Expense
                ? s.NetBefore - s.NetBeforeYear
                : s.NetBefore
            select new GeneralLedgerSummaryRow
            {
                AccountId = a.AccountId,
                AccountCode = a.AccountCode,
                Account = a.AccountName,
                OpeningBalance = opening,
                Debit = s.Debit,
                Credit = s.Credit,
                NetMovement = s.Debit - s.Credit,
                Closing = opening + s.Debit - s.Credit,
            };

        IQueryable<GeneralLedgerSummaryRow> idle =
            from a in accounts
            where a.IsActive && !upToEnd.Any(l => l.AccountId == a.AccountId)
            select new GeneralLedgerSummaryRow
            {
                AccountId = a.AccountId,
                AccountCode = a.AccountCode,
                Account = a.AccountName,
                OpeningBalance = 0m,
                Debit = 0m,
                Credit = 0m,
                NetMovement = 0m,
                Closing = 0m,
            };

        return posted.Concat(idle);
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

/// <summary>One account's postings summed up to the end of the period, before it is joined to the account.</summary>
public sealed class GeneralLedgerAccountSum
{
    public long AccountId { get; set; }

    /// <summary>Debits less credits before the period's first day.</summary>
    public decimal NetBefore { get; set; }

    /// <summary>Debits less credits before the first day of the financial year the period starts in.</summary>
    public decimal NetBeforeYear { get; set; }

    public decimal Debit { get; set; }

    public decimal Credit { get; set; }
}
