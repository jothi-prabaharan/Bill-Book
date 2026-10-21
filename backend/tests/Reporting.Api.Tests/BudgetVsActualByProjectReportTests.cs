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
/// Budget vs Actual by Project's own page (TK-207), on a budget kept by project
/// for April 2026 to March 2027 and two months of postings worked by hand. The
/// sums run on lists through the same expressions the repository runs against
/// the read models; the composition is pure. The last tests hold the catalog's
/// rewritten by-project query to the same figures.
/// </summary>
public sealed class BudgetVsActualByProjectReportTests
{
    private const int AssetType = 1, IncomeType = 4, ExpenseType = 5;
    private const long Bank = 1001, Equipment = 1501, Sales = 4001, SalesReturns = 4002,
        CostOfGoodsSold = 5001, Rent = 6001, Salaries = 6002;
    private const long Website = 11, FitOut = 12, Idle = 13, Purchase = 14;

    private static readonly List<AccountTypeDto> Types =
    [
        new(1, "Asset", "Asset", "Debit", "BalanceSheet", 1),
        new(2, "Liability", "Liability", "Credit", "BalanceSheet", 2),
        new(3, "Equity", "Equity", "Credit", "BalanceSheet", 3),
        new(4, "Income", "Income", "Credit", "ProfitAndLoss", 4),
        new(5, "Expense", "Expense", "Debit", "ProfitAndLoss", 5),
    ];

    private static ProfitAndLossAccount Account(long id, int type, string name, bool sales = false, bool contra = false, string? systemName = null) =>
        new()
        {
            AccountId = id, AccountTypeId = type, AccountCode = id.ToString(), AccountName = name,
            AccountSystemName = systemName, IsSales = sales, IsContra = contra, IsActive = true,
        };

    private static readonly List<ProfitAndLossAccount> Accounts =
    [
        Account(Bank, AssetType, "Bank"),
        Account(Equipment, AssetType, "Equipment"),
        Account(Sales, IncomeType, "Sales", sales: true),
        Account(SalesReturns, IncomeType, "Sales Returns", sales: true, contra: true),
        Account(CostOfGoodsSold, ExpenseType, "Cost of Goods Sold", systemName: BusinessPerformanceSourceRepository.CostOfGoodsSold),
        Account(Rent, ExpenseType, "Rent"),
        Account(Salaries, ExpenseType, "Salaries"),
    ];

    private static readonly List<BudgetVsActualProjectOptionDto> Projects =
    [
        new() { ProjectId = FitOut, ProjectCode = "PRJ-002", ProjectName = "Shop fit-out", Status = ProjectStatus.Completed },
        new() { ProjectId = Website, ProjectCode = "PRJ-001", ProjectName = "Website", Status = ProjectStatus.Active },
        new() { ProjectId = Idle, ProjectCode = "PRJ-003", ProjectName = "Idle", Status = ProjectStatus.OnHold },
        new() { ProjectId = Purchase, ProjectCode = "PRJ-004", ProjectName = "New till", Status = ProjectStatus.Active },
    ];

    private static readonly BudgetVsActualBudgetDto TheBudget = new()
    {
        BudgetId = 1, Name = "FY 2026-27 by project", FinancialYearStart = new(2026, 4, 1), FinancialYearEnd = new(2027, 3, 31),
        Status = BudgetStatus.Approved, IsBranchDefault = true, ByProject = true,
    };

    private static long _line;

    private static BudgetLineRead Line(long account, long? project, int period, decimal amount, long budgetId = 1) =>
        new() { BudgetLineId = ++_line, BudgetId = budgetId, AccountId = account, ProjectId = project, Period = (short)period, Amount = amount };

    /// <summary>
    /// Every month: the website sells 50,000 at a cost of 30,000 with 5,000 of
    /// salaries; the fit-out sells 20,000 and pays 4,000 rent.
    /// </summary>
    private static readonly List<BudgetLineRead> Lines =
    [
        .. Enumerable.Range(1, 12).SelectMany(p => new[]
        {
            Line(Sales, Website, p, 50_000m), Line(CostOfGoodsSold, Website, p, 30_000m), Line(Salaries, Website, p, 5_000m),
            Line(Sales, FitOut, p, 20_000m), Line(Rent, FitOut, p, 4_000m),
        }),
        Line(Rent, null, 2, 99_999m),                  // no project: not this report's
        Line(Sales, Website, 2, 77_777m, budgetId: 2), // another budget's line
    ];

