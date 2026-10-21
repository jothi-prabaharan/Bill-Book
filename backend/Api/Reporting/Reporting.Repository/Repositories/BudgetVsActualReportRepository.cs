using Microsoft.EntityFrameworkCore;
using Reporting.Entity.Enums;
using Reporting.Entity.Models;
using Reporting.Repository.ReadModels;
using static Reporting.Repository.Repositories.BatchedNameResolverRepository;

namespace Reporting.Repository.Repositories;

/// <summary>
/// Budget vs Actual (TK-204): one budget against the books for whole months of
/// its year (the as-at date's month, its quarter, or the year to date), laid
/// out as the Profit &amp; Loss with gross and net profit, each line with its
/// variance, positive when favourable.
///
/// <b>The window is the catalog report's</b> (<see cref="BudgetWindows.For"/>),
/// so the two cover the same months. <b>The layout is the Profit &amp; Loss's</b>
/// (<see cref="ProfitAndLossReportRepository.SectionOf"/>), so revenue, cost of
/// sales and the profit lines mean the same on both pages, and the actual net
/// profit for the months covered is the Profit &amp; Loss's for the same dates.
/// To keep that true an account that moved is shown even when it has since been
/// made inactive.
///
/// <b>Budgets and actuals are summed in the database</b>, one row per account
/// each. A budget amount is entered positive in its account's normal direction,
/// so a contra account's is negated to sit on the Profit &amp; Loss's sign; a
/// budget by project counts every project's lines under the account.
/// </summary>
public sealed class BudgetVsActualReportRepository
{
    private readonly ReportingDbContext _db;
    private readonly BatchedNameResolverRepository _master;

    public BudgetVsActualReportRepository(ReportingDbContext db, BatchedNameResolverRepository master)
    {
        _db = db;
        _master = master;
    }

    /// <summary>The branch's budgets, the latest year first and its own budget before the others.</summary>
    public async Task<IReadOnlyList<BudgetVsActualBudgetDto>> BudgetsAsync(CancellationToken ct) =>
        [
            .. (await _db.Budgets
                .OrderByDescending(b => b.FinancialYearStart)
                .ThenByDescending(b => b.IsBranchDefault)
                .ThenBy(b => b.Name)
                .ToListAsync(ct))
                .Select(ToDto),
        ];

    /// <summary>One of the branch's budgets, or null when it is not the branch's.</summary>
    public async Task<BudgetVsActualBudgetDto?> FindBudgetAsync(long budgetId, CancellationToken ct) =>
        await _db.Budgets.FirstOrDefaultAsync(b => b.BudgetId == budgetId, ct) is BudgetRead budget ? ToDto(budget) : null;

    /// <summary>Why the as-at date cannot be used with the budget, or null. It must fall within the budget's year. Pure.</summary>
    public static string? DateRefusal(BudgetVsActualBudgetDto budget, DateOnly asOf) =>
        asOf < budget.FinancialYearStart || asOf > budget.FinancialYearEnd
            ? $"Choose a date within the budget's year, {budget.FinancialYearStart:dd MMM yyyy} to {budget.FinancialYearEnd:dd MMM yyyy}."
            : null;

    /// <summary>The report for a budget and a request the controller has already validated.</summary>
    public async Task<BudgetVsActualReportResultDto> RunAsync(
        BudgetVsActualBudgetDto budget, BudgetVsActualReportRequest request, CancellationToken ct)
    {
        DateOnly asOf = request.AsOfDate!.Value;
        BudgetPeriod period = request.Period!.Value;
        BudgetWindow window = WindowOf(budget, asOf, period);

        List<AccountTypeDto> types = await _master.GetAccountTypesFullAsync(ct);

        List<ProfitAndLossAccount> accounts = await AccountsOf(_db.Accounts).ToListAsync(ct);

        List<AccountAmount> budgeted = await Budgeted(_db.BudgetLines, budget.BudgetId, window.PeriodFrom, window.PeriodTo).ToListAsync(ct);
        List<AccountAmount> actuals = await Actuals(_db.Ledger, window.From, window.To).ToListAsync(ct);

        return Compose(accounts, types, budgeted, actuals, budget, asOf, period, window);
    }

