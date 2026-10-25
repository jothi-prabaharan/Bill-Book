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
/// The Foreign Currency Gain or Loss Details report (TK-221), on books worked by hand. The
/// branch's base currency is INR; the report is for October 2026, to 31 October, at USD 84
/// and EUR 91:
/// <list type="bullet">
/// <item><b>INV-0001</b>, USD 1,000 at 80 to <b>Acme Exports</b> (10 August), settled in part by
/// RCM-0001 (USD 100 at 82, in September, a gain of 200) and RCM-0002 (USD 400 at 83, in
/// October, a gain of 1,200): USD 500 open, carried at 40,000.</item>
/// <item><b>RCM-0004</b>, an advance of USD 100 at 83 that settles nothing in particular: a
/// document of its own, owed the other way, carried at −8,300.</item>
/// <item><b>INV-0002</b>, EUR 200 at 90, and RCM-0003 settling EUR 50 of it at 88 (a loss of
/// 100): EUR 150 open.</item>
/// <item><b>BIL-0001</b>, USD 500 at 82 from <b>Globex Supplies</b>, and SPM-0001 paying USD 200
/// of it at 84 (a loss of 400): USD 300 owed.</item>
/// <item>USD 100 deposited into the <b>USD bank account</b> at 81, whose document has no number
/// of its own.</item>
/// <item>A <b>manual journal</b> of 50 to Realized FX Gain/Loss, and an invoice after the date.</item>
/// </list>
/// The builders run on lists through the same expressions the repository runs against the read
/// models; the composition is pure. Nothing here needs PostgreSQL.
/// </summary>
public sealed class ForeignCurrencyGainLossDetailsReportTests
{
    private const long Receivable = 31, Payable = 36, Sales = 43, RealizedFx = 50, UsdBank = 211, InrBank = 212;
    private const long Acme = 9101, Globex = 9102;

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

    /// <summary>A leg: its base amounts, its currency, the amounts the document gave in it, and the line it belongs to.</summary>
    private static JournalLedgerRead Leg(
        string type, long transactionId, DateOnly on, long account, decimal debitBase, decimal creditBase,
        string currency = "INR", decimal? debit = null, decimal? credit = null, long? contact = null, string? documentNo = null, long line = 1) =>
        new()
        {
            LedgerId = ++_id, LedgerDate = on, AccountId = account, TransactionTypeCode = type, TransactionId = transactionId,
            TransactionDetailId = line, DebitAmountBase = debitBase, CreditAmountBase = creditBase,
            DebitAmount = debit ?? debitBase, CreditAmount = credit ?? creditBase, CurrencyCode = currency, ExchangeRate = 1m,
            ContactId = contact, DocumentNo = documentNo,
        };

