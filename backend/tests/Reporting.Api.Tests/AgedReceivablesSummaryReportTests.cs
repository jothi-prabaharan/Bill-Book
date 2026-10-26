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
/// The Aged Receivables Summary (TK-222), on books worked by hand. The branch's base
/// currency is INR and the report is as at 31 October 2026:
/// <list type="bullet">
/// <item><b>Acme Exports</b> (USD and EUR): an invoice of USD 1,000 at 80 on 10 August, due
/// 10 September, settled in part by receipts of USD 100 (20 September) and USD 400
/// (12 October), so 40,000 is open; an invoice of EUR 200 at 90 on 1 September, due
/// 1 October, with EUR 50 received on 15 October, so 13,500 is open; a receipt of USD 100
/// on 22 October applied to nothing (8,300 held); and an invoice of USD 50 on 5 November,
/// after the date.</item>
/// <item><b>Zenith Traders</b> (INR): an invoice of 20,000 on 1 June, due 1 July, less a
/// credit note of 5,000 applied to it, so 15,000 is open; an invoice of 10,000 due
/// 25 October with 4,000 received on 28 October, so 6,000; an invoice of 7,000 due
/// 20 November; and a credit note of 1,000 on 30 October applied to nothing.</item>
/// <item>A <b>manual journal</b> of 2,500 to Accounts Receivable on 1 August with no contact.</item>
/// <item>Settlements that must not count: one voided, and one effective on 6 November.</item>
/// </list>
/// By due date in 30-day columns Acme is −8,300, 13,500, 40,000, 0 and 0 (45,200), Zenith
/// 6,000, 6,000, 0, 0 and 15,000 (27,000), and the journal 2,500 in the last column; the
/// totals are −2,300, 19,500, 40,000, 0 and 17,500, which is the 74,700 Accounts
/// Receivable stands at. The builders run on lists through the same expressions the
/// repository runs against the read models; the composition is pure. Nothing here needs
/// PostgreSQL.
/// </summary>
public sealed class AgedReceivablesSummaryReportTests
{
    private const long Receivable = 1, Sales = 2, Returns = 3, Bank = 4;
    private const long Acme = 101, Zenith = 102, Idle = 103, VendorOnly = 104;

    private static readonly DateOnly October31 = new(2026, 10, 31);

    private static AccountRead Account(long id, string name, string? system = null) =>
        new() { AccountId = id, AccountCode = id.ToString(), AccountName = name, AccountSystemName = system, AccountTypeId = 1, IsActive = true };

    private static readonly List<AccountRead> Accounts =
    [
        Account(Receivable, "Accounts Receivable", ForeignCurrencyGainLossReportRepository.ReceivableAccountName),
        Account(Sales, "Sales Revenue"),
        Account(Returns, "Sales Returns"),
        Account(Bank, "Bank"),
    ];

    private static ContactRead Contact(long id, string code, string name, bool customer = true) =>
        new() { ContactId = id, ContactCode = code, DisplayName = name, IsCustomer = customer, CurrencyCode = "INR", IsActive = true };

    private static readonly List<ContactRead> Contacts =
    [
        Contact(Acme, "ACME", "Acme Exports"),
        Contact(Zenith, "ZENITH", "Zenith Traders"),
        Contact(Idle, "IDLE", "Idle Co"),
        Contact(VendorOnly, "VEND", "Vendor Only", customer: false),
    ];

    private static long _id;

    private static JournalLedgerRead Leg(
        string type, long transactionId, DateOnly on, long account, decimal debit, decimal credit, long? contact,
        decimal? sourceDebit = null, decimal? sourceCredit = null, string currency = "INR") =>
        new()
        {
            LedgerId = ++_id, LedgerDate = on, AccountId = account, TransactionTypeCode = type, TransactionId = transactionId,
            TransactionDetailId = 1, DebitAmountBase = debit, CreditAmountBase = credit,
            DebitAmount = sourceDebit ?? debit, CreditAmount = sourceCredit ?? credit, CurrencyCode = currency,
            ExchangeRate = 1m, ContactId = contact,
        };

