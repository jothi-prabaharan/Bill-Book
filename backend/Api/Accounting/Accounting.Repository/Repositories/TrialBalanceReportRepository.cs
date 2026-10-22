using Accounting.Entity.Models;
using Accounting.Entity.TableEntities;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Numbering;

namespace Accounting.Repository.Repositories;

/// <summary>
/// The Trial Balance (TK-215): every account's balance when a period opens,
/// what was posted to it inside the period and its balance when the period
/// closes, with a subtotal for each account type and the totals that have to
/// agree. A second period can be shown beside it, as its closing balances.
///
/// <b>What "opening" means depends on the account.</b> A balance-sheet account
/// (asset, liability, equity) opens at everything posted before the period. An
/// income or expense account opens at what was posted since the financial year
/// began, because earlier years' profit is not income of this year: it is
/// carried into equity and shown as one worked-out line, "Profit of earlier
/// years", exactly as the Balance Sheet (TK-199) splits it. Without that line
/// the opening columns of a period that starts mid-year would not agree, and
/// the report would be wrong in the one way a trial balance exists to catch.
///
/// <b>Each balance is in one column</b>, the side it falls on, and both columns
/// are positive. Showing every account's debits and credits as well would foot
/// to two numbers that always agree, because every posting was balanced when it
/// was written, and prove nothing.
///
/// <b>One query.</b> The ledger is summed once in the database by account, for
/// both periods; everything after that is arithmetic over those rows, in
/// <see cref="Compose"/>, which is pure. LINQ only.
/// </summary>
public sealed class TrialBalanceReportRepository
{
    // mst.AccountTypes ids: contractual, as ChartOfAccountsSeed has them.
    private const int Equity = 3;
    private const int Income = 4;
    private const int Expense = 5;

    /// <summary>The name of the worked-out line carrying earlier years' profit, as the Balance Sheet calls it.</summary>
    public const string EarlierYearsProfit = "Profit of earlier years";

    private readonly AccountingDbContext _db;
    private readonly IFinancialYearProvider _financialYear;

    public TrialBalanceReportRepository(AccountingDbContext db, IFinancialYearProvider financialYear)
    {
        _db = db;
        _financialYear = financialYear;
    }

    /// <summary>
    /// The refusals the request's own attributes cannot make, each with the
    /// field it belongs to. Pure.
    /// </summary>
    public static IReadOnlyList<(string Field, string Message)> DateRefusals(TrialBalanceReportRequest request)
    {
        List<(string, string)> refusals = [];

        if (request.From is DateOnly from && request.To is DateOnly to && to < from)
        {
            refusals.Add((nameof(TrialBalanceReportRequest.To), "To date cannot be before the from date."));
        }

        if (request.CompareFrom is null && request.CompareTo is not null)
        {
            refusals.Add((nameof(TrialBalanceReportRequest.CompareFrom), "Give both comparison dates, or neither."));
        }
        else if (request.CompareFrom is not null && request.CompareTo is null)
        {
            refusals.Add((nameof(TrialBalanceReportRequest.CompareTo), "Give both comparison dates, or neither."));
        }
        else if (request.CompareFrom is DateOnly compareFrom && request.CompareTo is DateOnly compareTo
            && compareTo < compareFrom)
        {
            refusals.Add((nameof(TrialBalanceReportRequest.CompareTo), "Comparison to date cannot be before its from date."));
        }

        return refusals;
    }

    /// <summary>The first day of the financial year a date falls in. Pure.</summary>
    public static DateOnly FinancialYearStart(DateOnly date, int startMonth)
    {
        int month = startMonth is >= 1 and <= 12 ? startMonth : 4;
        return date.Month >= month ? new DateOnly(date.Year, month, 1) : new DateOnly(date.Year - 1, month, 1);
    }

    /// <summary>The trial balance for a request the controller has already validated.</summary>
    public async Task<TrialBalanceReportResultDto> RunAsync(TrialBalanceReportRequest request, CancellationToken ct)
    {
        DateOnly from = request.From!.Value;
        DateOnly to = request.To!.Value;
        DateOnly? compareFrom = request.CompareFrom;
        DateOnly? compareTo = request.CompareTo;

        int startMonth = await _financialYear.GetStartMonthAsync(ct);
        DateOnly yearStart = FinancialYearStart(from, startMonth);
        DateOnly? compareYearStart = compareFrom is DateOnly c ? FinancialYearStart(c, startMonth) : null;

        List<TrialBalanceAccount> accounts = await _db.Accounts
            .Select(a => new TrialBalanceAccount
            {
                AccountId = a.AccountId,
                AccountTypeId = a.AccountTypeId,
                AccountCode = a.AccountCode,
                AccountName = a.AccountName,
                IsContra = a.IsContra,
                IsActive = a.IsActive,
            })
            .ToListAsync(ct);

        List<AccountPeriodSums> sums = await Sums(
            _db.JournalLedger, from, to, yearStart, compareFrom, compareTo, compareYearStart).ToListAsync(ct);

        return Compose(
            accounts, sums, from, to, yearStart, compareFrom, compareTo, compareYearStart, request.IsZeroBalanceShown);
    }

