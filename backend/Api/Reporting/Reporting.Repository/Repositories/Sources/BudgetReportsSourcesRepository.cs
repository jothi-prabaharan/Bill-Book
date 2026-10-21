using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Reporting.Entity.Enums;
using Reporting.Repository;
using Reporting.Repository.ReadModels;
using Shared.Kernel.Apps;
using Shared.Kernel.Numbering;

namespace Reporting.Repository.Repositories;

// -----------------------------------------------------------------------------
// Budget reports (TK-109, design "Budgeting" → Reports): budget against actual
// for a month, a quarter or the year to date, the same by project, and a
// monthly trend. Actuals use the P&L source's own rule — income is credit
// less debit and every other type debit less credit, over active income and
// expense accounts — so the two reports never disagree. A budget amount is
// entered positive in the account's normal direction, so a contra account's
// is negated to sit on the P&L's sign.
// -----------------------------------------------------------------------------

/// <summary>The months and dates a budget report covers.</summary>
public sealed record BudgetWindow(DateOnly YearStart, int PeriodFrom, int PeriodTo, DateOnly From, DateOnly To);

/// <summary>The budget a report is against, and the window it reads.</summary>
public sealed record BudgetSelection(long? BudgetId, bool ByProject, BudgetWindow Window);

public static class BudgetWindows
{
    public const string Month = "month";
    public const string Quarter = "quarter";
    public const string YearToDate = "ytd";

    /// <summary>
    /// The window for <paramref name="asOf"/>: its month, its financial
    /// quarter, or the financial year up to the end of its month. Whole months
    /// always, because a budget is monthly. A date outside the year is taken
    /// as the year's nearest month.
    /// </summary>
    public static BudgetWindow For(DateOnly yearStart, DateOnly asOf, string? scope)
    {
        int months = ((asOf.Year - yearStart.Year) * 12) + asOf.Month - yearStart.Month;
        int period = Math.Clamp(months + 1, 1, 12);

        (int from, int to) = (scope ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            Month => (period, period),
            Quarter => ((((period - 1) / 3) * 3) + 1, (((period - 1) / 3) * 3) + 3),
            _ => (1, period),
        };

        return new BudgetWindow(yearStart, from, to, yearStart.AddMonths(from - 1), yearStart.AddMonths(to).AddDays(-1));
    }

    /// <summary>The first day of the financial year holding <paramref name="date"/>.</summary>
    public static DateOnly YearStartOf(DateOnly date, int startMonth) =>
        new(date.Month >= startMonth ? date.Year : date.Year - 1, startMonth, 1);

    /// <summary>
    /// The budget named by <c>budgetId</c>, or else the branch's approved budget
    /// for the year holding <c>asOf</c> (today when not given). With no budget
    /// the report still runs, showing actuals against nothing.
    /// </summary>
    public static async Task<BudgetSelection> ResolveAsync(
        ReportParameters parameters, ReportingDbContext db, IFinancialYearProvider financialYear, CancellationToken ct)
    {
        DateOnly asOf = parameters.Date("asOf") ?? DateOnly.FromDateTime(DateTime.UtcNow);
        string? scope = parameters.Text("scope");

        BudgetRead? budget = parameters.Id("budgetId") is long id
            ? await db.Budgets.AsNoTracking().FirstOrDefaultAsync(b => b.BudgetId == id, ct)
            : await db.Budgets.AsNoTracking()
                .Where(b => b.IsBranchDefault && b.Status == "Approved" && b.FinancialYearStart <= asOf)
                .OrderByDescending(b => b.FinancialYearStart)
                .FirstOrDefaultAsync(ct);

        if (budget is not null && parameters.Id("budgetId") is null && budget.FinancialYearStart.AddYears(1) <= asOf)
        {
            // The latest default is for an earlier year; this year has none.
            budget = null;
        }

        DateOnly yearStart = budget?.FinancialYearStart
            ?? YearStartOf(asOf, await financialYear.GetStartMonthAsync(ct));

        return new BudgetSelection(budget?.BudgetId, budget?.ByProject ?? false, For(yearStart, asOf, scope));
    }

    internal static IReadOnlyList<ReportParameterRepository> Parameters(bool withScope)
    {
        List<ReportParameterRepository> parameters = [new() { Name = "asOf", Label = "As at", DataType = ColumnDataType.Date }];

        if (withScope)
        {
            parameters.Add(new() { Name = "scope", Label = "Period: month, quarter or ytd", DataType = ColumnDataType.Text, DefaultValue = YearToDate });
        }

        parameters.Add(new() { Name = "budgetId", Label = "Budget (the branch's when blank)", DataType = ColumnDataType.Number });
        return parameters;
    }
}

