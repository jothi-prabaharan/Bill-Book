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
using Shared.Kernel.Tenancy;
using Xunit;

namespace Reporting.Api.Tests;

/// <summary>A base currency that is fixed, for a source that asks for it before it can build.</summary>
internal sealed class FixedBaseCurrency(string? code) : IBaseCurrencyProvider
{
    public Task<string?> GetBaseCurrencyAsync(CancellationToken ct = default) => Task.FromResult(code);
}

/// <summary>
/// The Foreign Currency Gain or Loss report (TK-220), on books worked by hand. The
/// branch's base currency is INR; the report is for October 2026, as at 31 October:
/// <list type="bullet">
/// <item><b>USD receivable</b>: an invoice of USD 1,000 at 80 (August), USD 100 collected
/// at 82 in September (a gain of 200, outside the period), USD 400 collected at 83 in
/// October (a gain of 1,200) and a further invoice of USD 50 on 5 November, after the
/// date. It stands at USD 500 carried at 40,000.</item>
/// <item><b>USD payable</b>: a bill of USD 500 at 82 and USD 200 paid at 84 in October (a
/// loss of 400): USD 300 owed, carried at 24,600.</item>
/// <item><b>USD bank account</b>: USD 100 deposited at 81, carried at 8,100.</item>
/// <item><b>EUR receivable</b>: an invoice of EUR 200 at 90 and EUR 50 collected at 88 in
/// October (a loss of 100): EUR 150, carried at 13,500.</item>
/// <item>The <b>INR bank account</b> holds the money those settlements moved, tagged with
/// the document's currency; it is a base-currency account and is no foreign balance.</item>
/// <item>A <b>manual journal</b> of 50 to Realized FX Gain/Loss, in base only.</item>
/// </list>
/// At USD 84 and EUR 91: the USD balances are worth 25,200 against 23,500 carried, 1,700
/// unrealized; the EUR receivable 13,650 against 13,500, 150. Realized for October is
/// 1,200 − 400 − 100 and the journal's 50, which is 750 and the account's movement on the
/// Profit and Loss. The builders run on lists through the same expressions the repository
/// runs against the read models; the composition is pure. Nothing here needs PostgreSQL.
/// </summary>
public sealed class ForeignCurrencyGainLossReportTests
{
    private const long Receivable = 31, Payable = 36, Sales = 43, RealizedFx = 50, UsdBank = 211, InrBank = 212;

    private static readonly DateOnly October1 = new(2026, 10, 1);
    private static readonly DateOnly October31 = new(2026, 10, 31);

    private static AccountRead Account(long id, string name, string? system = null, bool bank = false, string? currency = null) =>
        new() { AccountId = id, AccountCode = id.ToString(), AccountName = name, AccountSystemName = system, AccountTypeId = 1, IsBank = bank, CurrencyCode = currency, IsActive = true };

    private static readonly List<AccountRead> Accounts =
    [
        Account(Receivable, "Accounts Receivable", ForeignCurrencyGainLossReportRepository.ReceivableAccountName),
        Account(Payable, "Accounts Payable", ForeignCurrencyGainLossReportRepository.PayableAccountName),
        Account(Sales, "Sales Revenue"),
        Account(RealizedFx, "Realized FX Gain/Loss", ForeignCurrencyGainLossReportRepository.RealizedFxAccountName),
        Account(UsdBank, "USD Account", bank: true, currency: "USD"),
        Account(InrBank, "HDFC Current", bank: true, currency: "INR"),
    ];

    private static long _id;

    /// <summary>A leg: its base amounts, its currency, and the amounts the document gave in that currency.</summary>
    private static JournalLedgerRead Leg(
        string type, long transactionId, DateOnly on, long account, decimal debitBase, decimal creditBase,
        string currency = "INR", decimal? debit = null, decimal? credit = null) =>
        new()
        {
            LedgerId = ++_id, LedgerDate = on, AccountId = account, TransactionTypeCode = type, TransactionId = transactionId,
            TransactionDetailId = 1, DebitAmountBase = debitBase, CreditAmountBase = creditBase,
            DebitAmount = debit ?? debitBase, CreditAmount = credit ?? creditBase, CurrencyCode = currency, ExchangeRate = 1m,
        };

