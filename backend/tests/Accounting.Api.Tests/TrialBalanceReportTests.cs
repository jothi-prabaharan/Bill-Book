using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Security.Claims;
using Accounting.Api.Controllers;
using Accounting.Entity.Models;
using Accounting.Entity.TableEntities;
using Accounting.Repository.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Shared.Kernel.Internal;
using Xunit;

namespace Accounting.Api.Tests;

/// <summary>
/// The Trial Balance's own page (TK-215), on a small set of books worked by
/// hand. The financial year starts on 1 April and the period is October 2026:
/// <list type="bullet">
/// <item>Before the financial year, in 2025-26: capital of 10,000 paid into the
/// bank; rent of 1,000 and sales of 3,000, so earlier years' profit is 2,000.</item>
/// <item>April to September 2026: sales of 5,000, rent of 2,000 and stationery of
/// 300 (an account since made inactive).</item>
/// <item>October 2026: sales of 7,000, returns of 400 and rent of 2,500.</item>
/// <item>November 2026, after the period: sales of 9,000, which no figure holds.</item>
/// </list>
/// The sums run over lists through the same expressions the repository runs
/// against the database; the composition is pure. Nothing here needs PostgreSQL.
/// </summary>
public sealed class TrialBalanceReportTests
{
    private const int AssetType = 1, EquityType = 3, IncomeType = 4, ExpenseType = 5;
    private const long Bank = 1001, PettyCash = 1002, Dormant = 1003, Capital = 3001, Sales = 4001, SalesReturns = 4002,
        Rent = 5001, OldStationery = 5002;

    private static readonly DateOnly October1 = new(2026, 10, 1);
    private static readonly DateOnly October31 = new(2026, 10, 31);
    private static readonly DateOnly YearStart = new(2026, 4, 1);

    private static TrialBalanceAccount Account(long id, int type, string name, bool contra = false, bool active = true) =>
        new() { AccountId = id, AccountTypeId = type, AccountCode = id.ToString(), AccountName = name, IsContra = contra, IsActive = active };

    private static readonly List<TrialBalanceAccount> Accounts =
    [
        Account(Bank, AssetType, "Bank"),
        Account(PettyCash, AssetType, "Petty Cash"),
        Account(Dormant, AssetType, "Dormant Deposit", active: false),
        Account(Capital, EquityType, "Capital"),
        Account(Sales, IncomeType, "Sales"),
        Account(SalesReturns, IncomeType, "Sales Returns", contra: true),
        Account(Rent, ExpenseType, "Rent"),
        Account(OldStationery, ExpenseType, "Old Stationery", active: false),
    ];

    private static long _id;

    private static JournalLedger Leg(DateOnly on, long account, decimal debit, decimal credit) =>
        new()
        {
            LedgerId = ++_id, LedgerDate = on, AccountId = account, DebitAmountBase = debit, CreditAmountBase = credit,
            TransactionTypeCode = "JRN", CurrencyCode = "INR",
        };

    /// <summary>A balanced posting: one account debited and another credited.</summary>
    private static IEnumerable<JournalLedger> Post(DateOnly on, long debit, long credit, decimal amount) =>
        [Leg(on, debit, amount, 0m), Leg(on, credit, 0m, amount)];

    private static readonly List<JournalLedger> Ledger =
    [
        // Before the financial year.
        .. Post(new(2026, 1, 10), Bank, Capital, 10_000m),
        .. Post(new(2026, 2, 15), Rent, Bank, 1_000m),
        .. Post(new(2026, 2, 20), Bank, Sales, 3_000m),

        // April to September.
        .. Post(new(2026, 6, 10), Bank, Sales, 5_000m),
        .. Post(new(2026, 7, 5), Rent, Bank, 2_000m),
        .. Post(new(2026, 8, 1), OldStationery, Bank, 300m),

        // October.
        .. Post(new(2026, 10, 3), Bank, Sales, 7_000m),
        .. Post(new(2026, 10, 12), SalesReturns, Bank, 400m),
        .. Post(new(2026, 10, 20), Rent, Bank, 2_500m),

        // November, after the period.
        .. Post(new(2026, 11, 5), Bank, Sales, 9_000m),
    ];