    private static readonly List<JournalLedgerRead> Ledger =
    [
        // Acme: INV 1 of USD 1,000 at 80, receipts of USD 100 and USD 400 against it.
        Leg("INV", 1, new(2026, 8, 10), Receivable, 80_000m, 0m, Acme, 1_000m, 0m, "USD"), Leg("INV", 1, new(2026, 8, 10), Sales, 0m, 80_000m, Acme),
        Leg("RCM", 9, new(2026, 9, 20), Receivable, 0m, 8_000m, Acme, 0m, 100m, "USD"), Leg("RCM", 9, new(2026, 9, 20), Bank, 8_200m, 0m, Acme),
        Leg("RCM", 2, new(2026, 10, 12), Receivable, 0m, 32_000m, Acme, 0m, 400m, "USD"), Leg("RCM", 2, new(2026, 10, 12), Bank, 33_200m, 0m, Acme),
        // INV 6 of EUR 200 at 90, EUR 50 received.
        Leg("INV", 6, new(2026, 9, 1), Receivable, 18_000m, 0m, Acme, 200m, 0m, "EUR"), Leg("INV", 6, new(2026, 9, 1), Sales, 0m, 18_000m, Acme),
        Leg("RCM", 7, new(2026, 10, 15), Receivable, 0m, 4_500m, Acme, 0m, 50m, "EUR"), Leg("RCM", 7, new(2026, 10, 15), Bank, 4_400m, 0m, Acme),
        // A receipt of USD 100 applied to nothing, and an invoice after the date.
        Leg("RCM", 11, new(2026, 10, 22), Receivable, 0m, 8_300m, Acme, 0m, 100m, "USD"), Leg("RCM", 11, new(2026, 10, 22), Bank, 8_300m, 0m, Acme),
        Leg("INV", 10, new(2026, 11, 5), Receivable, 4_250m, 0m, Acme, 50m, 0m, "USD"), Leg("INV", 10, new(2026, 11, 5), Sales, 0m, 4_250m, Acme),

        // Zenith, in base currency.
        Leg("INV", 301, new(2026, 6, 1), Receivable, 20_000m, 0m, Zenith), Leg("INV", 301, new(2026, 6, 1), Sales, 0m, 20_000m, Zenith),
        Leg("CRN", 302, new(2026, 9, 15), Returns, 5_000m, 0m, Zenith), Leg("CRN", 302, new(2026, 9, 15), Receivable, 0m, 5_000m, Zenith),
        Leg("INV", 303, new(2026, 10, 10), Receivable, 10_000m, 0m, Zenith), Leg("INV", 303, new(2026, 10, 10), Sales, 0m, 10_000m, Zenith),
        Leg("INV", 304, new(2026, 10, 20), Receivable, 7_000m, 0m, Zenith), Leg("INV", 304, new(2026, 10, 20), Sales, 0m, 7_000m, Zenith),
        Leg("RCM", 305, new(2026, 10, 28), Bank, 4_000m, 0m, Zenith), Leg("RCM", 305, new(2026, 10, 28), Receivable, 0m, 4_000m, Zenith),
        Leg("CRN", 306, new(2026, 10, 30), Returns, 1_000m, 0m, Zenith), Leg("CRN", 306, new(2026, 10, 30), Receivable, 0m, 1_000m, Zenith),

        // A manual journal to Accounts Receivable with no contact.
        Leg("JRN", 307, new(2026, 8, 1), Receivable, 2_500m, 0m, null), Leg("JRN", 307, new(2026, 8, 1), Sales, 0m, 2_500m, null),
    ];

    private static TransactionRatioRead Ratio(string source, long sourceId, string target, long targetId, decimal amount, DateOnly on, bool voided = false) =>
        new()
        {
            TransactionRatioId = ++_id, SourceTransactionTypeCode = source, SourceTransactionId = sourceId,
            TargetTransactionTypeCode = target, TargetTransactionId = targetId, Amount = amount, AllocationDate = on, IsVoided = voided,
        };

    private static readonly List<TransactionRatioRead> Ratios =
    [
        // Receipts in their own currency: the invoice's rate takes each line to base.
        Ratio("RCM", 9, "INV", 1, 100m, new(2026, 9, 20)),
        Ratio("RCM", 2, "INV", 1, 400m, new(2026, 10, 12)),
        Ratio("RCM", 7, "INV", 6, 50m, new(2026, 10, 15)),
        // A credit note and a receipt in base currency.
        Ratio("CRN", 302, "INV", 301, 5_000m, new(2026, 9, 15)),
        Ratio("RCM", 305, "INV", 303, 4_000m, new(2026, 10, 28)),
        // Neither of these counts on 31 October: one voided, one effective in November.
        Ratio("RCM", 11, "INV", 1, 300m, new(2026, 10, 30), voided: true),
        Ratio("RCM", 11, "INV", 10, 20m, new(2026, 11, 6)),
    ];

    private static InvoiceRead Invoice(long id, DateOnly date, DateOnly? due, long contact) =>
        new() { InvoiceId = id, TransactionTypeCode = "INV", DocumentNo = $"INV-{id}", DocumentDate = date, DueDate = due, ContactId = contact, CurrencyCode = "INR" };

