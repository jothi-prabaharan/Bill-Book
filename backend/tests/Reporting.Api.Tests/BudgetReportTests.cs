using Reporting.Api.Services.Sources;
using Reporting.Repository.ReadModels;
using Shared.Kernel.Numbering;
using Xunit;

namespace Reporting.Api.Tests;

/// <summary>
/// The budget reports (TK-109), over lists. The card's "done when" is the
/// first test: the actual column is the P&L's figure for the same period,
/// asserted against the P&L source's own query rather than a copy of its rule.
/// Whether the queries translate still needs a real <c>acc</c> schema.
/// </summary>
public sealed class BudgetReportTests
{
    private static readonly DateOnly April = new(2026, 4, 1);

    private const long Sales = 1;
    private const long Returns = 2;
    private const long Rent = 3;
    private const long Bank = 4;
    private const long Dormant = 5;
    private const long BudgetId = 70;

    private static readonly List<AccountRead> Accounts =
    [
        new() { AccountId = Sales, AccountTypeId = 4, AccountCode = "4000", AccountName = "Sales", IsActive = true },
        new() { AccountId = Returns, AccountTypeId = 4, IsContra = true, AccountCode = "4100", AccountName = "Sales returns", IsActive = true },
        new() { AccountId = Rent, AccountTypeId = 5, AccountCode = "6100", AccountName = "Rent", IsActive = true },
        new() { AccountId = Bank, AccountTypeId = 1, AccountCode = "1020", AccountName = "Bank", IsActive = true },
        new() { AccountId = Dormant, AccountTypeId = 5, AccountCode = "6900", AccountName = "Old expense", IsActive = false },
    ];

    private static readonly List<JournalLedgerRead> Ledger =
    [
        Leg(Sales, new DateOnly(2026, 4, 10), credit: 50_000m),
        Leg(Sales, new DateOnly(2026, 5, 3), credit: 30_000m),
        Leg(Sales, new DateOnly(2026, 5, 20), debit: 2_000m),
        Leg(Returns, new DateOnly(2026, 5, 21), debit: 1_500m),
        Leg(Rent, new DateOnly(2026, 4, 1), debit: 20_000m),
        Leg(Rent, new DateOnly(2026, 5, 1), debit: 25_000m),
        Leg(Rent, new DateOnly(2026, 6, 1), debit: 20_000m),
        Leg(Bank, new DateOnly(2026, 5, 1), debit: 99_999m),
        Leg(Dormant, new DateOnly(2026, 5, 1), debit: 1_234m),
        Leg(Sales, new DateOnly(2026, 3, 31), credit: 7_777m),
    ];

    private static readonly List<BudgetLineRead> Lines =
    [
        Line(Sales, 1, 40_000m),
        Line(Sales, 2, 40_000m),
        Line(Returns, 2, 1_000m),
        Line(Rent, 1, 20_000m),
        Line(Rent, 2, 20_000m),
        Line(Rent, 3, 20_000m),
        new() { BudgetLineId = 999, BudgetId = 71, AccountId = Rent, Period = 1, Amount = 1_000_000m },
    ];

    /// <summary>The card's "done when".</summary>
    [Fact]
    public void The_actual_column_matches_the_pnl_for_the_same_period()
    {
        BudgetWindow ytd = BudgetWindows.For(April, new DateOnly(2026, 5, 15), BudgetWindows.YearToDate);

        Dictionary<long, decimal> pnl = ProfitAndLossSource
            .Rows(Accounts.AsQueryable(), Ledger.AsQueryable(), ytd.From, ytd.To)
            .ToDictionary(r => r.AccountId, r => r.NetAmount);
        List<BudgetActualRow> rows = [.. BudgetReports.AgainstActual(Accounts.AsQueryable(), Ledger.AsQueryable(), Lines.AsQueryable(), BudgetId, ytd)];

        Assert.NotEmpty(rows);
        Assert.All(rows, r => Assert.Equal(pnl[r.AccountId], r.Actual));
        Assert.Equal(pnl.Values.Where(v => v != 0m).Sum(), rows.Sum(r => r.Actual));
    }