    private static TrialBalanceReportResultDto Report(
        DateOnly from, DateOnly to, DateOnly? compareFrom = null, DateOnly? compareTo = null,
        bool zeroShown = false, IReadOnlyList<JournalLedger>? ledger = null)
    {
        DateOnly yearStart = TrialBalanceReportRepository.FinancialYearStart(from, 4);
        DateOnly? compareYearStart = compareFrom is DateOnly c ? TrialBalanceReportRepository.FinancialYearStart(c, 4) : null;

        return TrialBalanceReportRepository.Compose(
            Accounts,
            [.. TrialBalanceReportRepository.Sums((ledger ?? Ledger).AsQueryable(), from, to, yearStart, compareFrom, compareTo, compareYearStart)],
            from, to, yearStart, compareFrom, compareTo, compareYearStart, zeroShown);
    }

    private static TrialBalanceReportLineDto Line(TrialBalanceReportResultDto report, long accountId) =>
        report.Groups.SelectMany(g => g.Lines).Single(l => l.AccountId == accountId);

    private static (decimal, decimal, decimal, decimal, decimal, decimal) Figures(TrialBalanceReportLineDto l) =>
        (l.OpeningDebit, l.OpeningCredit, l.MovementDebit, l.MovementCredit, l.ClosingDebit, l.ClosingCredit);

    /// <summary>The compared period's closing balance, debit then credit; a line with no comparison reads as nought.</summary>
    private static (decimal, decimal) Compared(TrialBalanceReportResultDto report, long accountId)
    {
        TrialBalanceReportLineDto line = Line(report, accountId);
        return (line.CompareClosingDebit ?? 0m, line.CompareClosingCredit ?? 0m);
    }

    // ---- Each account, worked by hand -------------------------------------------------

    [Fact]
    public void A_balance_sheet_account_opens_at_everything_posted_before_the_period()
    {
        TrialBalanceReportResultDto report = Report(October1, October31);

        // Bank: 10,000 - 1,000 + 3,000 + 5,000 - 2,000 - 300 = 14,700 before October.
        // October: 7,000 in, 400 + 2,500 = 2,900 out. Closes at 18,800. The 9,000 in November is not in it.
        Assert.Equal((14_700m, 0m, 7_000m, 2_900m, 18_800m, 0m), Figures(Line(report, Bank)));

        // Capital is a credit and nothing moved in October.
        Assert.Equal((0m, 10_000m, 0m, 0m, 0m, 10_000m), Figures(Line(report, Capital)));
    }

    [Fact]
    public void An_income_or_expense_account_opens_at_what_was_posted_since_the_financial_year_began()
    {
        TrialBalanceReportResultDto report = Report(October1, October31);

        // Sales: 5,000 since 1 April (the 3,000 of 2025-26 is earlier years' profit),
        // 7,000 in October, so it closes at 12,000 credit.
        Assert.Equal((0m, 5_000m, 0m, 7_000m, 0m, 12_000m), Figures(Line(report, Sales)));

        // Rent: 2,000 since 1 April, 2,500 in October.
        Assert.Equal((2_000m, 0m, 2_500m, 0m, 4_500m, 0m), Figures(Line(report, Rent)));

        // Returns have a debit balance though they are income: the account's own side.
        Assert.Equal((0m, 0m, 400m, 0m, 400m, 0m), Figures(Line(report, SalesReturns)));
        Assert.True(Line(report, SalesReturns).IsContra);
    }

    [Fact]
    public void Earlier_years_profit_is_carried_into_equity_as_one_worked_out_line()
    {
        TrialBalanceReportResultDto report = Report(October1, October31);

        // Sales 3,000 less rent 1,000 before 1 April 2026: a profit of 2,000, a credit.
        TrialBalanceReportLineDto carried = report.Groups.Single(g => g.AccountTypeId == EquityType).Lines.Last();

        Assert.True(carried.IsComputed);
        Assert.Equal(0, carried.AccountId);
        Assert.Equal(TrialBalanceReportRepository.EarlierYearsProfit, carried.AccountName);
        Assert.Equal((0m, 2_000m, 0m, 0m, 0m, 2_000m), Figures(carried));
    }

