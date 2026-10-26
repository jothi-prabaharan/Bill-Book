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
/// The Aged Receivables Detail (TK-223), on books worked by hand. The branch's base currency is
/// INR and the report is as at 31 October 2026. Two customers and a journal with no customer:
/// <list type="bullet">
/// <item><b>Acme Exports</b>: an invoice of 80,000 raised 10 August and due 10 September, with
/// 40,000 applied (51 days overdue); an invoice of 18,000 raised 1 September and due 1 October,
/// with 4,500 applied (30 days); and a receipt of 8,300 on 22 October applied to nothing.</item>
/// <item><b>Zenith Traders</b>: an invoice of 20,000 raised 1 June and due 1 July with a credit
/// note of 5,000 applied (122 days); an invoice of 10,000 due 25 October with 4,000 applied
/// (6 days); an invoice of 7,000 due 20 November (not yet due); and a credit note of 1,000 on
/// 30 October applied to nothing.</item>
/// <item>A <b>manual journal</b> of 2,500 on 1 August with no contact (91 days).</item>
/// </list>
/// By due date in 30-day columns Acme is −8,300, 13,500, 40,000, 0 and 0 (45,200), Zenith 6,000,
/// 6,000, 0, 0 and 15,000 (27,000) and the journal 2,500 in the last column: the Aged Receivables
/// Summary's figures, which the subtotals and totals here must equal. The composition is pure and
/// the builders run on lists through the same expressions the repository runs against the read
/// models. Nothing here needs PostgreSQL.
/// </summary>
public sealed class AgedReceivablesDetailReportTests
{
    private const long Acme = 101, Zenith = 102;
    private const long Receivable = 1, Sales = 2, Returns = 3;

    private static readonly DateOnly October31 = new(2026, 10, 31);

    private static OpenReceivable Open(
        string type, long id, long? contact, string? number, DateOnly date, DateOnly due, decimal original, decimal outstanding) =>
        new()
        {
            TypeCode = type, DocumentId = id, ContactId = contact, DocumentNo = number, DocumentDate = date, DueDate = due,
            Original = original, Amount = outstanding,
        };

    private static readonly List<OpenReceivable> OpenDocuments =
    [
        Open("INV", 1, Acme, "INV-0001", new(2026, 8, 10), new(2026, 9, 10), 80_000m, 40_000m),
        Open("INV", 6, Acme, "INV-0002", new(2026, 9, 1), new(2026, 10, 1), 18_000m, 13_500m),
        Open("RCM", 11, Acme, null, new(2026, 10, 22), new(2026, 10, 22), -8_300m, -8_300m),
        Open("INV", 301, Zenith, "INV-9301", new(2026, 6, 1), new(2026, 7, 1), 20_000m, 15_000m),
        Open("INV", 303, Zenith, "INV-9303", new(2026, 10, 10), new(2026, 10, 25), 10_000m, 6_000m),
        Open("INV", 304, Zenith, "INV-9304", new(2026, 10, 20), new(2026, 11, 20), 7_000m, 7_000m),
        Open("CRN", 306, Zenith, "CRN-9306", new(2026, 10, 30), new(2026, 10, 30), -1_000m, -1_000m),
        Open("JRN", 307, null, "JRN-9307", new(2026, 8, 1), new(2026, 8, 1), 2_500m, 2_500m),
    ];

    private static readonly List<CustomerRef> Customers =
    [
        new() { ContactId = Acme, Code = "ACME", Name = "Acme Exports" },
        new() { ContactId = Zenith, Code = "ZENITH", Name = "Zenith Traders" },
    ];

    private static readonly List<MoneyNumber> Numbers = [new() { TypeCode = "RCM", DocumentId = 11, Number = "RCM-0004" }];

    private static AgedReceivablesDetailReportResultDto Run(
        AgeBasis? ageBy = null, int? bucketDays = null, DateOnly? asAt = null, IReadOnlyList<OpenReceivable>? open = null)
    {
        var request = new AgedReceivablesDetailReportRequest { AsAtDate = asAt ?? October31, AgeBy = ageBy, BucketDays = bucketDays };
        return AgedReceivablesDetailReportRepository.Compose(request, open ?? OpenDocuments, Customers, Numbers, "INR");
    }