    private static readonly List<JournalLedgerRead> Ledger =
    [
        Leg("INV", 9001, new(2026, 8, 10), Receivable, 80_000m, 0m, "USD", 1_000m, 0m, Acme, "INV-0001"), Leg("INV", 9001, new(2026, 8, 10), Sales, 0m, 80_000m, "USD", 0m, 1_000m, Acme, "INV-0001", 2),
        Leg("INV", 9006, new(2026, 9, 1), Receivable, 18_000m, 0m, "EUR", 200m, 0m, Acme, "INV-0002"), Leg("INV", 9006, new(2026, 9, 1), Sales, 0m, 18_000m, "EUR", 0m, 200m, Acme, "INV-0002", 2),
        Leg("BIL", 9003, new(2026, 9, 5), Sales, 41_000m, 0m, "USD", 500m, 0m, Globex, "BIL-0001"), Leg("BIL", 9003, new(2026, 9, 5), Payable, 0m, 41_000m, "USD", 0m, 500m, Globex, "BIL-0001", 2),
        // September: USD 100 of INV-0001 at 82, a gain of 200, before the period.
        Leg("RCM", 9009, new(2026, 9, 20), InrBank, 8_200m, 0m, "USD", 100m, 0m, Acme), Leg("RCM", 9009, new(2026, 9, 20), Receivable, 0m, 8_000m, "USD", 0m, 100m, Acme),
        Leg("RCM", 9009, new(2026, 9, 20), RealizedFx, 0m, 200m),
        // October: USD 400 of INV-0001 at 83, a gain of 1,200.
        Leg("RCM", 9002, new(2026, 10, 12), InrBank, 33_200m, 0m, "USD", 400m, 0m, Acme), Leg("RCM", 9002, new(2026, 10, 12), Receivable, 0m, 32_000m, "USD", 0m, 400m, Acme),
        Leg("RCM", 9002, new(2026, 10, 12), RealizedFx, 0m, 1_200m),
        // October: EUR 50 of INV-0002 at 88, a loss of 100.
        Leg("RCM", 9007, new(2026, 10, 15), InrBank, 4_400m, 0m, "EUR", 50m, 0m, Acme), Leg("RCM", 9007, new(2026, 10, 15), Receivable, 0m, 4_500m, "EUR", 0m, 50m, Acme),
        Leg("RCM", 9007, new(2026, 10, 15), RealizedFx, 100m, 0m),
        // October: USD 200 of BIL-0001 at 84, a loss of 400.
        Leg("SPM", 9004, new(2026, 10, 20), Payable, 16_400m, 0m, "USD", 200m, 0m, Globex), Leg("SPM", 9004, new(2026, 10, 20), InrBank, 0m, 16_800m, "USD", 0m, 200m, Globex),
        Leg("SPM", 9004, new(2026, 10, 20), RealizedFx, 400m, 0m),
        // An advance that settles nothing in particular: USD 100 at 83.
        Leg("RCM", 9011, new(2026, 10, 22), InrBank, 8_300m, 0m, "USD", 100m, 0m, Acme), Leg("RCM", 9011, new(2026, 10, 22), Receivable, 0m, 8_300m, "USD", 0m, 100m, Acme),
        // USD 100 into the USD account at 81.
        Leg("RCM", 9005, new(2026, 7, 1), UsdBank, 8_100m, 0m, "USD", 100m, 0m), Leg("RCM", 9005, new(2026, 7, 1), Sales, 0m, 8_100m, "USD", 0m, 100m, line: 2),
        // A manual journal on the realized account, in base only.
        Leg("JRN", 9008, new(2026, 10, 28), InrBank, 50m, 0m, documentNo: "JRN-0001"), Leg("JRN", 9008, new(2026, 10, 28), RealizedFx, 0m, 50m, documentNo: "JRN-0001", line: 2),
        // After the date.
        Leg("INV", 9010, new(2026, 11, 5), Receivable, 4_250m, 0m, "USD", 50m, 0m, Acme, "INV-0003"), Leg("INV", 9010, new(2026, 11, 5), Sales, 0m, 4_250m, "USD", 0m, 50m, Acme, "INV-0003", 2),
    ];

    /// <summary>What each line of a money document settles.</summary>
    private static readonly List<FxSettlementLine> Lines =
    [
        new() { Code = "RCM", Id = 9009, Line = 1, MappedCode = "INV", MappedId = 9001 },
        new() { Code = "RCM", Id = 9002, Line = 1, MappedCode = "INV", MappedId = 9001 },
        new() { Code = "RCM", Id = 9007, Line = 1, MappedCode = "INV", MappedId = 9006 },
        new() { Code = "SPM", Id = 9004, Line = 1, MappedCode = "BIL", MappedId = 9003 },
    ];

    private static readonly Dictionary<(string Code, long Id), string> MoneyNumbers = new()
    {
        [("RCM", 9009)] = "RCM-0001", [("RCM", 9002)] = "RCM-0002", [("RCM", 9007)] = "RCM-0003", [("RCM", 9011)] = "RCM-0004", [("SPM", 9004)] = "SPM-0001",
    };

    private static readonly Dictionary<long, string> ContactNames = new() { [Acme] = "Acme Exports", [Globex] = "Globex Supplies" };

    private static readonly Dictionary<string, decimal> BothRates = new() { ["USD"] = 84m, ["EUR"] = 91m };

    /// <summary>The report, through the same builders and composition the repository uses.</summary>
    private static ForeignCurrencyGainLossDetailsResultDto Run(
        List<JournalLedgerRead>? ledger = null, DateOnly? from = null, DateOnly? to = null, Dictionary<string, decimal>? rates = null,
        string? only = null, long? contact = null, long? account = null, List<FxSettlementLine>? lines = null)
    {
        DateOnly start = from ?? October1, end = to ?? October31;
        IQueryable<JournalLedgerRead> source = (ledger ?? Ledger).AsQueryable();
        IQueryable<AccountRead> accounts = Accounts.AsQueryable();

        List<ForeignLeg> legs = [.. ForeignCurrencyGainLossReportRepository.ForeignLegs(source, accounts, "INR").Where(l => l.LedgerDate <= end)];
        List<FxRealizedLeg> realized = [.. ForeignCurrencyGainLossDetailsReportRepository.RealizedLegDetails(source, accounts, "INR", start, end)];
        List<FxRealizedLeg> unassigned = [.. ForeignCurrencyGainLossDetailsReportRepository.UnassignedRealizedLegDetails(source, accounts, "INR", start, end)];

        return ForeignCurrencyGainLossDetailsReportRepository.Compose(
            start, end, "INR", Accounts, legs, realized, unassigned, lines ?? Lines, MoneyNumbers, ContactNames, rates ?? BothRates, only, contact, account);
    }

