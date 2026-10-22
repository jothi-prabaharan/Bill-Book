using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Reporting.Api.Controllers;
using Reporting.Entity.Enums;
using Reporting.Entity.Models;
using Reporting.Repository.ReadModels;
using Reporting.Repository.Repositories;
using Shared.Kernel.Apps;
using Shared.Kernel.Internal;
using Xunit;
using static Reporting.Repository.Repositories.BatchedNameResolverRepository;

namespace Reporting.Api.Tests;

/// <summary>
/// Budget Monthly Trend's own page (TK-214), on a budget for April 2026 to
/// March 2027 and three months of trading worked by hand: the books of Budget vs
/// Actual's tests, plus a June posting to an account since made inactive and an
/// August posting that falls after the as-at month. The sums run on lists
/// through the same expressions the repository runs against the read models; the
/// composition is pure. The partner tests hold every cell to Budget vs Actual's
/// own figures for the same months.
/// </summary>
public sealed class BudgetMonthlyTrendReportTests
{
    private const int AssetType = 1, IncomeType = 4, ExpenseType = 5;
    private const long Bank = 1001, Sales = 4001, SalesReturns = 4002, Interest = 4300,
        CostOfGoodsSold = 5001, Rent = 6001, Salaries = 6002, OldStationery = 6009;

    private static readonly List<AccountTypeDto> Types =
    [
        new(1, "Asset", "Asset", "Debit", "BalanceSheet", 1),
        new(2, "Liability", "Liability", "Credit", "BalanceSheet", 2),
        new(3, "Equity", "Equity", "Credit", "BalanceSheet", 3),
        new(4, "Income", "Income", "Credit", "ProfitAndLoss", 4),
        new(5, "Expense", "Expense", "Debit", "ProfitAndLoss", 5),
    ];

    private static ProfitAndLossAccount Account(
        long id, int type, string name, bool sales = false, bool contra = false, bool active = true, string? systemName = null) =>
        new()
        {
            AccountId = id, AccountTypeId = type, AccountCode = id.ToString(), AccountName = name,
            AccountSystemName = systemName, IsSales = sales, IsContra = contra, IsActive = active,
        };

    private static readonly List<ProfitAndLossAccount> Accounts =
    [
        Account(Bank, AssetType, "Bank"),
        Account(Sales, IncomeType, "Sales", sales: true),
        Account(SalesReturns, IncomeType, "Sales Returns", sales: true, contra: true),
        Account(Interest, IncomeType, "Interest Income"),
        Account(CostOfGoodsSold, ExpenseType, "Cost of Goods Sold", systemName: BusinessPerformanceSourceRepository.CostOfGoodsSold),
        Account(Rent, ExpenseType, "Rent"),
        Account(Salaries, ExpenseType, "Salaries"),
        Account(OldStationery, ExpenseType, "Old Stationery", active: false),
    ];

    private static readonly BudgetVsActualBudgetDto TheBudget = new()
    {
        BudgetId = 1, Name = "FY 2026-27", FinancialYearStart = new(2026, 4, 1), FinancialYearEnd = new(2027, 3, 31),
        Status = BudgetStatus.Approved, IsBranchDefault = true,
    };

    private static long _line;

    private static BudgetLineRead Line(long account, int period, decimal amount, long budgetId = 1) =>
        new() { BudgetLineId = ++_line, BudgetId = budgetId, AccountId = account, Period = (short)period, Amount = amount };

    /// <summary>Every month: sales 1,00,000, returns 2,000, cost 60,000, rent 10,000, salaries 8,000, interest 500.</summary>
    private static readonly List<BudgetLineRead> Lines =
    [
        .. Enumerable.Range(1, 12).SelectMany(p => new[]
        {
            Line(Sales, p, 100_000m), Line(SalesReturns, p, 2_000m), Line(CostOfGoodsSold, p, 60_000m),
            Line(Rent, p, 10_000m), Line(Salaries, p, 8_000m), Line(Interest, p, 500m),
        }),
        Line(Rent, 1, 99_999m, budgetId: 2), // another budget's line
    ];