    private static AgedReceivablesDetailCustomerDto Customer(AgedReceivablesDetailReportResultDto report, long? contact) =>
        report.Customers.Single(c => c.ContactId == contact);

    private static AgedReceivablesDetailDocumentDto Doc(AgedReceivablesDetailReportResultDto report, string type, long id) =>
        report.Customers.SelectMany(c => c.Documents).Single(d => d.TypeCode == type && d.DocumentId == id);

    private static decimal[] Columns(AgedReceivablesDetailDocumentDto d) => [d.Current, d.Bucket1, d.Bucket2, d.Bucket3, d.Bucket4];

    private static decimal[] Columns(AgedReceivablesDetailTotalsDto t) => [t.Current, t.Bucket1, t.Bucket2, t.Bucket3, t.Bucket4];

    // ---- A document -------------------------------------------------------------------

    [Fact]
    public void A_document_shows_what_it_came_to_what_has_been_applied_and_what_is_left()
    {
        AgedReceivablesDetailDocumentDto invoice = Doc(Run(), "INV", 1);

        Assert.Equal((80_000m, 40_000m, 40_000m), (invoice.Original, invoice.Settled, invoice.Outstanding));
        Assert.Equal(ReceivableDocumentKind.Invoice, invoice.Kind);
        Assert.Equal("INV-0001", invoice.DocumentNo);
    }

    [Fact]
    public void A_credit_note_or_receipt_applied_to_nothing_is_negative_and_has_nothing_settled()
    {
        AgedReceivablesDetailReportResultDto report = Run();

        AgedReceivablesDetailDocumentDto credit = Doc(report, "CRN", 306);
        Assert.Equal((-1_000m, 0m, -1_000m), (credit.Original, credit.Settled, credit.Outstanding));

        AgedReceivablesDetailDocumentDto receipt = Doc(report, "RCM", 11);
        Assert.Equal((-8_300m, 0m, -8_300m), (receipt.Original, receipt.Settled, receipt.Outstanding));
    }

    [Fact]
    public void A_credit_applied_in_part_keeps_its_sign_on_all_three_figures()
    {
        OpenReceivable[] open = [Open("CRN", 9, Zenith, "CRN-9", new(2026, 10, 1), new(2026, 10, 1), -5_000m, -3_000m)];

        AgedReceivablesDetailDocumentDto credit = Doc(Run(open: open), "CRN", 9);

        Assert.Equal((-5_000m, -2_000m, -3_000m), (credit.Original, credit.Settled, credit.Outstanding));
    }

    [Fact]
    public void Original_less_settled_is_always_what_is_outstanding()
    {
        foreach (AgedReceivablesDetailDocumentDto d in Run().Customers.SelectMany(c => c.Documents))
        {
            Assert.Equal(d.Outstanding, d.Original - d.Settled);
        }
    }

    [Theory]
    [InlineData("INV", ReceivableDocumentKind.Invoice)]
    [InlineData("POS", ReceivableDocumentKind.Invoice)]
    [InlineData("CRN", ReceivableDocumentKind.CreditNote)]
    [InlineData("RCM", ReceivableDocumentKind.Receipt)]
    [InlineData("SPM", ReceivableDocumentKind.Payment)]
    [InlineData("JRN", ReceivableDocumentKind.Journal)]
    [InlineData("OPB", ReceivableDocumentKind.OpeningBalance)]
    [InlineData("XYZ", ReceivableDocumentKind.Other)]
    public void A_document_is_what_the_code_on_its_postings_says(string code, ReceivableDocumentKind kind) =>
        Assert.Equal(kind, AgedReceivablesDetailReportRepository.KindOf(code));

    [Fact]
    public void Only_an_invoice_has_a_due_date()
    {
        AgedReceivablesDetailReportResultDto report = Run();

        Assert.Equal(new DateOnly(2026, 9, 10), Doc(report, "INV", 1).DueDate);
        Assert.Null(Doc(report, "CRN", 306).DueDate);
        Assert.Null(Doc(report, "JRN", 307).DueDate);
        Assert.Null(Doc(report, "RCM", 11).DueDate);
    }

    // ---- Numbers ----------------------------------------------------------------------