    private static ForeignCurrencyDetailBlockDto Block(ForeignCurrencyGainLossDetailsResultDto report, string currency) =>
        report.Currencies.Single(c => c.CurrencyCode == currency);

    private static ForeignCurrencyDetailAccountDto Account(ForeignCurrencyGainLossDetailsResultDto report, string currency, long account) =>
        Block(report, currency).Accounts.Single(a => a.AccountId == account);

    private static List<ForeignCurrencyDocumentDto> Open(ForeignCurrencyGainLossDetailsResultDto report, string currency, long account) =>
        [.. Account(report, currency, account).Documents.Where(d => d.Kind == ForeignDocumentKind.Open)];

    private static List<ForeignCurrencyDocumentDto> Settlements(ForeignCurrencyGainLossDetailsResultDto report, string currency, long account) =>
        [.. Account(report, currency, account).Documents.Where(d => d.Kind == ForeignDocumentKind.Settlement)];

    // ---- Open documents ---------------------------------------------------------------

    [Fact]
    public void A_settlement_comes_off_the_document_it_settles_so_the_invoice_shows_what_is_still_owed()
    {
        ForeignCurrencyGainLossDetailsResultDto report = Run();

        // USD 1,000 less the September USD 100 and the October USD 400, at the rate the invoice was booked at.
        ForeignCurrencyDocumentDto invoice = Assert.Single(Open(report, "USD", Receivable), d => d.DocumentNo == "INV-0001");

        Assert.Equal((new DateOnly(2026, 8, 10), "INV", "Acme Exports"), (invoice.Date, invoice.TypeCode, invoice.ContactName));
        Assert.Equal((500m, 80m, 40_000m), (invoice.Amount, invoice.BookedRate, invoice.CarryingBase));
        Assert.Equal((84m, 42_000m, 2_000m), (invoice.CurrentRate, invoice.CurrentBase, invoice.Unrealized));
        Assert.Null(invoice.Realized);
    }

    [Fact]
    public void An_advance_that_settles_nothing_in_particular_is_a_document_of_its_own_owed_the_other_way()
    {
        ForeignCurrencyDocumentDto advance = Assert.Single(Open(Run(), "USD", Receivable), d => d.DocumentNo == "RCM-0004");

        Assert.Equal(("RCM", -100m, 83m, -8_300m), (advance.TypeCode, advance.Amount, advance.BookedRate, advance.CarryingBase));
        Assert.Equal((-8_400m, -100m), (advance.CurrentBase, advance.Unrealized));
    }

    [Fact]
    public void A_payable_is_open_for_what_is_still_owed_and_reads_as_a_loss_when_the_rate_rises()
    {
        ForeignCurrencyDocumentDto bill = Assert.Single(Open(Run(), "USD", Payable));

        Assert.Equal(("BIL-0001", "Globex Supplies", -300m, -24_600m), (bill.DocumentNo, bill.ContactName, bill.Amount, bill.CarryingBase));
        Assert.Equal((-25_200m, -600m), (bill.CurrentBase, bill.Unrealized));
    }

    [Fact]
    public void A_document_settled_in_full_is_not_listed_and_one_with_nothing_left_in_either_currency_leaves_no_row()
    {
        List<JournalLedgerRead> ledger =
        [
            Leg("INV", 1, new(2026, 9, 1), Receivable, 8_000m, 0m, "USD", 100m, 0m, Acme, "INV-0009"), Leg("INV", 1, new(2026, 9, 1), Sales, 0m, 8_000m, "USD", 0m, 100m, Acme, "INV-0009", 2),
            Leg("RCM", 2, new(2026, 10, 5), InrBank, 8_300m, 0m, "USD", 100m, 0m, Acme), Leg("RCM", 2, new(2026, 10, 5), Receivable, 0m, 8_000m, "USD", 0m, 100m, Acme),
            Leg("RCM", 2, new(2026, 10, 5), RealizedFx, 0m, 300m),
        ];
        List<FxSettlementLine> lines = [new() { Code = "RCM", Id = 2, Line = 1, MappedCode = "INV", MappedId = 1 }];

        ForeignCurrencyGainLossDetailsResultDto report = Run(ledger, lines: lines);

        // Nothing is open, and the settlement is still shown: its account is listed for it.
        ForeignCurrencyDetailAccountDto receivable = Account(report, "USD", Receivable);
        Assert.DoesNotContain(receivable.Documents, d => d.Kind == ForeignDocumentKind.Open);
        Assert.Equal((0m, 0m), (receivable.DueSource, receivable.CarryingBase));
        Assert.Equal("Settles INV-0009", Assert.Single(receivable.Documents).Reference);
    }

