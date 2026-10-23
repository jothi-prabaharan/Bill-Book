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
using Shared.Kernel.Internal;
using Xunit;

namespace Reporting.Api.Tests;

/// <summary>
/// The Bank Summary's own page (TK-219), on books worked by hand. The branch's base
/// currency is INR and the period is October 2026:
/// <list type="bullet">
/// <item><b>HDFC Current</b> held 7,000 (10,000 in, 2,000 out, 1,000 transferred to
/// petty cash), then received 5,000, paid 600 and transferred 2,000 to petty cash.</item>
/// <item><b>Petty Cash</b> held 1,000, received those 2,000 and spent 150.</item>
/// <item><b>USD Account</b> held USD 100 (8,300 when received, 8,400 after a
/// revaluation of 100 in August), then received USD 50 (4,200), spent USD 20 (1,680)
/// and was revalued by 130 on 31 October, so it holds USD 130 and 11,050.</item>
/// <item><b>Old Savings</b>, switched off, still holds 500; <b>Closed Account</b>,
/// switched off with nothing; <b>Idle Active</b>, switched on with nothing; and
/// <b>HDFC Card</b>, a credit card liability, on which 300 was spent.</item>
/// <item>A receipt of 99 on 5 November, after the period.</item>
/// </list>
/// The builders run on lists through the same expressions the repository runs against
/// the read models; the composition is pure. Nothing here needs PostgreSQL.
/// </summary>
public sealed class BankSummaryReportTests
{
    private const long Hdfc = 1001, Petty = 1002, Usd = 1003, OldSavings = 1004, Closed = 1005, Idle = 1006, Card = 1007;
    private const long Sales = 4001, Rent = 5001, UnrealizedFx = 4910;
    private const long HdfcBank = 1, PettyBank = 2, UsdBank = 3, OldBank = 4, ClosedBank = 5, IdleBank = 6, CardBank = 7;

    private static readonly DateOnly October1 = new(2026, 10, 1);
    private static readonly DateOnly October31 = new(2026, 10, 31);

    private static BankAccountRead Bank(long id, string name, BankAccountType type, string currency, long ledger, bool active = true) =>
        new() { BankAccountId = id, AccountName = name, AccountNumber = $"NO-{id}", AccountType = type, CurrencyCode = currency, LedgerAccountId = ledger, IsActive = active };

    private static readonly List<BankAccountRead> Banks =
    [
        Bank(HdfcBank, "HDFC Current", BankAccountType.Current, "INR", Hdfc),
        Bank(PettyBank, "Petty Cash", BankAccountType.Cash, "INR", Petty),
        Bank(UsdBank, "USD Account", BankAccountType.EEFC, "USD", Usd),
        Bank(OldBank, "Old Savings", BankAccountType.Savings, "INR", OldSavings, active: false),
        Bank(ClosedBank, "Closed Account", BankAccountType.Current, "INR", Closed, active: false),
        Bank(IdleBank, "Idle Active", BankAccountType.Current, "INR", Idle),
        Bank(CardBank, "HDFC Card", BankAccountType.CreditCard, "INR", Card),
    ];

    private static AccountRead Account(long id, string name, string? system = null) =>
        new() { AccountId = id, AccountCode = id.ToString(), AccountName = name, AccountSystemName = system, AccountTypeId = 1, IsActive = true };

    private static readonly List<AccountRead> Accounts =
    [
        Account(Hdfc, "HDFC Current"), Account(Petty, "Petty Cash"), Account(Usd, "USD Account"), Account(OldSavings, "Old Savings"),
        Account(Closed, "Closed Account"), Account(Idle, "Idle Active"), Account(Card, "HDFC Card"),
        Account(Sales, "Sales"), Account(Rent, "Rent"),
        Account(UnrealizedFx, "Unrealized FX Gain/Loss", BankSummaryReportRepository.UnrealizedFxAccountName),
    ];

    private static long _id;

    private static JournalLedgerRead Leg(
        string type, long transactionId, DateOnly on, long account, decimal debit, decimal credit,
        string currency = "INR", decimal rate = 1m, decimal? sourceDebit = null, decimal? sourceCredit = null) =>
        new()
        {
            LedgerId = ++_id, LedgerDate = on, AccountId = account, TransactionTypeCode = type, TransactionId = transactionId,
            TransactionDetailId = 1, DebitAmountBase = debit, CreditAmountBase = credit,
            DebitAmount = sourceDebit ?? debit, CreditAmount = sourceCredit ?? credit, CurrencyCode = currency, ExchangeRate = rate,
        };

