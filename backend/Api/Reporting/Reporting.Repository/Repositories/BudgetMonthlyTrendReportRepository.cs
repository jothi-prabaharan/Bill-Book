using Microsoft.EntityFrameworkCore;
using Reporting.Entity.Enums;
using Reporting.Entity.Models;
using Reporting.Repository.ReadModels;
using static Reporting.Repository.Repositories.BatchedNameResolverRepository;

namespace Reporting.Repository.Repositories;

/// <summary>
/// Budget Monthly Trend (TK-214): a budget against the books month by month
/// across its year, laid out as Budget vs Actual (TK-204): accounts by section
/// with each section's totals, gross profit and net profit.
///
/// <b>Every figure is Budget vs Actual's.</b> Each month is
/// <see cref="BudgetVsActualReportRepository.Compose"/> run over that month's
/// sums, the year to date is it run over the sums of the months through the
/// as-at date, and the full-year budget is it run over all twelve months'
/// budgets, so a month's cell equals Budget vs Actual's Month report for that
/// month, the year to date equals its year to date, and a variance's sign and
/// percentage follow the same rules. Nothing is worked out twice.
///
/// <b>Two queries, not thirteen.</b> Budget lines and postings are each summed
/// once in the database, by account and month, and everything after that is
/// arithmetic over those rows. An account made inactive since it moved is
/// shown, as on Budget vs Actual, so the year to date's actual net profit is
/// the Profit &amp; Loss's for the same dates.
///
/// <b>The as-at date's month is whole</b>, as on Budget vs Actual: a posting
/// later in that month counts. The months after it have no actual yet, so each
/// shows its budget alone.
/// </summary>
public sealed class BudgetMonthlyTrendReportRepository
{
    private static readonly ProfitAndLossSection[] SectionOrder =
    [
        ProfitAndLossSection.Revenue,
        ProfitAndLossSection.CostOfSales,
        ProfitAndLossSection.OtherIncome,
        ProfitAndLossSection.Expenses,
    ];

    private readonly ReportingDbContext _db;
    private readonly BatchedNameResolverRepository _master;

    public BudgetMonthlyTrendReportRepository(ReportingDbContext db, BatchedNameResolverRepository master)
    {
        _db = db;
        _master = master;
    }

    /// <summary>The report for a budget and a request the controller has already validated.</summary>
    public async Task<BudgetMonthlyTrendReportResultDto> RunAsync(
        BudgetVsActualBudgetDto budget, BudgetMonthlyTrendReportRequest request, CancellationToken ct)
    {
        DateOnly asOf = request.AsOfDate!.Value;
        BudgetWindow toDate = BudgetVsActualReportRepository.WindowOf(budget, asOf, BudgetPeriod.YearToDate);

        List<AccountTypeDto> types = await _master.GetAccountTypesFullAsync(ct);
        List<ProfitAndLossAccount> accounts = await BudgetVsActualReportRepository.AccountsOf(_db.Accounts).ToListAsync(ct);
        List<MonthAccountAmount> budgeted = await BudgetedByMonth(_db.BudgetLines, budget.BudgetId).ToListAsync(ct);
        List<MonthAccountAmount> actuals = await ActualsByMonth(_db.Ledger, budget.FinancialYearStart, toDate.To).ToListAsync(ct);

        return Compose(accounts, types, budgeted, actuals, budget, asOf);
    }

    /// <summary>Each account's budget for each month of the budget, as entered. LINQ only.</summary>
    public static IQueryable<MonthAccountAmount> BudgetedByMonth(IQueryable<BudgetLineRead> lines, long budgetId) =>
        lines
            .Where(l => l.BudgetId == budgetId)
            .GroupBy(l => new { l.AccountId, l.Period })
            .Select(g => new MonthAccountAmount { AccountId = g.Key.AccountId, Period = g.Key.Period, Amount = g.Sum(l => l.Amount) });