    [Fact]
    public void An_open_invoice_and_an_unapplied_advance_that_net_to_nothing_are_still_two_documents_under_a_listed_account()
    {
        List<JournalLedgerRead> ledger =
        [
            Leg("INV", 1, new(2026, 9, 1), Receivable, 8_000m, 0m, "USD", 100m, 0m, Acme, "INV-0010"), Leg("INV", 1, new(2026, 9, 1), Sales, 0m, 8_000m, "USD", 0m, 100m, Acme, "INV-0010", 2),
            Leg("RCM", 2, new(2026, 10, 5), InrBank, 8_000m, 0m, "USD", 100m, 0m, Acme), Leg("RCM", 2, new(2026, 10, 5), Receivable, 0m, 8_000m, "USD", 0m, 100m, Acme),
        ];

        ForeignCurrencyGainLossDetailsResultDto report = Run(ledger, lines: []);

        ForeignCurrencyDetailAccountDto receivable = Account(report, "USD", Receivable);
        Assert.Equal((0m, 0m), (receivable.DueSource, receivable.CarryingBase));
        Assert.Equal([100m, -100m], receivable.Documents.OrderBy(d => d.Date).Select(d => d.Amount!.Value));
    }

    [Fact]
    public void A_foreign_bank_accounts_postings_are_each_their_own_document_and_a_document_with_no_number_is_named_by_its_ledger_id()
    {
        ForeignCurrencyDocumentDto deposit = Assert.Single(Open(Run(), "USD", UsdBank));

        Assert.Equal(("RCM", "RCM-9005", 100m, 81m, 8_100m), (deposit.TypeCode, deposit.DocumentNo, deposit.Amount, deposit.BookedRate, deposit.CarryingBase));
        Assert.Equal((8_400m, 300m), (deposit.CurrentBase, deposit.Unrealized));
    }

    [Fact]
    public void An_invoice_after_the_to_date_is_in_no_document_and_an_earlier_date_has_the_earlier_balance()
    {
        // 5 November's invoice is in nothing at 31 October.
        Assert.DoesNotContain(Run().Currencies.SelectMany(c => c.Accounts).SelectMany(a => a.Documents), d => d.DocumentNo == "INV-0003");

        // At 30 September the October settlements have not happened: INV-0001 is open for USD 900.
        ForeignCurrencyGainLossDetailsResultDto september = Run(from: new DateOnly(2026, 9, 1), to: new DateOnly(2026, 9, 30));
        ForeignCurrencyDocumentDto invoice = Assert.Single(Open(september, "USD", Receivable), d => d.DocumentNo == "INV-0001");
        Assert.Equal((900m, 72_000m), (invoice.Amount, invoice.CarryingBase));
    }

    // ---- Settlements ------------------------------------------------------------------

    [Fact]
    public void A_settlement_row_names_what_it_settled_the_rate_it_was_booked_at_and_the_rate_the_money_moved_at()
    {
        ForeignCurrencyDocumentDto receipt = Assert.Single(Settlements(Run(), "USD", Receivable));

        Assert.Equal((new DateOnly(2026, 10, 12), "RCM", "RCM-0002"), (receipt.Date, receipt.TypeCode, receipt.DocumentNo));
        Assert.Equal(("Acme Exports", "Settles INV-0001"), (receipt.ContactName, receipt.Reference));

        // USD 400: booked at 80 (32,000), moved at 83 (33,200), a gain of 1,200.
        Assert.Equal((400m, 80m, 83m), (receipt.Amount, receipt.BookedRate, receipt.CurrentRate));
        Assert.Equal((32_000m, 33_200m, 1_200m), (receipt.CarryingBase, receipt.CurrentBase, receipt.Realized));
        Assert.Null(receipt.Unrealized);
    }