    [Fact]
    public void An_inactive_account_that_holds_a_balance_is_listed()
    {
        TrialBalanceReportResultDto report = Report(October1, October31);

        // 300 of stationery was posted in August, and the account has since been closed.
        Assert.Equal((300m, 0m, 0m, 0m, 300m, 0m), Figures(Line(report, OldStationery)));
    }

    // ---- The totals that must agree -------------------------------------------------

    [Fact]
    public void Every_pair_of_columns_agrees_on_books_that_balance()
    {
        TrialBalanceReportResultDto report = Report(October1, October31);

        // Opening: bank 14,700 + rent 2,000 + stationery 300 against capital 10,000, sales 5,000 and the 2,000 carried.
        Assert.Equal(17_000m, report.Totals.OpeningDebit);
        Assert.Equal(17_000m, report.Totals.OpeningCredit);

        // Movement: 7,000 + 400 + 2,500 debited, 7,000 + 2,900 credited.
        Assert.Equal(9_900m, report.Totals.MovementDebit);
        Assert.Equal(9_900m, report.Totals.MovementCredit);

        // Closing: bank 18,800 + returns 400 + rent 4,500 + stationery 300; capital 10,000 + sales 12,000 + 2,000.
        Assert.Equal(24_000m, report.Totals.ClosingDebit);
        Assert.Equal(24_000m, report.Totals.ClosingCredit);

        Assert.Equal((0m, 0m, 0m), (report.OpeningDifference, report.MovementDifference, report.ClosingDifference));
        Assert.True(report.IsBalanced);
    }

    [Fact]
    public void A_posting_that_does_not_balance_is_reported_out_by_what_it_is_out()
    {
        // A debit of 100 with no credit against it, in October.
        List<JournalLedger> broken = [.. Ledger, Leg(new(2026, 10, 15), Rent, 100m, 0m)];

        TrialBalanceReportResultDto report = Report(October1, October31, ledger: broken);

        Assert.False(report.IsBalanced);
        Assert.Equal(0m, report.OpeningDifference);
        Assert.Equal(100m, report.MovementDifference);
        Assert.Equal(100m, report.ClosingDifference);
    }

    [Fact]
    public void Each_account_type_has_its_accounts_in_code_order_and_a_subtotal()
    {
        TrialBalanceReportResultDto report = Report(October1, October31);

        Assert.Equal([AssetType, EquityType, IncomeType, ExpenseType], report.Groups.Select(g => g.AccountTypeId));
        Assert.Equal([Sales, SalesReturns], report.Groups.Single(g => g.AccountTypeId == IncomeType).Lines.Select(l => l.AccountId));

        // Equity is the account, then the worked-out line last.
        Assert.Equal([Capital, 0L], report.Groups.Single(g => g.AccountTypeId == EquityType).Lines.Select(l => l.AccountId));

        TrialBalanceReportTotalsDto income = report.Groups.Single(g => g.AccountTypeId == IncomeType).Totals;
        Assert.Equal((0m, 5_000m, 400m, 7_000m, 400m, 12_000m),
            (income.OpeningDebit, income.OpeningCredit, income.MovementDebit, income.MovementCredit, income.ClosingDebit, income.ClosingCredit));

        TrialBalanceReportTotalsDto expenses = report.Groups.Single(g => g.AccountTypeId == ExpenseType).Totals;
        Assert.Equal((2_300m, 0m, 2_500m, 0m, 4_800m, 0m),
            (expenses.OpeningDebit, expenses.OpeningCredit, expenses.MovementDebit, expenses.MovementCredit, expenses.ClosingDebit, expenses.ClosingCredit));
    }

    // ---- Which accounts are listed --------------------------------------------------

    [Fact]
    public void An_account_with_nothing_posted_is_left_out_unless_zero_balances_are_asked_for()
    {
        Assert.DoesNotContain(Report(October1, October31).Groups.SelectMany(g => g.Lines), l => l.AccountId == PettyCash);

        TrialBalanceReportResultDto all = Report(October1, October31, zeroShown: true);

        Assert.Equal((0m, 0m, 0m, 0m, 0m, 0m), Figures(Line(all, PettyCash)));

        // An inactive account with nothing in it stays off even then: it cannot be posted to.
        Assert.DoesNotContain(all.Groups.SelectMany(g => g.Lines), l => l.AccountId == Dormant);
    }