/// <summary>The budget reports' queries, composed and never executed here.</summary>
public static class BudgetReports
{
    private const int Income = 4;
    private const int Expense = 5;

    /// <summary>
    /// One row per active income or expense account with a budget or an actual
    /// in the window. Budget lines and postings are each reduced to one figure
    /// per account and summed together before the accounts are joined: a
    /// sub-query per account (TK-109 as first written) cannot be translated,
    /// and the report answered 500 (TK-207).
    /// </summary>
    public static IQueryable<BudgetActualRow> AgainstActual(
        IQueryable<AccountRead> accounts,
        IQueryable<JournalLedgerRead> ledger,
        IQueryable<BudgetLineRead> lines,
        long? budgetId,
        BudgetWindow window)
    {
        DateOnly start = window.From;
        DateOnly end = window.To;
        int periodFrom = window.PeriodFrom;
        int periodTo = window.PeriodTo;

        var planned =
            from l in lines
            where l.BudgetId == budgetId && l.Period >= periodFrom && l.Period <= periodTo
            select new { l.AccountId, Budget = l.Amount, Net = 0m };
        var posted =
            from l in ledger
            where l.LedgerDate >= start && l.LedgerDate <= end
            select new { l.AccountId, Budget = 0m, Net = l.DebitAmountBase - l.CreditAmountBase };
        var sums = planned.Concat(posted)
            .GroupBy(x => x.AccountId)
            .Select(g => new { AccountId = g.Key, Budget = g.Sum(x => x.Budget), Net = g.Sum(x => x.Net) });

        IQueryable<BudgetActualRow> rows =
            from s in sums
            join a in accounts on s.AccountId equals a.AccountId
            where a.IsActive && (a.AccountTypeId == Income || a.AccountTypeId == Expense)
            select new BudgetActualRow
            {
                AccountId = a.AccountId,
                AccountTypeId = a.AccountTypeId,
                AccountCode = a.AccountCode,
                AccountName = a.AccountName,
                AccountType = a.AccountTypeId == Income ? "Income" : "Expense",
                Budget = a.IsContra ? -s.Budget : s.Budget,
                Actual = a.AccountTypeId == Income ? -s.Net : s.Net,
            };

        return rows.Where(r => r.Budget != 0m || r.Actual != 0m).Select(Finish);
    }

    /// <summary>
    /// One row per project and account with a budget or an actual in the
    /// window. Needs a budget by project for the budget column. Summed by
    /// project and account before the accounts and projects are joined, for
    /// the reason <see cref="AgainstActual"/> gives.
    /// </summary>
    public static IQueryable<BudgetActualRow> AgainstActualByProject(
        IQueryable<AccountRead> accounts,
        IQueryable<ProjectRead> projects,
        IQueryable<JournalLedgerRead> ledger,
        IQueryable<BudgetLineRead> lines,
        long? budgetId,
        BudgetWindow window)
    {
        DateOnly start = window.From;
        DateOnly end = window.To;
        int periodFrom = window.PeriodFrom;
        int periodTo = window.PeriodTo;

        var planned =
            from l in lines
            where l.BudgetId == budgetId && l.ProjectId != null && l.Period >= periodFrom && l.Period <= periodTo
            select new { l.AccountId, ProjectId = l.ProjectId!.Value, Budget = l.Amount, Net = 0m };
        var posted =
            from l in ledger
            where l.ProjectId != null && l.LedgerDate >= start && l.LedgerDate <= end
            select new { l.AccountId, ProjectId = l.ProjectId!.Value, Budget = 0m, Net = l.DebitAmountBase - l.CreditAmountBase };
        var sums = planned.Concat(posted)
            .GroupBy(x => new { x.AccountId, x.ProjectId })
            .Select(g => new { g.Key.AccountId, g.Key.ProjectId, Budget = g.Sum(x => x.Budget), Net = g.Sum(x => x.Net) });

        IQueryable<BudgetActualRow> rows =
            from s in sums
            join a in accounts on s.AccountId equals a.AccountId
            where a.IsActive && (a.AccountTypeId == Income || a.AccountTypeId == Expense)
            join p in projects on s.ProjectId equals p.ProjectId
            select new BudgetActualRow
            {
                RowKey = p.ProjectCode + "/" + a.AccountCode,
                ProjectCode = p.ProjectCode,
                ProjectName = p.ProjectName,
                AccountId = a.AccountId,
                AccountTypeId = a.AccountTypeId,
                AccountCode = a.AccountCode,
                AccountName = a.AccountName,
                AccountType = a.AccountTypeId == Income ? "Income" : "Expense",
                Budget = a.IsContra ? -s.Budget : s.Budget,
                Actual = a.AccountTypeId == Income ? -s.Net : s.Net,
            };

        return rows.Where(r => r.Budget != 0m || r.Actual != 0m).Select(Finish);
    }