    [Fact]
    public void A_payment_and_a_loss_read_the_other_way_round_with_the_same_arithmetic()
    {
        ForeignCurrencyDocumentDto payment = Assert.Single(Settlements(Run(), "USD", Payable));

        // USD 200: booked at 82 (16,400), moved at 84 (16,800), a loss of 400.
        Assert.Equal(("SPM-0001", "Settles BIL-0001", "Globex Supplies"), (payment.DocumentNo, payment.Reference, payment.ContactName));
        Assert.Equal((200m, 82m, 84m), (payment.Amount, payment.BookedRate, payment.CurrentRate));
        Assert.Equal((16_400m, 16_800m, -400m), (payment.CarryingBase, payment.CurrentBase, payment.Realized));

        ForeignCurrencyDocumentDto eur = Assert.Single(Settlements(Run(), "EUR", Receivable));
        Assert.Equal((50m, 90m, 88m, -100m), (eur.Amount, eur.BookedRate, eur.CurrentRate, eur.Realized));
    }

    [Fact]
    public void A_settlement_before_the_period_is_not_a_row_in_it_but_is_in_its_own_period_and_both_ends_count()
    {
        Assert.DoesNotContain(Settlements(Run(), "USD", Receivable), d => d.DocumentNo == "RCM-0001");

        ForeignCurrencyGainLossDetailsResultDto september = Run(from: new DateOnly(2026, 9, 1), to: new DateOnly(2026, 9, 30));
        ForeignCurrencyDocumentDto earlier = Assert.Single(Settlements(september, "USD", Receivable));
        Assert.Equal(("RCM-0001", 200m, 82m), (earlier.DocumentNo, earlier.Realized, earlier.CurrentRate));

        // 12 October is the first day and the last: the receipt and nothing else.
        ForeignCurrencyGainLossDetailsResultDto one = Run(from: new DateOnly(2026, 10, 12), to: new DateOnly(2026, 10, 12));
        Assert.Equal(1_200m, one.TotalRealized);
    }

    [Fact]
    public void A_settlement_with_no_balance_leg_in_the_books_still_shows_its_realized_amount_and_leaves_the_rest_blank()
    {
        // The money document's line is gone from the ledger's foreign legs: only the exchange difference remains.
        List<JournalLedgerRead> ledger =
        [
            Leg("RCM", 5, new(2026, 10, 5), UsdBank, 100m, 0m, "USD", 1m, 0m), Leg("RCM", 5, new(2026, 10, 5), RealizedFx, 0m, 7m),
        ];

        ForeignCurrencyGainLossDetailsResultDto report = Run(ledger, lines: []);

        ForeignCurrencyDocumentDto row = Assert.Single(Settlements(report, "USD", UsdBank));
        Assert.Equal(7m, row.Realized);
        Assert.Null(row.Reference);
    }

    [Fact]
    public void A_posting_with_no_foreign_balance_is_listed_apart_and_counted_in_the_total()
    {
        ForeignCurrencyGainLossDetailsResultDto report = Run();

        ForeignCurrencyDocumentDto journal = Assert.Single(report.Unassigned);
        Assert.Equal(("JRN", "JRN-0001", 50m), (journal.TypeCode, journal.DocumentNo, journal.Realized));
        Assert.Equal(50m, report.UnassignedRealized);
        Assert.Equal(750m, report.TotalRealized);
    }

    // ---- Agreement with the summary ---------------------------------------------------

    [Fact]
    public void Each_currency_and_account_total_is_the_summarys_for_the_same_dates_and_rates()
    {
        ForeignCurrencyGainLossDetailsResultDto details = Run();

        IQueryable<JournalLedgerRead> ledger = Ledger.AsQueryable();
        IQueryable<AccountRead> accounts = Accounts.AsQueryable();
        ForeignCurrencyGainLossReportResultDto summary = ForeignCurrencyGainLossReportRepository.Compose(
            October1, October31, "INR", Accounts,
            [.. ForeignCurrencyGainLossReportRepository.Balances(ledger, accounts, "INR", October31)],
            [.. ForeignCurrencyGainLossReportRepository.Realized(ledger, accounts, "INR", October1, October31)],
            ForeignCurrencyGainLossReportRepository.RealizedLegs(ledger, accounts, October1, October31).Sum(l => l.CreditAmountBase - l.DebitAmountBase),
            BothRates, null);

        Assert.Equal(summary.Currencies.Select(c => c.CurrencyCode), details.Currencies.Select(c => c.CurrencyCode));

        foreach ((ForeignCurrencyBlockDto s, ForeignCurrencyDetailBlockDto d) in summary.Currencies.Zip(details.Currencies))
        {
            Assert.Equal((s.Rate, s.NeedsRate, s.DueSource, s.CarryingBase, s.RevaluedBase, s.Unrealized, s.Realized, s.Net),
                (d.Rate, d.NeedsRate, d.DueSource, d.CarryingBase, d.RevaluedBase, d.Unrealized, d.Realized, d.Net));

            Assert.Equal(
                s.Accounts.Select(a => (a.AccountId, a.DueSource, a.CarryingBase, a.RevaluedBase, a.Unrealized, a.Realized)),
                d.Accounts.Select(a => (a.AccountId, a.DueSource, a.CarryingBase, a.RevaluedBase, a.Unrealized, a.Realized)));
        }

        Assert.Equal((summary.TotalRealized, summary.TotalUnrealized, summary.TotalNet, summary.UnassignedRealized),
            (details.TotalRealized, details.TotalUnrealized, details.TotalNet, details.UnassignedRealized));
    }