    private static JournalLedgerRead Leg(DateOnly date, long account, long? project, decimal debit, decimal credit) =>
        new()
        {
            LedgerDate = date, AccountId = account, ProjectId = project,
            DebitAmountBase = debit, CreditAmountBase = credit, TransactionTypeCode = "JRN",
        };

    private static IEnumerable<JournalLedgerRead> Earned(DateOnly date, long account, long? project, decimal amount) =>
        [Leg(date, Bank, project, amount, 0m), Leg(date, account, project, 0m, amount)];

    private static IEnumerable<JournalLedgerRead> Spent(DateOnly date, long account, long? project, decimal amount) =>
        [Leg(date, account, project, amount, 0m), Leg(date, Bank, project, 0m, amount)];

    private static readonly List<JournalLedgerRead> Ledger =
    [
        // April.
        .. Earned(new(2026, 4, 15), Sales, Website, 40_000m), .. Spent(new(2026, 4, 30), Rent, FitOut, 4_000m),
        // May: the website sells 55,000, a 1,000 return, cost 28,000, salaries 6,000.
        .. Earned(new(2026, 5, 10), Sales, Website, 55_000m), .. Spent(new(2026, 5, 12), SalesReturns, Website, 1_000m),
        .. Spent(new(2026, 5, 20), CostOfGoodsSold, Website, 28_000m), .. Spent(new(2026, 5, 31), Salaries, Website, 6_000m),
        // May: the fit-out sells 18,000 and pays 4,500 rent.
        .. Earned(new(2026, 5, 15), Sales, FitOut, 18_000m), .. Spent(new(2026, 5, 31), Rent, FitOut, 4_500m),
        // May: a till bought for a project, which touches no profit and loss account.
        .. Spent(new(2026, 5, 5), Equipment, Purchase, 25_000m),
        // May: rent with no project, which is Budget vs Actual's and not this report's.
        .. Spent(new(2026, 5, 31), Rent, null, 10_000m),
    ];

    private static BudgetVsActualByProjectReportResultDto Report(
        DateOnly asOf, BudgetPeriod period, long? projectId = null,
        List<JournalLedgerRead>? ledger = null, List<BudgetVsActualProjectOptionDto>? projects = null)
    {
        BudgetWindow window = BudgetVsActualReportRepository.WindowOf(TheBudget, asOf, period);
        return BudgetVsActualByProjectReportRepository.Compose(
            Accounts, Types, projects ?? Projects,
            [.. BudgetVsActualByProjectReportRepository.Budgeted(Lines.AsQueryable(), TheBudget.BudgetId, window.PeriodFrom, window.PeriodTo, projectId)],
            [.. BudgetVsActualByProjectReportRepository.Actuals((ledger ?? Ledger).AsQueryable(), window.From, window.To, projectId)],
            TheBudget, asOf, period, window, projectId);
    }

    private static BudgetVsActualProjectDto Project(BudgetVsActualByProjectReportResultDto report, long id) =>
        report.Projects.Single(p => p.ProjectId == id);

    private static BudgetVsActualLineDto Line(BudgetVsActualProjectDto project, long account) =>
        project.Sections.SelectMany(s => s.Lines).Single(l => l.AccountId == account);

    private static BudgetVsActualSectionDto Section(BudgetVsActualProjectDto project, ProfitAndLossSection section) =>
        project.Sections.Single(s => s.Section == section);

    private static readonly DateOnly May20 = new(2026, 5, 20);

    // ---- The sums -------------------------------------------------------------------

    [Fact]
    public void Only_the_budgets_own_lines_with_a_project_in_the_months_covered_are_summed()
    {
        List<ProjectAccountAmount> budgeted = [.. BudgetVsActualByProjectReportRepository.Budgeted(Lines.AsQueryable(), 1, 2, 2, null)];

        Assert.Equal(50_000m, budgeted.Single(b => b.ProjectId == Website && b.AccountId == Sales).Amount); // not the other budget's
        Assert.DoesNotContain(budgeted, b => b.AccountId == Rent && b.Amount == 99_999m);
        Assert.Equal(5, budgeted.Count);
    }

