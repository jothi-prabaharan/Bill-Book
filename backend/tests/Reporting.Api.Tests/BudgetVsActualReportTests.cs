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
/// Budget vs Actual's own page (TK-204), on a budget for April 2026 to March
/// 2027 and three months of trading worked by hand. The sums run on lists
/// through the same expressions the repository runs against the read models;
/// the composition is pure.
/// </summary>
public sealed class BudgetVsActualReportTests
{
    private const int AssetType = 1, IncomeType = 4, ExpenseType = 5;
    private const long Bank = 1001, Sales = 4001, SalesReturns = 4002, Interest = 4300,
        CostOfGoodsSold = 5001, PurchaseReturns = 5002, Rent = 6001, Salaries = 6002, OldStationery = 6009;

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
        Account(PurchaseReturns, ExpenseType, "Purchase Returns", contra: true, systemName: BusinessPerformanceSourceRepository.PurchaseReturns),
        Account(Rent, ExpenseType, "Rent"),
        Account(Salaries, ExpenseType, "Salaries"),
        Account(OldStationery, ExpenseType, "Old Stationery", active: false),
    ];

    private static readonly BudgetVsActualBudgetDto TheBudget = new()
    {
        BudgetId = 1, Name = "FY 2026-27", FinancialYearStart = new(2026, 4, 1), FinancialYearEnd = new(2027, 3, 31),
        Status = BudgetStatus.Approved, IsBranchDefault = true,
    };

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

    private static long _line;

    private static BudgetLineRead Line(long account, int period, decimal amount, long budgetId = 1) =>
        new() { BudgetLineId = ++_line, BudgetId = budgetId, AccountId = account, Period = (short)period, Amount = amount };

    private static JournalLedgerRead Leg(DateOnly date, long account, decimal debit, decimal credit) =>
        new() { LedgerDate = date, AccountId = account, DebitAmountBase = debit, CreditAmountBase = credit, TransactionTypeCode = "JRN" };

    /// <summary>One entry per amount, balanced against the bank.</summary>
    private static IEnumerable<JournalLedgerRead> Income(DateOnly date, long account, decimal amount) =>
        [Leg(date, Bank, amount, 0m), Leg(date, account, 0m, amount)];

    private static IEnumerable<JournalLedgerRead> Spend(DateOnly date, long account, decimal amount) =>
        [Leg(date, account, amount, 0m), Leg(date, Bank, 0m, amount)];

    private static readonly List<JournalLedgerRead> Ledger =
    [
        // April: sales 1,10,000, returns 3,000, cost 65,000, rent 10,000, salaries 9,000.
        .. Income(new(2026, 4, 10), Sales, 110_000m), .. Spend(new(2026, 4, 12), SalesReturns, 3_000m),
        .. Spend(new(2026, 4, 20), CostOfGoodsSold, 65_000m), .. Spend(new(2026, 4, 30), Rent, 10_000m),
        .. Spend(new(2026, 4, 30), Salaries, 9_000m),
        // May: sales 90,000, cost 55,000, rent 10,000, salaries 8,000, interest 700.
        .. Income(new(2026, 5, 10), Sales, 90_000m), .. Spend(new(2026, 5, 20), CostOfGoodsSold, 55_000m),
        .. Spend(new(2026, 5, 31), Rent, 10_000m), .. Spend(new(2026, 5, 31), Salaries, 8_000m),
        .. Income(new(2026, 5, 31), Interest, 700m),
        // June: sales 1,20,000, returns 1,000, cost 70,000, rent 12,000, salaries 8,000.
        .. Income(new(2026, 6, 10), Sales, 120_000m), .. Spend(new(2026, 6, 12), SalesReturns, 1_000m),
        .. Spend(new(2026, 6, 20), CostOfGoodsSold, 70_000m), .. Spend(new(2026, 6, 30), Rent, 12_000m),
        .. Spend(new(2026, 6, 30), Salaries, 8_000m),
        // July: stationery on an account since closed.
        .. Spend(new(2026, 7, 5), OldStationery, 1_000m),
        // Before the year.
        .. Income(new(2026, 3, 31), Sales, 9_999m),
    ];

    private static BudgetVsActualReportResultDto Report(DateOnly asOf, BudgetPeriod period)
    {
        BudgetWindow window = BudgetVsActualReportRepository.WindowOf(TheBudget, asOf, period);
        return BudgetVsActualReportRepository.Compose(
            Accounts, Types,
            [.. BudgetVsActualReportRepository.Budgeted(Lines.AsQueryable(), TheBudget.BudgetId, window.PeriodFrom, window.PeriodTo)],
            [.. BudgetVsActualReportRepository.Actuals(Ledger.AsQueryable(), window.From, window.To)],
            TheBudget, asOf, period, window);
    }

    private static BudgetVsActualSectionDto Section(BudgetVsActualReportResultDto report, ProfitAndLossSection section) =>
        report.Sections.Single(s => s.Section == section);

    private static BudgetVsActualLineDto Line(BudgetVsActualReportResultDto report, long account) =>
        report.Sections.SelectMany(s => s.Lines).Single(l => l.AccountId == account);

    // ---- The window ----------------------------------------------------------------

    [Theory]
    [InlineData(2026, 5, 20, BudgetPeriod.Month, 2, 2, "2026-05-01", "2026-05-31")]
    [InlineData(2026, 8, 10, BudgetPeriod.Quarter, 4, 6, "2026-07-01", "2026-09-30")]
    [InlineData(2026, 6, 15, BudgetPeriod.YearToDate, 1, 3, "2026-04-01", "2026-06-30")]
    [InlineData(2027, 2, 1, BudgetPeriod.Quarter, 10, 12, "2027-01-01", "2027-03-31")]
    public void The_period_covers_whole_months_of_the_budgets_year(
        int year, int month, int day, BudgetPeriod period, int from, int to, string fromDate, string toDate)
    {
        BudgetWindow window = BudgetVsActualReportRepository.WindowOf(TheBudget, new(year, month, day), period);

        Assert.Equal((from, to), (window.PeriodFrom, window.PeriodTo));
        Assert.Equal(DateOnly.Parse(fromDate), window.From);
        Assert.Equal(DateOnly.Parse(toDate), window.To);
    }

    [Fact]
    public void Only_the_budgets_own_lines_in_the_months_covered_are_summed()
    {
        List<AccountAmount> budgeted = [.. BudgetVsActualReportRepository.Budgeted(Lines.AsQueryable(), 1, 1, 3)];

        Assert.Equal(30_000m, budgeted.Single(b => b.AccountId == Rent).Amount); // not the other budget's 99,999
        Assert.Equal(300_000m, budgeted.Single(b => b.AccountId == Sales).Amount);
    }

    // ---- One month -----------------------------------------------------------------

    [Fact]
    public void A_month_sets_each_account_budget_against_actual_with_favourable_positive()
    {
        BudgetVsActualReportResultDto report = Report(new(2026, 5, 20), BudgetPeriod.Month);

        BudgetVsActualLineDto sales = Line(report, Sales);
        Assert.Equal((100_000m, 90_000m, -10_000m, -10.00m), (sales.Budget, sales.Actual, sales.Variance, sales.VariancePercent));

        // Returns budgeted at 2,000 and none made: a contra account, so the budget reads −2,000 and the variance is favourable.
        BudgetVsActualLineDto returns = Line(report, SalesReturns);
        Assert.Equal((-2_000m, 0m, 2_000m, 100.00m), (returns.Budget, returns.Actual, returns.Variance, returns.VariancePercent));

        // Spending below budget is favourable.
        BudgetVsActualLineDto cost = Line(report, CostOfGoodsSold);
        Assert.Equal((60_000m, 55_000m, 5_000m, 8.33m), (cost.Budget, cost.Actual, cost.Variance, cost.VariancePercent));

        BudgetVsActualLineDto interest = Line(report, Interest);
        Assert.Equal((500m, 700m, 200m, 40.00m), (interest.Budget, interest.Actual, interest.Variance, interest.VariancePercent));
    }

    [Fact]
    public void A_month_totals_each_section_and_works_the_profit_lines()
    {
        BudgetVsActualReportResultDto report = Report(new(2026, 5, 20), BudgetPeriod.Month);

        BudgetVsActualSectionDto revenue = Section(report, ProfitAndLossSection.Revenue);
        Assert.Equal((98_000m, 90_000m, -8_000m, -8.16m), (revenue.Budget, revenue.Actual, revenue.Variance, revenue.VariancePercent));

        Assert.Equal((38_000m, 35_000m, -3_000m, -7.89m),
            (report.GrossProfit.Budget, report.GrossProfit.Actual, report.GrossProfit.Variance, report.GrossProfit.VariancePercent));

        // 38,000 + 500 − 18,000 budgeted; 35,000 + 700 − 18,000 made.
        Assert.Equal((20_500m, 17_700m, -2_800m, -13.66m),
            (report.NetProfit.Budget, report.NetProfit.Actual, report.NetProfit.Variance, report.NetProfit.VariancePercent));
    }

    [Fact]
    public void An_account_with_neither_a_budget_nor_an_actual_is_left_out_and_balance_sheet_accounts_never_appear()
    {
        BudgetVsActualReportResultDto report = Report(new(2026, 5, 20), BudgetPeriod.Month);

        Assert.DoesNotContain(report.Sections.SelectMany(s => s.Lines), l => l.AccountId is PurchaseReturns or Bank or OldStationery);
    }

    // ---- The year to date ----------------------------------------------------------

    [Fact]
    public void The_year_to_date_sums_every_month_up_to_the_as_at_date()
    {
        BudgetVsActualReportResultDto report = Report(new(2026, 6, 15), BudgetPeriod.YearToDate);

        Assert.Equal((300_000m, 320_000m, 20_000m, 6.67m),
            (Line(report, Sales).Budget, Line(report, Sales).Actual, Line(report, Sales).Variance, Line(report, Sales).VariancePercent));
        Assert.Equal((-6_000m, -4_000m, 2_000m), (Line(report, SalesReturns).Budget, Line(report, SalesReturns).Actual, Line(report, SalesReturns).Variance));
        Assert.Equal((294_000m, 316_000m, 22_000m, 7.48m),
            (Section(report, ProfitAndLossSection.Revenue).Budget, Section(report, ProfitAndLossSection.Revenue).Actual,
             Section(report, ProfitAndLossSection.Revenue).Variance, Section(report, ProfitAndLossSection.Revenue).VariancePercent));
        Assert.Equal((114_000m, 126_000m, 12_000m, 10.53m),
            (report.GrossProfit.Budget, report.GrossProfit.Actual, report.GrossProfit.Variance, report.GrossProfit.VariancePercent));
        Assert.Equal((54_000m, 57_000m, -3_000m), (Section(report, ProfitAndLossSection.Expenses).Budget,
            Section(report, ProfitAndLossSection.Expenses).Actual, Section(report, ProfitAndLossSection.Expenses).Variance));
        Assert.Equal((61_500m, 69_700m, 8_200m, 13.33m),
            (report.NetProfit.Budget, report.NetProfit.Actual, report.NetProfit.Variance, report.NetProfit.VariancePercent));
        Assert.Equal(new DateOnly(2026, 4, 1), report.FromDate);
        Assert.Equal(new DateOnly(2026, 6, 30), report.ToDate);
    }

    [Fact]
    public void The_actual_net_profit_is_the_profit_and_loss_for_the_same_dates()
    {
        foreach ((DateOnly asOf, BudgetPeriod period) in new[]
        {
            (new DateOnly(2026, 5, 20), BudgetPeriod.Month),
            (new DateOnly(2026, 6, 15), BudgetPeriod.YearToDate),
            (new DateOnly(2026, 8, 10), BudgetPeriod.Quarter),
        })
        {
            BudgetVsActualReportResultDto report = Report(asOf, period);
            ProfitAndLossReportResultDto pnl = ProfitAndLossReportRepository.Compose(
                Accounts, [.. ProfitAndLossReportRepository.Movements(Ledger.AsQueryable(), report.FromDate, report.ToDate, null, null)],
                Types, report.FromDate, report.ToDate, null, null, isZeroBalanceShown: false);

            Assert.Equal(pnl.Totals.NetProfit, report.NetProfit.Actual);
            Assert.Equal(pnl.Totals.GrossProfit, report.GrossProfit.Actual);
        }
    }

    [Fact]
    public void An_account_made_inactive_still_shows_what_it_spent()
    {
        // July to September: the stationery, against no budget.
        BudgetVsActualLineDto stationery = Line(Report(new(2026, 8, 10), BudgetPeriod.Quarter), OldStationery);

        Assert.Equal((0m, 1_000m, -1_000m), (stationery.Budget, stationery.Actual, stationery.Variance));
        Assert.Null(stationery.VariancePercent);
    }

    [Fact]
    public void Sections_come_in_the_profit_and_loss_order_with_lines_by_code()
    {
        BudgetVsActualReportResultDto report = Report(new(2026, 6, 15), BudgetPeriod.YearToDate);

        Assert.Equal(["Revenue", "Cost of sales", "Other income", "Expenses"], report.Sections.Select(s => s.Title));
        Assert.Equal(["4001", "4002"], Section(report, ProfitAndLossSection.Revenue).Lines.Select(l => l.AccountCode));
    }

    // ---- The arithmetic and the request --------------------------------------------

    [Theory]
    [InlineData(ProfitAndLossSection.Revenue, 100, 120, 20)]
    [InlineData(ProfitAndLossSection.OtherIncome, 100, 80, -20)]
    [InlineData(ProfitAndLossSection.CostOfSales, 100, 120, -20)]
    [InlineData(ProfitAndLossSection.Expenses, 100, 80, 20)]
    public void Variance_is_positive_when_favourable(ProfitAndLossSection section, decimal budget, decimal actual, decimal expected) =>
        Assert.Equal(expected, BudgetVsActualReportRepository.VarianceOf(section, budget, actual));

    [Theory]
    [InlineData(1, 8, 12.5)]
    [InlineData(1, 800, 0.13)] // 0.125: half away from zero
    [InlineData(-1, 800, -0.13)]
    [InlineData(2_000, -2_000, 100)] // of the budget's size, whatever its sign
    public void A_variance_percentage_is_of_the_budget_to_two_places(decimal variance, decimal budget, decimal expected) =>
        Assert.Equal(expected, BudgetVsActualReportRepository.PercentOf(variance, budget));

    [Fact]
    public void Without_a_budget_there_is_no_percentage() =>
        Assert.Null(BudgetVsActualReportRepository.PercentOf(500m, 0m));

    [Theory]
    [InlineData(2026, 4, 1, true)]
    [InlineData(2027, 3, 31, true)]
    [InlineData(2026, 3, 31, false)]
    [InlineData(2027, 4, 1, false)]
    public void The_as_at_date_must_fall_within_the_budgets_year(int year, int month, int day, bool accepted) =>
        Assert.Equal(accepted, BudgetVsActualReportRepository.DateRefusal(TheBudget, new(year, month, day)) is null);

    [Fact]
    public void The_budget_the_as_at_date_and_the_period_are_required()
    {
        Assert.Equal(
            [nameof(BudgetVsActualReportRequest.BudgetId), nameof(BudgetVsActualReportRequest.AsOfDate), nameof(BudgetVsActualReportRequest.Period)],
            typeof(BudgetVsActualReportRequest).GetProperties()
                .Where(p => p.GetCustomAttribute<RequiredAttribute>() is not null)
                .Select(p => p.Name));
    }

    [Fact]
    public async Task Another_branch_in_the_request_is_forbidden_not_hidden()
    {
        var controller = new BudgetVsActualReportController(null!)
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
            new BudgetVsActualReportRequest
            {
                BudgetId = 1, AsOfDate = new(2026, 5, 20), Period = BudgetPeriod.Month, OrgId = Guid.NewGuid(),
            },
            default);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public void Both_endpoints_need_the_budget_add_on_alone_reports_and_accounting_view()
    {
        Type controller = typeof(BudgetVsActualReportController);

        Assert.Equal("api/statements/budget-vs-actual", controller.GetCustomAttribute<RouteAttribute>()!.Template);
        Assert.Equal("budgets", controller.GetMethod(nameof(BudgetVsActualReportController.Budgets))!
            .GetCustomAttribute<HttpGetAttribute>()!.Template);
        Assert.Equal("reports", controller.GetCustomAttribute<RequireModulePermissionAttribute>()!.Module);

        // With the business-type guard beside it, either would admit: a branch without budgets would get in.
        Assert.Equal(AddOn.Budget, controller.GetCustomAttribute<RequireAddOnAttribute>()!.AddOn);
        Assert.Null(controller.GetCustomAttribute<RequireBusinessTypeAttribute>());

        foreach (string action in new[] { nameof(BudgetVsActualReportController.Run), nameof(BudgetVsActualReportController.Budgets) })
        {
            Assert.Contains(
                "accounting.view",
                controller.GetMethod(action)!.GetCustomAttributes<RequirePermissionAttribute>().Select(a => a.Permission));
        }
    }
}