    [Fact]
    public void Year_to_date_compares_budget_and_actual_with_favourable_or_adverse_by_type()
    {
        BudgetWindow ytd = BudgetWindows.For(April, new DateOnly(2026, 5, 15), BudgetWindows.YearToDate);
        List<BudgetActualRow> rows = [.. BudgetReports.AgainstActual(Accounts.AsQueryable(), Ledger.AsQueryable(), Lines.AsQueryable(), BudgetId, ytd)];

        // Income: 78,000 earned against 80,000 planned: 2,000 short, adverse.
        BudgetActualRow sales = rows.Single(r => r.AccountId == Sales);
        Assert.Equal(80_000m, sales.Budget);
        Assert.Equal(78_000m, sales.Actual);
        Assert.Equal(-2_000m, sales.Variance);
        Assert.Equal(-2.5m, sales.VariancePercent);
        Assert.Equal("Adverse", sales.Result);

        // Expense: 45,000 spent against 40,000: 5,000 over, adverse.
        BudgetActualRow rent = rows.Single(r => r.AccountId == Rent);
        Assert.Equal(40_000m, rent.Budget);
        Assert.Equal(45_000m, rent.Actual);
        Assert.Equal(-5_000m, rent.Variance);
        Assert.Equal("Adverse", rent.Result);

        // A contra income account is on the P&L's sign: returns planned at 1,000 read -1,000,
        // and 1,500 of returns (-1,500) is 500 worse than planned.
        BudgetActualRow returns = rows.Single(r => r.AccountId == Returns);
        Assert.Equal(-1_000m, returns.Budget);
        Assert.Equal(-1_500m, returns.Actual);
        Assert.Equal(-500m, returns.Variance);
        Assert.Equal("Adverse", returns.Result);

        // Balance-sheet and inactive accounts are not on the P&L, so not here either.
        Assert.DoesNotContain(rows, r => r.AccountId is Bank or Dormant);
    }

    [Fact]
    public void A_single_month_reads_only_that_months_budget_and_actual()
    {
        BudgetWindow april = BudgetWindows.For(April, new DateOnly(2026, 4, 30), BudgetWindows.Month);
        BudgetActualRow rent = BudgetReports
            .AgainstActual(Accounts.AsQueryable(), Ledger.AsQueryable(), Lines.AsQueryable(), BudgetId, april)
            .Single(r => r.AccountId == Rent);

        Assert.Equal(20_000m, rent.Budget);
        Assert.Equal(20_000m, rent.Actual);
        Assert.Equal(0m, rent.Variance);
        Assert.Equal("Favourable", rent.Result);
    }

    [Fact]
    public void With_no_budget_the_report_still_shows_actuals()
    {
        BudgetWindow ytd = BudgetWindows.For(April, new DateOnly(2026, 5, 15), BudgetWindows.YearToDate);
        BudgetActualRow rent = BudgetReports
            .AgainstActual(Accounts.AsQueryable(), Ledger.AsQueryable(), Lines.AsQueryable(), null, ytd)
            .Single(r => r.AccountId == Rent);

        Assert.Equal(0m, rent.Budget);
        Assert.Equal(45_000m, rent.Actual);
        Assert.Null(rent.VariancePercent);
    }

    [Theory]
    [InlineData("month", 5, 5, "2026-08-01", "2026-08-31")]
    [InlineData("quarter", 4, 6, "2026-07-01", "2026-09-30")]
    [InlineData("ytd", 1, 5, "2026-04-01", "2026-08-31")]
    [InlineData(null, 1, 5, "2026-04-01", "2026-08-31")]
    public void The_window_is_whole_months_of_the_financial_year(string? scope, int from, int to, string start, string end)
    {
        BudgetWindow window = BudgetWindows.For(April, new DateOnly(2026, 8, 15), scope);

        Assert.Equal(from, window.PeriodFrom);
        Assert.Equal(to, window.PeriodTo);
        Assert.Equal(DateOnly.Parse(start), window.From);
        Assert.Equal(DateOnly.Parse(end), window.To);
    }