    private static readonly List<JournalLedgerRead> Ledger =
    [
        // Before the period.
        Leg("RCM", 1, new(2026, 6, 10), Hdfc, 10_000m, 0m), Leg("RCM", 1, new(2026, 6, 10), Sales, 0m, 10_000m),
        Leg("SPM", 3, new(2026, 6, 20), Rent, 2_000m, 0m), Leg("SPM", 3, new(2026, 6, 20), Hdfc, 0m, 2_000m),
        Leg("TRM", 1, new(2026, 6, 25), Petty, 1_000m, 0m), Leg("TRM", 1, new(2026, 6, 25), Hdfc, 0m, 1_000m),
        Leg("RCM", 2, new(2026, 7, 1), OldSavings, 500m, 0m), Leg("RCM", 2, new(2026, 7, 1), Sales, 0m, 500m),
        Leg("RCM", 3, new(2026, 5, 5), Usd, 8_300m, 0m, "USD", 83m, 100m, 0m), Leg("RCM", 3, new(2026, 5, 5), Sales, 0m, 8_300m),
        // A revaluation in August: the account is worth 100 more in base currency, in USD it holds the same.
        Leg("JRN", 5, new(2026, 8, 31), Usd, 100m, 0m), Leg("JRN", 5, new(2026, 8, 31), UnrealizedFx, 0m, 100m),

        // October.
        Leg("RCM", 10, new(2026, 10, 3), Hdfc, 5_000m, 0m), Leg("RCM", 10, new(2026, 10, 3), Sales, 0m, 5_000m),
        Leg("SPM", 4, new(2026, 10, 5), Rent, 600m, 0m), Leg("SPM", 4, new(2026, 10, 5), Hdfc, 0m, 600m),
        Leg("TRM", 2, new(2026, 10, 8), Petty, 2_000m, 0m), Leg("TRM", 2, new(2026, 10, 8), Hdfc, 0m, 2_000m),
        Leg("SPM", 5, new(2026, 10, 10), Rent, 150m, 0m), Leg("SPM", 5, new(2026, 10, 10), Petty, 0m, 150m),
        Leg("RCM", 11, new(2026, 10, 12), Usd, 4_200m, 0m, "USD", 84m, 50m, 0m), Leg("RCM", 11, new(2026, 10, 12), Sales, 0m, 4_200m),
        Leg("SPM", 7, new(2026, 10, 15), Rent, 300m, 0m), Leg("SPM", 7, new(2026, 10, 15), Card, 0m, 300m),
        Leg("SPM", 6, new(2026, 10, 20), Rent, 1_680m, 0m), Leg("SPM", 6, new(2026, 10, 20), Usd, 0m, 1_680m, "USD", 84m, 0m, 20m),
        // The month-end revaluation: USD 130 at 85 is 11,050, and the books had 10,920.
        Leg("JRN", 9, new(2026, 10, 31), Usd, 130m, 0m), Leg("JRN", 9, new(2026, 10, 31), UnrealizedFx, 0m, 130m),

        // After the period.
        Leg("RCM", 12, new(2026, 11, 5), Hdfc, 99m, 0m), Leg("RCM", 12, new(2026, 11, 5), Sales, 0m, 99m),
    ];

    /// <summary>The summary, through the same builders and composition the repository uses.</summary>
    private static BankSummaryReportResultDto Run(
        DateOnly? from = null, DateOnly? to = null, long? bank = null, string baseCurrency = "INR")
    {
        DateOnly start = from ?? October1, end = to ?? October31;
        IQueryable<BankAccountRead> banks = Banks.AsQueryable();
        if (bank is long chosen)
        {
            banks = banks.Where(b => b.BankAccountId == chosen);
        }

        List<BankSum> all = [.. BankSummaryReportRepository.Sums(Ledger.AsQueryable(), banks, start, end)];
        List<BankSum> revaluation = [.. BankSummaryReportRepository.Sums(BankSummaryReportRepository.RevaluationLegs(Ledger.AsQueryable(), Accounts.AsQueryable()), banks, start, end)];

        return BankSummaryReportRepository.Compose(start, end, baseCurrency, [.. banks], all, revaluation, bank);
    }

    private static BankSummaryAccountDto Row(BankSummaryReportResultDto report, long bankAccountId) =>
        report.Accounts.Single(a => a.BankAccountId == bankAccountId);

    // ---- Which accounts, in what order -----------------------------------------------