    /// <summary>
    /// Each account's postings, summed once: everything before the period and
    /// before its financial year, and the period's debits and credits, and the
    /// same for the comparison period. One pass over the ledger up to the later
    /// end. Built from a plain queryable so a test runs it over a list.
    /// </summary>
    public static IQueryable<AccountPeriodSums> Sums(
        IQueryable<JournalLedger> ledger,
        DateOnly from,
        DateOnly to,
        DateOnly yearStart,
        DateOnly? compareFrom,
        DateOnly? compareTo,
        DateOnly? compareYearStart)
    {
        // With no comparison the comparison sums repeat the period's, so the query
        // has one shape; Compose ignores them.
        DateOnly otherFrom = compareFrom ?? from;
        DateOnly otherTo = compareTo ?? to;
        DateOnly otherYearStart = compareYearStart ?? yearStart;
        DateOnly latest = otherTo > to ? otherTo : to;

        return ledger
            .Where(l => l.LedgerDate <= latest)
            .GroupBy(l => l.AccountId)
            .Select(g => new AccountPeriodSums
            {
                AccountId = g.Key,
                NetBefore = g.Sum(l => l.LedgerDate < from ? l.DebitAmountBase - l.CreditAmountBase : 0m),
                NetBeforeYear = g.Sum(l => l.LedgerDate < yearStart ? l.DebitAmountBase - l.CreditAmountBase : 0m),
                MovementDebit = g.Sum(l => l.LedgerDate >= from && l.LedgerDate <= to ? l.DebitAmountBase : 0m),
                MovementCredit = g.Sum(l => l.LedgerDate >= from && l.LedgerDate <= to ? l.CreditAmountBase : 0m),
                CompareNetBefore = g.Sum(l => l.LedgerDate < otherFrom ? l.DebitAmountBase - l.CreditAmountBase : 0m),
                CompareNetBeforeYear = g.Sum(l => l.LedgerDate < otherYearStart ? l.DebitAmountBase - l.CreditAmountBase : 0m),
                CompareMovementDebit = g.Sum(l => l.LedgerDate >= otherFrom && l.LedgerDate <= otherTo ? l.DebitAmountBase : 0m),
                CompareMovementCredit = g.Sum(l => l.LedgerDate >= otherFrom && l.LedgerDate <= otherTo ? l.CreditAmountBase : 0m),
            });
    }

    /// <summary>
    /// The report from the accounts and their sums. Pure.
    ///
    /// An account is listed when its opening balance, its movement, its closing
    /// balance or the comparison's closing balance is not nothing; with zero
    /// balances asked for, an active account with none of them is listed too. An
    /// inactive account that moved or holds a balance is always listed, since
    /// leaving it out would unbalance the columns. The accounts of a type are in
    /// code order, with the worked-out line last.
    /// </summary>
    public static TrialBalanceReportResultDto Compose(
        IReadOnlyList<TrialBalanceAccount> accounts,
        IReadOnlyList<AccountPeriodSums> sums,
        DateOnly from,
        DateOnly to,
        DateOnly yearStart,
        DateOnly? compareFrom,
        DateOnly? compareTo,
        DateOnly? compareYearStart,
        bool isZeroBalanceShown)
    {
        bool comparing = compareFrom.HasValue && compareTo.HasValue;
        Dictionary<long, AccountPeriodSums> sumsById = sums.ToDictionary(s => s.AccountId);

        List<TrialBalanceReportLineDto> lines = [];

        // Debits less credits on the income and expense accounts before each
        // period's financial year: earlier years' profit, which is a credit.
        decimal carried = 0m, compareCarried = 0m;

        foreach (TrialBalanceAccount account in accounts)
        {
            AccountPeriodSums s = sumsById.GetValueOrDefault(account.AccountId) ?? new AccountPeriodSums();
            bool isProfitAndLoss = account.AccountTypeId is Income or Expense;

            decimal opening = isProfitAndLoss ? s.NetBefore - s.NetBeforeYear : s.NetBefore;
            decimal closing = opening + s.MovementDebit - s.MovementCredit;
            decimal compareClosing = (isProfitAndLoss ? s.CompareNetBefore - s.CompareNetBeforeYear : s.CompareNetBefore)
                + s.CompareMovementDebit - s.CompareMovementCredit;

            if (isProfitAndLoss)
            {
                carried += s.NetBeforeYear;
                compareCarried += s.CompareNetBeforeYear;
            }

            bool hasFigures = opening != 0m || s.MovementDebit != 0m || s.MovementCredit != 0m
                || closing != 0m || (comparing && compareClosing != 0m);

            if (!hasFigures && !(isZeroBalanceShown && account.IsActive))
            {
                continue;
            }

            lines.Add(Line(
                account.AccountId, account.AccountCode, account.AccountName, account.AccountTypeId, account.IsContra,
                isComputed: false, opening, s.MovementDebit, s.MovementCredit, closing, comparing ? compareClosing : null));
        }

        if (carried != 0m || (comparing && compareCarried != 0m))
        {
            lines.Add(Line(
                0, string.Empty, EarlierYearsProfit, Equity, isContra: false,
                isComputed: true, carried, 0m, 0m, carried, comparing ? compareCarried : null));
        }

        List<TrialBalanceReportGroupDto> groups =
        [
            .. lines
                .GroupBy(l => l.AccountTypeId)
                .OrderBy(g => g.Key)
                .Select(g =>
                {
                    List<TrialBalanceReportLineDto> ordered =
                    [
                        .. g.OrderBy(l => l.IsComputed ? 1 : 0).ThenBy(l => l.AccountCode, StringComparer.Ordinal),
                    ];

                    return new TrialBalanceReportGroupDto
                    {
                        AccountTypeId = g.Key,
                        Lines = ordered,
                        Totals = Totals(ordered, comparing),
                    };
                }),
        ];

        TrialBalanceReportTotalsDto totals = Totals(lines, comparing);
        decimal openingDifference = totals.OpeningDebit - totals.OpeningCredit;
        decimal movementDifference = totals.MovementDebit - totals.MovementCredit;
        decimal closingDifference = totals.ClosingDebit - totals.ClosingCredit;
        decimal? compareDifference = comparing ? totals.CompareClosingDebit - totals.CompareClosingCredit : null;

        return new TrialBalanceReportResultDto
        {
            From = from,
            To = to,
            CompareFrom = compareFrom,
            CompareTo = compareTo,
            FinancialYearStart = yearStart,
            CompareFinancialYearStart = comparing ? compareYearStart : null,
            Groups = groups,
            Totals = totals,
            OpeningDifference = openingDifference,
            MovementDifference = movementDifference,
            ClosingDifference = closingDifference,
            CompareClosingDifference = compareDifference,
            IsBalanced = openingDifference == 0m && movementDifference == 0m && closingDifference == 0m
                && (compareDifference ?? 0m) == 0m,
        };
    }