    private static JournalLedgerRead Leg(DateOnly date, long account, decimal debit, decimal credit) =>
        new() { LedgerDate = date, AccountId = account, DebitAmountBase = debit, CreditAmountBase = credit, TransactionTypeCode = "JRN" };

    private static IEnumerable<JournalLedgerRead> Earned(DateOnly date, long account, decimal amount) =>
        [Leg(date, Bank, amount, 0m), Leg(date, account, 0m, amount)];

    private static IEnumerable<JournalLedgerRead> Spent(DateOnly date, long account, decimal amount) =>
        [Leg(date, account, amount, 0m), Leg(date, Bank, 0m, amount)];

    private static readonly List<JournalLedgerRead> Ledger =
    [
        // April: sales 1,10,000, returns 3,000, cost 65,000, rent 10,000, salaries 9,000.
        .. Earned(new(2026, 4, 10), Sales, 110_000m), .. Spent(new(2026, 4, 12), SalesReturns, 3_000m),
        .. Spent(new(2026, 4, 20), CostOfGoodsSold, 65_000m), .. Spent(new(2026, 4, 30), Rent, 10_000m),
        .. Spent(new(2026, 4, 30), Salaries, 9_000m),
        // May: sales 90,000, cost 55,000, rent 10,000, salaries 8,000, interest 700.
        .. Earned(new(2026, 5, 10), Sales, 90_000m), .. Spent(new(2026, 5, 20), CostOfGoodsSold, 55_000m),
        .. Spent(new(2026, 5, 31), Rent, 10_000m), .. Spent(new(2026, 5, 31), Salaries, 8_000m),
        .. Earned(new(2026, 5, 31), Interest, 700m),
        // June: sales 1,20,000, returns 1,000, cost 70,000, rent 12,000, salaries 8,000, 500 to an account since closed.
        .. Earned(new(2026, 6, 10), Sales, 120_000m), .. Spent(new(2026, 6, 12), SalesReturns, 1_000m),
        .. Spent(new(2026, 6, 20), CostOfGoodsSold, 70_000m), .. Spent(new(2026, 6, 30), Rent, 12_000m),
        .. Spent(new(2026, 6, 30), Salaries, 8_000m), .. Spent(new(2026, 6, 25), OldStationery, 500m),
        // August: after the as-at month in most tests, so never counted there.
        .. Spent(new(2026, 8, 5), OldStationery, 1_000m),
        // Before the year.
        .. Earned(new(2026, 3, 31), Sales, 9_999m),
    ];

    private static readonly DateOnly June15 = new(2026, 6, 15);

    private static BudgetMonthlyTrendReportResultDto Report(DateOnly asOf, List<JournalLedgerRead>? ledger = null)
    {
        BudgetWindow toDate = BudgetVsActualReportRepository.WindowOf(TheBudget, asOf, BudgetPeriod.YearToDate);
        return BudgetMonthlyTrendReportRepository.Compose(
            Accounts, Types,
            [.. BudgetMonthlyTrendReportRepository.BudgetedByMonth(Lines.AsQueryable(), TheBudget.BudgetId)],
            [.. BudgetMonthlyTrendReportRepository.ActualsByMonth((ledger ?? Ledger).AsQueryable(), TheBudget.FinancialYearStart, toDate.To)],
            TheBudget, asOf);
    }

    private static BudgetMonthlyTrendSectionDto Section(BudgetMonthlyTrendReportResultDto report, ProfitAndLossSection section) =>
        report.Sections.Single(s => s.Section == section);

    private static BudgetMonthlyTrendLineDto Line(BudgetMonthlyTrendReportResultDto report, long account) =>
        report.Sections.SelectMany(s => s.Lines).Single(l => l.AccountId == account);

    private static (decimal Budget, decimal? Actual, decimal? Variance, decimal? Percent) Of(BudgetMonthlyTrendCellDto cell) =>
        (cell.Budget, cell.Actual, cell.Variance, cell.VariancePercent);

    // ---- The sums ---------------------------------------------------------------------