    /// <summary>
    /// Every account of the branch, active or not, as the layout needs it: an
    /// account made inactive since it moved still has to be shown. LINQ only.
    /// </summary>
    public static IQueryable<ProfitAndLossAccount> AccountsOf(IQueryable<AccountRead> accounts) =>
        accounts.Select(a => new ProfitAndLossAccount
        {
            AccountId = a.AccountId,
            AccountTypeId = a.AccountTypeId,
            AccountCode = a.AccountCode,
            AccountName = a.AccountName,
            AccountSystemName = a.AccountSystemName,
            IsContra = a.IsContra,
            IsSales = a.IsSales,
            IsActive = a.IsActive,
        });

    /// <summary>The whole months the as-at date and period cover in the budget's year. Pure.</summary>
    public static BudgetWindow WindowOf(BudgetVsActualBudgetDto budget, DateOnly asOf, BudgetPeriod period) =>
        BudgetWindows.For(budget.FinancialYearStart, asOf, period switch
        {
            BudgetPeriod.Month => BudgetWindows.Month,
            BudgetPeriod.Quarter => BudgetWindows.Quarter,
            _ => BudgetWindows.YearToDate,
        });

    /// <summary>Each account's budget in the months covered, as entered. LINQ only.</summary>
    public static IQueryable<AccountAmount> Budgeted(IQueryable<BudgetLineRead> lines, long budgetId, int periodFrom, int periodTo) =>
        lines
            .Where(l => l.BudgetId == budgetId && l.Period >= periodFrom && l.Period <= periodTo)
            .GroupBy(l => l.AccountId)
            .Select(g => new AccountAmount { AccountId = g.Key, Amount = g.Sum(l => l.Amount) });

    /// <summary>Each account's debits less credits between two dates. LINQ only.</summary>
    public static IQueryable<AccountAmount> Actuals(IQueryable<JournalLedgerRead> ledger, DateOnly from, DateOnly to) =>
        ledger
            .Where(l => l.LedgerDate >= from && l.LedgerDate <= to)
            .GroupBy(l => l.AccountId)
            .Select(g => new AccountAmount { AccountId = g.Key, Amount = g.Sum(l => l.DebitAmountBase - l.CreditAmountBase) });

