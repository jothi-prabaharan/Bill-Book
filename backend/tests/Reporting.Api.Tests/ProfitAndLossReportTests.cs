using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Reporting.Api.Controllers;
using Reporting.Entity.Enums;
using Reporting.Entity.Models;
using Reporting.Repository.ReadModels;
using Reporting.Repository.Repositories;
using Shared.Kernel.Internal;
using Xunit;
using static Reporting.Repository.Repositories.BatchedNameResolverRepository;

namespace Reporting.Api.Tests;

/// <summary>
/// The Profit &amp; Loss (TK-200), on a small ledger worked by hand: April to
/// September 2026, compared with April to September 2025. Every journal
/// balances, and legs just outside each period are there to be left out.
///
/// The movements run on a list through the same expression the repository
/// runs against <c>acc.JournalLedger</c>; the composition is pure.
/// </summary>
public sealed class ProfitAndLossReportTests
{
    private static readonly DateOnly From = new(2026, 4, 1);
    private static readonly DateOnly To = new(2026, 9, 30);
    private static readonly DateOnly CompareFrom = new(2025, 4, 1);
    private static readonly DateOnly CompareTo = new(2025, 9, 30);

    private static readonly List<AccountTypeDto> Types =
    [
        new(1, "Asset", "Asset", "Debit", "BalanceSheet", 1),
        new(2, "Liability", "Liability", "Credit", "BalanceSheet", 2),
        new(3, "Equity", "Equity", "Credit", "BalanceSheet", 3),
        new(4, "Income", "Income", "Credit", "ProfitAndLoss", 4),
        new(5, "Expense", "Expense", "Debit", "ProfitAndLoss", 5),
    ];

    private static ProfitAndLossAccount Account(
        long id, int type, string code, string name,
        bool sales = false, bool contra = false, bool active = true, string? systemName = null) =>
        new()
        {
            AccountId = id,
            AccountTypeId = type,
            AccountCode = code,
            AccountName = name,
            AccountSystemName = systemName,
            IsSales = sales,
            IsContra = contra,
            IsActive = active,
        };

    private static readonly List<ProfitAndLossAccount> Accounts =
    [
        Account(1001, 1, "1001", "Cash"),
        Account(1002, 1, "1002", "Inventory"),
        Account(4002, 4, "4002", "Sales Returns", sales: true, contra: true),
        Account(4001, 4, "4001", "Sales", sales: true),
        Account(4100, 4, "4100", "Fee Income"),
        Account(4200, 4, "4200", "Foreign Exchange Gain"),
        Account(5001, 5, "5001", "Cost of Goods Sold", systemName: BusinessPerformanceSourceRepository.CostOfGoodsSold),
        Account(5002, 5, "5002", "Purchase Returns", contra: true, systemName: BusinessPerformanceSourceRepository.PurchaseReturns),
        Account(6001, 5, "6001", "Rent"),
        Account(6002, 5, "6002", "Salaries"),
        Account(6003, 5, "6003", "Office Supplies", active: false),
        Account(6004, 5, "6004", "Insurance"),
        Account(6005, 5, "6005", "Advertising"),
    ];

    private static long _leg;

    private static JournalLedgerRead Leg(DateOnly date, long account, decimal debit, decimal credit) =>
        new() { LedgerId = ++_leg, LedgerDate = date, AccountId = account, DebitAmountBase = debit, CreditAmountBase = credit };

    private static IEnumerable<JournalLedgerRead> Journal(DateOnly date, long debitAccount, long creditAccount, decimal amount) =>
        [Leg(date, debitAccount, amount, 0m), Leg(date, creditAccount, 0m, amount)];