    private static readonly List<JournalLedgerRead> Ledger =
    [
        // USD invoice of 1,000 at 80.
        Leg("INV", 9001, new(2026, 8, 10), Receivable, 80_000m, 0m, "USD", 1_000m, 0m), Leg("INV", 9001, new(2026, 8, 10), Sales, 0m, 80_000m, "USD", 0m, 1_000m),
        // USD 400 collected at 83: the bank gets 33,200, the receivable comes off at 32,000, a gain of 1,200.
        Leg("RCM", 9002, new(2026, 10, 12), InrBank, 33_200m, 0m, "USD", 400m, 0m), Leg("RCM", 9002, new(2026, 10, 12), Receivable, 0m, 32_000m, "USD", 0m, 400m),
        Leg("RCM", 9002, new(2026, 10, 12), RealizedFx, 0m, 1_200m),
        // USD bill of 500 at 82; USD 200 paid at 84: the payable comes off at 16,400, 16,800 leaves the bank, a loss of 400.
        Leg("BIL", 9003, new(2026, 9, 5), Sales, 41_000m, 0m, "USD", 500m, 0m), Leg("BIL", 9003, new(2026, 9, 5), Payable, 0m, 41_000m, "USD", 0m, 500m),
        Leg("SPM", 9004, new(2026, 10, 20), Payable, 16_400m, 0m, "USD", 200m, 0m), Leg("SPM", 9004, new(2026, 10, 20), InrBank, 0m, 16_800m, "USD", 0m, 200m),
        Leg("SPM", 9004, new(2026, 10, 20), RealizedFx, 400m, 0m),
        // USD 100 into the USD account at 81.
        Leg("RCM", 9005, new(2026, 7, 1), UsdBank, 8_100m, 0m, "USD", 100m, 0m), Leg("RCM", 9005, new(2026, 7, 1), Sales, 0m, 8_100m, "USD", 0m, 100m),
        // EUR invoice of 200 at 90; EUR 50 collected at 88: a loss of 100.
        Leg("INV", 9006, new(2026, 9, 1), Receivable, 18_000m, 0m, "EUR", 200m, 0m), Leg("INV", 9006, new(2026, 9, 1), Sales, 0m, 18_000m, "EUR", 0m, 200m),
        Leg("RCM", 9007, new(2026, 10, 15), InrBank, 4_400m, 0m, "EUR", 50m, 0m), Leg("RCM", 9007, new(2026, 10, 15), Receivable, 0m, 4_500m, "EUR", 0m, 50m),
        Leg("RCM", 9007, new(2026, 10, 15), RealizedFx, 100m, 0m),
        // A manual journal on the realized account, in base only: nothing foreign to tie it to.
        Leg("JRN", 9008, new(2026, 10, 28), InrBank, 50m, 0m), Leg("JRN", 9008, new(2026, 10, 28), RealizedFx, 0m, 50m),
        // USD 100 collected at 82 in September: a gain of 200, before the period.
        Leg("RCM", 9009, new(2026, 9, 20), InrBank, 8_200m, 0m, "USD", 100m, 0m), Leg("RCM", 9009, new(2026, 9, 20), Receivable, 0m, 8_000m, "USD", 0m, 100m),
        Leg("RCM", 9009, new(2026, 9, 20), RealizedFx, 0m, 200m),
        // After the date.
        Leg("INV", 9010, new(2026, 11, 5), Receivable, 4_250m, 0m, "USD", 50m, 0m), Leg("INV", 9010, new(2026, 11, 5), Sales, 0m, 4_250m, "USD", 0m, 50m),
    ];

    private static readonly Dictionary<string, decimal> BothRates = new() { ["USD"] = 84m, ["EUR"] = 91m };