    private static readonly List<InvoiceRead> Invoices =
    [
        Invoice(1, new(2026, 8, 10), new(2026, 9, 10), Acme),
        Invoice(6, new(2026, 9, 1), new(2026, 10, 1), Acme),
        Invoice(10, new(2026, 11, 5), new(2026, 12, 5), Acme),
        Invoice(301, new(2026, 6, 1), new(2026, 7, 1), Zenith),
        Invoice(303, new(2026, 10, 10), new(2026, 10, 25), Zenith),
        Invoice(304, new(2026, 10, 20), new(2026, 11, 20), Zenith),
    ];

    private static IQueryable<ReceivableDocument> DocumentsOn(DateOnly asAt, long? contact = null)
    {
        IQueryable<JournalLedgerRead> ledger = Ledger.AsQueryable();
        if (contact is long chosen)
        {
            ledger = ledger.Where(l => l.ContactId == chosen);
        }

        return AgedReceivablesSummaryReportRepository.Documents(ledger, Accounts.AsQueryable(), asAt);
    }

    private static List<OpenReceivable> OpenOn(DateOnly asAt, long? contact = null)
    {
        IQueryable<ReceivableDocument> documents = DocumentsOn(asAt, contact);
        return
        [
            .. AgedReceivablesSummaryReportRepository.OpenDocuments(
                documents,
                AgedReceivablesSummaryReportRepository.Settlements(Ratios.AsQueryable(), documents, asAt),
                Invoices.AsQueryable()),
        ];
    }

    private static List<CustomerRef> Customers(bool everyCustomer = false) =>
    [
        .. Contacts
            .Where(c => everyCustomer ? c.IsCustomer : c.ContactId is Acme or Zenith)
            .Select(c => new CustomerRef { ContactId = c.ContactId, Code = c.ContactCode, Name = c.DisplayName }),
    ];

    /// <summary>The report, through the same builders and composition the repository uses.</summary>
    private static AgedReceivablesSummaryReportResultDto Run(
        AgeBasis? ageBy = null, int? bucketDays = null, long? contact = null, bool showZero = false, DateOnly? asAt = null)
    {
        DateOnly on = asAt ?? October31;
        var request = new AgedReceivablesSummaryReportRequest
        {
            AsAtDate = on, AgeBy = ageBy, BucketDays = bucketDays, ContactId = contact, ShowZeroBalances = showZero,
        };

        return AgedReceivablesSummaryReportRepository.Compose(request, OpenOn(on, contact), Customers(showZero), "INR");
    }

    private static AgedReceivablesSummaryRowDto Row(AgedReceivablesSummaryReportResultDto report, long contact) =>
        report.Customers.Single(c => c.ContactId == contact);

    private static decimal[] Columns(AgedReceivablesSummaryRowDto row) =>
        [row.Current, row.Bucket1, row.Bucket2, row.Bucket3, row.Bucket4];

    // ---- The documents ----------------------------------------------------------------

    [Fact]
    public void A_document_is_the_receivable_legs_that_one_posting_made_and_other_accounts_are_not_counted()
    {
        List<ReceivableDocument> documents = [.. DocumentsOn(October31)];

        // The sales and bank legs of every document are on other accounts.
        ReceivableDocument invoice = documents.Single(d => d is { TypeCode: "INV", DocumentId: 1 });
        Assert.Equal((80_000m, 1_000m, Acme), (invoice.NetBase, invoice.NetSource, invoice.ContactId));

        ReceivableDocument receipt = documents.Single(d => d is { TypeCode: "RCM", DocumentId: 2 });
        Assert.Equal((-32_000m, -400m), (receipt.NetBase, receipt.NetSource));
    }

    [Fact]
    public void A_posting_after_the_date_is_not_a_document_on_it()
    {
        Assert.DoesNotContain(DocumentsOn(October31), d => d is { TypeCode: "INV", DocumentId: 10 });
        Assert.Contains(DocumentsOn(new DateOnly(2026, 11, 5)), d => d is { TypeCode: "INV", DocumentId: 10 });
    }

    [Fact]
    public void A_posting_with_no_contact_is_a_document_with_none()
    {
        ReceivableDocument journal = DocumentsOn(October31).Single(d => d.TypeCode == "JRN");

        Assert.Null(journal.ContactId);
        Assert.Equal(2_500m, journal.NetBase);
    }

    // ---- Settlements ------------------------------------------------------------------