    [Fact]
    public void A_number_on_the_ledger_is_used_and_a_receipts_own_number_fills_a_gap()
    {
        AgedReceivablesDetailReportResultDto report = Run();

        Assert.Equal("INV-0001", Doc(report, "INV", 1).DocumentNo);
        Assert.Equal("RCM-0004", Doc(report, "RCM", 11).DocumentNo);
    }

    [Fact]
    public void A_document_with_no_number_anywhere_reads_as_its_code_and_id()
    {
        OpenReceivable[] open = [Open("RCM", 5, Acme, null, new(2026, 10, 1), new(2026, 10, 1), -10m, -10m)];

        Assert.Equal("RCM-5", Doc(Run(open: open), "RCM", 5).DocumentNo);
    }

    [Fact]
    public void A_receipts_number_is_not_taken_for_a_payments_with_the_same_id()
    {
        OpenReceivable[] open =
        [
            Open("SPM", 11, Acme, null, new(2026, 10, 1), new(2026, 10, 1), 10m, 10m),
        ];

        // The only number on offer is for receipt 11, not payment 11.
        Assert.Equal("SPM-11", Doc(Run(open: open), "SPM", 11).DocumentNo);
    }

    // ---- Ageing -----------------------------------------------------------------------

    [Fact]
    public void An_amount_sits_in_the_one_column_its_age_gives_by_due_date()
    {
        AgedReceivablesDetailReportResultDto report = Run();

        Assert.Equal([0m, 0m, 40_000m, 0m, 0m], Columns(Doc(report, "INV", 1)));
        Assert.Equal([0m, 13_500m, 0m, 0m, 0m], Columns(Doc(report, "INV", 6)));
        Assert.Equal([0m, 0m, 0m, 0m, 15_000m], Columns(Doc(report, "INV", 301)));
        Assert.Equal([7_000m, 0m, 0m, 0m, 0m], Columns(Doc(report, "INV", 304)));
        Assert.Equal([0m, 0m, 0m, 0m, 2_500m], Columns(Doc(report, "JRN", 307)));
    }

    [Fact]
    public void A_credit_is_in_current_whatever_its_age()
    {
        OpenReceivable[] open = [Open("CRN", 9, Zenith, "CRN-9", new(2025, 1, 1), new(2025, 1, 1), -400m, -400m)];

        Assert.Equal([-400m, 0m, 0m, 0m, 0m], Columns(Doc(Run(open: open), "CRN", 9)));
    }

    [Fact]
    public void Aged_by_invoice_date_each_invoice_counts_from_the_day_it_was_raised()
    {
        AgedReceivablesDetailReportResultDto report = Run(ageBy: AgeBasis.InvoiceDate);

        // 82 and 60 days for Acme's invoices; 152, 21 and 11 for Zenith's.
        Assert.Equal([0m, 0m, 0m, 40_000m, 0m], Columns(Doc(report, "INV", 1)));
        Assert.Equal([0m, 0m, 13_500m, 0m, 0m], Columns(Doc(report, "INV", 6)));
        Assert.Equal([0m, 6_000m, 0m, 0m, 0m], Columns(Doc(report, "INV", 303)));
        Assert.Equal([0m, 7_000m, 0m, 0m, 0m], Columns(Doc(report, "INV", 304)));
        Assert.Equal(AgeBasis.InvoiceDate, report.AgeBy);
    }

    [Fact]
    public void A_narrower_column_moves_documents_right()
    {
        AgedReceivablesDetailReportResultDto fifteen = Run(bucketDays: 15);
        AgedReceivablesDetailReportResultDto seven = Run(bucketDays: 7);

        Assert.Equal([0m, 0m, 0m, 0m, 40_000m], Columns(Doc(fifteen, "INV", 1)));
        Assert.Equal([0m, 0m, 13_500m, 0m, 0m], Columns(Doc(fifteen, "INV", 6)));
        Assert.Equal([0m, 0m, 0m, 0m, 13_500m], Columns(Doc(seven, "INV", 6)));
        Assert.Equal((15, 7), (fifteen.BucketDays, seven.BucketDays));
        Assert.Equal(["Current", "1-15 days", "16-30 days", "31-45 days", "Over 45 days"], fifteen.BucketLabels);
    }