    /// <summary>The report, through the same builders and composition the repository uses.</summary>
    private static ForeignCurrencyGainLossReportResultDto Run(
        DateOnly? from = null, DateOnly? asAt = null, Dictionary<string, decimal>? rates = null, string? only = null, string baseCurrency = "INR")
    {
        DateOnly start = from ?? October1, end = asAt ?? October31;
        IQueryable<JournalLedgerRead> ledger = Ledger.AsQueryable();
        IQueryable<AccountRead> accounts = Accounts.AsQueryable();

        List<FxBalance> balances = [.. ForeignCurrencyGainLossReportRepository.Balances(ledger, accounts, baseCurrency, end)];
        List<FxRealized> realized = [.. ForeignCurrencyGainLossReportRepository.Realized(ledger, accounts, baseCurrency, start, end)];
        decimal total = ForeignCurrencyGainLossReportRepository.RealizedLegs(ledger, accounts, start, end).Sum(l => l.CreditAmountBase - l.DebitAmountBase);

        return ForeignCurrencyGainLossReportRepository.Compose(start, end, baseCurrency, Accounts, balances, realized, total, rates ?? BothRates, only);
    }

    private static ForeignCurrencyBlockDto Block(ForeignCurrencyGainLossReportResultDto report, string currency) =>
        report.Currencies.Single(c => c.CurrencyCode == currency);

    private static ForeignCurrencyAccountDto Row(ForeignCurrencyGainLossReportResultDto report, string currency, long account) =>
        Block(report, currency).Accounts.Single(a => a.AccountId == account);

    // ---- The balances -----------------------------------------------------------------

    [Fact]
    public void Receivables_payables_and_a_foreign_bank_account_hold_the_balances_at_the_date()
    {
        ForeignCurrencyGainLossReportResultDto report = Run();

        Assert.Equal(["EUR", "USD"], report.Currencies.Select(c => c.CurrencyCode));

        ForeignCurrencyAccountDto receivable = Row(report, "USD", Receivable);
        Assert.Equal((500m, 40_000m), (receivable.DueSource, receivable.CarryingBase));

        ForeignCurrencyAccountDto payable = Row(report, "USD", Payable);
        Assert.Equal((-300m, -24_600m), (payable.DueSource, payable.CarryingBase));

        ForeignCurrencyAccountDto bank = Row(report, "USD", UsdBank);
        Assert.Equal((100m, 8_100m), (bank.DueSource, bank.CarryingBase));

        ForeignCurrencyAccountDto eur = Row(report, "EUR", Receivable);
        Assert.Equal((150m, 13_500m), (eur.DueSource, eur.CarryingBase));
    }

    [Fact]
    public void A_base_currency_bank_account_is_no_foreign_balance_whatever_its_legs_are_tagged_with()
    {
        // The INR account carries legs tagged USD and EUR, because the documents that moved its money were in them.
        ForeignCurrencyGainLossReportResultDto report = Run();

        Assert.DoesNotContain(report.Currencies.SelectMany(c => c.Accounts), a => a.AccountId == InrBank);
        Assert.DoesNotContain(report.Currencies, c => c.CurrencyCode == "INR");
    }

    [Fact]
    public void A_posting_after_the_date_is_in_no_balance_and_an_earlier_date_has_the_earlier_balance()
    {
        // 5 November's invoice of USD 50 is in nothing at 31 October.
        Assert.Equal(500m, Row(Run(), "USD", Receivable).DueSource);

        // At 30 September the September collection is in, the October ones are not, and the bill has not been paid.
        ForeignCurrencyGainLossReportResultDto september = Run(from: new DateOnly(2026, 9, 1), asAt: new DateOnly(2026, 9, 30));
        Assert.Equal((900m, 72_000m), (Row(september, "USD", Receivable).DueSource, Row(september, "USD", Receivable).CarryingBase));
        Assert.Equal((-500m, -41_000m), (Row(september, "USD", Payable).DueSource, Row(september, "USD", Payable).CarryingBase));
        Assert.Equal((200m, 18_000m), (Row(september, "EUR", Receivable).DueSource, Row(september, "EUR", Receivable).CarryingBase));
    }

