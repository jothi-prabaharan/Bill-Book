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
/// The Balance Sheet (TK-199), on a small ledger worked by hand across two
/// financial years (April to March). Every journal balances, so the sheet must
/// balance on every date; one test breaks that on purpose.
///
/// The ledger sums run on a list through the same expression the repository
/// runs against <c>acc.JournalLedger</c>; the composition is pure.
/// </summary>
public sealed class BalanceSheetReportTests
{
    private static readonly DateOnly AsOf = new(2026, 10, 31);
    private static readonly DateOnly YearStart = new(2026, 4, 1);
    private static readonly DateOnly Compare = new(2025, 10, 31);
    private static readonly DateOnly CompareYearStart = new(2025, 4, 1);

    private static readonly List<AccountTypeDto> Types =
    [
        new(1, "Asset", "Asset", "Debit", "BalanceSheet", 1),
        new(2, "Liability", "Liability", "Credit", "BalanceSheet", 2),
        new(3, "Equity", "Equity", "Credit", "BalanceSheet", 3),
        new(4, "Income", "Income", "Credit", "ProfitAndLoss", 4),
        new(5, "Expense", "Expense", "Debit", "ProfitAndLoss", 5),
    ];

    private static BalanceSheetAccount Account(long id, int type, string code, string name, bool contra = false, bool active = true) =>
        new() { AccountId = id, AccountTypeId = type, AccountCode = code, AccountName = name, IsContra = contra, IsActive = active };

    private static readonly List<BalanceSheetAccount> Accounts =
    [
        Account(1500, 1, "1500", "Fixed Asset"),
        Account(1001, 1, "1001", "Cash"),
        Account(1002, 1, "1002", "Inventory"),
        Account(1003, 1, "1003", "Petty Cash", active: false),
        Account(1510, 1, "1510", "Accumulated Depreciation", contra: true),
        Account(1999, 1, "1999", "Old Account", active: false),
        Account(2001, 2, "2001", "Accounts Payable"),
        Account(2002, 2, "2002", "GST Payable"),
        Account(3001, 3, "3001", "Capital"),
        Account(4001, 4, "4001", "Sales"),
        Account(5001, 5, "5001", "Cost of Goods Sold"),
        Account(5002, 5, "5002", "Depreciation"),
    ];

    private static long _leg;

    private static JournalLedgerRead Leg(DateOnly date, long account, decimal debit, decimal credit) =>
        new() { LedgerId = ++_leg, LedgerDate = date, AccountId = account, DebitAmountBase = debit, CreditAmountBase = credit };

    private static IEnumerable<JournalLedgerRead> Journal(DateOnly date, long debitAccount, long creditAccount, decimal amount) =>
        [Leg(date, debitAccount, amount, 0m), Leg(date, creditAccount, 0m, amount)];

    private static readonly List<JournalLedgerRead> Ledger =
    [
        .. Journal(new(2025, 1, 10), 1001, 3001, 100_000m), // capital paid in
        .. Journal(new(2025, 2, 1), 1500, 1001, 50_000m), // a fixed asset bought
        .. Journal(new(2025, 3, 15), 1001, 4001, 30_000m), // sales, 2024-25
        .. Journal(new(2025, 3, 20), 5001, 1001, 10_000m), // cost of sales, 2024-25
        .. Journal(new(2025, 9, 1), 1001, 4001, 40_000m), // sales, 2025-26
        .. Journal(new(2025, 9, 5), 1002, 2001, 15_000m), // stock on credit
        .. Journal(new(2026, 3, 31), 5002, 1510, 5_000m), // depreciation, 2025-26
        .. Journal(new(2026, 5, 1), 1001, 4001, 25_000m), // sales, 2026-27
        .. Journal(new(2026, 6, 1), 1003, 1001, 2_000m), // into petty cash, since closed
        .. Journal(new(2026, 11, 15), 1001, 4001, 9_999m), // after the as-at date
    ];

    private static List<AccountLedgerBalance> Sums(IEnumerable<JournalLedgerRead> ledger, DateOnly? compare = null) =>
    [
        .. BalanceSheetReportRepository.Balances(
            ledger.AsQueryable(), AsOf, compare, YearStart, compare is null ? null : CompareYearStart),
    ];