    /// <summary>One row per account and month of the budget's year: budget and actual side by side, for a pivot by month.</summary>
    public static IQueryable<BudgetTrendRow> MonthlyTrend(
        IQueryable<AccountRead> accounts,
        IQueryable<JournalLedgerRead> ledger,
        IQueryable<BudgetLineRead> lines,
        long? budgetId,
        DateOnly yearStart)
    {
        DateOnly yearEnd = yearStart.AddYears(1);
        int baseIndex = (yearStart.Year * 12) + yearStart.Month - 1;

        var plannedRows =
            from l in lines
            where l.BudgetId == budgetId
            join a in accounts on l.AccountId equals a.AccountId
            where a.IsActive && (a.AccountTypeId == Income || a.AccountTypeId == Expense)
            select new { l.AccountId, Period = (int)l.Period, Budget = a.IsContra ? -l.Amount : l.Amount, Actual = 0m };

        var actualRows =
            from l in ledger
            where l.LedgerDate >= yearStart && l.LedgerDate < yearEnd
            join a in accounts on l.AccountId equals a.AccountId
            where a.IsActive && (a.AccountTypeId == Income || a.AccountTypeId == Expense)
            select new
            {
                l.AccountId,
                Period = (l.LedgerDate.Year * 12) + l.LedgerDate.Month - baseIndex,
                Budget = 0m,
                Actual = a.AccountTypeId == Income ? l.CreditAmountBase - l.DebitAmountBase : l.DebitAmountBase - l.CreditAmountBase,
            };

        var merged = plannedRows.Concat(actualRows)
            .GroupBy(x => new { x.AccountId, x.Period })
            .Select(g => new { g.Key.AccountId, g.Key.Period, Budget = g.Sum(x => x.Budget), Actual = g.Sum(x => x.Actual) });

        return from m in merged
               join a in accounts on m.AccountId equals a.AccountId
               let index = baseIndex + m.Period - 1
               let month = (index % 12) + 1
               let label = (index / 12).ToString() + "-" + (month < 10 ? "0" : string.Empty) + month.ToString()
               select new BudgetTrendRow
               {
                   RowKey = a.AccountCode + "/" + label,
                   AccountId = a.AccountId,
                   AccountCode = a.AccountCode,
                   AccountName = a.AccountName,
                   AccountType = a.AccountTypeId == Income ? "Income" : "Expense",
                   Month = label,
                   Period = m.Period,
                   Budget = m.Budget,
                   Actual = m.Actual,
                   Variance = a.AccountTypeId == Income ? m.Actual - m.Budget : m.Budget - m.Actual,
               };
    }

    /// <summary>
    /// Variance is signed so that positive is good: income above budget, or
    /// expense below it. The percentage is of the budget, and blank without one.
    /// </summary>
    private static readonly Expression<Func<BudgetActualRow, BudgetActualRow>> Finish = r => new BudgetActualRow
    {
        RowKey = r.RowKey,
        ProjectCode = r.ProjectCode,
        ProjectName = r.ProjectName,
        AccountId = r.AccountId,
        AccountTypeId = r.AccountTypeId,
        AccountCode = r.AccountCode,
        AccountName = r.AccountName,
        AccountType = r.AccountType,
        Budget = r.Budget,
        Actual = r.Actual,
        Variance = r.AccountTypeId == Income ? r.Actual - r.Budget : r.Budget - r.Actual,
        VariancePercent = r.Budget != 0m
            ? Math.Round((r.AccountTypeId == Income ? r.Actual - r.Budget : r.Budget - r.Actual) / Math.Abs(r.Budget) * 100m, 2)
            : null,
        Result = (r.AccountTypeId == Income ? r.Actual - r.Budget : r.Budget - r.Actual) >= 0m ? "Favourable" : "Adverse",
    };
}