    [Fact]
    public void Days_overdue_is_how_far_past_the_date_counted_from_the_report_is_for_what_is_owed()
    {
        AgedReceivablesDetailReportResultDto byDue = Run();

        Assert.Equal(51, Doc(byDue, "INV", 1).DaysOverdue);
        Assert.Equal(30, Doc(byDue, "INV", 6).DaysOverdue);
        Assert.Equal(6, Doc(byDue, "INV", 303).DaysOverdue);
        Assert.Equal(91, Doc(byDue, "JRN", 307).DaysOverdue);

        // By invoice date the days run from the day it was raised.
        Assert.Equal(82, Doc(Run(ageBy: AgeBasis.InvoiceDate), "INV", 1).DaysOverdue);
    }

    [Fact]
    public void Nothing_is_overdue_for_what_is_not_yet_due_a_document_due_on_the_date_or_a_credit()
    {
        AgedReceivablesDetailReportResultDto report = Run();

        Assert.Null(Doc(report, "INV", 304).DaysOverdue);
        Assert.Null(Doc(report, "CRN", 306).DaysOverdue);
        Assert.Null(Doc(report, "RCM", 11).DaysOverdue);

        OpenReceivable[] onTheDay = [Open("INV", 8, Acme, "INV-8", new(2026, 10, 1), October31, 50m, 50m)];
        Assert.Null(Doc(Run(open: onTheDay), "INV", 8).DaysOverdue);
    }

    // ---- The grouping -----------------------------------------------------------------

    [Fact]
    public void Customers_sort_by_name_and_what_no_customer_claims_comes_last()
    {
        AgedReceivablesDetailReportResultDto report = Run();

        Assert.Equal(["Acme Exports", "Zenith Traders", AgedReceivablesSummaryReportRepository.UnassignedName], report.Customers.Select(c => c.ContactName));
        Assert.Null(report.Customers[^1].ContactId);
    }

    [Fact]
    public void A_customers_documents_run_oldest_first_then_by_code_and_id()
    {
        AgedReceivablesDetailReportResultDto report = Run();

        Assert.Equal([301L, 303L, 304L, 306L], Customer(report, Zenith).Documents.Select(d => d.DocumentId));

        OpenReceivable[] tied =
        [
            Open("RCM", 2, Acme, "R2", new(2026, 10, 1), new(2026, 10, 1), -1m, -1m),
            Open("INV", 9, Acme, "I9", new(2026, 10, 1), new(2026, 10, 1), 1m, 1m),
            Open("INV", 3, Acme, "I3", new(2026, 10, 1), new(2026, 10, 1), 1m, 1m),
        ];
        Assert.Equal(["I3", "I9", "R2"], Customer(Run(open: tied), Acme).Documents.Select(d => d.DocumentNo));
    }

    [Fact]
    public void A_contact_the_names_do_not_know_is_still_listed_by_its_number()
    {
        OpenReceivable[] open = [Open("INV", 1, 77, "INV-1", new(2026, 10, 1), new(2026, 10, 1), 10m, 10m)];

        Assert.Equal("Customer 77", Assert.Single(Run(open: open).Customers).ContactName);
    }

    // ---- The figures ------------------------------------------------------------------

    [Fact]
    public void A_customers_subtotal_adds_its_documents_in_every_column()
    {
        AgedReceivablesDetailReportResultDto report = Run();

        AgedReceivablesDetailTotalsDto acme = Customer(report, Acme).Subtotal;
        Assert.Equal([-8_300m, 13_500m, 40_000m, 0m, 0m], Columns(acme));
        Assert.Equal((89_700m, 44_500m, 45_200m), (acme.Original, acme.Settled, acme.Outstanding));

        AgedReceivablesDetailTotalsDto zenith = Customer(report, Zenith).Subtotal;
        Assert.Equal([6_000m, 6_000m, 0m, 0m, 15_000m], Columns(zenith));
        Assert.Equal((36_000m, 9_000m, 27_000m), (zenith.Original, zenith.Settled, zenith.Outstanding));
    }

    [Fact]
    public void The_totals_add_every_document_and_are_the_receivable_accounts_balance()
    {
        AgedReceivablesDetailReportResultDto report = Run();

        Assert.Equal([-2_300m, 19_500m, 40_000m, 0m, 17_500m], Columns(report.Totals));
        Assert.Equal(74_700m, report.Totals.Outstanding);
        Assert.Equal(report.Customers.Sum(c => c.Subtotal.Outstanding), report.Totals.Outstanding);
        Assert.Equal(Columns(report.Totals).Sum(), report.Totals.Outstanding);
    }