    [Fact]
    public void A_money_documents_line_is_taken_to_base_at_the_rate_its_target_was_booked_at()
    {
        IQueryable<ReceivableDocument> documents = DocumentsOn(October31);
        List<ReceivableSettlement> settlements = [.. AgedReceivablesSummaryReportRepository.Settlements(Ratios.AsQueryable(), documents, October31)];

        // USD 400 against an invoice of 80,000 for USD 1,000 is 32,000; USD 100 is 8,000; EUR 50 against 18,000 for EUR 200 is 4,500.
        Assert.Equal(32_000m, settlements.Single(s => s is { SourceTypeCode: "RCM", SourceId: 2 }).Amount);
        Assert.Equal(8_000m, settlements.Single(s => s is { SourceTypeCode: "RCM", SourceId: 9 }).Amount);
        Assert.Equal(4_500m, settlements.Single(s => s is { SourceTypeCode: "RCM", SourceId: 7 }).Amount);
    }

    [Fact]
    public void An_allocation_that_is_not_a_money_document_and_a_base_currency_receipt_are_taken_as_they_are()
    {
        IQueryable<ReceivableDocument> documents = DocumentsOn(October31);
        List<ReceivableSettlement> settlements = [.. AgedReceivablesSummaryReportRepository.Settlements(Ratios.AsQueryable(), documents, October31)];

        Assert.Equal(5_000m, settlements.Single(s => s is { SourceTypeCode: "CRN", SourceId: 302 }).Amount);
        Assert.Equal(4_000m, settlements.Single(s => s is { SourceTypeCode: "RCM", SourceId: 305 }).Amount);
    }

    [Fact]
    public void A_voided_settlement_and_one_effective_after_the_date_hold_nothing()
    {
        IQueryable<ReceivableDocument> documents = DocumentsOn(October31);
        List<ReceivableSettlement> settlements = [.. AgedReceivablesSummaryReportRepository.Settlements(Ratios.AsQueryable(), documents, October31)];

        Assert.DoesNotContain(settlements, s => s is { SourceTypeCode: "RCM", SourceId: 11 });
        Assert.Equal(5, settlements.Count);
    }

    [Fact]
    public void A_settlement_effective_on_the_date_counts_and_one_the_day_after_does_not()
    {
        IQueryable<ReceivableDocument> documents = DocumentsOn(new DateOnly(2026, 10, 12));
        List<ReceivableSettlement> onTheDay = [.. AgedReceivablesSummaryReportRepository.Settlements(Ratios.AsQueryable(), documents, new DateOnly(2026, 10, 12))];
        Assert.Contains(onTheDay, s => s is { SourceTypeCode: "RCM", SourceId: 2 });

        documents = DocumentsOn(new DateOnly(2026, 10, 11));
        List<ReceivableSettlement> dayBefore = [.. AgedReceivablesSummaryReportRepository.Settlements(Ratios.AsQueryable(), documents, new DateOnly(2026, 10, 11))];
        Assert.DoesNotContain(dayBefore, s => s is { SourceTypeCode: "RCM", SourceId: 2 });
    }

    // ---- Open documents ---------------------------------------------------------------

    [Fact]
    public void An_invoice_is_open_for_what_is_left_after_what_settled_it()
    {
        List<OpenReceivable> open = OpenOn(October31);

        Assert.Equal(40_000m, open.Single(o => o is { TypeCode: "INV", DocumentId: 1 }).Amount);
        Assert.Equal(13_500m, open.Single(o => o is { TypeCode: "INV", DocumentId: 6 }).Amount);
        Assert.Equal(15_000m, open.Single(o => o is { TypeCode: "INV", DocumentId: 301 }).Amount);
        Assert.Equal(6_000m, open.Single(o => o is { TypeCode: "INV", DocumentId: 303 }).Amount);
        Assert.Equal(7_000m, open.Single(o => o is { TypeCode: "INV", DocumentId: 304 }).Amount);
    }

    [Fact]
    public void A_receipt_or_credit_note_applied_in_full_is_gone_and_one_applied_to_nothing_is_negative()
    {
        List<OpenReceivable> open = OpenOn(October31);

        Assert.DoesNotContain(open, o => o is { TypeCode: "RCM", DocumentId: 2 or 9 or 7 or 305 });
        Assert.DoesNotContain(open, o => o is { TypeCode: "CRN", DocumentId: 302 });
        Assert.Equal(-8_300m, open.Single(o => o is { TypeCode: "RCM", DocumentId: 11 }).Amount);
        Assert.Equal(-1_000m, open.Single(o => o is { TypeCode: "CRN", DocumentId: 306 }).Amount);
    }