    [Fact]
    public void Active_accounts_come_first_by_name_then_a_switched_off_one_that_still_holds_a_balance()
    {
        BankSummaryReportResultDto report = Run();

        Assert.Equal(
            [CardBank, HdfcBank, IdleBank, PettyBank, UsdBank, OldBank],
            report.Accounts.Select(a => a.BankAccountId));
    }

    [Fact]
    public void A_switched_off_account_with_nothing_to_show_is_left_out_and_one_asked_for_by_name_is_not()
    {
        Assert.DoesNotContain(Run().Accounts, a => a.BankAccountId == ClosedBank);

        BankSummaryAccountDto closed = Assert.Single(Run(bank: ClosedBank).Accounts);
        Assert.Equal((0m, 0m, 0m, 0m, 0m), (closed.OpeningBalance, closed.Received, closed.Spent, closed.Revaluation, closed.ClosingBalance));
    }

    [Fact]
    public void A_switched_on_account_with_nothing_posted_is_listed_at_nothing()
    {
        BankSummaryAccountDto idle = Row(Run(), IdleBank);

        Assert.Equal((0m, 0m, 0m, 0m, 0m), (idle.OpeningBalance, idle.Received, idle.Spent, idle.Revaluation, idle.ClosingBalance));
    }

    [Fact]
    public void A_switched_off_account_that_holds_a_balance_is_listed_with_it()
    {
        BankSummaryAccountDto old = Row(Run(), OldBank);

        Assert.Equal((500m, 0m, 0m, 0m, 500m), (old.OpeningBalance, old.Received, old.Spent, old.Revaluation, old.ClosingBalance));
        Assert.False(old.IsActive);
        Assert.Equal(BankAccountType.Savings, old.AccountType);
    }

    // ---- The figures ------------------------------------------------------------------

    [Fact]
    public void An_account_is_what_it_held_what_came_in_and_went_out_and_what_it_holds()
    {
        BankSummaryAccountDto hdfc = Row(Run(), HdfcBank);

        // 10,000 − 2,000 − 1,000 before; 5,000 in; 600 and the 2,000 transferred out.
        Assert.Equal((7_000m, 5_000m, 2_600m, 0m, 9_400m), (hdfc.OpeningBalance, hdfc.Received, hdfc.Spent, hdfc.Revaluation, hdfc.ClosingBalance));
        Assert.Equal((BankAccountType.Current, "INR", false), (hdfc.AccountType, hdfc.CurrencyCode, hdfc.IsForeign));
    }

    [Fact]
    public void A_transfer_is_spent_from_one_account_and_received_by_the_other()
    {
        BankSummaryReportResultDto report = Run();

        // The 2,000 moved on 8 October: it is in what petty cash received and, with the 600 paid, in the 2,600 HDFC spent.
        BankSummaryAccountDto petty = Row(report, PettyBank);
        Assert.Equal((1_000m, 2_000m, 150m, 2_850m), (petty.OpeningBalance, petty.Received, petty.Spent, petty.ClosingBalance));
        Assert.Equal(2_600m, Row(report, HdfcBank).Spent);
    }

    [Fact]
    public void A_revaluation_has_its_own_column_and_is_not_money_received_or_spent()
    {
        BankSummaryAccountDto usd = Row(Run(), UsdBank);

        // Opening takes August's revaluation of 100; received and spent leave out October's 130.
        Assert.Equal((8_400m, 4_200m, 1_680m, 130m, 11_050m), (usd.OpeningBalance, usd.Received, usd.Spent, usd.Revaluation, usd.ClosingBalance));
    }

    [Fact]
    public void A_foreign_account_also_has_its_own_currency_figures_without_the_revaluation()
    {
        BankSummaryAccountDto usd = Row(Run(), UsdBank);

        Assert.True(usd.IsForeign);
        Assert.Equal((100m, 50m, 20m, 130m), (usd.OwnOpeningBalance, usd.OwnReceived, usd.OwnSpent, usd.OwnClosingBalance));
    }

    [Fact]
    public void An_account_in_the_base_currency_has_no_own_currency_figures()
    {
        BankSummaryAccountDto hdfc = Row(Run(), HdfcBank);

        Assert.Null(hdfc.OwnOpeningBalance);
        Assert.Null(hdfc.OwnReceived);
        Assert.Null(hdfc.OwnSpent);
        Assert.Null(hdfc.OwnClosingBalance);
    }

    [Fact]
    public void Without_a_known_base_currency_no_account_is_called_foreign_and_the_comparison_ignores_case()
    {
        Assert.All(Run(baseCurrency: "").Accounts, a => Assert.False(a.IsForeign));

        BankSummaryReportResultDto lower = Run(baseCurrency: "inr");
        Assert.True(Row(lower, UsdBank).IsForeign);
        Assert.False(Row(lower, HdfcBank).IsForeign);
    }