    /// <summary>
    /// The report from the accounts, the budgeted and actual sums and the
    /// account types. Pure.
    ///
    /// An actual reads positive on its section's side, as on the Profit &amp;
    /// Loss; a budget is negated for a contra account. A variance is the actual
    /// less the budget for income and the budget less the actual for spending,
    /// so it is positive when favourable; for a profit line, higher is
    /// favourable. Accounts with neither a budget nor an actual are left out.
    /// </summary>
    public static BudgetVsActualReportResultDto Compose(
        IReadOnlyList<ProfitAndLossAccount> accounts,
        IReadOnlyList<AccountTypeDto> types,
        IReadOnlyList<AccountAmount> budgeted,
        IReadOnlyList<AccountAmount> actuals,
        BudgetVsActualBudgetDto budget,
        DateOnly asOf,
        BudgetPeriod period,
        BudgetWindow window)
    {
        Dictionary<int, AccountTypeDto> typeById = types.ToDictionary(t => t.AccountTypeId);
        Dictionary<long, decimal> budgetById = budgeted.ToDictionary(b => b.AccountId, b => b.Amount);
        Dictionary<long, decimal> actualById = actuals.ToDictionary(a => a.AccountId, a => a.Amount);

        Dictionary<ProfitAndLossSection, List<BudgetVsActualLineDto>> lines = new()
        {
            [ProfitAndLossSection.Revenue] = [],
            [ProfitAndLossSection.CostOfSales] = [],
            [ProfitAndLossSection.OtherIncome] = [],
            [ProfitAndLossSection.Expenses] = [],
        };

        foreach (ProfitAndLossAccount account in accounts)
        {
            if (!typeById.TryGetValue(account.AccountTypeId, out AccountTypeDto? type)
                || ProfitAndLossReportRepository.SectionOf(type, account) is not ProfitAndLossSection section)
            {
                continue;
            }

            decimal side = type.NormalBalance == "Debit" ? 1m : -1m;
            decimal actual = side * actualById.GetValueOrDefault(account.AccountId);
            decimal planned = budgetById.GetValueOrDefault(account.AccountId);
            decimal budgetAmount = account.IsContra ? -planned : planned;

            if (budgetAmount == 0m && actual == 0m)
            {
                continue;
            }

            decimal variance = VarianceOf(section, budgetAmount, actual);
            lines[section].Add(new BudgetVsActualLineDto
            {
                AccountId = account.AccountId,
                AccountCode = account.AccountCode,
                AccountName = account.AccountName,
                IsContra = account.IsContra,
                Budget = budgetAmount,
                Actual = actual,
                Variance = variance,
                VariancePercent = PercentOf(variance, budgetAmount),
            });
        }

        List<BudgetVsActualSectionDto> sections =
        [
            .. lines.Select(pair =>
            {
                decimal sectionBudget = pair.Value.Sum(l => l.Budget);
                decimal sectionActual = pair.Value.Sum(l => l.Actual);
                decimal variance = VarianceOf(pair.Key, sectionBudget, sectionActual);
                return new BudgetVsActualSectionDto
                {
                    Section = pair.Key,
                    Title = TitleOf(pair.Key),
                    Lines = [.. pair.Value.OrderBy(l => l.AccountCode, StringComparer.Ordinal)],
                    Budget = sectionBudget,
                    Actual = sectionActual,
                    Variance = variance,
                    VariancePercent = PercentOf(variance, sectionBudget),
                };
            }),
        ];

        BudgetVsActualSectionDto Of(ProfitAndLossSection s) => sections.Single(x => x.Section == s);
        BudgetVsActualSectionDto revenue = Of(ProfitAndLossSection.Revenue);
        BudgetVsActualSectionDto cost = Of(ProfitAndLossSection.CostOfSales);
        BudgetVsActualSectionDto other = Of(ProfitAndLossSection.OtherIncome);
        BudgetVsActualSectionDto expenses = Of(ProfitAndLossSection.Expenses);

        BudgetVsActualProfitDto gross = Profit(revenue.Budget - cost.Budget, revenue.Actual - cost.Actual);
        BudgetVsActualProfitDto net = Profit(
            gross.Budget + other.Budget - expenses.Budget,
            gross.Actual + other.Actual - expenses.Actual);

        return new BudgetVsActualReportResultDto
        {
            Budget = budget,
            AsOfDate = asOf,
            Period = period,
            PeriodFrom = window.PeriodFrom,
            PeriodTo = window.PeriodTo,
            FromDate = window.From,
            ToDate = window.To,
            Sections = sections,
            GrossProfit = gross,
            NetProfit = net,
        };
    }

    /// <summary>Actual less budget for income, budget less actual for spending: positive when favourable. Pure.</summary>
    public static decimal VarianceOf(ProfitAndLossSection section, decimal budget, decimal actual) =>
        section is ProfitAndLossSection.Revenue or ProfitAndLossSection.OtherIncome ? actual - budget : budget - actual;

    /// <summary>A variance as a percentage of its budget, to two places half away from zero; null without a budget. Pure.</summary>
    public static decimal? PercentOf(decimal variance, decimal budget) =>
        budget == 0m ? null : Math.Round(variance / Math.Abs(budget) * 100m, 2, MidpointRounding.AwayFromZero);

    private static BudgetVsActualProfitDto Profit(decimal budget, decimal actual) => new()
    {
        Budget = budget,
        Actual = actual,
        Variance = actual - budget,
        VariancePercent = PercentOf(actual - budget, budget),
    };

    private static string TitleOf(ProfitAndLossSection section) => section switch
    {
        ProfitAndLossSection.Revenue => "Revenue",
        ProfitAndLossSection.CostOfSales => "Cost of sales",
        ProfitAndLossSection.OtherIncome => "Other income",
        _ => "Expenses",
    };

    private static BudgetVsActualBudgetDto ToDto(BudgetRead budget) => new()
    {
        BudgetId = budget.BudgetId,
        Name = budget.Name,
        FinancialYearStart = budget.FinancialYearStart,
        FinancialYearEnd = budget.FinancialYearStart.AddYears(1).AddDays(-1),
        Status = Enum.TryParse(budget.Status, out BudgetStatus status) ? status : BudgetStatus.Draft,
        IsBranchDefault = budget.IsBranchDefault,
        ByProject = budget.ByProject,
    };
}

/// <summary>One account's summed amount: a budget as entered, or debits less credits.</summary>
public sealed class AccountAmount
{
    public long AccountId { get; set; }
    public decimal Amount { get; set; }
}