    [Fact]
    public void Nothing_is_foreign_when_the_branch_has_no_base_currency_known_to_the_caller()
    {
        // The repository answers an empty report rather than guessing; Compose of nothing is that report.
        ForeignCurrencyGainLossReportResultDto report = ForeignCurrencyGainLossReportRepository.Compose(
            October1, October31, string.Empty, [], [], [], 0m, new Dictionary<string, decimal>(), null);

        Assert.Empty(report.Currencies);
        Assert.Equal((0m, 0m, 0m), (report.TotalRealized, report.TotalUnrealized, report.TotalNet));
    }

    // ---- The unrealized part ----------------------------------------------------------

    [Fact]
    public void Unrealized_is_the_balance_at_the_rate_less_what_it_is_carried_at_and_a_payable_reads_the_other_way()
    {
        ForeignCurrencyGainLossReportResultDto report = Run();

        // USD 500 at 84 is 42,000 against 40,000: a gain on a receivable.
        ForeignCurrencyAccountDto receivable = Row(report, "USD", Receivable);
        Assert.Equal((42_000m, 2_000m), (receivable.RevaluedBase, receivable.Unrealized));

        // USD 300 owed at 84 is 25,200 against 24,600: the rate rose, so owing it costs more, a loss.
        ForeignCurrencyAccountDto payable = Row(report, "USD", Payable);
        Assert.Equal((-25_200m, -600m), (payable.RevaluedBase, payable.Unrealized));

        ForeignCurrencyAccountDto bank = Row(report, "USD", UsdBank);
        Assert.Equal((8_400m, 300m), (bank.RevaluedBase, bank.Unrealized));

        ForeignCurrencyBlockDto usd = Block(report, "USD");
        Assert.Equal((300m, 23_500m, 25_200m, 1_700m), (usd.DueSource, usd.CarryingBase, usd.RevaluedBase, usd.Unrealized));
        Assert.Equal((150m, 150m), (Row(report, "EUR", Receivable).Unrealized, Block(report, "EUR").Unrealized));
    }

    [Fact]
    public void A_revalued_balance_is_rounded_to_two_places_away_from_zero_the_way_the_ledger_rounds()
    {
        List<FxBalance> balances =
        [
            new() { AccountId = Receivable, CurrencyCode = "USD", DueSource = 3m, CarryingBase = 240m },
            new() { AccountId = Payable, CurrencyCode = "USD", DueSource = -3m, CarryingBase = -240m },
        ];

        ForeignCurrencyGainLossReportResultDto report = ForeignCurrencyGainLossReportRepository.Compose(
            October1, October31, "INR", Accounts, balances, [], 0m, new Dictionary<string, decimal> { ["USD"] = 83.115m }, null);

        // 3 × 83.115 is exactly 249.345, half way: away from zero is 249.35 and −249.35, where banker's rounding would give 249.34.
        Assert.Equal(249.35m, Row(report, "USD", Receivable).RevaluedBase);
        Assert.Equal(-249.35m, Row(report, "USD", Payable).RevaluedBase);
    }

    [Fact]
    public void A_currency_with_no_rate_has_no_unrealized_figure_and_its_net_is_its_realized_part()
    {
        ForeignCurrencyGainLossReportResultDto report = Run(rates: new Dictionary<string, decimal> { ["USD"] = 84m });

        ForeignCurrencyBlockDto eur = Block(report, "EUR");
        Assert.Null(eur.Rate);
        Assert.True(eur.NeedsRate);
        Assert.Null(eur.Unrealized);
        Assert.Null(eur.RevaluedBase);
        Assert.All(eur.Accounts, a => Assert.Null(a.Unrealized));
        Assert.Equal((-100m, -100m), (eur.Realized, eur.Net));

        Assert.Equal(["EUR"], report.CurrenciesWithoutRate);
        Assert.Equal(1_700m, report.TotalUnrealized);
        Assert.Equal(750m + 1_700m, report.TotalNet);
    }