    private static BalanceSheetReportResultDto Sheet(
        DateOnly? compare = null, bool zeroes = false, IEnumerable<JournalLedgerRead>? ledger = null) =>
        BalanceSheetReportRepository.Compose(
            Accounts, Sums(ledger ?? Ledger, compare), Types,
            AsOf, compare, YearStart, compare is null ? null : CompareYearStart, zeroes);

    private static BalanceSheetSectionDto Section(BalanceSheetReportResultDto sheet, BalanceSheetSection section) =>
        sheet.Sections.Single(s => s.Section == section);

    private static BalanceSheetLineDto Line(BalanceSheetReportResultDto sheet, string name) =>
        sheet.Sections.SelectMany(s => s.Lines).Single(l => l.AccountName == name);

    // ---- The sums --------------------------------------------------------------

    [Fact]
    public void Each_account_is_summed_to_the_date_and_before_its_year_with_later_legs_left_out()
    {
        AccountLedgerBalance cash = Sums(Ledger).Single(b => b.AccountId == 1001);

        // 100,000 − 50,000 + 30,000 − 10,000 + 40,000 + 25,000 − 2,000; the 9,999 is after the date.
        Assert.Equal(133_000m, cash.Net);
        // Before 1 April 2026: everything but the 25,000 and the 2,000.
        Assert.Equal(110_000m, cash.NetBeforeYear);
    }

    [Fact]
    public void A_comparison_date_is_summed_in_the_same_pass()
    {
        AccountLedgerBalance cash = Sums(Ledger, Compare).Single(b => b.AccountId == 1001);

        Assert.Equal(133_000m, cash.Net);
        Assert.Equal(110_000m, cash.CompareNet);
        Assert.Equal(70_000m, cash.CompareNetBeforeYear);
    }

    // ---- The sections ----------------------------------------------------------

    [Fact]
    public void Each_balance_reads_positive_on_its_own_side_and_a_contra_account_negative()
    {
        BalanceSheetReportResultDto sheet = Sheet();

        Assert.Equal(133_000m, Line(sheet, "Cash").Balance);
        Assert.Equal(15_000m, Line(sheet, "Accounts Payable").Balance);
        Assert.Equal(100_000m, Line(sheet, "Capital").Balance);

        BalanceSheetLineDto depreciation = Line(sheet, "Accumulated Depreciation");
        Assert.Equal(-5_000m, depreciation.Balance);
        Assert.True(depreciation.IsContra);
    }

    [Fact]
    public void Lines_come_in_account_code_order_within_their_section()
    {
        Assert.Equal(
            ["1001", "1002", "1003", "1500", "1510"],
            Section(Sheet(), BalanceSheetSection.Assets).Lines.Select(l => l.AccountCode));
    }

    [Fact]
    public void Income_and_expense_accounts_are_not_listed_but_their_profit_is_carried_into_equity()
    {
        BalanceSheetReportResultDto sheet = Sheet();

        Assert.DoesNotContain(sheet.Sections.SelectMany(s => s.Lines), l => l.AccountName is "Sales" or "Depreciation");

        // Sales 95,000 less costs 15,000 to the date; 55,000 of it before 1 April 2026.
        BalanceSheetLineDto earlier = Line(sheet, "Profit of earlier years");
        BalanceSheetLineDto current = Line(sheet, "Current year earnings");
        Assert.Equal(55_000m, earlier.Balance);
        Assert.Equal(25_000m, current.Balance);
        Assert.True(earlier.IsComputed);
        Assert.Null(current.AccountId);
        Assert.Equal(current, Section(sheet, BalanceSheetSection.Equity).Lines[^1]);
    }

    [Fact]
    public void An_inactive_account_still_holding_a_balance_is_shown_and_an_empty_one_never_is()
    {
        Assert.Equal(2_000m, Line(Sheet(), "Petty Cash").Balance);
        Assert.DoesNotContain(Sheet(zeroes: true).Sections.SelectMany(s => s.Lines), l => l.AccountName == "Old Account");
    }

    [Fact]
    public void An_account_with_no_balance_is_shown_only_when_asked_for()
    {
        Assert.DoesNotContain(Sheet().Sections.SelectMany(s => s.Lines), l => l.AccountName == "GST Payable");
        Assert.Equal(0m, Line(Sheet(zeroes: true), "GST Payable").Balance);
    }

    // ---- The totals --------------------------------------------------------------