    [Fact]
    public void A_credit_card_is_money_held_so_what_is_owed_reads_negative()
    {
        BankSummaryAccountDto card = Row(Run(), CardBank);

        Assert.Equal((0m, 0m, 300m, 0m, -300m), (card.OpeningBalance, card.Received, card.Spent, card.Revaluation, card.ClosingBalance));
        Assert.Equal(BankAccountType.CreditCard, card.AccountType);
    }

    [Fact]
    public void Opening_plus_received_less_spent_plus_the_revaluation_is_the_closing_balance_for_every_account()
    {
        foreach (BankSummaryAccountDto account in Run().Accounts)
        {
            Assert.Equal(account.OpeningBalance + account.Received - account.Spent + account.Revaluation, account.ClosingBalance);
        }
    }

    [Fact]
    public void The_totals_add_the_accounts_in_base_currency_and_the_equation_holds_for_them_too()
    {
        BankSummaryReportResultDto report = Run();

        Assert.Equal((16_900m, 11_200m, 4_730m, 130m, 23_500m),
            (report.TotalOpeningBalance, report.TotalReceived, report.TotalSpent, report.TotalRevaluation, report.TotalClosingBalance));
        Assert.Equal(report.TotalOpeningBalance + report.TotalReceived - report.TotalSpent + report.TotalRevaluation, report.TotalClosingBalance);
    }

    [Fact]
    public void Each_accounts_closing_balance_is_its_balance_in_the_books_on_the_last_day_on_the_reconciliation_report()
    {
        foreach (BankSummaryAccountDto account in Run().Accounts)
        {
            BankAccountRead bank = Banks.Single(b => b.BankAccountId == account.BankAccountId);
            BookBalances book = BankReconciliationReportRepository
                .BookBalance(Ledger.AsQueryable(), bank.LedgerAccountId!.Value, bank.CurrencyCode, October31)
                .FirstOrDefault() ?? new BookBalances();

            Assert.Equal(book.BalanceBase, account.ClosingBalance);
        }
    }

    // ---- The period -------------------------------------------------------------------

    [Fact]
    public void A_posting_after_the_period_is_in_nothing_and_both_ends_of_it_count()
    {
        // 5 November's 99 is in no figure.
        Assert.Equal(9_400m, Row(Run(), HdfcBank).ClosingBalance);

        // The 3rd is the first day here, and the 5th the last: the receipt and the payment of 600, not the transfer on the 8th.
        BankSummaryAccountDto hdfc = Row(Run(from: new DateOnly(2026, 10, 3), to: new DateOnly(2026, 10, 5)), HdfcBank);
        Assert.Equal((7_000m, 5_000m, 600m, 11_400m), (hdfc.OpeningBalance, hdfc.Received, hdfc.Spent, hdfc.ClosingBalance));
    }

    [Fact]
    public void A_later_period_opens_at_everything_before_it_revaluations_included()
    {
        BankSummaryReportResultDto november = Run(from: new DateOnly(2026, 11, 1), to: new DateOnly(2026, 11, 30));

        Assert.Equal((9_400m, 99m, 0m, 9_499m), (Row(november, HdfcBank).OpeningBalance, Row(november, HdfcBank).Received, Row(november, HdfcBank).Spent, Row(november, HdfcBank).ClosingBalance));

        // The USD account opens at its 11,050 with October's revaluation in it, and USD 130 in its own currency.
        BankSummaryAccountDto usd = Row(november, UsdBank);
        Assert.Equal((11_050m, 0m, 0m, 0m, 11_050m), (usd.OpeningBalance, usd.Received, usd.Spent, usd.Revaluation, usd.ClosingBalance));
        Assert.Equal((130m, 130m), (usd.OwnOpeningBalance, usd.OwnClosingBalance));
    }

    [Fact]
    public void One_bank_account_asked_for_is_the_only_one_listed_with_its_own_totals()
    {
        BankSummaryReportResultDto report = Run(bank: PettyBank);

        BankSummaryAccountDto petty = Assert.Single(report.Accounts);
        Assert.Equal(PettyBank, petty.BankAccountId);
        Assert.Equal((1_000m, 2_850m), (report.TotalOpeningBalance, report.TotalClosingBalance));
    }

    // ---- The revaluation and the catalog's figures ------------------------------------