    private static readonly List<JournalLedgerRead> Ledger =
    [
        .. Journal(new(2025, 3, 31), 1001, 4001, 4_444m), // before the comparison period
        .. Journal(new(2025, 5, 1), 1001, 4001, 50_000m), // sales, compared
        .. Journal(new(2025, 5, 2), 5001, 1002, 30_000m), // cost of sales, compared
        .. Journal(new(2025, 6, 1), 6001, 1001, 12_000m), // rent, compared
        .. Journal(new(2025, 7, 1), 6005, 1001, 1_000m), // advertising, compared only
        .. Journal(new(2026, 3, 31), 1001, 4001, 7_777m), // between the two periods
        .. Journal(new(2026, 4, 10), 1001, 4001, 100_000m), // sales
        .. Journal(new(2026, 5, 5), 4002, 1001, 5_000m), // a sales return
        .. Journal(new(2026, 5, 10), 5001, 1002, 60_000m), // cost of sales
        .. Journal(new(2026, 6, 1), 1001, 5002, 2_000m), // a purchase return
        .. Journal(new(2026, 6, 15), 6001, 1001, 12_000m), // rent
        .. Journal(new(2026, 7, 1), 6002, 1001, 8_000m), // salaries
        .. Journal(new(2026, 8, 1), 1001, 4100, 3_000m), // fee income
        .. Journal(new(2026, 9, 30), 1001, 4200, 500m), // an exchange gain, on the last day
        .. Journal(new(2026, 10, 1), 1001, 4001, 9_999m), // after the period
    ];

    private static List<AccountPeriodMovement> Movements(IEnumerable<JournalLedgerRead> ledger, bool compare = false) =>
    [
        .. ProfitAndLossReportRepository.Movements(
            ledger.AsQueryable(), From, To, compare ? CompareFrom : null, compare ? CompareTo : null),
    ];

    private static ProfitAndLossReportResultDto Report(
        bool compare = false, bool zeroes = false, IEnumerable<JournalLedgerRead>? ledger = null) =>
        ProfitAndLossReportRepository.Compose(
            Accounts, Movements(ledger ?? Ledger, compare), Types,
            From, To, compare ? CompareFrom : null, compare ? CompareTo : null, zeroes);

    private static ProfitAndLossSectionDto Section(ProfitAndLossReportResultDto report, ProfitAndLossSection section) =>
        report.Sections.Single(s => s.Section == section);

    private static ProfitAndLossLineDto Line(ProfitAndLossReportResultDto report, string name) =>
        report.Sections.SelectMany(s => s.Lines).Single(l => l.AccountName == name);

    private static IEnumerable<string> Names(ProfitAndLossReportResultDto report) =>
        report.Sections.SelectMany(s => s.Lines).Select(l => l.AccountName);

    // ---- The movements -----------------------------------------------------------

    [Fact]
    public void Each_account_is_summed_over_the_period_with_legs_either_side_left_out()
    {
        AccountPeriodMovement sales = Movements(Ledger).Single(m => m.AccountId == 4001);

        // Only the 100,000: not the 7,777 the day before nor the 9,999 the day after.
        Assert.Equal(-100_000m, sales.Net);
    }

    [Fact]
    public void A_comparison_period_is_summed_in_the_same_pass()
    {
        AccountPeriodMovement sales = Movements(Ledger, compare: true).Single(m => m.AccountId == 4001);

        Assert.Equal(-100_000m, sales.Net);
        // Only the 50,000: not the 4,444 before it nor the 7,777 between the periods.
        Assert.Equal(-50_000m, sales.CompareNet);
    }

    [Fact]
    public void Both_days_at_the_ends_of_the_period_count()
    {
        AccountPeriodMovement gain = Movements(Ledger).Single(m => m.AccountId == 4200);
        AccountPeriodMovement first = Movements([.. Journal(From, 1001, 4100, 1m)]).Single(m => m.AccountId == 4100);

        Assert.Equal(-500m, gain.Net);
        Assert.Equal(-1m, first.Net);
    }

    // ---- The sections ------------------------------------------------------------

    [Fact]
    public void Sales_accounts_are_revenue_and_other_income_accounts_are_other_income()
    {
        ProfitAndLossReportResultDto report = Report();

        Assert.Equal(["4001", "4002"], Section(report, ProfitAndLossSection.Revenue).Lines.Select(l => l.AccountCode));
        Assert.Equal(["4100", "4200"], Section(report, ProfitAndLossSection.OtherIncome).Lines.Select(l => l.AccountCode));
    }