    [Fact]
    public void The_documents_under_an_account_add_up_to_its_balance_and_its_gains_and_losses()
    {
        ForeignCurrencyGainLossDetailsResultDto report = Run();

        foreach (ForeignCurrencyDetailAccountDto account in report.Currencies.SelectMany(c => c.Accounts))
        {
            Assert.Equal(account.DueSource, account.Documents.Where(d => d.Kind == ForeignDocumentKind.Open).Sum(d => d.Amount));
            Assert.Equal(account.CarryingBase, account.Documents.Where(d => d.Kind == ForeignDocumentKind.Open).Sum(d => d.CarryingBase));
            Assert.Equal(account.Unrealized, account.Documents.Where(d => d.Kind == ForeignDocumentKind.Open).Sum(d => d.Unrealized));
            Assert.Equal(account.Realized, account.Documents.Where(d => d.Kind == ForeignDocumentKind.Settlement).Sum(d => d.Realized));
        }
    }

    [Fact]
    public void The_builders_the_report_composes_from_give_what_the_summarys_do()
    {
        IQueryable<JournalLedgerRead> ledger = Ledger.AsQueryable();
        IQueryable<AccountRead> accounts = Accounts.AsQueryable();

        List<ForeignLeg> legs = [.. ForeignCurrencyGainLossReportRepository.ForeignLegs(ledger, accounts, "INR").Where(l => l.LedgerDate <= October31)];
        Assert.Equal(
            ForeignCurrencyGainLossReportRepository.Balances(ledger, accounts, "INR", October31).AsEnumerable().OrderBy(b => b.AccountId).ThenBy(b => b.CurrencyCode).Select(b => (b.AccountId, b.CurrencyCode, b.DueSource, b.CarryingBase)),
            ForeignCurrencyGainLossDetailsReportRepository.BalancesOf(legs).OrderBy(b => b.AccountId).ThenBy(b => b.CurrencyCode).Select(b => (b.AccountId, b.CurrencyCode, b.DueSource, b.CarryingBase)));

        List<FxRealizedLeg> realized = [.. ForeignCurrencyGainLossDetailsReportRepository.RealizedLegDetails(ledger, accounts, "INR", October1, October31)];
        Assert.Equal(
            ForeignCurrencyGainLossReportRepository.Realized(ledger, accounts, "INR", October1, October31).AsEnumerable().OrderBy(r => r.AccountId).ThenBy(r => r.CurrencyCode).Select(r => (r.AccountId, r.CurrencyCode, r.Amount, r.Count)),
            ForeignCurrencyGainLossDetailsReportRepository.RealizedOf(realized).OrderBy(r => r.AccountId).ThenBy(r => r.CurrencyCode).Select(r => (r.AccountId, r.CurrencyCode, r.Amount, r.Count)));
    }

    // ---- Rates and filters ------------------------------------------------------------

    [Fact]
    public void A_currency_with_no_rate_has_no_revalued_or_unrealized_figure_on_any_document_and_is_named()
    {
        ForeignCurrencyGainLossDetailsResultDto report = Run(rates: new Dictionary<string, decimal> { ["USD"] = 84m });

        ForeignCurrencyDetailBlockDto eur = Block(report, "EUR");
        Assert.True(eur.NeedsRate);
        Assert.Null(eur.Unrealized);

        ForeignCurrencyDocumentDto invoice = Assert.Single(Open(report, "EUR", Receivable));
        Assert.Equal((150m, 90m, 13_500m), (invoice.Amount, invoice.BookedRate, invoice.CarryingBase));
        Assert.Null(invoice.CurrentRate);
        Assert.Null(invoice.CurrentBase);
        Assert.Null(invoice.Unrealized);

        // The settlement does not need a rate: it is what happened.
        Assert.Equal(-100m, Assert.Single(Settlements(report, "EUR", Receivable)).Realized);

        Assert.Equal(["EUR"], report.CurrenciesWithoutRate);
    }