    [Fact]
    public void A_date_outside_the_year_is_taken_as_its_nearest_month()
    {
        Assert.Equal(1, BudgetWindows.For(April, new DateOnly(2026, 1, 5), BudgetWindows.Month).PeriodFrom);
        Assert.Equal(12, BudgetWindows.For(April, new DateOnly(2027, 9, 5), BudgetWindows.Month).PeriodFrom);
        Assert.Equal(new DateOnly(2026, 4, 1), BudgetWindows.YearStartOf(new DateOnly(2027, 3, 31), 4));
    }

    [Fact]
    public void By_project_pairs_each_projects_budget_with_its_own_ledger_rows()
    {
        List<ProjectRead> projects =
        [
            new() { ProjectId = 8, ProjectCode = "PRJ-0008", ProjectName = "Fit-out", BillingMethod = "FixedFee", Status = "Active" },
        ];
        List<BudgetLineRead> lines = [new() { BudgetLineId = 1, BudgetId = BudgetId, AccountId = Rent, ProjectId = 8, Period = 2, Amount = 3_000m }];
        List<JournalLedgerRead> ledger =
        [
            Leg(Rent, new DateOnly(2026, 5, 2), debit: 3_500m, projectId: 8),
            Leg(Rent, new DateOnly(2026, 5, 2), debit: 10_000m),
        ];

        BudgetActualRow row = Assert.Single(BudgetReports.AgainstActualByProject(
            Accounts.AsQueryable(), projects.AsQueryable(), ledger.AsQueryable(), lines.AsQueryable(), BudgetId,
            BudgetWindows.For(April, new DateOnly(2026, 5, 31), BudgetWindows.Month)));

        Assert.Equal("PRJ-0008", row.ProjectCode);
        Assert.Equal(3_000m, row.Budget);
        Assert.Equal(3_500m, row.Actual);
        Assert.Equal("Adverse", row.Result);
    }

    [Fact]
    public void The_monthly_trend_puts_budget_and_actual_in_the_same_month_row()
    {
        List<BudgetTrendRow> rows = [.. BudgetReports.MonthlyTrend(Accounts.AsQueryable(), Ledger.AsQueryable(), Lines.AsQueryable(), BudgetId, April)];

        BudgetTrendRow may = rows.Single(r => r.AccountId == Rent && r.Month == "2026-05");
        Assert.Equal(2, may.Period);
        Assert.Equal(20_000m, may.Budget);
        Assert.Equal(25_000m, may.Actual);
        Assert.Equal(-5_000m, may.Variance);

        // March 2026 is the previous year, and the other budget's lines are not this one's.
        Assert.DoesNotContain(rows, r => r.Month == "2026-03");
        Assert.Equal(60_000m, rows.Where(r => r.AccountId == Rent).Sum(r => r.Budget));
        Assert.Equal(rows.Count, rows.Select(r => r.RowKey).Distinct().Count());
    }

    private static long _ledgerId;

    private static JournalLedgerRead Leg(long accountId, DateOnly on, decimal debit = 0m, decimal credit = 0m, long? projectId = null) =>
        new()
        {
            LedgerId = Interlocked.Increment(ref _ledgerId),
            LedgerDate = on,
            AccountId = accountId,
            TransactionTypeCode = "JNL",
            DebitAmount = debit,
            CreditAmount = credit,
            DebitAmountBase = debit,
            CreditAmountBase = credit,
            CurrencyCode = "INR",
            ExchangeRate = 1m,
            ProjectId = projectId,
        };

    private static long _lineId;

    private static BudgetLineRead Line(long accountId, short period, decimal amount) =>
        new() { BudgetLineId = Interlocked.Increment(ref _lineId), BudgetId = BudgetId, AccountId = accountId, Period = period, Amount = amount };
}

/// <summary>An April start, for sources that fall back to the branch's financial year.</summary>
public sealed class StubFinancialYear : IFinancialYearProvider
{
    public Task<int> GetStartMonthAsync(CancellationToken ct = default) => Task.FromResult(4);
}