    [Fact]
    public void Every_customers_subtotal_and_every_column_total_equal_the_summarys()
    {
        var detailRequest = new AgedReceivablesDetailReportRequest { AsAtDate = October31 };
        var summaryRequest = new AgedReceivablesSummaryReportRequest { AsAtDate = October31 };

        foreach ((AgeBasis? ageBy, int? width) in new (AgeBasis?, int?)[] { (null, null), (AgeBasis.InvoiceDate, 15), (AgeBasis.DueDate, 7) })
        {
            detailRequest.AgeBy = summaryRequest.AgeBy = ageBy;
            detailRequest.BucketDays = summaryRequest.BucketDays = width;

            AgedReceivablesDetailReportResultDto detail = AgedReceivablesDetailReportRepository.Compose(detailRequest, OpenDocuments, Customers, Numbers, "INR");
            AgedReceivablesSummaryReportResultDto summary = AgedReceivablesSummaryReportRepository.Compose(summaryRequest, OpenDocuments, Customers, "INR");

            Assert.Equal(summary.Customers.Select(c => c.ContactId), detail.Customers.Select(c => c.ContactId));

            for (int i = 0; i < summary.Customers.Count; i++)
            {
                AgedReceivablesSummaryRowDto s = summary.Customers[i];
                AgedReceivablesDetailTotalsDto d = detail.Customers[i].Subtotal;

                Assert.Equal([s.Current, s.Bucket1, s.Bucket2, s.Bucket3, s.Bucket4], Columns(d));
                Assert.Equal(s.Total, d.Outstanding);
            }

            Assert.Equal(
                [summary.Totals.Current, summary.Totals.Bucket1, summary.Totals.Bucket2, summary.Totals.Bucket3, summary.Totals.Bucket4],
                Columns(detail.Totals));
            Assert.Equal(summary.Totals.Total, detail.Totals.Outstanding);
            Assert.Equal(summary.BucketLabels, detail.BucketLabels);
        }
    }

    [Fact]
    public void The_defaults_are_the_due_date_and_thirty_days()
    {
        AgedReceivablesDetailReportResultDto report = Run();

        Assert.Equal((AgeBasis.DueDate, 30), (report.AgeBy, report.BucketDays));
        Assert.Equal("INR", report.BaseCurrencyCode);
        Assert.Equal(October31, report.AsAtDate);
    }

    [Fact]
    public void An_empty_book_is_an_empty_report_with_totals_of_nothing()
    {
        AgedReceivablesDetailReportResultDto report = Run(open: []);

        Assert.Empty(report.Customers);
        Assert.Equal([0m, 0m, 0m, 0m, 0m], Columns(report.Totals));
        Assert.Equal(0m, report.Totals.Outstanding);
        Assert.Equal(5, report.BucketLabels.Count);
    }

    // ---- The pipeline, on a small ledger ----------------------------------------------

    private static AccountRead Account(long id, string name, string? system = null) =>
        new() { AccountId = id, AccountCode = id.ToString(), AccountName = name, AccountSystemName = system, AccountTypeId = 1, IsActive = true };

    private static long _id;

    private static JournalLedgerRead Leg(string type, long transactionId, DateOnly on, long account, decimal debit, decimal credit, long? contact) =>
        new()
        {
            LedgerId = ++_id, LedgerDate = on, AccountId = account, TransactionTypeCode = type, TransactionId = transactionId,
            TransactionDetailId = 1, DebitAmountBase = debit, CreditAmountBase = credit, DebitAmount = debit, CreditAmount = credit,
            CurrencyCode = "INR", ExchangeRate = 1m, ContactId = contact,
        };