    [Fact]
    public void One_contact_asked_for_has_only_their_documents_and_settlements_and_totals_of_their_own()
    {
        ForeignCurrencyGainLossDetailsResultDto globex = Run(contact: Globex);

        Assert.Equal(["USD"], globex.Currencies.Select(c => c.CurrencyCode));
        ForeignCurrencyDetailAccountDto payable = Assert.Single(Block(globex, "USD").Accounts);
        Assert.Equal(Payable, payable.AccountId);
        Assert.Equal(["BIL-0001", "SPM-0001"], payable.Documents.Select(d => d.DocumentNo));
        Assert.Equal((-400m, -600m, -1_000m), (globex.TotalRealized, globex.TotalUnrealized, globex.TotalNet));

        // The exchange difference carries no contact: a settlement is the contact of its balance leg's.
        Assert.Equal((1_100m, 2_050m), (Run(contact: Acme).TotalRealized, Run(contact: Acme).TotalUnrealized));
    }

    [Fact]
    public void Any_filter_leaves_out_what_no_currency_account_or_contact_can_claim()
    {
        Assert.Equal(50m, Run().UnassignedRealized);

        Assert.Empty(Run(contact: Acme).Unassigned);
        Assert.Empty(Run(account: Receivable).Unassigned);
        Assert.Empty(Run(only: "USD").Unassigned);
        Assert.Equal(0m, Run(only: "USD").UnassignedRealized);
    }

    [Fact]
    public void One_account_and_one_currency_asked_for_narrow_the_report_and_keep_the_summarys_figures_for_them()
    {
        ForeignCurrencyGainLossDetailsResultDto receivable = Run(account: Receivable);
        Assert.Equal(["EUR", "USD"], receivable.Currencies.Select(c => c.CurrencyCode));
        Assert.All(receivable.Currencies.SelectMany(c => c.Accounts), a => Assert.Equal(Receivable, a.AccountId));

        ForeignCurrencyGainLossDetailsResultDto usd = Run(only: "USD");
        Assert.Equal(["USD"], usd.Currencies.Select(c => c.CurrencyCode));
        Assert.Equal((800m, 1_600m, 2_400m), (usd.TotalRealized, usd.TotalUnrealized, usd.TotalNet));
    }

    // ---- The currencies, accounts and contacts the filters offer ------------------------

    [Fact]
    public void The_choices_are_every_currency_with_a_posting_and_say_which_still_have_a_balance()
    {
        List<JournalLedgerRead> ledger =
        [
            Leg("INV", 1, new(2026, 9, 1), Receivable, 9_000m, 0m, "GBP", 100m, 0m, Acme), Leg("INV", 1, new(2026, 9, 1), Sales, 0m, 9_000m, "GBP", 0m, 100m, Acme, line: 2),
            Leg("RCM", 2, new(2026, 10, 5), InrBank, 9_100m, 0m, "GBP", 100m, 0m, Acme), Leg("RCM", 2, new(2026, 10, 5), Receivable, 0m, 9_000m, "GBP", 0m, 100m, Acme),
            .. Ledger,
        ];

        List<ForeignLeg> legs = [.. ForeignCurrencyGainLossReportRepository.ForeignLegs(ledger.AsQueryable(), Accounts.AsQueryable(), "INR").Where(l => l.LedgerDate <= October31)];

        ForeignCurrencyDetailChoicesDto choices = ForeignCurrencyGainLossDetailsReportRepository.ChoicesOf(
            "INR", legs, Accounts.Where(a => a.AccountId is Receivable or Payable or UsdBank).ToList(),
            [new ForeignCurrencyContactChoiceDto { ContactId = Acme, ContactName = "Acme Exports" }]);

        Assert.Equal("INR", choices.BaseCurrencyCode);
        Assert.Equal([("EUR", true), ("GBP", false), ("USD", true)], choices.Currencies.Select(c => (c.CurrencyCode, c.HasBalance)));
        Assert.Equal([Receivable, Payable, UsdBank], choices.Accounts.Select(a => a.AccountId));
        Assert.Equal([ForeignAccountKind.Receivable, ForeignAccountKind.Payable, ForeignAccountKind.Bank], choices.Accounts.Select(a => a.Kind));
        Assert.Equal(Acme, Assert.Single(choices.Contacts).ContactId);
    }