    [Fact]
    public void Cost_of_goods_sold_and_purchase_returns_are_cost_of_sales_and_the_rest_are_expenses()
    {
        ProfitAndLossReportResultDto report = Report();

        Assert.Equal(["5001", "5002"], Section(report, ProfitAndLossSection.CostOfSales).Lines.Select(l => l.AccountCode));
        Assert.Equal(["6001", "6002"], Section(report, ProfitAndLossSection.Expenses).Lines.Select(l => l.AccountCode));
    }

    [Fact]
    public void Sections_come_in_their_order_with_their_titles()
    {
        Assert.Equal(
            ["Revenue", "Cost of sales", "Other income", "Expenses"],
            Report().Sections.Select(s => s.Title));
    }

    [Fact]
    public void Each_amount_reads_positive_on_its_own_side_and_a_contra_account_negative()
    {
        ProfitAndLossReportResultDto report = Report();

        Assert.Equal(100_000m, Line(report, "Sales").Amount);
        Assert.Equal(60_000m, Line(report, "Cost of Goods Sold").Amount);

        ProfitAndLossLineDto returns = Line(report, "Sales Returns");
        Assert.Equal(-5_000m, returns.Amount);
        Assert.True(returns.IsContra);
        Assert.Equal(-2_000m, Line(report, "Purchase Returns").Amount);
    }

    [Fact]
    public void Balance_sheet_accounts_are_never_listed()
    {
        Assert.DoesNotContain(Names(Report(zeroes: true)), n => n is "Cash" or "Inventory");
    }

    [Fact]
    public void An_account_with_no_movement_is_shown_only_when_asked_for_and_only_while_active()
    {
        Assert.DoesNotContain(Names(Report()), n => n == "Insurance");
        Assert.Equal(0m, Line(Report(zeroes: true), "Insurance").Amount);
        Assert.DoesNotContain(Names(Report(zeroes: true)), n => n == "Office Supplies");
    }

    [Fact]
    public void An_account_that_moved_only_in_the_comparison_period_is_shown_beside_it()
    {
        Assert.DoesNotContain(Names(Report()), n => n == "Advertising");

        ProfitAndLossLineDto advertising = Line(Report(compare: true), "Advertising");
        Assert.Equal(0m, advertising.Amount);
        Assert.Equal(1_000m, advertising.CompareAmount);
    }

    [Fact]
    public void An_account_of_a_type_master_does_not_list_is_left_out()
    {
        ProfitAndLossReportResultDto report = ProfitAndLossReportRepository.Compose(
            [.. Accounts, Account(9001, 99, "9001", "Unknown type")], Movements(Ledger), Types,
            From, To, null, null, isZeroBalanceShown: true);

        Assert.DoesNotContain(Names(report), n => n == "Unknown type");
    }

    // ---- The totals --------------------------------------------------------------

    [Fact]
    public void The_profit_lines_are_worked_from_the_sections()
    {
        ProfitAndLossTotalsDto totals = Report().Totals;

        Assert.Equal(95_000m, totals.Revenue); // 100,000 less 5,000 returned
        Assert.Equal(58_000m, totals.CostOfSales); // 60,000 less 2,000 returned
        Assert.Equal(37_000m, totals.GrossProfit);
        Assert.Equal(3_500m, totals.OtherIncome);
        Assert.Equal(20_000m, totals.Expenses);
        Assert.Equal(20_500m, totals.NetProfit);
        Assert.Equal(38.95m, totals.GrossMarginPercent); // 37,000 / 95,000 = 38.947…
        Assert.Equal(21.58m, totals.NetMarginPercent); // 20,500 / 95,000 = 21.578…
    }

    [Fact]
    public void Each_section_total_is_the_sum_of_its_lines()
    {
        Assert.All(Report().Sections, s => Assert.Equal(s.Lines.Sum(l => l.Amount), s.Total));
    }