    [Fact]
    public void A_settlement_takes_the_same_figure_off_both_ends_so_the_documents_add_up_to_the_ledger()
    {
        // 74,700: the receivable account's balance on 31 October, whatever was applied to what.
        decimal ledger = Ledger.Where(l => l.AccountId == Receivable && l.LedgerDate <= October31).Sum(l => l.DebitAmountBase - l.CreditAmountBase);

        Assert.Equal(74_700m, ledger);
        Assert.Equal(ledger, OpenOn(October31).Sum(o => o.Amount));
    }

    [Fact]
    public void The_documents_add_up_to_the_ledger_on_any_other_date_too()
    {
        foreach (DateOnly on in new[] { new DateOnly(2026, 6, 30), new DateOnly(2026, 9, 30), new DateOnly(2026, 10, 15), new DateOnly(2026, 11, 30) })
        {
            decimal ledger = Ledger.Where(l => l.AccountId == Receivable && l.LedgerDate <= on).Sum(l => l.DebitAmountBase - l.CreditAmountBase);

            Assert.Equal(ledger, OpenOn(on).Sum(o => o.Amount));
        }
    }

    [Fact]
    public void An_invoice_brings_its_own_dates_and_any_other_document_its_ledger_date()
    {
        List<OpenReceivable> open = OpenOn(October31);

        OpenReceivable invoice = open.Single(o => o is { TypeCode: "INV", DocumentId: 1 });
        Assert.Equal((new DateOnly(2026, 8, 10), new DateOnly(2026, 9, 10)), (invoice.DocumentDate, invoice.DueDate));

        OpenReceivable journal = open.Single(o => o.TypeCode == "JRN");
        Assert.Equal((new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 1)), (journal.DocumentDate, journal.DueDate));
    }

    [Fact]
    public void An_invoice_with_no_due_date_is_due_on_its_invoice_date()
    {
        InvoiceRead[] invoices = [Invoice(500, new DateOnly(2026, 10, 5), null, Zenith)];
        JournalLedgerRead[] ledger = [Leg("INV", 500, new DateOnly(2026, 10, 5), Receivable, 900m, 0m, Zenith)];

        IQueryable<ReceivableDocument> documents = AgedReceivablesSummaryReportRepository.Documents(ledger.AsQueryable(), Accounts.AsQueryable(), October31);
        OpenReceivable only = Assert.Single(AgedReceivablesSummaryReportRepository.OpenDocuments(
            documents,
            AgedReceivablesSummaryReportRepository.Settlements(Array.Empty<TransactionRatioRead>().AsQueryable(), documents, October31),
            invoices.AsQueryable()));

        Assert.Equal(new DateOnly(2026, 10, 5), only.DueDate);
        Assert.Equal(900m, only.Amount);
    }

    [Fact]
    public void A_document_that_nets_to_nothing_is_not_open()
    {
        JournalLedgerRead[] ledger =
        [
            Leg("INV", 600, new DateOnly(2026, 10, 5), Receivable, 500m, 0m, Zenith),
            Leg("INV", 600, new DateOnly(2026, 10, 5), Receivable, 0m, 500m, Zenith),
        ];

        IQueryable<ReceivableDocument> documents = AgedReceivablesSummaryReportRepository.Documents(ledger.AsQueryable(), Accounts.AsQueryable(), October31);

        Assert.Empty(AgedReceivablesSummaryReportRepository.OpenDocuments(
            documents,
            AgedReceivablesSummaryReportRepository.Settlements(Array.Empty<TransactionRatioRead>().AsQueryable(), documents, October31),
            Array.Empty<InvoiceRead>().AsQueryable()));
    }

    // ---- The report -------------------------------------------------------------------

    [Fact]
    public void By_due_date_in_thirty_day_columns_each_customer_is_spread_over_the_five()
    {
        AgedReceivablesSummaryReportResultDto report = Run();

        Assert.Equal([-8_300m, 13_500m, 40_000m, 0m, 0m], Columns(Row(report, Acme)));
        Assert.Equal([6_000m, 6_000m, 0m, 0m, 15_000m], Columns(Row(report, Zenith)));
        Assert.Equal(45_200m, Row(report, Acme).Total);
        Assert.Equal(27_000m, Row(report, Zenith).Total);
    }

    [Fact]
    public void What_no_customer_claims_is_a_row_of_its_own_and_comes_last()
    {
        AgedReceivablesSummaryReportResultDto report = Run();

        AgedReceivablesSummaryRowDto last = report.Customers[^1];
        Assert.Null(last.ContactId);
        Assert.Equal(AgedReceivablesSummaryReportRepository.UnassignedName, last.ContactName);
        Assert.Equal([0m, 0m, 0m, 0m, 2_500m], Columns(last));
    }

    [Fact]
    public void The_totals_are_every_column_added_up_and_are_the_receivable_accounts_balance()
    {
        AgedReceivablesSummaryReportResultDto report = Run();

        Assert.Equal([-2_300m, 19_500m, 40_000m, 0m, 17_500m], Columns(report.Totals));
        Assert.Equal(74_700m, report.Totals.Total);
        Assert.Equal(report.Customers.Sum(c => c.Total), report.Totals.Total);
        Assert.All(report.Customers, c => Assert.Equal(Columns(c).Sum(), c.Total));
    }

    [Fact]
    public void A_credit_note_and_an_unapplied_receipt_are_negative_in_current()
    {
        AgedReceivablesSummaryReportResultDto report = Run();

        // Acme's 8,300 receipt; Zenith's 1,000 credit note comes off its 7,000 not yet due.
        Assert.Equal(-8_300m, Row(report, Acme).Current);
        Assert.Equal(6_000m, Row(report, Zenith).Current);
    }

    [Fact]
    public void A_document_due_on_the_date_is_not_yet_overdue()
    {
        var request = new AgedReceivablesSummaryReportRequest { AsAtDate = October31 };
        OpenReceivable[] open = [new() { ContactId = Acme, Amount = 100m, DocumentDate = new DateOnly(2026, 10, 1), DueDate = October31 }];

        AgedReceivablesSummaryReportResultDto report = AgedReceivablesSummaryReportRepository.Compose(request, open, Customers(), "INR");

        Assert.Equal([100m, 0m, 0m, 0m, 0m], Columns(Row(report, Acme)));
    }

    [Fact]
    public void Aged_by_invoice_date_each_invoice_counts_from_the_day_it_was_raised()
    {
        AgedReceivablesSummaryReportResultDto report = Run(ageBy: AgeBasis.InvoiceDate);

        // Acme's invoices are 82 and 60 days old; Zenith's are 152, 21 and 11, and the credit note is current.
        Assert.Equal([-8_300m, 0m, 13_500m, 40_000m, 0m], Columns(Row(report, Acme)));
        Assert.Equal([-1_000m, 13_000m, 0m, 0m, 15_000m], Columns(Row(report, Zenith)));
        Assert.Equal(AgeBasis.InvoiceDate, report.AgeBy);
        Assert.Equal(74_700m, report.Totals.Total);
    }

    [Fact]
    public void A_narrower_column_pushes_the_same_documents_further_right_and_the_total_does_not_move()
    {
        AgedReceivablesSummaryReportResultDto fifteen = Run(bucketDays: 15);
        AgedReceivablesSummaryReportResultDto seven = Run(bucketDays: 7);

        // Acme's 40,000 is 51 days overdue and its 13,500 is 30.
        Assert.Equal([-8_300m, 0m, 13_500m, 0m, 40_000m], Columns(Row(fifteen, Acme)));
        Assert.Equal([-8_300m, 0m, 0m, 0m, 53_500m], Columns(Row(seven, Acme)));
        Assert.Equal(74_700m, fifteen.Totals.Total);
        Assert.Equal(74_700m, seven.Totals.Total);
        Assert.Equal((15, 7), (fifteen.BucketDays, seven.BucketDays));
    }

    [Fact]
    public void The_defaults_are_the_due_date_and_thirty_days()
    {
        AgedReceivablesSummaryReportResultDto report = Run();

        Assert.Equal((AgeBasis.DueDate, 30), (report.AgeBy, report.BucketDays));
        Assert.Equal("INR", report.BaseCurrencyCode);
        Assert.Equal(October31, report.AsAtDate);
    }

    [Fact]
    public void The_report_is_as_at_the_date_given_and_ignores_what_came_after()
    {
        AgedReceivablesSummaryReportResultDto september = Run(asAt: new DateOnly(2026, 9, 30));

        // Acme's invoice of 1 September is not yet due; the 10 August one has 8,000 received and is 20 days overdue.
        Assert.Equal([18_000m, 72_000m, 0m, 0m, 0m], Columns(Row(september, Acme)));
        Assert.Equal([0m, 0m, 0m, 0m, 15_000m], Columns(Row(september, Zenith)));
        Assert.Equal(107_500m, september.Totals.Total);
    }

    [Fact]
    public void One_customer_can_be_asked_for_and_the_journal_with_no_customer_is_then_out()
    {
        AgedReceivablesSummaryReportResultDto report = Run(contact: Zenith);

        Assert.Equal([Zenith], report.Customers.Select(c => c.ContactId ?? 0));
        Assert.Equal(27_000m, report.Totals.Total);
    }

    [Fact]
    public void Customers_sort_by_name()
    {
        AgedReceivablesSummaryReportResultDto report = Run();

        Assert.Equal(["Acme Exports", "Zenith Traders", AgedReceivablesSummaryReportRepository.UnassignedName], report.Customers.Select(c => c.ContactName));
    }

    [Fact]
    public void A_customer_with_nothing_open_is_left_out_unless_the_zero_ones_are_asked_for()
    {
        Assert.DoesNotContain(Run().Customers, c => c.ContactId == Idle);

        AgedReceivablesSummaryReportResultDto withZero = Run(showZero: true);
        AgedReceivablesSummaryRowDto idle = Row(withZero, Idle);
        Assert.Equal([0m, 0m, 0m, 0m, 0m], Columns(idle));
        Assert.Equal(0m, idle.Total);
        Assert.Equal("Idle Co", idle.ContactName);

        // A contact that is not a customer is not offered as a zero row.
        Assert.DoesNotContain(withZero.Customers, c => c.ContactId == VendorOnly);
        Assert.Equal(74_700m, withZero.Totals.Total);
    }

    [Fact]
    public void A_customer_whose_open_documents_cancel_in_every_column_has_nothing_owed()
    {
        var request = new AgedReceivablesSummaryReportRequest { AsAtDate = October31 };
        OpenReceivable[] open =
        [
            new() { ContactId = Acme, Amount = 100m, DocumentDate = October31, DueDate = October31.AddDays(10) },
            new() { ContactId = Acme, Amount = -100m, DocumentDate = October31, DueDate = October31 },
        ];

        Assert.Empty(AgedReceivablesSummaryReportRepository.Compose(request, open, Customers(), "INR").Customers);

        request.ShowZeroBalances = true;
        Assert.Single(AgedReceivablesSummaryReportRepository.Compose(request, open, Customers(), "INR").Customers, c => c.ContactId == Acme);
    }

    [Fact]
    public void A_contact_the_names_do_not_know_is_still_listed_by_its_number()
    {
        var request = new AgedReceivablesSummaryReportRequest { AsAtDate = October31 };
        OpenReceivable[] open = [new() { ContactId = 77, Amount = 10m, DocumentDate = October31, DueDate = October31 }];

        AgedReceivablesSummaryRowDto row = Assert.Single(AgedReceivablesSummaryReportRepository.Compose(request, open, [], "INR").Customers);

        Assert.Equal("Customer 77", row.ContactName);
        Assert.Equal(77L, row.ContactId);
    }

    [Fact]
    public void An_empty_book_is_an_empty_report_with_totals_of_nothing()
    {
        var request = new AgedReceivablesSummaryReportRequest { AsAtDate = October31 };

        AgedReceivablesSummaryReportResultDto report = AgedReceivablesSummaryReportRepository.Compose(request, [], [], "INR");

        Assert.Empty(report.Customers);
        Assert.Equal([0m, 0m, 0m, 0m, 0m], Columns(report.Totals));
        Assert.Equal(5, report.BucketLabels.Count);
    }

    // ---- Columns ----------------------------------------------------------------------

    [Theory]
    [InlineData(-5, 30, 0)]
    [InlineData(0, 30, 0)]
    [InlineData(1, 30, 1)]
    [InlineData(30, 30, 1)]
    [InlineData(31, 30, 2)]
    [InlineData(60, 30, 2)]
    [InlineData(61, 30, 3)]
    [InlineData(90, 30, 3)]
    [InlineData(91, 30, 4)]
    [InlineData(7, 7, 1)]
    [InlineData(8, 7, 2)]
    [InlineData(21, 7, 3)]
    [InlineData(22, 7, 4)]
    [InlineData(15, 15, 1)]
    [InlineData(46, 15, 4)]
    public void A_document_falls_in_the_column_its_days_overdue_give(int days, int width, int expected) =>
        Assert.Equal(expected, AgedReceivablesSummaryReportRepository.ColumnFor(days, width));

    [Fact]
    public void The_headings_name_each_range_for_the_width_in_use()
    {
        Assert.Equal(["Current", "1-30 days", "31-60 days", "61-90 days", "Over 90 days"], AgedReceivablesSummaryReportRepository.BucketLabels(30));
        Assert.Equal(["Current", "1-7 days", "8-14 days", "15-21 days", "Over 21 days"], AgedReceivablesSummaryReportRepository.BucketLabels(7));
        Assert.Equal(["Current", "1-15 days", "16-30 days", "31-45 days", "Over 45 days"], AgedReceivablesSummaryReportRepository.BucketLabels(15));
    }

    // ---- The request and the guards ---------------------------------------------------

    [Fact]
    public void The_as_at_date_is_required_and_nothing_else_is()
    {
        Assert.Equal(
            [nameof(AgedReceivablesSummaryReportRequest.AsAtDate)],
            typeof(AgedReceivablesSummaryReportRequest).GetProperties()
                .Where(p => p.GetCustomAttribute<RequiredAttribute>() is not null)
                .Select(p => p.Name));
    }

    [Fact]
    public void A_missing_date_is_refused_at_its_field_by_the_request_itself()
    {
        var request = new AgedReceivablesSummaryReportRequest();
        var results = new List<ValidationResult>();

        Assert.False(Validator.TryValidateObject(request, new ValidationContext(request), results, validateAllProperties: true));
        Assert.Equal(["As at date is required."], results.Select(r => r.ErrorMessage));
    }

    [Fact]
    public void A_bucket_width_other_than_seven_fifteen_or_thirty_is_refused_at_its_field()
    {
        Assert.Equal(
            [(nameof(AgedReceivablesSummaryReportRequest.BucketDays), "Bucket size must be 7, 15 or 30 days.")],
            AgedReceivablesSummaryReportRepository.Refusals(new AgedReceivablesSummaryReportRequest { AsAtDate = October31, BucketDays = 10 }));

        foreach (int width in new[] { 7, 15, 30 })
        {
            Assert.Empty(AgedReceivablesSummaryReportRepository.Refusals(new AgedReceivablesSummaryReportRequest { AsAtDate = October31, BucketDays = width }));
        }

        Assert.Empty(AgedReceivablesSummaryReportRepository.Refusals(new AgedReceivablesSummaryReportRequest { AsAtDate = October31 }));
    }

    [Fact]
    public void An_age_basis_that_is_not_one_of_the_two_is_refused_at_its_field()
    {
        Assert.Equal(
            [(nameof(AgedReceivablesSummaryReportRequest.AgeBy), "Age by must be the due date or the invoice date.")],
            AgedReceivablesSummaryReportRepository.Refusals(new AgedReceivablesSummaryReportRequest { AsAtDate = October31, AgeBy = (AgeBasis)9 }));
    }

    [Fact]
    public async Task Another_branch_in_the_request_is_forbidden_not_hidden()
    {
        var controller = new AgedReceivablesSummaryReportController(null!)
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
            new AgedReceivablesSummaryReportRequest { AsAtDate = October31, OrgId = Guid.NewGuid() }, default);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public void The_endpoint_needs_the_reports_module_sales_view_and_a_trade()
    {
        Type controller = typeof(AgedReceivablesSummaryReportController);

        Assert.Equal("api/statements/aged-receivables-summary", controller.GetCustomAttribute<RouteAttribute>()!.Template);
        Assert.Equal("reports", controller.GetCustomAttribute<RequireModulePermissionAttribute>()!.Module);
        Assert.NotNull(controller.GetCustomAttribute<RequireBusinessTypeAttribute>());

        foreach (string action in new[] { nameof(AgedReceivablesSummaryReportController.Run), nameof(AgedReceivablesSummaryReportController.Customers) })
        {
            Assert.Contains(
                "sales.view",
                controller.GetMethod(action)!.GetCustomAttributes<RequirePermissionAttribute>().Select(a => a.Permission));
        }
    }

    [Fact]
    public void The_customers_lookup_is_a_sub_route_of_the_report()
    {
        Assert.Equal(
            "customers",
            typeof(AgedReceivablesSummaryReportController).GetMethod(nameof(AgedReceivablesSummaryReportController.Customers))!.GetCustomAttribute<HttpGetAttribute>()!.Template);
    }

    // ---- The catalog source, held to the same date ------------------------------------

    [Fact]
    public void The_catalog_source_declares_the_as_at_date_required()
    {
        var source = new ArAgingSummarySourceRepository();

        ReportParameterRepository date = Assert.Single(source.Parameters);
        Assert.Equal("asOf", date.Name);
        Assert.True(date.IsRequired);
    }

    [Fact]
    public void The_catalog_source_keeps_the_columns_the_catalog_names()
    {
        var source = new ArAgingSummarySourceRepository();

        Assert.Equal(
            ["contactCode", "contactName", "current", "days1_30", "days31_60", "days61_90", "days90Plus", "total", "contactId"],
            source.Columns.Select(c => c.Key));
    }
}