    [Fact]
    public void A_rate_of_a_currency_with_no_balance_is_ignored_and_codes_are_matched_in_upper_case()
    {
        ForeignCurrencyGainLossReportResultDto report = Run(rates: new Dictionary<string, decimal> { ["USD"] = 84m, ["EUR"] = 91m, ["JPY"] = 0.55m });

        Assert.DoesNotContain(report.Currencies, c => c.CurrencyCode == "JPY");

        Dictionary<string, decimal> rates = ForeignCurrencyGainLossReportRepository.RatesOf(
            new ForeignCurrencyGainLossReportRequest { Rates = new Dictionary<string, decimal> { ["usd"] = 80m, [" USD "] = 84m, ["eur"] = 91m } });

        Assert.Equal(2, rates.Count);
        Assert.Equal(84m, rates["USD"]);
        Assert.Equal(91m, rates["EUR"]);
    }

    // ---- The realized part ------------------------------------------------------------

    [Fact]
    public void Realized_is_each_settlement_in_the_period_tied_to_its_currency_and_account_and_a_gain_is_positive()
    {
        ForeignCurrencyGainLossReportResultDto report = Run();

        // The October collection (a gain) on the USD receivable; the payment (a loss) on the payable; the EUR collection a loss.
        Assert.Equal(1_200m, Row(report, "USD", Receivable).Realized);
        Assert.Equal(-400m, Row(report, "USD", Payable).Realized);
        Assert.Equal(0m, Row(report, "USD", UsdBank).Realized);
        Assert.Equal(-100m, Row(report, "EUR", Receivable).Realized);

        Assert.Equal((800m, 2_500m), (Block(report, "USD").Realized, Block(report, "USD").Net));
        Assert.Equal((-100m, 50m), (Block(report, "EUR").Realized, Block(report, "EUR").Net));
    }

    [Fact]
    public void A_settlement_before_the_period_is_not_in_the_realized_figure_and_both_ends_count()
    {
        // The September collection's gain of 200 is outside October.
        Assert.Equal(1_200m, Row(Run(), "USD", Receivable).Realized);

        // The whole of September is its own period: the 200 and nothing else.
        ForeignCurrencyGainLossReportResultDto september = Run(from: new DateOnly(2026, 9, 1), asAt: new DateOnly(2026, 9, 30));
        Assert.Equal(200m, september.TotalRealized);
        Assert.Equal(200m, Row(september, "USD", Receivable).Realized);

        // 12 October is the first day and the last: the collection is in, the payment on the 20th is not.
        ForeignCurrencyGainLossReportResultDto one = Run(from: new DateOnly(2026, 10, 12), asAt: new DateOnly(2026, 10, 12));
        Assert.Equal(1_200m, one.TotalRealized);
    }

    [Fact]
    public void A_document_with_no_foreign_balance_belongs_to_no_currency_and_is_shown_apart_but_counted()
    {
        ForeignCurrencyGainLossReportResultDto report = Run();

        // The manual journal's 50 is in no currency's block, and is in the total.
        Assert.Equal(50m, report.UnassignedRealized);
        Assert.Equal(750m, report.TotalRealized);
        Assert.Equal(report.TotalRealized, report.Currencies.Sum(c => c.Realized) + report.UnassignedRealized);
    }