    // ---- The request and the guards ---------------------------------------------------

    [Fact]
    public void The_two_dates_are_required_and_nothing_else_is()
    {
        Assert.Equal(
            [nameof(ForeignCurrencyGainLossDetailsRequest.FromDate), nameof(ForeignCurrencyGainLossDetailsRequest.ToDate)],
            typeof(ForeignCurrencyGainLossDetailsRequest).GetProperties()
                .Where(p => p.GetCustomAttribute<RequiredAttribute>() is not null)
                .Select(p => p.Name));
    }

    [Fact]
    public void A_missing_date_is_refused_at_its_field_by_the_request_itself()
    {
        var request = new ForeignCurrencyGainLossDetailsRequest();
        var results = new List<ValidationResult>();

        Assert.False(Validator.TryValidateObject(request, new ValidationContext(request), results, validateAllProperties: true));
        Assert.Equal(
            ["From date is required.", "To date is required."],
            results.Select(r => r.ErrorMessage).OrderBy(m => m, StringComparer.Ordinal));
    }

    [Fact]
    public void A_to_date_before_the_from_date_a_bad_currency_and_a_bad_rate_are_refused_at_their_own_fields()
    {
        Assert.Equal(
            [(nameof(ForeignCurrencyGainLossDetailsRequest.ToDate), "To date cannot be before the from date.")],
            ForeignCurrencyGainLossDetailsReportRepository.Refusals(new ForeignCurrencyGainLossDetailsRequest { FromDate = October31, ToDate = October1 }));

        Assert.Empty(ForeignCurrencyGainLossDetailsReportRepository.Refusals(new ForeignCurrencyGainLossDetailsRequest { FromDate = October1, ToDate = October1 }));

        Assert.Equal(
            [
                (nameof(ForeignCurrencyGainLossDetailsRequest.CurrencyCode), "Currency must be a 3-letter code."),
                ("Rates[USD]", "Rate must be greater than zero."),
                ("Rates[EU]", "Currency must be a 3-letter code."),
            ],
            ForeignCurrencyGainLossDetailsReportRepository.Refusals(new ForeignCurrencyGainLossDetailsRequest
            {
                FromDate = October1,
                ToDate = October31,
                CurrencyCode = "US",
                Rates = new Dictionary<string, decimal> { ["USD"] = 0m, ["EU"] = 1m, ["EUR"] = 91m },
            }));
    }

    [Fact]
    public async Task Another_branch_in_the_request_is_forbidden_not_hidden_on_both_endpoints()
    {
        var controller = new ForeignCurrencyGainLossDetailsReportController(null!)
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
            new ForeignCurrencyGainLossDetailsRequest { FromDate = October1, ToDate = October31, OrgId = Guid.NewGuid() }, default);
        IActionResult choices = await controller.Choices(
            new ForeignCurrencyDetailChoicesRequest { ToDate = October31, OrgId = Guid.NewGuid() }, default);

        Assert.IsType<ForbidResult>(report);
        Assert.IsType<ForbidResult>(choices);
    }

    [Fact]
    public void The_endpoints_need_the_reports_module_accounting_view_and_a_trade()
    {
        Type controller = typeof(ForeignCurrencyGainLossDetailsReportController);

        Assert.Equal("api/statements/foreign-currency-gain-or-loss-details", controller.GetCustomAttribute<RouteAttribute>()!.Template);
        Assert.Equal("reports", controller.GetCustomAttribute<RequireModulePermissionAttribute>()!.Module);
        Assert.NotNull(controller.GetCustomAttribute<RequireBusinessTypeAttribute>());

        foreach (string action in new[] { nameof(ForeignCurrencyGainLossDetailsReportController.Run), nameof(ForeignCurrencyGainLossDetailsReportController.Choices) })
        {
            Assert.Contains(
                "accounting.view",
                controller.GetMethod(action)!.GetCustomAttributes<RequirePermissionAttribute>().Select(a => a.Permission));
        }

        Assert.Equal("choices", controller.GetMethod(nameof(ForeignCurrencyGainLossDetailsReportController.Choices))!.GetCustomAttribute<HttpGetAttribute>()!.Template);
    }

    // ---- The catalog source, held to the same dates -------------------------------------

    [Fact]
    public void The_catalog_source_declares_both_dates_required()
    {
        var source = new FxGainLossDetailsSourceRepository(new FixedBaseCurrency("INR"));

        Assert.Equal(["from", "to"], source.Parameters.Select(p => p.Name));
        Assert.All(source.Parameters, p => Assert.True(p.IsRequired));
    }
}