    [Fact]
    public void A_revaluation_is_every_leg_of_a_document_that_posts_to_unrealized_fx_once()
    {
        List<JournalLedgerRead> ledger =
        [
            Leg("JRN", 7, October1, Usd, 10m, 0m), Leg("JRN", 7, October1, UnrealizedFx, 0m, 6m), Leg("JRN", 7, October1, UnrealizedFx, 0m, 4m),
            Leg("RCM", 20, October1, Hdfc, 50m, 0m), Leg("RCM", 20, October1, Sales, 0m, 50m),
        ];

        List<JournalLedgerRead> legs = [.. BankSummaryReportRepository.RevaluationLegs(ledger.AsQueryable(), Accounts.AsQueryable())];

        // The journal's three legs, none repeated for its two on Unrealized FX; the receipt is not one.
        Assert.Equal(3, legs.Count);
        Assert.All(legs, l => Assert.Equal(7L, l.TransactionId));
    }

    [Fact]
    public void The_catalogs_figures_are_the_pages_for_every_account()
    {
        List<BankFigures> figures = [.. BankSummaryReportRepository.Figures(Ledger.AsQueryable(), Accounts.AsQueryable(), Banks.AsQueryable(), October1, October31)];
        BankSummaryReportResultDto report = Run();

        // An account with no leg at all has none; every other matches the page.
        Assert.DoesNotContain(figures, f => f.BankAccountId is IdleBank or ClosedBank);

        foreach (BankFigures f in figures)
        {
            BankSummaryAccountDto row = Row(report, f.BankAccountId);
            Assert.Equal((row.OpeningBalance, row.Received, row.Spent, row.Revaluation), (f.OpeningBase, f.ReceivedBase, f.SpentBase, f.RevaluationBase));
            Assert.Equal(row.ClosingBalance, f.OpeningBase + f.ReceivedBase - f.SpentBase + f.RevaluationBase);
        }
    }

    // ---- The request and the guards ---------------------------------------------------

    [Fact]
    public void The_two_dates_are_required_and_nothing_else_is()
    {
        Assert.Equal(
            [nameof(BankSummaryReportRequest.FromDate), nameof(BankSummaryReportRequest.ToDate)],
            typeof(BankSummaryReportRequest).GetProperties()
                .Where(p => p.GetCustomAttribute<RequiredAttribute>() is not null)
                .Select(p => p.Name));
    }

    [Fact]
    public void A_missing_date_is_refused_at_its_field_by_the_request_itself()
    {
        var request = new BankSummaryReportRequest();
        var results = new List<ValidationResult>();

        Assert.False(Validator.TryValidateObject(request, new ValidationContext(request), results, validateAllProperties: true));
        Assert.Equal(
            ["From date is required.", "To date is required."],
            results.Select(r => r.ErrorMessage).OrderBy(m => m, StringComparer.Ordinal));
    }

    [Fact]
    public void A_period_that_ends_before_it_begins_is_refused_at_the_to_date()
    {
        Assert.Equal(
            [(nameof(BankSummaryReportRequest.ToDate), "To date cannot be before the from date.")],
            BankSummaryReportRepository.DateRefusals(new BankSummaryReportRequest { FromDate = October31, ToDate = October1 }));

        Assert.Empty(BankSummaryReportRepository.DateRefusals(new BankSummaryReportRequest { FromDate = October1, ToDate = October1 }));
    }

    [Fact]
    public async Task Another_branch_in_the_request_is_forbidden_not_hidden()
    {
        var controller = new BankSummaryReportController(null!)
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
            new BankSummaryReportRequest { FromDate = October1, ToDate = October31, OrgId = Guid.NewGuid() }, default);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public void The_endpoint_needs_the_reports_module_accounting_view_and_a_trade()
    {
        Type controller = typeof(BankSummaryReportController);

        Assert.Equal("api/statements/bank-summary", controller.GetCustomAttribute<RouteAttribute>()!.Template);
        Assert.Equal("reports", controller.GetCustomAttribute<RequireModulePermissionAttribute>()!.Module);
        Assert.NotNull(controller.GetCustomAttribute<RequireBusinessTypeAttribute>());

        Assert.Contains(
            "accounting.view",
            controller.GetMethod(nameof(BankSummaryReportController.Run))!.GetCustomAttributes<RequirePermissionAttribute>().Select(a => a.Permission));
    }

    // ---- The catalog source, held to the same period ----------------------------------

    [Fact]
    public void The_catalog_source_declares_both_dates_required()
    {
        var source = new BankSummarySourceRepository();

        Assert.Equal(["from", "to"], source.Parameters.Select(p => p.Name));
        Assert.All(source.Parameters, p => Assert.True(p.IsRequired));
    }
}