    /// <summary>
    /// Each account's debits less credits for each month of the budget's year
    /// from its first day to <paramref name="through"/>; the month is its place
    /// in the year, 1 to 12. LINQ only.
    /// </summary>
    public static IQueryable<MonthAccountAmount> ActualsByMonth(
        IQueryable<JournalLedgerRead> ledger, DateOnly yearStart, DateOnly through)
    {
        int baseIndex = (yearStart.Year * 12) + yearStart.Month - 1;

        return ledger
            .Where(l => l.LedgerDate >= yearStart && l.LedgerDate <= through)
            .Select(l => new
            {
                l.AccountId,
                Period = (l.LedgerDate.Year * 12) + l.LedgerDate.Month - baseIndex,
                Net = l.DebitAmountBase - l.CreditAmountBase,
            })
            .GroupBy(x => new { x.AccountId, x.Period })
            .Select(g => new MonthAccountAmount { AccountId = g.Key.AccountId, Period = g.Key.Period, Amount = g.Sum(x => x.Net) });
    }

    /// <summary>
    /// The report from the accounts, the account types and the sums by account
    /// and month. Pure.
    ///
    /// Budget vs Actual's composition is run once for each of the twelve
    /// months, once for the year to date and once for the full year's budget;
    /// each account then reads its twelve cells and its totals from those. An
    /// account appears when any of them has a line for it. A cell with no line
    /// is zero. A month after the as-at date's has its budget and a null actual.
    /// </summary>
    public static BudgetMonthlyTrendReportResultDto Compose(
        IReadOnlyList<ProfitAndLossAccount> accounts,
        IReadOnlyList<AccountTypeDto> types,
        IReadOnlyList<MonthAccountAmount> budgeted,
        IReadOnlyList<MonthAccountAmount> actuals,
        BudgetVsActualBudgetDto budget,
        DateOnly asOf)
    {
        DateOnly yearStart = budget.FinancialYearStart;
        BudgetWindow toDate = BudgetVsActualReportRepository.WindowOf(budget, asOf, BudgetPeriod.YearToDate);
        int asOfPeriod = toDate.PeriodTo;

        List<BudgetWindow> windows =
        [
            .. Enumerable.Range(1, 12)
                .Select(p => BudgetVsActualReportRepository.WindowOf(budget, yearStart.AddMonths(p - 1), BudgetPeriod.Month)),
        ];

        List<BudgetMonthlyTrendMonthDto> months =
        [
            .. windows.Select((w, i) => new BudgetMonthlyTrendMonthDto
            {
                Period = i + 1,
                FromDate = w.From,
                ToDate = w.To,
                IsFuture = i + 1 > asOfPeriod,
            }),
        ];

        BudgetVsActualReportResultDto[] monthly =
        [
            .. months.Select((m, i) => BudgetVsActualReportRepository.Compose(
                accounts, types,
                Sum(budgeted, m.Period, m.Period),
                m.IsFuture ? [] : Sum(actuals, m.Period, m.Period),
                budget, m.FromDate, BudgetPeriod.Month, windows[i])),
        ];

        BudgetVsActualReportResultDto yearToDate = BudgetVsActualReportRepository.Compose(
            accounts, types, Sum(budgeted, 1, asOfPeriod), Sum(actuals, 1, asOfPeriod),
            budget, asOf, BudgetPeriod.YearToDate, toDate);

        // The whole year's budget, with nothing posted against it.
        BudgetVsActualReportResultDto fullYear = BudgetVsActualReportRepository.Compose(
            accounts, types, Sum(budgeted, 1, 12), [],
            budget, asOf, BudgetPeriod.YearToDate,
            new BudgetWindow(yearStart, 1, 12, yearStart, budget.FinancialYearEnd));

        List<BudgetMonthlyTrendSectionDto> sections = [];
        foreach (ProfitAndLossSection section in SectionOrder)
        {
            BudgetVsActualSectionDto[] byMonth = [.. monthly.Select(r => r.Sections.Single(s => s.Section == section))];
            BudgetVsActualSectionDto toDateSection = yearToDate.Sections.Single(s => s.Section == section);
            BudgetVsActualSectionDto yearSection = fullYear.Sections.Single(s => s.Section == section);

            Dictionary<long, BudgetVsActualLineDto> seen = [];
            foreach (BudgetVsActualSectionDto source in byMonth.Append(toDateSection).Append(yearSection))
            {
                foreach (BudgetVsActualLineDto line in source.Lines)
                {
                    seen.TryAdd(line.AccountId, line);
                }
            }

            sections.Add(new BudgetMonthlyTrendSectionDto
            {
                Section = section,
                Title = toDateSection.Title,
                Lines =
                [
                    .. seen.Values
                        .OrderBy(l => l.AccountCode, StringComparer.Ordinal)
                        .Select(l => new BudgetMonthlyTrendLineDto
                        {
                            AccountId = l.AccountId,
                            AccountCode = l.AccountCode,
                            AccountName = l.AccountName,
                            IsContra = l.IsContra,
                            Months =
                            [
                                .. byMonth.Select((s, i) =>
                                    Cell(s.Lines.FirstOrDefault(x => x.AccountId == l.AccountId), months[i].IsFuture)),
                            ],
                            YearToDate = Cell(toDateSection.Lines.FirstOrDefault(x => x.AccountId == l.AccountId), future: false),
                            YearBudget = yearSection.Lines.FirstOrDefault(x => x.AccountId == l.AccountId)?.Budget ?? 0m,
                        }),
                ],
                Months = [.. byMonth.Select((s, i) => Cell(s.Budget, s.Actual, s.Variance, s.VariancePercent, months[i].IsFuture))],
                YearToDate = Cell(toDateSection.Budget, toDateSection.Actual, toDateSection.Variance, toDateSection.VariancePercent, future: false),
                YearBudget = yearSection.Budget,
            });
        }

        return new BudgetMonthlyTrendReportResultDto
        {
            Budget = budget,
            AsOfDate = asOf,
            AsOfPeriod = asOfPeriod,
            FromDate = yearStart,
            ThroughDate = toDate.To,
            Months = months,
            Sections = sections,
            GrossProfit = Profit(monthly.Select(r => r.GrossProfit), yearToDate.GrossProfit, fullYear.GrossProfit, months),
            NetProfit = Profit(monthly.Select(r => r.NetProfit), yearToDate.NetProfit, fullYear.NetProfit, months),
        };
    }