    [Fact]
    public void Only_postings_with_a_project_in_the_window_are_summed_as_debits_less_credits()
    {
        List<ProjectAccountAmount> actuals = [.. BudgetVsActualByProjectReportRepository.Actuals(Ledger.AsQueryable(), new(2026, 5, 1), new(2026, 5, 31), null)];

        Assert.Equal(-55_000m, actuals.Single(a => a.ProjectId == Website && a.AccountId == Sales).Amount);
        Assert.Equal(4_500m, actuals.Single(a => a.ProjectId == FitOut && a.AccountId == Rent).Amount);
        Assert.DoesNotContain(actuals, a => a.AccountId == Rent && a.Amount == 10_000m);
    }

    [Fact]
    public void One_project_asked_for_narrows_both_sums()
    {
        Assert.All(BudgetVsActualByProjectReportRepository.Budgeted(Lines.AsQueryable(), 1, 1, 12, FitOut), b => Assert.Equal(FitOut, b.ProjectId));
        Assert.All(BudgetVsActualByProjectReportRepository.Actuals(Ledger.AsQueryable(), new(2026, 4, 1), new(2027, 3, 31), FitOut), a => Assert.Equal(FitOut, a.ProjectId));
    }

    // ---- One month, project by project ---------------------------------------------

    [Fact]
    public void The_website_in_May_is_laid_out_as_budget_vs_actual_lays_out_the_branch()
    {
        BudgetVsActualProjectDto website = Project(Report(May20, BudgetPeriod.Month), Website);

        Assert.Equal(("PRJ-001", "Website", ProjectStatus.Active), (website.ProjectCode, website.ProjectName, website.Status));
        Assert.Equal((50_000m, 55_000m, 5_000m, 10.00m),
            (Line(website, Sales).Budget, Line(website, Sales).Actual, Line(website, Sales).Variance, Line(website, Sales).VariancePercent));

        // A return with no budget: adverse, and no percentage.
        Assert.Equal((0m, -1_000m, -1_000m), (Line(website, SalesReturns).Budget, Line(website, SalesReturns).Actual, Line(website, SalesReturns).Variance));
        Assert.Null(Line(website, SalesReturns).VariancePercent);

        Assert.Equal((50_000m, 54_000m, 4_000m, 8.00m),
            (Section(website, ProfitAndLossSection.Revenue).Budget, Section(website, ProfitAndLossSection.Revenue).Actual,
             Section(website, ProfitAndLossSection.Revenue).Variance, Section(website, ProfitAndLossSection.Revenue).VariancePercent));
        Assert.Equal((30_000m, 28_000m, 2_000m, 6.67m),
            (Line(website, CostOfGoodsSold).Budget, Line(website, CostOfGoodsSold).Actual, Line(website, CostOfGoodsSold).Variance, Line(website, CostOfGoodsSold).VariancePercent));
        Assert.Equal((20_000m, 26_000m, 6_000m, 30.00m),
            (website.GrossProfit.Budget, website.GrossProfit.Actual, website.GrossProfit.Variance, website.GrossProfit.VariancePercent));
        Assert.Equal((5_000m, 6_000m, -1_000m, -20.00m),
            (Line(website, Salaries).Budget, Line(website, Salaries).Actual, Line(website, Salaries).Variance, Line(website, Salaries).VariancePercent));
        Assert.Equal((15_000m, 20_000m, 5_000m, 33.33m),
            (website.NetProfit.Budget, website.NetProfit.Actual, website.NetProfit.Variance, website.NetProfit.VariancePercent));
    }