    /// <summary>A line with each balance put in the column it falls on. Pure.</summary>
    private static TrialBalanceReportLineDto Line(
        long accountId,
        string code,
        string name,
        int accountTypeId,
        bool isContra,
        bool isComputed,
        decimal openingNet,
        decimal movementDebit,
        decimal movementCredit,
        decimal closingNet,
        decimal? compareClosingNet)
    {
        (decimal openingDebit, decimal openingCredit) = LedgerArithmeticRepository.Sides(openingNet);
        (decimal closingDebit, decimal closingCredit) = LedgerArithmeticRepository.Sides(closingNet);
        (decimal compareDebit, decimal compareCredit) = LedgerArithmeticRepository.Sides(compareClosingNet ?? 0m);

        return new TrialBalanceReportLineDto
        {
            AccountId = accountId,
            AccountCode = code,
            AccountName = name,
            AccountTypeId = accountTypeId,
            IsContra = isContra,
            IsComputed = isComputed,
            OpeningDebit = openingDebit,
            OpeningCredit = openingCredit,
            MovementDebit = movementDebit,
            MovementCredit = movementCredit,
            ClosingDebit = closingDebit,
            ClosingCredit = closingCredit,
            CompareClosingDebit = compareClosingNet is null ? null : compareDebit,
            CompareClosingCredit = compareClosingNet is null ? null : compareCredit,
        };
    }

    private static TrialBalanceReportTotalsDto Totals(IReadOnlyCollection<TrialBalanceReportLineDto> lines, bool comparing) =>
        new()
        {
            OpeningDebit = lines.Sum(l => l.OpeningDebit),
            OpeningCredit = lines.Sum(l => l.OpeningCredit),
            MovementDebit = lines.Sum(l => l.MovementDebit),
            MovementCredit = lines.Sum(l => l.MovementCredit),
            ClosingDebit = lines.Sum(l => l.ClosingDebit),
            ClosingCredit = lines.Sum(l => l.ClosingCredit),
            CompareClosingDebit = comparing ? lines.Sum(l => l.CompareClosingDebit ?? 0m) : null,
            CompareClosingCredit = comparing ? lines.Sum(l => l.CompareClosingCredit ?? 0m) : null,
        };
}

/// <summary>An account as the trial balance needs it.</summary>
public sealed class TrialBalanceAccount
{
    public long AccountId { get; set; }

    public int AccountTypeId { get; set; }

    public string AccountCode { get; set; } = string.Empty;

    public string AccountName { get; set; } = string.Empty;

    public bool IsContra { get; set; }

    public bool IsActive { get; set; }
}

/// <summary>One account's postings summed for the trial balance's two periods: debits less credits, and the movement.</summary>
public sealed class AccountPeriodSums
{
    public long AccountId { get; set; }

    /// <summary>Debits less credits before the period's first day.</summary>
    public decimal NetBefore { get; set; }

    /// <summary>Debits less credits before the first day of the financial year the period starts in.</summary>
    public decimal NetBeforeYear { get; set; }

    public decimal MovementDebit { get; set; }

    public decimal MovementCredit { get; set; }

    public decimal CompareNetBefore { get; set; }

    public decimal CompareNetBeforeYear { get; set; }

    public decimal CompareMovementDebit { get; set; }

    public decimal CompareMovementCredit { get; set; }
}