    [Fact]
    public void Open_documents_carry_what_each_came_to_before_anything_was_applied()
    {
        AccountRead[] accounts =
        [
            Account(Receivable, "Accounts Receivable", ForeignCurrencyGainLossReportRepository.ReceivableAccountName),
            Account(Sales, "Sales Revenue"),
            Account(Returns, "Sales Returns"),
        ];
        JournalLedgerRead[] ledger =
        [
            Leg("INV", 301, new(2026, 6, 1), Receivable, 20_000m, 0m, Zenith), Leg("INV", 301, new(2026, 6, 1), Sales, 0m, 20_000m, Zenith),
            Leg("CRN", 302, new(2026, 9, 15), Returns, 5_000m, 0m, Zenith), Leg("CRN", 302, new(2026, 9, 15), Receivable, 0m, 5_000m, Zenith),
            Leg("CRN", 306, new(2026, 10, 30), Returns, 1_000m, 0m, Zenith), Leg("CRN", 306, new(2026, 10, 30), Receivable, 0m, 1_000m, Zenith),
        ];
        TransactionRatioRead[] ratios =
        [
            new() { SourceTransactionTypeCode = "CRN", SourceTransactionId = 302, TargetTransactionTypeCode = "INV", TargetTransactionId = 301, Amount = 5_000m, AllocationDate = new DateOnly(2026, 9, 15) },
        ];
        InvoiceRead[] invoices =
        [
            new() { InvoiceId = 301, TransactionTypeCode = "INV", DocumentNo = "INV-9301", DocumentDate = new DateOnly(2026, 6, 1), DueDate = new DateOnly(2026, 7, 1), ContactId = Zenith, CurrencyCode = "INR" },
        ];

        IQueryable<ReceivableDocument> documents = AgedReceivablesSummaryReportRepository.Documents(ledger.AsQueryable(), accounts.AsQueryable(), October31);
        List<OpenReceivable> open =
        [
            .. AgedReceivablesSummaryReportRepository.OpenDocuments(
                documents,
                AgedReceivablesSummaryReportRepository.Settlements(ratios.AsQueryable(), documents, October31),
                invoices.AsQueryable()),
        ];

        AgedReceivablesDetailReportResultDto report = Run(open: open);

        // The invoice came to 20,000 and has 5,000 applied; the credit note that was applied in full is gone;
        // the other is whole.
        AgedReceivablesDetailDocumentDto invoice = Doc(report, "INV", 301);
        Assert.Equal((20_000m, 5_000m, 15_000m), (invoice.Original, invoice.Settled, invoice.Outstanding));
        Assert.DoesNotContain(report.Customers.SelectMany(c => c.Documents), d => d.DocumentId == 302);
        AgedReceivablesDetailDocumentDto credit = Doc(report, "CRN", 306);
        Assert.Equal((-1_000m, 0m, -1_000m), (credit.Original, credit.Settled, credit.Outstanding));
    }

    // ---- Lookups ----------------------------------------------------------------------

    [Fact]
    public void A_receipts_number_is_looked_up_only_for_the_ids_asked_and_only_if_it_has_one()
    {
        ReceiveMoneyRead[] receipts =
        [
            new() { ReceiveMoneyId = 1, TransactionNo = "RCM-0001", CurrencyCode = "INR" },
            new() { ReceiveMoneyId = 2, TransactionNo = null, CurrencyCode = "INR" },
            new() { ReceiveMoneyId = 3, TransactionNo = "RCM-0003", CurrencyCode = "INR" },
        ];

        List<MoneyNumber> numbers = [.. AgedReceivablesDetailReportRepository.ReceiptNumbers(receipts.AsQueryable(), [1L, 2L])];

        MoneyNumber only = Assert.Single(numbers);
        Assert.Equal(("RCM", 1L, "RCM-0001"), (only.TypeCode, only.DocumentId, only.Number));
    }

    [Fact]
    public void A_payments_number_is_looked_up_the_same_way()
    {
        SpendMoneyRead[] payments =
        [
            new() { SpendMoneyId = 4, TransactionNo = "SPM-0004", CurrencyCode = "INR" },
            new() { SpendMoneyId = 5, TransactionNo = null, CurrencyCode = "INR" },
        ];

        MoneyNumber only = Assert.Single(AgedReceivablesDetailReportRepository.PaymentNumbers(payments.AsQueryable(), [4L, 5L]));

        Assert.Equal(("SPM", 4L, "SPM-0004"), (only.TypeCode, only.DocumentId, only.Number));
    }