    [Fact]
    public void Net_profit_is_every_income_and_expense_leg_in_the_period_credits_less_debits()
    {
        HashSet<long> profitAndLoss = [.. Accounts.Where(a => a.AccountTypeId is 4 or 5).Select(a => a.AccountId)];
        decimal expected = Ledger
            .Where(l => l.LedgerDate >= From && l.LedgerDate <= To && profitAndLoss.Contains(l.AccountId))
            .Sum(l => l.CreditAmountBase - l.DebitAmountBase);

        Assert.Equal(expected, Report().Totals.NetProfit);
    }

    [Fact]
    public void The_comparison_period_has_its_own_amounts_and_profit_lines()
    {
        ProfitAndLossReportResultDto report = Report(compare: true);
        ProfitAndLossTotalsDto totals = report.Totals;

        Assert.Equal(50_000m, Line(report, "Sales").CompareAmount);
        Assert.Equal(0m, Line(report, "Sales Returns").CompareAmount);
        Assert.Equal(50_000m, totals.CompareRevenue);
        Assert.Equal(30_000m, totals.CompareCostOfSales);
        Assert.Equal(20_000m, totals.CompareGrossProfit);
        Assert.Equal(0m, totals.CompareOtherIncome);
        Assert.Equal(13_000m, totals.CompareExpenses);
        Assert.Equal(7_000m, totals.CompareNetProfit);
        Assert.Equal(40.00m, totals.CompareGrossMarginPercent);
        Assert.Equal(14.00m, totals.CompareNetMarginPercent);
        Assert.Equal(CompareFrom, report.CompareFromDate);
        Assert.Equal(CompareTo, report.CompareToDate);

        // The period's own figures do not move for being compared.
        Assert.Equal(20_500m, totals.NetProfit);
    }

    [Fact]
    public void Without_a_comparison_period_there_are_no_comparison_figures()
    {
        ProfitAndLossReportResultDto report = Report();

        Assert.All(report.Sections.SelectMany(s => s.Lines), l => Assert.Null(l.CompareAmount));
        Assert.All(report.Sections, s => Assert.Null(s.CompareTotal));
        Assert.Null(report.Totals.CompareNetProfit);
        Assert.Null(report.CompareFromDate);
        Assert.Equal(From, report.FromDate);
        Assert.Equal(To, report.ToDate);
    }

    [Fact]
    public void A_period_with_no_revenue_has_no_margins_and_can_make_a_loss()
    {
        ProfitAndLossTotalsDto totals = Report(ledger: [.. Journal(new(2026, 6, 15), 6001, 1001, 12_000m)]).Totals;

        Assert.Equal(-12_000m, totals.NetProfit);
        Assert.Null(totals.GrossMarginPercent);
        Assert.Null(totals.NetMarginPercent);
        Assert.All(Report(ledger: []).Sections, s => Assert.Empty(s.Lines));
    }

    [Theory]
    [InlineData(1, 3, 33.33)]
    [InlineData(2, 3, 66.67)]
    [InlineData(1, 800, 0.13)] // 0.125: half away from zero
    [InlineData(-1, 800, -0.13)]
    public void A_margin_is_a_percentage_of_revenue_to_two_places(decimal profit, decimal revenue, decimal expected) =>
        Assert.Equal(expected, ProfitAndLossReportRepository.MarginPercent(profit, revenue));

    [Fact]
    public void A_margin_without_revenue_is_null() =>
        Assert.Null(ProfitAndLossReportRepository.MarginPercent(5m, 0m));

    // ---- The classification ------------------------------------------------------

    [Fact]
    public void A_balance_sheet_type_has_no_section()
    {
        Assert.Null(ProfitAndLossReportRepository.SectionOf(Types[0], Account(1, 1, "1", "Cash")));
        Assert.Null(ProfitAndLossReportRepository.SectionOf(Types[2], Account(3, 3, "3", "Capital")));
    }

    [Fact]
    public void Business_performance_and_this_report_name_cost_of_sales_alike()
    {
        Assert.Equal(
            ProfitAndLossSection.CostOfSales,
            ProfitAndLossReportRepository.SectionOf(
                Types[4], Account(1, 5, "1", "Any name", systemName: BusinessPerformanceSourceRepository.CostOfGoodsSold)));
        Assert.Equal(
            ProfitAndLossSection.Expenses,
            ProfitAndLossReportRepository.SectionOf(Types[4], Account(2, 5, "2", "Cost of Goods Sold")));
    }