public sealed class BudgetActualRow
{
    public string? RowKey { get; set; }
    public string? ProjectCode { get; set; }
    public string? ProjectName { get; set; }
    public long AccountId { get; set; }
    public int AccountTypeId { get; set; }
    public string AccountCode { get; set; } = null!;
    public string AccountName { get; set; } = null!;
    public string AccountType { get; set; } = null!;
    public decimal Budget { get; set; }
    public decimal Actual { get; set; }
    public decimal Variance { get; set; }
    public decimal? VariancePercent { get; set; }
    public string Result { get; set; } = null!;
}

public sealed class BudgetTrendRow
{
    public string RowKey { get; set; } = null!;
    public long AccountId { get; set; }
    public string AccountCode { get; set; } = null!;
    public string AccountName { get; set; } = null!;
    public string AccountType { get; set; } = null!;

    /// <summary><c>2026-04</c>: text so it can group and pivot.</summary>
    public string Month { get; set; } = null!;

    public int Period { get; set; }
    public decimal Budget { get; set; }
    public decimal Actual { get; set; }
    public decimal Variance { get; set; }
}

public sealed class BudgetAgainstActualSource : ReportSource<BudgetActualRow>
{
    private readonly IFinancialYearProvider _financialYear;
    private BudgetSelection? _selection;

    public BudgetAgainstActualSource(IFinancialYearProvider financialYear) => _financialYear = financialYear;

    public override string ReportKey => "budget-vs-actual";
    public override string Title => "Budget vs Actual";
    public override ReportModule Module => ReportModule.Accounting;
    public override string RequiredPermission => "accounting.view";
    public override AddOn RequiredAddOn => AddOn.Budget;
    public override IReadOnlyList<ReportParameterRepository> Parameters => BudgetWindows.Parameters(withScope: true);