    [Fact]
    public void Budgets_are_summed_by_account_and_month_from_the_budgets_own_lines_only()
    {
        List<MonthAccountAmount> budgeted = [.. BudgetMonthlyTrendReportRepository.BudgetedByMonth(Lines.AsQueryable(), 1)];

        Assert.Equal(72, budgeted.Count); // six accounts, twelve months; the other budget's line is not here
        Assert.Equal(100_000m, budgeted.Single(b => b.AccountId == Sales && b.Period == 7).Amount);
        Assert.DoesNotContain(budgeted, b => b.Amount == 99_999m);
    }

    [Fact]
    public void Postings_are_summed_by_account_and_month_in_the_budgets_year_with_april_the_first()
    {
        List<MonthAccountAmount> actuals =
            [.. BudgetMonthlyTrendReportRepository.ActualsByMonth(Ledger.AsQueryable(), new(2026, 4, 1), new(2027, 3, 31))];

        Assert.Equal(-110_000m, actuals.Single(a => a.AccountId == Sales && a.Period == 1).Amount); // credits are negative
        Assert.Equal(12_000m, actuals.Single(a => a.AccountId == Rent && a.Period == 3).Amount);
        Assert.Equal(1_000m, actuals.Single(a => a.AccountId == OldStationery && a.Period == 5).Amount); // August
        Assert.DoesNotContain(actuals, a => a.AccountId == Sales && a.Amount == -9_999m); // 31 March, before the year
    }

    [Fact]
    public void Postings_after_the_through_date_are_not_summed()
    {
        List<MonthAccountAmount> actuals =
            [.. BudgetMonthlyTrendReportRepository.ActualsByMonth(Ledger.AsQueryable(), new(2026, 4, 1), new(2026, 6, 30))];

        Assert.DoesNotContain(actuals, a => a.Period > 3);
        Assert.Equal(500m, actuals.Single(a => a.AccountId == OldStationery).Amount);
    }

    [Fact]
    public void A_year_that_starts_in_january_numbers_its_months_from_one()
    {
        var calendar = new BudgetVsActualBudgetDto { BudgetId = 9, FinancialYearStart = new(2026, 1, 1), FinancialYearEnd = new(2026, 12, 31) };
        List<JournalLedgerRead> ledger = [.. Earned(new(2026, 1, 5), Sales, 10m), .. Earned(new(2026, 12, 5), Sales, 20m)];

        List<MonthAccountAmount> actuals =
            [.. BudgetMonthlyTrendReportRepository.ActualsByMonth(ledger.AsQueryable(), calendar.FinancialYearStart, calendar.FinancialYearEnd)];

        Assert.Equal([1, 12], actuals.Where(a => a.AccountId == Sales).Select(a => a.Period).Order());
    }

    // ---- The twelve months ------------------------------------------------------------