    [Fact]
    public void An_account_that_moved_and_netted_to_nothing_is_still_listed()
    {
        List<JournalLedger> ledger = [.. Post(new(2026, 10, 2), PettyCash, Sales, 50m), .. Post(new(2026, 10, 9), Sales, PettyCash, 50m)];

        TrialBalanceReportResultDto report = Report(October1, October31, ledger: ledger);

        // 50 in and 50 out: closing nothing, movement not.
        Assert.Equal((0m, 0m, 50m, 50m, 0m, 0m), Figures(Line(report, PettyCash)));
    }

    // ---- The period -----------------------------------------------------------------

    [Fact]
    public void A_period_beginning_with_the_financial_year_has_no_income_or_expense_opening()
    {
        TrialBalanceReportResultDto report = Report(YearStart, new DateOnly(2026, 9, 30));

        Assert.Equal((0m, 0m, 0m, 5_000m, 0m, 5_000m), Figures(Line(report, Sales)));
        Assert.Equal((0m, 0m, 2_000m, 0m, 2_000m, 0m), Figures(Line(report, Rent)));

        // The bank opens at what 2025-26 left, 12,000, and the carried profit is still 2,000.
        Assert.Equal(12_000m, Line(report, Bank).OpeningDebit);
        Assert.Equal(2_000m, Line(report, 0).OpeningCredit);
        Assert.True(report.IsBalanced);
    }

    [Fact]
    public void A_period_in_an_earlier_year_has_nothing_carried_from_before_it()
    {
        // March 2026, the last month of the financial year before: its year began on 1 April 2025.
        TrialBalanceReportResultDto report = Report(new(2026, 3, 1), new(2026, 3, 31));

        Assert.DoesNotContain(report.Groups.SelectMany(g => g.Lines), l => l.IsComputed);
        Assert.Equal(new DateOnly(2025, 4, 1), report.FinancialYearStart);

        // Sales and rent opened in the year's own postings: 3,000 and 1,000 are this year's.
        Assert.Equal((0m, 3_000m, 0m, 0m, 0m, 3_000m), Figures(Line(report, Sales)));
        Assert.Equal((1_000m, 0m, 0m, 0m, 1_000m, 0m), Figures(Line(report, Rent)));
        Assert.True(report.IsBalanced);
    }

    [Fact]
    public void The_columns_agree_for_every_month_of_the_books()
    {
        for (int month = 1; month <= 12; month++)
        {
            DateOnly from = new(2026, month, 1);
            TrialBalanceReportResultDto report = Report(from, from.AddMonths(1).AddDays(-1));

            Assert.True(report.IsBalanced, $"Month {month} is out of balance.");
        }
    }

    [Fact]
    public void The_closing_balance_of_a_balance_sheet_account_does_not_depend_on_where_the_period_started()
    {
        // Bank, as at 31 October, from the start of the year and from the start of the month.
        decimal fromYearStart = Line(Report(YearStart, October31), Bank).ClosingDebit;
        decimal fromMonthStart = Line(Report(October1, October31), Bank).ClosingDebit;

        Assert.Equal(18_800m, fromYearStart);
        Assert.Equal(fromYearStart, fromMonthStart);
    }

    // ---- The second period ----------------------------------------------------------

    [Fact]
    public void A_second_period_shows_its_closing_balances_beside_the_first()
    {
        // September 2026: closing at 30 September.
        TrialBalanceReportResultDto report = Report(October1, October31, new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30));

        Assert.Equal((14_700m, 0m), Compared(report, Bank));
        Assert.Equal((0m, 5_000m), Compared(report, Sales));
        Assert.Equal((0m, 2_000m), Compared(report, 0));

        // Returns were posted in October, so nothing yet at the end of September.
        Assert.Equal((0m, 0m), Compared(report, SalesReturns));
        Assert.NotNull(Line(report, SalesReturns).CompareClosingDebit);