    // ---- The dates ---------------------------------------------------------------

    [Fact]
    public void A_period_and_a_comparison_period_in_order_are_accepted()
    {
        Assert.Empty(ProfitAndLossReportRepository.DateRefusals(new ProfitAndLossReportRequest
        {
            FromDate = From, ToDate = To, CompareFromDate = CompareFrom, CompareToDate = CompareTo,
        }));
        Assert.Empty(ProfitAndLossReportRepository.DateRefusals(new ProfitAndLossReportRequest { FromDate = To, ToDate = To }));
    }

    [Fact]
    public void A_to_date_before_the_from_date_is_refused_at_the_to_date()
    {
        var refusal = Assert.Single(ProfitAndLossReportRepository.DateRefusals(
            new ProfitAndLossReportRequest { FromDate = To, ToDate = From }));

        Assert.Equal(nameof(ProfitAndLossReportRequest.ToDate), refusal.Field);
    }

    [Fact]
    public void A_comparison_period_needs_both_dates_and_names_the_missing_one()
    {
        var noTo = Assert.Single(ProfitAndLossReportRepository.DateRefusals(
            new ProfitAndLossReportRequest { FromDate = From, ToDate = To, CompareFromDate = CompareFrom }));
        var noFrom = Assert.Single(ProfitAndLossReportRepository.DateRefusals(
            new ProfitAndLossReportRequest { FromDate = From, ToDate = To, CompareToDate = CompareTo }));

        Assert.Equal(nameof(ProfitAndLossReportRequest.CompareToDate), noTo.Field);
        Assert.Equal(nameof(ProfitAndLossReportRequest.CompareFromDate), noFrom.Field);
    }

    [Fact]
    public void A_comparison_period_out_of_order_is_refused_at_its_to_date()
    {
        var refusal = Assert.Single(ProfitAndLossReportRepository.DateRefusals(new ProfitAndLossReportRequest
        {
            FromDate = From, ToDate = To, CompareFromDate = CompareTo, CompareToDate = CompareFrom,
        }));

        Assert.Equal(nameof(ProfitAndLossReportRequest.CompareToDate), refusal.Field);
    }

    [Fact]
    public void The_from_and_to_dates_are_required_by_the_request_itself()
    {
        PropertyInfo[] required = [.. typeof(ProfitAndLossReportRequest).GetProperties()
            .Where(p => p.GetCustomAttribute<System.ComponentModel.DataAnnotations.RequiredAttribute>() is not null)];

        Assert.Equal(
            [nameof(ProfitAndLossReportRequest.FromDate), nameof(ProfitAndLossReportRequest.ToDate)],
            required.Select(p => p.Name));
    }

    // ---- The endpoint ------------------------------------------------------------

    [Fact]
    public async Task Another_branch_in_the_request_is_forbidden_not_hidden()
    {
        var controller = new ProfitAndLossReportController(null!)
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
            new ProfitAndLossReportRequest { FromDate = From, ToDate = To, OrgId = Guid.NewGuid() }, default);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public void The_endpoint_keeps_its_address_and_needs_reports_and_accounting_view()
    {
        Type controller = typeof(ProfitAndLossReportController);

        // The gateway routes this exact path to Reporting; the page calls it.
        Assert.Equal("api/statements/profit-and-loss", controller.GetCustomAttribute<RouteAttribute>()!.Template);
        Assert.Equal("reports", controller.GetCustomAttribute<RequireModulePermissionAttribute>()!.Module);
        Assert.Contains(
            "accounting.view",
            controller.GetMethod(nameof(ProfitAndLossReportController.Run))!
                .GetCustomAttributes<RequirePermissionAttribute>()
                .Select(a => a.Permission));
        Assert.Null(typeof(StatementsController).GetMethod("ProfitAndLoss"));
    }
}