    /// <summary>Sums by account and month added up by account over the months from one to another. Pure.</summary>
    private static List<AccountAmount> Sum(IEnumerable<MonthAccountAmount> amounts, int fromPeriod, int toPeriod) =>
        [
            .. amounts
                .Where(a => a.Period >= fromPeriod && a.Period <= toPeriod)
                .GroupBy(a => a.AccountId)
                .Select(g => new AccountAmount { AccountId = g.Key, Amount = g.Sum(a => a.Amount) }),
        ];

    private static BudgetMonthlyTrendProfitDto Profit(
        IEnumerable<BudgetVsActualProfitDto> byMonth,
        BudgetVsActualProfitDto yearToDate,
        BudgetVsActualProfitDto fullYear,
        IReadOnlyList<BudgetMonthlyTrendMonthDto> months) =>
        new()
        {
            Months = [.. byMonth.Select((p, i) => Cell(p.Budget, p.Actual, p.Variance, p.VariancePercent, months[i].IsFuture))],
            YearToDate = Cell(yearToDate.Budget, yearToDate.Actual, yearToDate.Variance, yearToDate.VariancePercent, future: false),
            YearBudget = fullYear.Budget,
        };

    /// <summary>An account's cell for a month, or zeros where the month has no line for it.</summary>
    private static BudgetMonthlyTrendCellDto Cell(BudgetVsActualLineDto? line, bool future) =>
        line is null
            ? Cell(0m, 0m, 0m, null, future)
            : Cell(line.Budget, line.Actual, line.Variance, line.VariancePercent, future);

    /// <summary>A cell; a future month keeps its budget and has no actual, variance or percentage.</summary>
    private static BudgetMonthlyTrendCellDto Cell(
        decimal budget, decimal actual, decimal variance, decimal? percent, bool future) =>
        new()
        {
            Budget = budget,
            Actual = future ? null : actual,
            Variance = future ? null : variance,
            VariancePercent = future ? null : percent,
        };
}

/// <summary>One account's summed amount for one month of a budget's year: a budget as entered, or debits less credits.</summary>
public sealed class MonthAccountAmount
{
    public long AccountId { get; set; }

    /// <summary>The month's place in the budget's year, 1 to 12.</summary>
    public int Period { get; set; }

    public decimal Amount { get; set; }
}