    [Fact]
    public void The_customers_offered_are_the_summarys_list_customers_only_by_name()
    {
        ContactRead[] contacts =
        [
            new() { ContactId = 2, ContactCode = "ZED", DisplayName = "Zed Traders", IsCustomer = true, CurrencyCode = "INR" },
            new() { ContactId = 1, ContactCode = "ABC", DisplayName = "Abc Exports", IsCustomer = true, CurrencyCode = "INR" },
            new() { ContactId = 3, ContactCode = "VEN", DisplayName = "Vendor Only", IsCustomer = false, CurrencyCode = "INR" },
        ];

        List<AgedReceivablesCustomerChoiceDto> choices = [.. AgedReceivablesSummaryReportRepository.CustomerChoices(contacts.AsQueryable())];

        Assert.Equal(["ABC", "ZED"], choices.Select(c => c.ContactCode));
    }

    // ---- The request and the guards ---------------------------------------------------

    [Fact]
    public void The_as_at_date_is_required_and_nothing_else_is()
    {
        Assert.Equal(
            [nameof(AgedReceivablesDetailReportRequest.AsAtDate)],
            typeof(AgedReceivablesDetailReportRequest).GetProperties()
                .Where(p => p.GetCustomAttribute<RequiredAttribute>() is not null)
                .Select(p => p.Name));
    }

    [Fact]
    public void A_missing_date_is_refused_at_its_field_by_the_request_itself()
    {
        var request = new AgedReceivablesDetailReportRequest();
        var results = new List<ValidationResult>();

        Assert.False(Validator.TryValidateObject(request, new ValidationContext(request), results, validateAllProperties: true));
        Assert.Equal(["As at date is required."], results.Select(r => r.ErrorMessage));
    }

    [Fact]
    public void The_refusals_are_the_summarys_in_the_same_words()
    {
        var request = new AgedReceivablesDetailReportRequest { AsAtDate = October31, BucketDays = 10, AgeBy = (AgeBasis)9 };

        Assert.Equal(
            AgedReceivablesSummaryReportRepository.Refusals(new AgedReceivablesSummaryReportRequest { AsAtDate = October31, BucketDays = 10, AgeBy = (AgeBasis)9 }),
            AgedReceivablesDetailReportRepository.Refusals(request));
        Assert.Equal(2, AgedReceivablesDetailReportRepository.Refusals(request).Count);
        Assert.Empty(AgedReceivablesDetailReportRepository.Refusals(new AgedReceivablesDetailReportRequest { AsAtDate = October31, BucketDays = 15 }));
    }

    [Fact]
    public async Task Another_branch_in_the_request_is_forbidden_not_hidden()
    {
        var controller = new AgedReceivablesDetailReportController(null!)
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
            new AgedReceivablesDetailReportRequest { AsAtDate = October31, OrgId = Guid.NewGuid() }, default);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public void The_endpoint_needs_the_reports_module_sales_view_and_a_trade()
    {
        Type controller = typeof(AgedReceivablesDetailReportController);

        Assert.Equal("api/statements/aged-receivables-detail", controller.GetCustomAttribute<RouteAttribute>()!.Template);
        Assert.Equal("reports", controller.GetCustomAttribute<RequireModulePermissionAttribute>()!.Module);
        Assert.NotNull(controller.GetCustomAttribute<RequireBusinessTypeAttribute>());

        foreach (string action in new[] { nameof(AgedReceivablesDetailReportController.Run), nameof(AgedReceivablesDetailReportController.Customers) })
        {
            Assert.Contains(
                "sales.view",
                controller.GetMethod(action)!.GetCustomAttributes<RequirePermissionAttribute>().Select(a => a.Permission));
        }

        Assert.Equal(
            "customers",
            controller.GetMethod(nameof(AgedReceivablesDetailReportController.Customers))!.GetCustomAttribute<HttpGetAttribute>()!.Template);
    }

    // ---- The catalog source, held to the same date ------------------------------------

    [Fact]
    public void The_catalog_source_declares_the_as_at_date_required_and_keeps_its_columns()
    {
        var source = new AgedReceivablesDetailSourceRepository();

        ReportParameterRepository date = Assert.Single(source.Parameters);
        Assert.Equal("asOf", date.Name);
        Assert.True(date.IsRequired);
        Assert.Equal(
            ["documentNo", "documentDate", "dueDate", "contactName", "totalAmount", "amountDue"],
            source.Columns.Select(c => c.Key));
    }
}