    [Fact]
    public void The_fit_out_in_May_shows_its_own_figures_and_its_status()
    {
        BudgetVsActualProjectDto fitOut = Project(Report(May20, BudgetPeriod.Month), FitOut);

        Assert.Equal(ProjectStatus.Completed, fitOut.Status);
        Assert.Equal((20_000m, 18_000m, -2_000m, -10.00m),
            (Line(fitOut, Sales).Budget, Line(fitOut, Sales).Actual, Line(fitOut, Sales).Variance, Line(fitOut, Sales).VariancePercent));
        Assert.Equal((4_000m, 4_500m, -500m, -12.50m),
            (Line(fitOut, Rent).Budget, Line(fitOut, Rent).Actual, Line(fitOut, Rent).Variance, Line(fitOut, Rent).VariancePercent));

        // 16,000 budgeted, 13,500 made: 2,500 short, 15.625% rounded half away from zero.
        Assert.Equal((16_000m, 13_500m, -2_500m, -15.63m),
            (fitOut.NetProfit.Budget, fitOut.NetProfit.Actual, fitOut.NetProfit.Variance, fitOut.NetProfit.VariancePercent));
    }

    [Fact]
    public void The_profit_across_the_projects_is_theirs_added_up()
    {
        BudgetVsActualByProjectReportResultDto report = Report(May20, BudgetPeriod.Month);

        Assert.Equal((40_000m, 44_000m, 4_000m, 10.00m),
            (report.GrossProfit.Budget, report.GrossProfit.Actual, report.GrossProfit.Variance, report.GrossProfit.VariancePercent));
        Assert.Equal((31_000m, 33_500m, 2_500m, 8.06m),
            (report.NetProfit.Budget, report.NetProfit.Actual, report.NetProfit.Variance, report.NetProfit.VariancePercent));
        Assert.Equal(report.Projects.Sum(p => p.NetProfit.Actual), report.NetProfit.Actual);
        Assert.Equal(report.Projects.Sum(p => p.NetProfit.Budget), report.NetProfit.Budget);
    }

    [Fact]
    public void Projects_with_nothing_on_the_profit_and_loss_are_left_out_and_the_rest_come_by_code()
    {
        BudgetVsActualByProjectReportResultDto report = Report(May20, BudgetPeriod.Month);

        // The idle project has nothing at all; the till is a purchase of equipment.
        Assert.Equal(["PRJ-001", "PRJ-002"], report.Projects.Select(p => p.ProjectCode));
        Assert.DoesNotContain(report.Projects.SelectMany(p => p.Sections).SelectMany(s => s.Lines), l => l.AccountId is Bank or Equipment);
    }

    [Fact]
    public void Rent_with_no_project_is_not_in_any_project()
    {
        BudgetVsActualProjectDto fitOut = Project(Report(May20, BudgetPeriod.Month), FitOut);

        Assert.Equal(4_500m, Line(fitOut, Rent).Actual);
        Assert.Equal(4_000m, Line(fitOut, Rent).Budget);
    }

    // ---- Windows, one project, and the tie to Budget vs Actual ----------------------

    [Fact]
    public void The_year_to_date_sums_every_month_up_to_the_as_at_date()
    {
        BudgetVsActualByProjectReportResultDto report = Report(new(2026, 6, 15), BudgetPeriod.YearToDate);

        Assert.Equal((150_000m, 95_000m), (Line(Project(report, Website), Sales).Budget, Line(Project(report, Website), Sales).Actual));
        Assert.Equal((12_000m, 8_500m), (Line(Project(report, FitOut), Rent).Budget, Line(Project(report, FitOut), Rent).Actual));
        Assert.Equal((new DateOnly(2026, 4, 1), new DateOnly(2026, 6, 30)), (report.FromDate, report.ToDate));
    }

    [Fact]
    public void One_project_asked_for_is_the_only_one_shown_and_the_total_is_its_own()
    {
        BudgetVsActualByProjectReportResultDto report = Report(May20, BudgetPeriod.Month, FitOut);

        BudgetVsActualProjectDto only = Assert.Single(report.Projects);
        Assert.Equal(FitOut, only.ProjectId);
        Assert.Equal(FitOut, report.ProjectId);
        Assert.Equal((only.NetProfit.Budget, only.NetProfit.Actual), (report.NetProfit.Budget, report.NetProfit.Actual));
    }