    [Fact]
    public void Total_realized_is_the_realized_account_s_movement_for_the_same_dates_as_on_the_profit_and_loss()
    {
        foreach ((DateOnly start, DateOnly end) in new[] { (October1, October31), (new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30)), (new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31)) })
        {
            decimal movement = Ledger
                .Where(l => l.AccountId == RealizedFx && l.LedgerDate >= start && l.LedgerDate <= end)
                .Sum(l => l.CreditAmountBase - l.DebitAmountBase);

            Assert.Equal(movement, Run(from: start, asAt: end).TotalRealized);
        }
    }

    [Fact]
    public void A_document_is_tied_to_its_receivable_before_its_bank_account_and_to_the_bank_account_when_it_has_no_receivable()
    {
        // A USD bank account with a lower id than the receivable, so the choice is the rule and not the id.
        const long EarlyUsdBank = 20;
        List<AccountRead> accounts = [.. Accounts, Account(EarlyUsdBank, "Early USD Account", bank: true, currency: "USD")];

        // One document: a leg on that bank account and a leg on the receivable. The receivable wins,
        // and the realized leg counts once, not once for each of the document's foreign legs.
        List<JournalLedgerRead> ledger =
        [
            Leg("RCM", 1, October1, EarlyUsdBank, 100m, 0m, "USD", 1m, 0m), Leg("RCM", 1, October1, Receivable, 0m, 98m, "USD", 0m, 1m),
            Leg("RCM", 1, October1, RealizedFx, 0m, 2m),
        ];

        FxRealized viaReceivable = Assert.Single(ForeignCurrencyGainLossReportRepository.Realized(ledger.AsQueryable(), accounts.AsQueryable(), "INR", October1, October31));
        Assert.Equal(("USD", Receivable, 2m, 1), (viaReceivable.CurrencyCode, viaReceivable.AccountId, viaReceivable.Amount, viaReceivable.Count));

        // A document with only a foreign bank leg is tied to that bank account, and a loss is negative.
        List<JournalLedgerRead> bankOnly =
        [
            Leg("TRM", 2, October1, UsdBank, 100m, 0m, "USD", 1m, 0m), Leg("TRM", 2, October1, InrBank, 0m, 103m),
            Leg("TRM", 2, October1, RealizedFx, 3m, 0m),
        ];

        FxRealized viaBank = Assert.Single(ForeignCurrencyGainLossReportRepository.Realized(bankOnly.AsQueryable(), Accounts.AsQueryable(), "INR", October1, October31));
        Assert.Equal(("USD", UsdBank, -3m), (viaBank.CurrencyCode, viaBank.AccountId, viaBank.Amount));
    }

    // ---- The currency filter ----------------------------------------------------------

    [Fact]
    public void One_currency_asked_for_is_the_only_block_with_its_own_totals_and_no_unassigned_realized()
    {
        ForeignCurrencyGainLossReportResultDto report = Run(only: "USD");

        Assert.Equal(["USD"], report.Currencies.Select(c => c.CurrencyCode));
        Assert.Equal(0m, report.UnassignedRealized);
        Assert.Equal((800m, 1_700m, 2_500m), (report.TotalRealized, report.TotalUnrealized, report.TotalNet));
    }

    [Fact]
    public void The_totals_add_every_currency_and_the_unassigned_part()
    {
        ForeignCurrencyGainLossReportResultDto report = Run();

        Assert.Equal((750m, 1_850m, 2_600m), (report.TotalRealized, report.TotalUnrealized, report.TotalNet));
        Assert.Empty(report.CurrenciesWithoutRate);
    }

    [Fact]
    public void A_currency_with_only_a_realized_figure_still_has_a_block_and_needs_no_rate()
    {
        // Settled in full: nothing is owed in EUR any more, but the period posted a loss on it.
        List<FxRealized> realized = [new() { CurrencyCode = "EUR", AccountId = Receivable, Amount = -100m, Count = 1 }];

        ForeignCurrencyGainLossReportResultDto report = ForeignCurrencyGainLossReportRepository.Compose(
            October1, October31, "INR", Accounts, [], realized, -100m, new Dictionary<string, decimal>(), null);

        ForeignCurrencyBlockDto eur = Assert.Single(report.Currencies);
        Assert.False(eur.NeedsRate);
        Assert.Equal((-100m, -100m), (eur.Realized, eur.Net));
        Assert.Empty(report.CurrenciesWithoutRate);
    }

    [Fact]
    public void Accounts_run_receivables_then_payables_then_banks_by_name()
    {
        ForeignCurrencyGainLossReportResultDto report = Run();

        Assert.Equal(
            [ForeignAccountKind.Receivable, ForeignAccountKind.Payable, ForeignAccountKind.Bank],
            Block(report, "USD").Accounts.Select(a => a.Kind));
    }

    // ---- The currencies the page offers rates for ---------------------------------------

    [Fact]
    public void The_choices_are_the_currencies_with_a_balance_and_how_many_accounts_hold_one()
    {
        List<FxBalance> balances = [.. ForeignCurrencyGainLossReportRepository.Balances(Ledger.AsQueryable(), Accounts.AsQueryable(), "INR", October31)];

        ForeignCurrencyChoicesDto choices = ForeignCurrencyGainLossReportRepository.ChoicesOf("INR", balances);

        Assert.Equal("INR", choices.BaseCurrencyCode);
        Assert.Equal([("EUR", 1), ("USD", 3)], choices.Currencies.Select(c => (c.CurrencyCode, c.Accounts)));
    }

    [Fact]
    public void A_settled_balance_is_no_choice()
    {
        List<FxBalance> balances = [new() { AccountId = Receivable, CurrencyCode = "GBP", DueSource = 0m, CarryingBase = 0m }];

        Assert.Empty(ForeignCurrencyGainLossReportRepository.ChoicesOf("INR", balances).Currencies);
    }

    // ---- The request and the guards ---------------------------------------------------

    [Fact]
    public void The_two_dates_are_required_and_nothing_else_is()
    {
        Assert.Equal(
            [nameof(ForeignCurrencyGainLossReportRequest.FromDate), nameof(ForeignCurrencyGainLossReportRequest.AsAtDate)],
            typeof(ForeignCurrencyGainLossReportRequest).GetProperties()
                .Where(p => p.GetCustomAttribute<RequiredAttribute>() is not null)
                .Select(p => p.Name));
    }

    [Fact]
    public void A_missing_date_is_refused_at_its_field_by_the_request_itself()
    {
        var request = new ForeignCurrencyGainLossReportRequest();
        var results = new List<ValidationResult>();

        Assert.False(Validator.TryValidateObject(request, new ValidationContext(request), results, validateAllProperties: true));
        Assert.Equal(
            ["As at date is required.", "From date is required."],
            results.Select(r => r.ErrorMessage).OrderBy(m => m, StringComparer.Ordinal));
    }

    [Fact]
    public void An_as_at_date_before_the_from_date_is_refused_at_the_as_at_date()
    {
        Assert.Equal(
            [(nameof(ForeignCurrencyGainLossReportRequest.AsAtDate), "As at date cannot be before the from date.")],
            ForeignCurrencyGainLossReportRepository.Refusals(new ForeignCurrencyGainLossReportRequest { FromDate = October31, AsAtDate = October1 }));

        Assert.Empty(ForeignCurrencyGainLossReportRepository.Refusals(new ForeignCurrencyGainLossReportRequest { FromDate = October1, AsAtDate = October1 }));
    }

    [Fact]
    public void A_currency_that_is_not_a_three_letter_code_and_a_rate_that_is_not_above_nought_are_refused_at_their_fields()
    {
        var request = new ForeignCurrencyGainLossReportRequest
        {
            FromDate = October1,
            AsAtDate = October31,
            CurrencyCode = "US",
            Rates = new Dictionary<string, decimal> { ["USD"] = 0m, ["EU"] = 1m, ["EUR"] = 91m, ["GBP"] = -2m },
        };

        Assert.Equal(
            [
                (nameof(ForeignCurrencyGainLossReportRequest.CurrencyCode), "Currency must be a 3-letter code."),
                ("Rates[USD]", "Rate must be greater than zero."),
                ("Rates[EU]", "Currency must be a 3-letter code."),
                ("Rates[GBP]", "Rate must be greater than zero."),
            ],
            ForeignCurrencyGainLossReportRepository.Refusals(request));
    }

    [Fact]
    public async Task Another_branch_in_the_request_is_forbidden_not_hidden_on_both_endpoints()
    {
        var controller = new ForeignCurrencyGainLossReportController(null!)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("org_id", Guid.NewGuid().ToString())], "test")),
                },
            },
        };

        IActionResult report = await controller.Run(
            new ForeignCurrencyGainLossReportRequest { FromDate = October1, AsAtDate = October31, OrgId = Guid.NewGuid() }, default);
        IActionResult currencies = await controller.Currencies(
            new ForeignCurrencyChoicesRequest { AsAtDate = October31, OrgId = Guid.NewGuid() }, default);

        Assert.IsType<ForbidResult>(report);
        Assert.IsType<ForbidResult>(currencies);
    }

    [Fact]
    public void The_endpoints_need_the_reports_module_accounting_view_and_a_trade()
    {
        Type controller = typeof(ForeignCurrencyGainLossReportController);

        Assert.Equal("api/statements/foreign-currency-gain-or-loss", controller.GetCustomAttribute<RouteAttribute>()!.Template);
        Assert.Equal("reports", controller.GetCustomAttribute<RequireModulePermissionAttribute>()!.Module);
        Assert.NotNull(controller.GetCustomAttribute<RequireBusinessTypeAttribute>());

        foreach (string action in new[] { nameof(ForeignCurrencyGainLossReportController.Run), nameof(ForeignCurrencyGainLossReportController.Currencies) })
        {
            Assert.Contains(
                "accounting.view",
                controller.GetMethod(action)!.GetCustomAttributes<RequirePermissionAttribute>().Select(a => a.Permission));
        }

        Assert.Equal("currencies", controller.GetMethod(nameof(ForeignCurrencyGainLossReportController.Currencies))!.GetCustomAttribute<HttpGetAttribute>()!.Template);
    }

    // ---- The catalog source, held to the same figures ---------------------------------

    [Fact]
    public void The_catalog_source_declares_both_dates_required()
    {
        var source = new FxGainLossSourceRepository(new FixedBaseCurrency("INR"));

        Assert.Equal(["from", "asOf"], source.Parameters.Select(p => p.Name));
        Assert.All(source.Parameters, p => Assert.True(p.IsRequired));
    }

    [Fact]
    public void The_catalogs_rows_are_the_pages_for_every_account_and_currency_and_add_up_to_the_realized_account()
    {
        List<FxFigures> figures = [.. FxGainLossSourceRepository.Figures(Ledger.AsQueryable(), Accounts.AsQueryable(), "INR", October1, October31)];
        ForeignCurrencyGainLossReportResultDto report = Run();

        // Every account and currency the page shows is a row with the same figures.
        foreach (ForeignCurrencyBlockDto block in report.Currencies)
        {
            foreach (ForeignCurrencyAccountDto row in block.Accounts)
            {
                FxFigures f = figures.Single(x => x.AccountId == row.AccountId && x.CurrencyCode == block.CurrencyCode);
                Assert.Equal((row.DueSource, row.CarryingBase, row.Realized), (f.DueSource, f.CarryingBase, f.Realized));
            }
        }

        // What the page shows apart is the realized account's own row, so the rows add up to its movement.
        FxFigures unassigned = Assert.Single(figures, f => f.AccountId == RealizedFx);
        Assert.Equal(report.UnassignedRealized, unassigned.Realized);
        Assert.Equal(report.TotalRealized, figures.Sum(f => f.Realized));
    }

    [Fact]
    public void The_catalogs_empty_columns_are_typed_nulls_so_the_runner_can_sum_them()
    {
        List<FxFigures> figures = [.. FxGainLossSourceRepository.Figures(Ledger.AsQueryable(), Accounts.AsQueryable(), "INR", October1, October31)];

        Assert.NotEmpty(figures);
        Assert.All(figures, f => Assert.Null(f.Blank));
    }
}