    public override IReadOnlyList<ReportColumn> Columns =>
    [
        ReportColumn.Of<BudgetActualRow, string>("accountType", ColumnDataType.Text, r => r.AccountType, groupable: true),
        ReportColumn.Of<BudgetActualRow, string>("accountCode", ColumnDataType.Text, r => r.AccountCode),
        ReportColumn.Of<BudgetActualRow, string>("accountName", ColumnDataType.Text, r => r.AccountName),
        ReportColumn.Of<BudgetActualRow, decimal>("budget", ColumnDataType.Money, r => r.Budget, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<BudgetActualRow, decimal>("actual", ColumnDataType.Money, r => r.Actual, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<BudgetActualRow, decimal>("variance", ColumnDataType.Money, r => r.Variance, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<BudgetActualRow, decimal?>("variancePercent", ColumnDataType.Percent, r => r.VariancePercent),
        ReportColumn.Of<BudgetActualRow, string>("result", ColumnDataType.Text, r => r.Result, groupable: true),
        ReportColumn.Of<BudgetActualRow, long>("accountId", ColumnDataType.Number, r => r.AccountId, filterable: false),
    ];

    protected override async Task PrepareAsync(ReportParameters parameters, ReportingDbContext db, CancellationToken ct) =>
        _selection = await BudgetWindows.ResolveAsync(parameters, db, _financialYear, ct);

    protected override IQueryable<BudgetActualRow> Build(ReportParameters parameters, ReportingDbContext db) =>
        BudgetReports.AgainstActual(db.Accounts, db.Ledger, db.BudgetLines, _selection!.BudgetId, _selection.Window);

    protected override LambdaExpression DefaultOrder =>
        (Expression<Func<BudgetActualRow, string>>)(r => r.AccountCode);
}

public sealed class BudgetAgainstActualByProjectSource : ReportSource<BudgetActualRow>
{
    private readonly IFinancialYearProvider _financialYear;
    private BudgetSelection? _selection;

    public BudgetAgainstActualByProjectSource(IFinancialYearProvider financialYear) => _financialYear = financialYear;

    public override string ReportKey => "budget-vs-actual-by-project";
    public override string Title => "Budget vs Actual by Project";
    public override ReportModule Module => ReportModule.Accounting;
    public override string RequiredPermission => "accounting.view";
    public override AddOn RequiredAddOn => AddOn.Budget;
    public override IReadOnlyList<ReportParameterRepository> Parameters => BudgetWindows.Parameters(withScope: true);

    public override IReadOnlyList<ReportColumn> Columns =>
    [
        ReportColumn.Of<BudgetActualRow, string?>("projectCode", ColumnDataType.Text, r => r.ProjectCode, groupable: true),
        ReportColumn.Of<BudgetActualRow, string?>("projectName", ColumnDataType.Text, r => r.ProjectName, groupable: true),
        ReportColumn.Of<BudgetActualRow, string>("accountType", ColumnDataType.Text, r => r.AccountType, groupable: true),
        ReportColumn.Of<BudgetActualRow, string>("accountCode", ColumnDataType.Text, r => r.AccountCode),
        ReportColumn.Of<BudgetActualRow, string>("accountName", ColumnDataType.Text, r => r.AccountName),
        ReportColumn.Of<BudgetActualRow, decimal>("budget", ColumnDataType.Money, r => r.Budget, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<BudgetActualRow, decimal>("actual", ColumnDataType.Money, r => r.Actual, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<BudgetActualRow, decimal>("variance", ColumnDataType.Money, r => r.Variance, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<BudgetActualRow, decimal?>("variancePercent", ColumnDataType.Percent, r => r.VariancePercent),
        ReportColumn.Of<BudgetActualRow, string>("result", ColumnDataType.Text, r => r.Result, groupable: true),
    ];

    protected override async Task PrepareAsync(ReportParameters parameters, ReportingDbContext db, CancellationToken ct) =>
        _selection = await BudgetWindows.ResolveAsync(parameters, db, _financialYear, ct);

    protected override IQueryable<BudgetActualRow> Build(ReportParameters parameters, ReportingDbContext db) =>
        BudgetReports.AgainstActualByProject(
            db.Accounts,
            db.Projects,
            db.Ledger,
            db.BudgetLines,
            _selection!.ByProject ? _selection.BudgetId : null,
            _selection.Window);

    protected override LambdaExpression DefaultOrder =>
        (Expression<Func<BudgetActualRow, string?>>)(r => r.RowKey);
}

public sealed class BudgetMonthlyTrendSource : ReportSource<BudgetTrendRow>
{
    private readonly IFinancialYearProvider _financialYear;
    private BudgetSelection? _selection;

    public BudgetMonthlyTrendSource(IFinancialYearProvider financialYear) => _financialYear = financialYear;

    public override string ReportKey => "budget-monthly-trend";
    public override string Title => "Budget Monthly Trend";
    public override ReportModule Module => ReportModule.Accounting;
    public override string RequiredPermission => "accounting.view";
    public override AddOn RequiredAddOn => AddOn.Budget;
    public override IReadOnlyList<ReportParameterRepository> Parameters => BudgetWindows.Parameters(withScope: false);

    public override IReadOnlyList<ReportColumn> Columns =>
    [
        ReportColumn.Of<BudgetTrendRow, string>("accountType", ColumnDataType.Text, r => r.AccountType, groupable: true),
        ReportColumn.Of<BudgetTrendRow, string>("accountCode", ColumnDataType.Text, r => r.AccountCode, groupable: true),
        ReportColumn.Of<BudgetTrendRow, string>("accountName", ColumnDataType.Text, r => r.AccountName, groupable: true),
        ReportColumn.Of<BudgetTrendRow, string>("month", ColumnDataType.Text, r => r.Month, groupable: true),
        ReportColumn.Of<BudgetTrendRow, int>("period", ColumnDataType.Number, r => r.Period),
        ReportColumn.Of<BudgetTrendRow, decimal>("budget", ColumnDataType.Money, r => r.Budget, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<BudgetTrendRow, decimal>("actual", ColumnDataType.Money, r => r.Actual, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<BudgetTrendRow, decimal>("variance", ColumnDataType.Money, r => r.Variance, aggregate: AggregateFunction.Sum),
    ];

    protected override async Task PrepareAsync(ReportParameters parameters, ReportingDbContext db, CancellationToken ct) =>
        _selection = await BudgetWindows.ResolveAsync(parameters, db, _financialYear, ct);

    protected override IQueryable<BudgetTrendRow> Build(ReportParameters parameters, ReportingDbContext db) =>
        BudgetReports.MonthlyTrend(db.Accounts, db.Ledger, db.BudgetLines, _selection!.BudgetId, _selection.Window.YearStart);

    protected override LambdaExpression DefaultOrder =>
        (Expression<Func<BudgetTrendRow, string>>)(r => r.RowKey);
}