    [Fact]
    public void With_every_posting_on_a_project_the_actual_profit_across_projects_is_budget_vs_actuals()
    {
        List<JournalLedgerRead> projected = [.. Ledger.Where(l => l.ProjectId is not null)];

        foreach ((DateOnly asOf, BudgetPeriod period) in new[]
        {
            (May20, BudgetPeriod.Month),
            (new DateOnly(2026, 6, 15), BudgetPeriod.YearToDate),
            (new DateOnly(2026, 4, 1), BudgetPeriod.Quarter),
        })
        {
            BudgetVsActualByProjectReportResultDto byProject = Report(asOf, period, ledger: projected);
            BudgetWindow window = BudgetVsActualReportRepository.WindowOf(TheBudget, asOf, period);
            BudgetVsActualReportResultDto branch = BudgetVsActualReportRepository.Compose(
                Accounts, Types, [],
                [.. BudgetVsActualReportRepository.Actuals(projected.AsQueryable(), window.From, window.To)],
                TheBudget, asOf, period, window);

            Assert.Equal(branch.NetProfit.Actual, byProject.NetProfit.Actual);
            Assert.Equal(branch.GrossProfit.Actual, byProject.GrossProfit.Actual);
        }
    }

    [Fact]
    public void A_posting_on_a_project_not_in_the_list_is_still_shown_so_nothing_is_lost()
    {
        BudgetVsActualByProjectReportResultDto report = Report(
            May20, BudgetPeriod.Month, ledger: [.. Ledger, .. Earned(May20, Sales, 99, 1_000m)]);

        BudgetVsActualProjectDto unknown = Project(report, 99);
        Assert.Equal(("Project 99", 1_000m), (unknown.ProjectName, unknown.NetProfit.Actual));
    }

    // ---- The request ----------------------------------------------------------------

    [Fact]
    public void A_budget_must_be_kept_by_project()
    {
        Assert.Null(BudgetVsActualByProjectReportRepository.BudgetRefusal(TheBudget));
        Assert.NotNull(BudgetVsActualByProjectReportRepository.BudgetRefusal(new BudgetVsActualBudgetDto { ByProject = false }));
    }

    [Fact]
    public void The_budget_the_as_at_date_and_the_period_are_required_and_the_project_is_not()
    {
        Assert.Equal(
            [nameof(BudgetVsActualByProjectReportRequest.BudgetId), nameof(BudgetVsActualByProjectReportRequest.AsOfDate), nameof(BudgetVsActualByProjectReportRequest.Period)],
            typeof(BudgetVsActualByProjectReportRequest).GetProperties()
                .Where(p => p.GetCustomAttribute<RequiredAttribute>() is not null)
                .Select(p => p.Name));
    }

    [Fact]
    public async Task Another_branch_in_the_request_is_forbidden_not_hidden()
    {
        var controller = new BudgetVsActualByProjectReportController(null!, null!)
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
            new BudgetVsActualByProjectReportRequest
            {
                BudgetId = 1, AsOfDate = May20, Period = BudgetPeriod.Month, OrgId = Guid.NewGuid(),
            },
            default);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public void Both_endpoints_need_the_budget_add_on_alone_reports_and_accounting_view()
    {
        Type controller = typeof(BudgetVsActualByProjectReportController);

        Assert.Equal("api/statements/budget-vs-actual-by-project", controller.GetCustomAttribute<RouteAttribute>()!.Template);
        Assert.Equal("projects", controller.GetMethod(nameof(BudgetVsActualByProjectReportController.Projects))!
            .GetCustomAttribute<HttpGetAttribute>()!.Template);
        Assert.Equal("reports", controller.GetCustomAttribute<RequireModulePermissionAttribute>()!.Module);
        Assert.Equal(AddOn.Budget, controller.GetCustomAttribute<RequireAddOnAttribute>()!.AddOn);
        Assert.Null(controller.GetCustomAttribute<RequireBusinessTypeAttribute>());

        foreach (string action in new[] { nameof(BudgetVsActualByProjectReportController.Run), nameof(BudgetVsActualByProjectReportController.Projects) })
        {
            Assert.Contains(
                "accounting.view",
                controller.GetMethod(action)!.GetCustomAttributes<RequirePermissionAttribute>().Select(a => a.Permission));
        }
    }