    [Fact]
    public void A_balanced_ledger_gives_assets_equal_to_liabilities_and_equity()
    {
        BalanceSheetTotalsDto totals = Sheet().Totals;

        Assert.Equal(195_000m, totals.TotalAssets);
        Assert.Equal(15_000m, totals.TotalLiabilities);
        Assert.Equal(180_000m, totals.TotalEquity);
        Assert.Equal(195_000m, totals.TotalLiabilitiesAndEquity);
        Assert.Equal(0m, totals.Difference);
        Assert.True(Sheet().IsBalanced);
    }

    [Fact]
    public void The_comparison_column_has_its_own_balances_profit_split_and_totals()
    {
        BalanceSheetReportResultDto sheet = Sheet(Compare);

        Assert.Equal(110_000m, Line(sheet, "Cash").CompareBalance);
        Assert.Equal(0m, Line(sheet, "Accumulated Depreciation").CompareBalance);
        Assert.Equal(20_000m, Line(sheet, "Profit of earlier years").CompareBalance);
        Assert.Equal(40_000m, Line(sheet, "Current year earnings").CompareBalance);

        Assert.Equal(175_000m, sheet.Totals.CompareTotalAssets);
        Assert.Equal(175_000m, sheet.Totals.CompareTotalLiabilitiesAndEquity);
        Assert.Equal(0m, sheet.Totals.CompareDifference);
        Assert.Equal(CompareYearStart, sheet.CompareFinancialYearStart);
        Assert.True(sheet.IsBalanced);
    }

    [Fact]
    public void Without_a_comparison_date_there_are_no_comparison_figures()
    {
        BalanceSheetReportResultDto sheet = Sheet();

        Assert.All(sheet.Sections.SelectMany(s => s.Lines), l => Assert.Null(l.CompareBalance));
        Assert.Null(sheet.Totals.CompareTotalAssets);
        Assert.Null(sheet.CompareAsOfDate);
    }

    [Fact]
    public void A_ledger_that_does_not_balance_says_so_with_the_difference()
    {
        BalanceSheetReportResultDto sheet = Sheet(ledger: [.. Ledger, Leg(new(2026, 7, 1), 1001, 100m, 0m)]);

        Assert.Equal(100m, sheet.Totals.Difference);
        Assert.False(sheet.IsBalanced);
    }

    [Fact]
    public void An_account_of_a_type_master_does_not_list_is_left_out()
    {
        BalanceSheetReportResultDto sheet = BalanceSheetReportRepository.Compose(
            [.. Accounts, Account(9001, 99, "9001", "Unknown type")], Sums(Ledger), Types,
            AsOf, null, YearStart, null, isZeroBalanceShown: true);

        Assert.DoesNotContain(sheet.Sections.SelectMany(s => s.Lines), l => l.AccountName == "Unknown type");
    }

    [Theory]
    [InlineData("Asset", "BalanceSheet", BalanceSheetSection.Assets)]
    [InlineData("Liability", "BalanceSheet", BalanceSheetSection.Liabilities)]
    [InlineData("Equity", "BalanceSheet", BalanceSheetSection.Equity)]
    public void Each_balance_sheet_type_has_its_section(string systemName, string reportSection, BalanceSheetSection expected) =>
        Assert.Equal(expected, BalanceSheetReportRepository.SectionOf(new(1, systemName, systemName, "Debit", reportSection, 1)));

    [Fact]
    public void An_income_or_expense_type_has_no_section() =>
        Assert.Null(BalanceSheetReportRepository.SectionOf(Types[3]));

    // ---- The endpoint --------------------------------------------------------------

    [Fact]
    public async Task Another_branch_in_the_request_is_forbidden_not_hidden()
    {
        var controller = new BalanceSheetReportController(null!)
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
            new BalanceSheetReportRequest { AsOfDate = AsOf, OrgId = Guid.NewGuid() }, default);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public void The_endpoint_keeps_its_address_and_needs_reports_and_accounting_view()
    {
        Type controller = typeof(BalanceSheetReportController);

        // The gateway routes this exact path to Reporting; the page calls it.
        Assert.Equal("api/statements/balance-sheet", controller.GetCustomAttribute<RouteAttribute>()!.Template);
        Assert.Equal("reports", controller.GetCustomAttribute<RequireModulePermissionAttribute>()!.Module);
        Assert.Contains(
            "accounting.view",
            controller.GetMethod(nameof(BalanceSheetReportController.Run))!
                .GetCustomAttributes<RequirePermissionAttribute>()
                .Select(a => a.Permission));
    }
}