        Assert.Equal(17_000m, report.Totals.CompareClosingDebit);
        Assert.Equal(17_000m, report.Totals.CompareClosingCredit);
        Assert.Equal(0m, report.CompareClosingDifference);
        Assert.True(report.IsBalanced);
    }

    [Fact]
    public void A_second_period_has_its_own_financial_year()
    {
        // March 2026 sits in 2025-26, so the 3,000 of sales is its year's, not carried: no profit carried for it.
        TrialBalanceReportResultDto report = Report(October1, October31, new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31));

        Assert.Equal(new DateOnly(2025, 4, 1), report.CompareFinancialYearStart);
        Assert.Equal((12_000m, 0m), Compared(report, Bank));
        Assert.Equal((0m, 3_000m), Compared(report, Sales));
        Assert.Equal((1_000m, 0m), Compared(report, Rent));
        Assert.Equal((0m, 0m), Compared(report, 0));
        Assert.Equal(13_000m, report.Totals.CompareClosingDebit);
        Assert.Equal(13_000m, report.Totals.CompareClosingCredit);
    }

    [Fact]
    public void Without_a_second_period_there_is_nothing_in_the_comparison_columns()
    {
        TrialBalanceReportResultDto report = Report(October1, October31);

        Assert.All(report.Groups.SelectMany(g => g.Lines), l => Assert.Null(l.CompareClosingDebit));
        Assert.Null(report.Totals.CompareClosingDebit);
        Assert.Null(report.CompareClosingDifference);
        Assert.Null(report.CompareFinancialYearStart);
    }

    // ---- The sums, one pass over the ledger -------------------------------------------

    [Fact]
    public void The_sums_split_before_the_period_before_the_year_and_the_period_itself()
    {
        List<AccountPeriodSums> sums = [.. TrialBalanceReportRepository.Sums(
            Ledger.AsQueryable(), October1, October31, YearStart, null, null, null)];

        AccountPeriodSums rent = sums.Single(s => s.AccountId == Rent);

        // 1,000 + 2,000 before October, 1,000 before the year, 2,500 in October.
        Assert.Equal((3_000m, 1_000m, 2_500m, 0m), (rent.NetBefore, rent.NetBeforeYear, rent.MovementDebit, rent.MovementCredit));

        AccountPeriodSums sales = sums.Single(s => s.AccountId == Sales);
        Assert.Equal((-8_000m, -3_000m, 0m, 7_000m), (sales.NetBefore, sales.NetBeforeYear, sales.MovementDebit, sales.MovementCredit));
    }

    [Fact]
    public void Postings_after_the_later_period_are_not_read_at_all()
    {
        // The November sale is the only difference between these two ledgers, and no sum changes.
        List<JournalLedger> withoutNovember = [.. Ledger.Where(l => l.LedgerDate < new DateOnly(2026, 11, 1))];

        static List<(long, decimal, decimal, decimal, decimal)> Of(IReadOnlyList<JournalLedger> ledger) =>
        [
            .. TrialBalanceReportRepository.Sums(ledger.AsQueryable(), October1, October31, YearStart, null, null, null)
                .OrderBy(s => s.AccountId)
                .Select(s => ValueTuple.Create(s.AccountId, s.NetBefore, s.NetBeforeYear, s.MovementDebit, s.MovementCredit)),
        ];

        Assert.Equal(Of(withoutNovember), Of(Ledger));
        Assert.Equal(7_000m, TrialBalanceReportRepository
            .Sums(Ledger.AsQueryable(), October1, October31, YearStart, null, null, null)
            .Single(s => s.AccountId == Sales).MovementCredit);
    }

    [Theory]
    [InlineData(2026, 10, 22, 4, 2026, 4, 1)]
    [InlineData(2026, 4, 1, 4, 2026, 4, 1)]
    [InlineData(2026, 3, 31, 4, 2025, 4, 1)]
    [InlineData(2026, 1, 1, 1, 2026, 1, 1)]
    [InlineData(2026, 6, 15, 7, 2025, 7, 1)]
    [InlineData(2026, 6, 15, 0, 2026, 4, 1)]
    public void The_financial_year_starts_on_the_branchs_month(
        int year, int month, int day, int startMonth, int startYear, int expectedMonth, int expectedDay) =>
        Assert.Equal(new DateOnly(startYear, expectedMonth, expectedDay),
            TrialBalanceReportRepository.FinancialYearStart(new DateOnly(year, month, day), startMonth));

    // ---- The request and the guards -------------------------------------------------

    [Fact]
    public void The_two_dates_are_required_and_nothing_else_is()
    {
        Assert.Equal(
            [nameof(TrialBalanceReportRequest.From), nameof(TrialBalanceReportRequest.To)],
            typeof(TrialBalanceReportRequest).GetProperties()
                .Where(p => p.GetCustomAttribute<RequiredAttribute>() is not null)
                .Select(p => p.Name));
    }

    [Fact]
    public void A_missing_date_is_refused_at_its_field_by_the_request_itself()
    {
        var request = new TrialBalanceReportRequest();
        var results = new List<ValidationResult>();

        Assert.False(Validator.TryValidateObject(request, new ValidationContext(request), results, validateAllProperties: true));
        Assert.Equal(
            ["From date is required.", "To date is required."],
            results.Select(r => r.ErrorMessage).OrderBy(m => m, StringComparer.Ordinal));
    }

    [Fact]
    public void A_period_that_ends_before_it_begins_is_refused_at_the_to_date()
    {
        var refusals = TrialBalanceReportRepository.DateRefusals(
            new TrialBalanceReportRequest { From = October31, To = October1 });

        Assert.Equal([(nameof(TrialBalanceReportRequest.To), "To date cannot be before the from date.")], refusals);
    }

    [Fact]
    public void The_comparison_is_both_dates_or_neither_and_in_order()
    {
        Assert.Empty(TrialBalanceReportRepository.DateRefusals(
            new TrialBalanceReportRequest { From = October1, To = October31 }));

        Assert.Equal(
            [(nameof(TrialBalanceReportRequest.CompareFrom), "Give both comparison dates, or neither.")],
            TrialBalanceReportRepository.DateRefusals(
                new TrialBalanceReportRequest { From = October1, To = October31, CompareTo = new DateOnly(2026, 9, 30) }));

        Assert.Equal(
            [(nameof(TrialBalanceReportRequest.CompareTo), "Give both comparison dates, or neither.")],
            TrialBalanceReportRepository.DateRefusals(
                new TrialBalanceReportRequest { From = October1, To = October31, CompareFrom = new DateOnly(2026, 9, 1) }));

        Assert.Equal(
            [(nameof(TrialBalanceReportRequest.CompareTo), "Comparison to date cannot be before its from date.")],
            TrialBalanceReportRepository.DateRefusals(new TrialBalanceReportRequest
            {
                From = October1, To = October31, CompareFrom = new DateOnly(2026, 9, 30), CompareTo = new DateOnly(2026, 9, 1),
            }));
    }

    [Fact]
    public async Task Another_branch_in_the_request_is_forbidden_not_hidden()
    {
        var controller = new LedgerController(null!, null!)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("org_id", Guid.NewGuid().ToString())], "test")),
                },
            },
        };

        IActionResult result = await controller.TrialBalance(
            new TrialBalanceReportRequest { From = October1, To = October31, OrgId = Guid.NewGuid() }, default);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public void The_report_and_the_chart_of_accounts_feed_are_at_their_own_addresses_under_the_ledger_guards()
    {
        Type controller = typeof(LedgerController);

        Assert.Equal("api/ledger", controller.GetCustomAttribute<RouteAttribute>()!.Template);
        Assert.Equal("accounting", controller.GetCustomAttribute<RequireModulePermissionAttribute>()!.Module);

        MethodInfo report = controller.GetMethod(nameof(LedgerController.TrialBalance))!;
        MethodInfo balances = controller.GetMethod(nameof(LedgerController.AccountBalances))!;

        Assert.Equal("trial-balance", report.GetCustomAttribute<HttpGetAttribute>()!.Template);
        Assert.Equal("account-balances", balances.GetCustomAttribute<HttpGetAttribute>()!.Template);

        // The chart of accounts' feed keeps its optional dates; the report's request takes a model with required ones.
        Assert.Equal(typeof(TrialBalanceReportRequest), report.GetParameters()[0].ParameterType);
        Assert.All(balances.GetParameters().Take(2), p => Assert.Equal(typeof(DateOnly?), p.ParameterType));
    }
}