    // ---- The catalog's rewritten queries --------------------------------------------

    private static readonly List<AccountRead> CatalogAccounts =
    [
        .. Accounts.Select(a => new AccountRead
        {
            AccountId = a.AccountId, AccountTypeId = a.AccountTypeId, AccountCode = a.AccountCode, AccountName = a.AccountName,
            IsContra = a.IsContra, IsActive = true,
        }),
    ];

    private static readonly List<ProjectRead> CatalogProjects =
    [
        .. Projects.Select(p => new ProjectRead
        {
            ProjectId = p.ProjectId, ProjectCode = p.ProjectCode, ProjectName = p.ProjectName,
            BillingMethod = "NonBillable", Status = p.Status.ToString(),
        }),
    ];

    [Fact]
    public void The_catalogs_by_project_rows_carry_the_pages_figures()
    {
        BudgetWindow window = BudgetVsActualReportRepository.WindowOf(TheBudget, May20, BudgetPeriod.Month);
        List<BudgetActualRow> rows =
        [
            .. BudgetReports.AgainstActualByProject(
                CatalogAccounts.AsQueryable(), CatalogProjects.AsQueryable(), Ledger.AsQueryable(), Lines.AsQueryable(), TheBudget.BudgetId, window),
        ];
        BudgetVsActualByProjectReportResultDto page = Report(May20, BudgetPeriod.Month);

        foreach (BudgetVsActualProjectDto project in page.Projects)
        {
            foreach (BudgetVsActualLineDto line in project.Sections.SelectMany(s => s.Lines))
            {
                BudgetActualRow row = rows.Single(r => r.ProjectCode == project.ProjectCode && r.AccountId == line.AccountId);
                Assert.Equal((line.Budget, line.Actual, line.Variance), (row.Budget, row.Actual, row.Variance));
            }
        }

        Assert.Equal(page.Projects.Sum(p => p.Sections.Sum(s => s.Lines.Count)), rows.Count);
        Assert.Equal("PRJ-001/4001", rows.Single(r => r.ProjectCode == "PRJ-001" && r.AccountId == Sales).RowKey);
    }

    [Fact]
    public void The_catalogs_by_project_rows_show_no_budget_without_a_budget_kept_by_project()
    {
        BudgetWindow window = BudgetVsActualReportRepository.WindowOf(TheBudget, May20, BudgetPeriod.Month);
        List<BudgetActualRow> rows =
        [
            .. BudgetReports.AgainstActualByProject(
                CatalogAccounts.AsQueryable(), CatalogProjects.AsQueryable(), Ledger.AsQueryable(), Lines.AsQueryable(), null, window),
        ];

        Assert.All(rows, r => Assert.Equal(0m, r.Budget));
        Assert.Equal(55_000m, rows.Single(r => r.ProjectCode == "PRJ-001" && r.AccountId == Sales).Actual);
    }

    [Fact]
    public void The_catalogs_branch_rows_count_every_posting_project_or_not()
    {
        BudgetWindow window = BudgetVsActualReportRepository.WindowOf(TheBudget, May20, BudgetPeriod.Month);
        List<BudgetActualRow> rows =
            [.. BudgetReports.AgainstActual(CatalogAccounts.AsQueryable(), Ledger.AsQueryable(), Lines.AsQueryable(), TheBudget.BudgetId, window)];

        // 4,500 on the fit-out and 10,000 on no project; 4,000 budgeted on the fit-out and 99,999 on none.
        BudgetActualRow rent = rows.Single(r => r.AccountId == Rent);
        Assert.Equal((103_999m, 14_500m, 89_499m, "Favourable"), (rent.Budget, rent.Actual, rent.Variance, rent.Result));
        Assert.Equal((70_000m, 73_000m), (rows.Single(r => r.AccountId == Sales).Budget, rows.Single(r => r.AccountId == Sales).Actual));
        Assert.Equal(-1_000m, rows.Single(r => r.AccountId == SalesReturns).Actual);
        Assert.DoesNotContain(rows, r => r.AccountId is Bank or Equipment);
    }
}