    [Fact]
    public void There_are_twelve_months_from_april_each_whole_and_those_after_the_as_at_month_are_future()
    {
        BudgetMonthlyTrendReportResultDto report = Report(June15);

        Assert.Equal(Enumerable.Range(1, 12), report.Months.Select(m => m.Period));
        Assert.Equal((new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 30)), (report.Months[0].FromDate, report.Months[0].ToDate));
        Assert.Equal((new DateOnly(2027, 3, 1), new DateOnly(2027, 3, 31)), (report.Months[11].FromDate, report.Months[11].ToDate));
        Assert.Equal([false, false, false], report.Months.Take(3).Select(m => m.IsFuture));
        Assert.All(report.Months.Skip(3), m => Assert.True(m.IsFuture));
        Assert.Equal(3, report.AsOfPeriod);
        Assert.Equal((new DateOnly(2026, 4, 1), new DateOnly(2026, 6, 30)), (report.FromDate, report.ThroughDate));
    }

    [Fact]
    public void An_account_reads_its_budget_actual_variance_and_percentage_for_each_month_with_favourable_positive()
    {
        BudgetMonthlyTrendReportResultDto report = Report(June15);

        BudgetMonthlyTrendLineDto sales = Line(report, Sales);
        Assert.Equal((100_000m, 110_000m, 10_000m, 10.00m), Of(sales.Months[0]));
        Assert.Equal((100_000m, 90_000m, -10_000m, -10.00m), Of(sales.Months[1]));
        Assert.Equal((100_000m, 120_000m, 20_000m, 20.00m), Of(sales.Months[2]));

        // Spending above budget is adverse, below it favourable.
        BudgetMonthlyTrendLineDto cost = Line(report, CostOfGoodsSold);
        Assert.Equal((60_000m, 65_000m, -5_000m, -8.33m), Of(cost.Months[0]));
        Assert.Equal((60_000m, 55_000m, 5_000m, 8.33m), Of(cost.Months[1]));
        Assert.Equal((60_000m, 70_000m, -10_000m, -16.67m), Of(cost.Months[2]));

        // A contra account's budget reads on the Profit & Loss's sign: returns budgeted at 2,000 read −2,000.
        BudgetMonthlyTrendLineDto returns = Line(report, SalesReturns);
        Assert.Equal((-2_000m, -3_000m, -1_000m, -50.00m), Of(returns.Months[0]));
        Assert.Equal((-2_000m, 0m, 2_000m, 100.00m), Of(returns.Months[1]));
        Assert.Equal(-24_000m, returns.YearBudget);
        Assert.Equal(-4_000m, returns.Months.Sum(m => m.Actual ?? 0m));
    }

    [Fact]
    public void A_month_after_the_as_at_month_keeps_its_budget_and_has_no_actual_variance_or_percentage()
    {
        BudgetMonthlyTrendReportResultDto report = Report(June15);

        foreach (BudgetMonthlyTrendCellDto cell in Line(report, Sales).Months.Skip(3))
        {
            Assert.Equal((100_000m, (decimal?)null, (decimal?)null, (decimal?)null), Of(cell));
        }

        Assert.All(report.NetProfit.Months.Skip(3), c => Assert.Equal((20_500m, (decimal?)null), (c.Budget, c.Actual)));
    }

    [Fact]
    public void The_as_at_months_whole_month_counts_so_a_posting_on_its_last_day_is_in()
    {
        // The rent of 12,000 was posted on 30 June; the as-at date is 15 June.
        Assert.Equal(12_000m, Line(Report(June15), Rent).Months[2].Actual);
    }

    [Fact]
    public void A_posting_after_the_as_at_month_is_not_counted_even_on_an_account_made_inactive()
    {
        BudgetMonthlyTrendReportResultDto report = Report(June15);

        // 500 in June counts; the 1,000 in August does not, and August shows no actual at all.
        BudgetMonthlyTrendLineDto stationery = Line(report, OldStationery);
        Assert.Equal((0m, 500m, -500m, (decimal?)null), Of(stationery.Months[2]));
        Assert.Equal((0m, (decimal?)null), (stationery.Months[4].Budget, stationery.Months[4].Actual));
        Assert.Equal(500m, stationery.YearToDate.Actual);
    }

    // ---- Sections, profit and totals --------------------------------------------------

    [Fact]
    public void Sections_total_each_month_and_come_in_the_profit_and_loss_order_with_lines_by_code()
    {
        BudgetMonthlyTrendReportResultDto report = Report(June15);

        Assert.Equal(["Revenue", "Cost of sales", "Other income", "Expenses"], report.Sections.Select(s => s.Title));
        Assert.Equal(["4001", "4002"], Section(report, ProfitAndLossSection.Revenue).Lines.Select(l => l.AccountCode));

        BudgetMonthlyTrendSectionDto revenue = Section(report, ProfitAndLossSection.Revenue);
        Assert.Equal((98_000m, 107_000m, 9_000m, 9.18m), Of(revenue.Months[0]));
        Assert.Equal((98_000m, 90_000m, -8_000m, -8.16m), Of(revenue.Months[1]));
        Assert.Equal((98_000m, 119_000m, 21_000m, 21.43m), Of(revenue.Months[2]));
    }

    [Fact]
    public void Gross_and_net_profit_are_worked_for_each_month()
    {
        BudgetMonthlyTrendReportResultDto report = Report(June15);

        Assert.Equal((38_000m, 42_000m, 4_000m, 10.53m), Of(report.GrossProfit.Months[0]));
        Assert.Equal((38_000m, 35_000m, -3_000m, -7.89m), Of(report.GrossProfit.Months[1]));
        Assert.Equal((38_000m, 49_000m, 11_000m, 28.95m), Of(report.GrossProfit.Months[2]));

        Assert.Equal((20_500m, 23_000m, 2_500m, 12.20m), Of(report.NetProfit.Months[0]));
        Assert.Equal((20_500m, 17_700m, -2_800m, -13.66m), Of(report.NetProfit.Months[1]));
        Assert.Equal((20_500m, 28_500m, 8_000m, 39.02m), Of(report.NetProfit.Months[2]));
    }

    [Fact]
    public void The_year_to_date_covers_the_months_through_the_as_at_month_and_the_year_budget_all_twelve()
    {
        BudgetMonthlyTrendReportResultDto report = Report(June15);

        BudgetMonthlyTrendLineDto sales = Line(report, Sales);
        Assert.Equal((300_000m, 320_000m, 20_000m, 6.67m), Of(sales.YearToDate));
        Assert.Equal(1_200_000m, sales.YearBudget);

        Assert.Equal((294_000m, 316_000m, 22_000m, 7.48m), Of(Section(report, ProfitAndLossSection.Revenue).YearToDate));
        Assert.Equal((114_000m, 126_000m, 12_000m, 10.53m), Of(report.GrossProfit.YearToDate));
        Assert.Equal((61_500m, 69_200m, 7_700m, 12.52m), Of(report.NetProfit.YearToDate));
        Assert.Equal((456_000m, 246_000m), (report.GrossProfit.YearBudget, report.NetProfit.YearBudget));
    }

    [Fact]
    public void An_account_with_nothing_budgeted_or_posted_in_the_year_so_far_is_left_out_and_balance_sheet_accounts_never_appear()
    {
        BudgetMonthlyTrendReportResultDto report = Report(new(2026, 4, 20));

        IEnumerable<long> shown = report.Sections.SelectMany(s => s.Lines).Select(l => l.AccountId);
        Assert.DoesNotContain(Bank, shown);
        Assert.DoesNotContain(OldStationery, shown); // its only postings are June and August, after the as-at month
    }

    [Fact]
    public void With_the_as_at_date_in_the_first_month_every_other_month_is_future()
    {
        BudgetMonthlyTrendReportResultDto report = Report(new(2026, 4, 20));

        Assert.Equal(1, report.AsOfPeriod);
        Assert.Equal(1, report.Months.Count(m => !m.IsFuture));
        Assert.Equal((100_000m, 110_000m, 10_000m, 10.00m), Of(Line(report, Sales).YearToDate));
    }

    [Fact]
    public void With_the_as_at_date_in_the_last_month_there_is_no_future_and_the_year_to_date_is_the_year()
    {
        BudgetMonthlyTrendReportResultDto report = Report(new(2027, 3, 31));

        Assert.Equal(12, report.AsOfPeriod);
        Assert.DoesNotContain(report.Months, m => m.IsFuture);
        Assert.Equal(Line(report, Sales).YearBudget, Line(report, Sales).YearToDate.Budget);
        Assert.Equal(Line(report, Sales).Months.Sum(m => m.Budget), Line(report, Sales).YearToDate.Budget);
    }

    [Fact]
    public void Months_added_up_equal_the_year_to_date()
    {
        BudgetMonthlyTrendReportResultDto report = Report(June15);

        foreach (BudgetMonthlyTrendLineDto line in report.Sections.SelectMany(s => s.Lines))
        {
            Assert.Equal(line.YearToDate.Budget, line.Months.Take(3).Sum(m => m.Budget));
            Assert.Equal(line.YearToDate.Actual, line.Months.Take(3).Sum(m => m.Actual));
        }

        Assert.Equal(report.NetProfit.YearToDate.Actual, report.NetProfit.Months.Take(3).Sum(m => m.Actual));
    }

    // ---- Ties to Budget vs Actual and the Profit & Loss -------------------------------

    [Fact]
    public void Each_months_cells_equal_budget_vs_actuals_month_report_for_the_same_month()
    {
        BudgetMonthlyTrendReportResultDto trend = Report(June15);

        for (int period = 1; period <= 3; period++)
        {
            DateOnly first = TheBudget.FinancialYearStart.AddMonths(period - 1);
            BudgetWindow window = BudgetVsActualReportRepository.WindowOf(TheBudget, first, BudgetPeriod.Month);
            BudgetVsActualReportResultDto month = BudgetVsActualReportRepository.Compose(
                Accounts, Types,
                [.. BudgetVsActualReportRepository.Budgeted(Lines.AsQueryable(), 1, period, period)],
                [.. BudgetVsActualReportRepository.Actuals(Ledger.AsQueryable(), window.From, window.To)],
                TheBudget, first, BudgetPeriod.Month, window);

            foreach (BudgetVsActualLineDto line in month.Sections.SelectMany(s => s.Lines))
            {
                Assert.Equal(
                    (line.Budget, (decimal?)line.Actual, (decimal?)line.Variance, line.VariancePercent),
                    Of(Line(trend, line.AccountId).Months[period - 1]));
            }

            Assert.Equal(
                (month.NetProfit.Budget, (decimal?)month.NetProfit.Actual, (decimal?)month.NetProfit.Variance, month.NetProfit.VariancePercent),
                Of(trend.NetProfit.Months[period - 1]));
        }
    }

    [Fact]
    public void The_year_to_date_cells_equal_budget_vs_actuals_year_to_date()
    {
        BudgetMonthlyTrendReportResultDto trend = Report(June15);
        BudgetWindow window = BudgetVsActualReportRepository.WindowOf(TheBudget, June15, BudgetPeriod.YearToDate);
        BudgetVsActualReportResultDto ytd = BudgetVsActualReportRepository.Compose(
            Accounts, Types,
            [.. BudgetVsActualReportRepository.Budgeted(Lines.AsQueryable(), 1, window.PeriodFrom, window.PeriodTo)],
            [.. BudgetVsActualReportRepository.Actuals(Ledger.AsQueryable(), window.From, window.To)],
            TheBudget, June15, BudgetPeriod.YearToDate, window);

        foreach (BudgetVsActualLineDto line in ytd.Sections.SelectMany(s => s.Lines))
        {
            Assert.Equal(
                (line.Budget, (decimal?)line.Actual, (decimal?)line.Variance, line.VariancePercent),
                Of(Line(trend, line.AccountId).YearToDate));
        }

        Assert.Equal(
            (ytd.GrossProfit.Budget, (decimal?)ytd.GrossProfit.Actual), (trend.GrossProfit.YearToDate.Budget, trend.GrossProfit.YearToDate.Actual));
    }

    [Fact]
    public void The_year_to_dates_actual_profit_is_the_profit_and_loss_for_the_same_dates()
    {
        foreach (DateOnly asOf in new[] { new DateOnly(2026, 4, 20), June15, new DateOnly(2026, 9, 1) })
        {
            BudgetMonthlyTrendReportResultDto trend = Report(asOf);
            ProfitAndLossReportResultDto pnl = ProfitAndLossReportRepository.Compose(
                Accounts,
                [.. ProfitAndLossReportRepository.Movements(Ledger.AsQueryable(), trend.FromDate, trend.ThroughDate, null, null)],
                Types, trend.FromDate, trend.ThroughDate, null, null, isZeroBalanceShown: false);

            Assert.Equal(pnl.Totals.NetProfit, trend.NetProfit.YearToDate.Actual);
            Assert.Equal(pnl.Totals.GrossProfit, trend.GrossProfit.YearToDate.Actual);
        }
    }

    // ---- The request and the guards ---------------------------------------------------

    [Fact]
    public void The_budget_and_the_as_at_date_are_required_and_nothing_else_is()
    {
        Assert.Equal(
            [nameof(BudgetMonthlyTrendReportRequest.BudgetId), nameof(BudgetMonthlyTrendReportRequest.AsOfDate)],
            typeof(BudgetMonthlyTrendReportRequest).GetProperties()
                .Where(p => p.GetCustomAttribute<RequiredAttribute>() is not null)
                .Select(p => p.Name));
    }

    [Theory]
    [InlineData(2026, 4, 1, true)]
    [InlineData(2027, 3, 31, true)]
    [InlineData(2026, 3, 31, false)]
    [InlineData(2027, 4, 1, false)]
    public void The_as_at_date_must_fall_within_the_budgets_year(int year, int month, int day, bool accepted) =>
        Assert.Equal(accepted, BudgetVsActualReportRepository.DateRefusal(TheBudget, new(year, month, day)) is null);

    [Fact]
    public async Task Another_branch_in_the_request_is_forbidden_not_hidden()
    {
        var controller = new BudgetMonthlyTrendReportController(null!, null!)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("org_id", Guid.NewGuid().ToString())], "test")),
                },
            },
        };

        IActionResult result = await controller.Run(
            new BudgetMonthlyTrendReportRequest { BudgetId = 1, AsOfDate = June15, OrgId = Guid.NewGuid() }, default);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public void The_endpoint_needs_the_budget_add_on_alone_reports_and_accounting_view()
    {
        Type controller = typeof(BudgetMonthlyTrendReportController);

        Assert.Equal("api/statements/budget-monthly-trend", controller.GetCustomAttribute<RouteAttribute>()!.Template);
        Assert.Equal("reports", controller.GetCustomAttribute<RequireModulePermissionAttribute>()!.Module);

        // With the business-type guard beside it, either would admit: a branch without budgets would get in.
        Assert.Equal(AddOn.Budget, controller.GetCustomAttribute<RequireAddOnAttribute>()!.AddOn);
        Assert.Null(controller.GetCustomAttribute<RequireBusinessTypeAttribute>());

        Assert.Contains(
            "accounting.view",
            controller.GetMethod(nameof(BudgetMonthlyTrendReportController.Run))!
                .GetCustomAttributes<RequirePermissionAttribute>().Select(a => a.Permission));
    }

    // ---- The catalog source, held to the same refusals --------------------------------

    private static BudgetSelection Selection(long? budgetId) =>
        new(budgetId, ByProject: false, BudgetVsActualReportRepository.WindowOf(TheBudget, June15, BudgetPeriod.Month));

    private static ReportParameters Parameters(string? asOf, string? budgetId) =>
        new(new Dictionary<string, string?> { ["asOf"] = asOf, ["budgetId"] = budgetId });

    [Fact]
    public void The_catalog_source_declares_the_date_and_budget_required_and_the_other_budget_reports_do_not()
    {
        var trend = new BudgetMonthlyTrendSource(new StubFinancialYear());
        var against = new BudgetAgainstActualSource(new StubFinancialYear());

        Assert.All(trend.Parameters, p => Assert.True(p.IsRequired));
        Assert.Equal(["asOf", "budgetId"], trend.Parameters.Select(p => p.Name));
        Assert.DoesNotContain(against.Parameters, p => p.IsRequired);
    }

    [Theory]
    [InlineData(null, "1", "As at date is required.")]
    [InlineData("2026-06-15", null, "Budget is required.")]
    [InlineData("2027-04-01", "1", "Choose a date within the budget's year, 01 Apr 2026 to 31 Mar 2027.")]
    [InlineData("2026-03-31", "1", "Choose a date within the budget's year, 01 Apr 2026 to 31 Mar 2027.")]
    public void The_catalog_source_refuses_what_the_page_refuses(string? asOf, string? budgetId, string message)
    {
        ReportQueryException refusal = Assert.Throws<ReportQueryException>(
            () => BudgetWindows.Require(Parameters(asOf, budgetId), Selection(1)));

        Assert.Equal(message, refusal.Message);
    }

    [Fact]
    public void The_catalog_source_refuses_a_budget_that_is_not_the_branchs_and_accepts_a_good_request()
    {
        Assert.Equal(
            "That budget is not on this branch.",
            Assert.Throws<ReportQueryException>(() => BudgetWindows.Require(Parameters("2026-06-15", "99"), Selection(null))).Message);

        BudgetWindows.Require(Parameters("2026-06-15", "1"), Selection(1));
        BudgetWindows.Require(Parameters("2027-03-31", "1"), Selection(1));
    }
}
