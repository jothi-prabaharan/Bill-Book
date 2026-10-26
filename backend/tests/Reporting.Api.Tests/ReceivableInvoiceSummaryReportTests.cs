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
using Shared.Kernel.Documents;
using Shared.Kernel.Internal;
using Xunit;

namespace Reporting.Api.Tests;

/// <summary>
/// The Receivable Invoice Summary (TK-224), on books worked by hand. The branch's base currency is
/// INR and the period is October 2026, so received and due are as at 31 October:
/// <list type="bullet">
/// <item><b>INV-9401</b> (2 October, due 20 October): taxable 10,000 and CGST and SGST of 900,
/// total 11,800, all of it received on 10 October. Paid.</item>
/// <item><b>INV-9402</b> (5 October, due 15 October, IGST): taxable 20,000 and IGST 3,600, total
/// 23,600. 10,000 received on 12 October and a credit note of 1,180 applied on 26 October, so 11,180
/// is received and 12,420 due, 16 days past its due date. Overdue, though part of it was received.</item>
/// <item><b>INV-9403</b> (10 October, due 10 November): total 5,900, 2,000 received. Partly paid.</item>
/// <item><b>INV-9404</b> (15 October, due 15 November): total 9,440, nothing received on the date: a
/// settlement voided and one effective in November do not count. Awaiting payment.</item>
/// <item><b>INV-9405</b> in US dollars at 80: taxable USD 500 and IGST USD 90, total USD 590 or
/// 47,200, of which a receipt of USD 100 is 8,000. Taxable 40,000 and tax 7,200 in base. Partly paid.</item>
/// <item>A voided invoice of 1,180, a draft, and an invoice dated in September, which are not in the
/// default list.</item>
/// <item>Credit notes <b>CRN-9421</b> of 1,180, applied in full to INV-9402, and <b>CRN-9422</b> of
/// 2,360, applied to nothing.</item>
/// </list>
/// Without credit notes the taxable value is 83,000, the tax 14,940, the total 97,940, received 32,980
/// and due 64,960. The builders run on lists through the same expressions the repository runs
/// against the read models; the composition is pure. Nothing here needs PostgreSQL.
/// </summary>
public sealed class ReceivableInvoiceSummaryReportTests
{
    private const long Acme = 9101, Zenith = 9103;

    private static readonly DateOnly October1 = new(2026, 10, 1);
    private static readonly DateOnly October31 = new(2026, 10, 31);

    private static InvoiceRead Invoice(
        long id, DateOnly date, DateOnly? due, long contact, string currency, decimal rate, decimal taxable,
        decimal cgst, decimal sgst, decimal igst, DocumentStatus status = DocumentStatus.Posted)
    {
        decimal total = taxable + cgst + sgst + igst;
        return new()
        {
            InvoiceId = id, TransactionTypeCode = "INV", DocumentNo = $"INV-{id}", DocumentDate = date, DueDate = due, ContactId = contact,
            CurrencyCode = currency, ExchangeRate = rate, TaxableAmount = taxable, CgstAmount = cgst, SgstAmount = sgst, IgstAmount = igst,
            TotalAmount = total, TotalAmountBase = Math.Round(total * rate, 2), Status = status,
        };
    }

    private static readonly List<InvoiceRead> Invoices =
    [
        Invoice(9401, new(2026, 10, 2), new(2026, 10, 20), Zenith, "INR", 1m, 10_000m, 900m, 900m, 0m),
        Invoice(9402, new(2026, 10, 5), new(2026, 10, 15), Zenith, "INR", 1m, 20_000m, 0m, 0m, 3_600m),
        Invoice(9403, new(2026, 10, 10), new(2026, 11, 10), Zenith, "INR", 1m, 5_000m, 450m, 450m, 0m),
        Invoice(9404, new(2026, 10, 15), new(2026, 11, 15), Zenith, "INR", 1m, 8_000m, 720m, 720m, 0m),
        Invoice(9405, new(2026, 10, 18), new(2026, 11, 18), Acme, "USD", 80m, 500m, 0m, 0m, 90m),
        Invoice(9406, new(2026, 10, 20), new(2026, 11, 20), Zenith, "INR", 1m, 1_000m, 90m, 90m, 0m, DocumentStatus.Void),
        Invoice(9407, new(2026, 10, 22), new(2026, 11, 22), Zenith, "INR", 1m, 3_000m, 270m, 270m, 0m, DocumentStatus.Draft),
        Invoice(9408, new(2026, 9, 20), new(2026, 10, 20), Zenith, "INR", 1m, 4_000m, 360m, 360m, 0m),
    ];

    private static CreditNoteRead CreditNote(long id, DateOnly date, decimal taxable, decimal cgst, decimal sgst) =>
        new()
        {
            CreditNoteId = id, TransactionTypeCode = "CRN", DocumentNo = $"CRN-{id}", DocumentDate = date, ContactId = Zenith, CurrencyCode = "INR",
            ExchangeRate = 1m, TaxableAmount = taxable, CgstAmount = cgst, SgstAmount = sgst, TotalAmount = taxable + cgst + sgst,
            TotalAmountBase = taxable + cgst + sgst, Status = DocumentStatus.Posted,
        };

    private static readonly List<CreditNoteRead> CreditNotes =
    [
        CreditNote(9421, new(2026, 10, 25), 1_000m, 90m, 90m),
        CreditNote(9422, new(2026, 10, 28), 2_000m, 180m, 180m),
    ];

    private static long _id;

    private static TransactionRatioRead Ratio(string source, long sourceId, string target, long targetId, decimal amount, DateOnly on, bool voided = false) =>
        new()
        {
            TransactionRatioId = ++_id, SourceTransactionTypeCode = source, SourceTransactionId = sourceId,
            TargetTransactionTypeCode = target, TargetTransactionId = targetId, Amount = amount, AllocationDate = on, IsVoided = voided,
        };

    private static readonly List<TransactionRatioRead> Ratios =
    [
        Ratio("RCM", 9411, "INV", 9401, 11_800m, new(2026, 10, 10)),
        Ratio("RCM", 9412, "INV", 9402, 10_000m, new(2026, 10, 12)),
        Ratio("CRN", 9421, "INV", 9402, 1_180m, new(2026, 10, 26)),
        Ratio("RCM", 9414, "INV", 9403, 2_000m, new(2026, 10, 20)),
        // USD 100 in the receipt's own currency: the invoice's rate takes it to 8,000.
        Ratio("RCM", 9413, "INV", 9405, 100m, new(2026, 10, 22)),
        // Neither counts on 31 October: one voided, one effective in November.
        Ratio("RCM", 9415, "INV", 9404, 9_440m, new(2026, 10, 30), voided: true),
        Ratio("RCM", 9416, "INV", 9404, 9_440m, new(2026, 11, 3)),
    ];

    private static ReceivableInvoiceSummaryReportRequest Request(
        bool credits = false, ReceivableInvoiceStatus? status = null, long? contact = null, string? currency = null,
        DateOnly? from = null, DateOnly? to = null) =>
        new()
        {
            FromDate = from ?? October1, ToDate = to ?? October31, IncludeCreditNotes = credits, Status = status, ContactId = contact, CurrencyCode = currency,
        };

    private static List<ReceivableInvoiceFigure> FiguresFor(ReceivableInvoiceSummaryReportRequest request) =>
        [.. ReceivableInvoiceSummaryReportRepository.Figures(Invoices.AsQueryable(), CreditNotes.AsQueryable(), request, request.FromDate!.Value, request.ToDate!.Value)];

    /// <summary>Every row for a request, through the same builders the repository uses, before any status filter.</summary>
    private static List<ReceivableInvoiceRow> RowsFor(ReceivableInvoiceSummaryReportRequest request)
    {
        IQueryable<ReceivableInvoiceFigure> figures = ReceivableInvoiceSummaryReportRepository.Figures(
            Invoices.AsQueryable(), CreditNotes.AsQueryable(), request, request.FromDate!.Value, request.ToDate!.Value);

        return
        [
            .. ReceivableInvoiceSummaryReportRepository.Rows(
                figures,
                ReceivableInvoiceSummaryReportRepository.ReceivedAmounts(Ratios.AsQueryable(), figures, request.ToDate!.Value),
                request.ToDate!.Value),
        ];
    }

    private static ReceivableInvoiceRow Row(IEnumerable<ReceivableInvoiceRow> rows, string type, long id) =>
        rows.Single(r => r.TypeCode == type && r.DocumentId == id);

    // ---- Which documents ---------------------------------------------------------------

    [Fact]
    public void Posted_and_voided_invoices_in_the_period_are_figures_and_drafts_and_other_months_are_not()
    {
        List<ReceivableInvoiceFigure> figures = FiguresFor(Request());

        Assert.Equal([9401L, 9402L, 9403L, 9404L, 9405L, 9406L], figures.Select(f => f.DocumentId).Order());
        Assert.DoesNotContain(figures, f => f.DocumentId == 9407);
        Assert.DoesNotContain(figures, f => f.DocumentId == 9408);
    }

    [Fact]
    public void A_period_is_by_the_invoice_date_and_both_ends_are_in_it()
    {
        List<ReceivableInvoiceFigure> one = FiguresFor(Request(from: new DateOnly(2026, 10, 2), to: new DateOnly(2026, 10, 2)));

        Assert.Equal([9401L], one.Select(f => f.DocumentId));
    }

    [Fact]
    public void Credit_notes_are_figures_only_when_asked_for_and_are_negative()
    {
        Assert.DoesNotContain(FiguresFor(Request()), f => f.TypeCode == "CRN");

        ReceivableInvoiceFigure credit = FiguresFor(Request(credits: true)).Single(f => f is { TypeCode: "CRN", DocumentId: 9421 });

        Assert.Equal((-1_000m, -90m, -90m, -1_180m, -1), (credit.Taxable, credit.Cgst, credit.Sgst, credit.Total, credit.Sign));
        Assert.Null(credit.DueDate);
    }

    [Fact]
    public void A_foreign_document_is_in_base_currency_with_its_own_total_kept()
    {
        ReceivableInvoiceFigure usd = FiguresFor(Request()).Single(f => f.DocumentId == 9405);

        // USD 500 and USD 90 at 80; the total is the document's own base total.
        Assert.Equal((40_000m, 7_200m, 47_200m, 590m), (usd.Taxable, usd.Igst, usd.Total, usd.TotalSource));
        Assert.Equal("USD", usd.CurrencyCode);
    }

    [Fact]
    public void One_customer_can_be_asked_for()
    {
        Assert.Equal([9405L], FiguresFor(Request(contact: Acme)).Select(f => f.DocumentId));
    }

    [Fact]
    public void One_currency_can_be_asked_for_whatever_case_it_is_written_in()
    {
        Assert.Equal([9405L], FiguresFor(Request(currency: "usd")).Select(f => f.DocumentId));
        Assert.Equal([9405L], FiguresFor(Request(currency: " USD ")).Select(f => f.DocumentId));
        Assert.Equal(5, FiguresFor(Request(currency: "INR")).Count);
    }

    // ---- What was received -------------------------------------------------------------

    private static List<ReceivedAmount> ReceivedFor(ReceivableInvoiceSummaryReportRequest request)
    {
        IQueryable<ReceivableInvoiceFigure> figures = ReceivableInvoiceSummaryReportRepository.Figures(
            Invoices.AsQueryable(), CreditNotes.AsQueryable(), request, request.FromDate!.Value, request.ToDate!.Value);

        return [.. ReceivableInvoiceSummaryReportRepository.ReceivedAmounts(Ratios.AsQueryable(), figures, request.ToDate!.Value)];
    }

    [Fact]
    public void A_money_documents_line_is_taken_to_base_at_the_invoices_own_rate()
    {
        List<ReceivedAmount> received = ReceivedFor(Request());

        // USD 100 against an invoice of 47,200 for USD 590 is 8,000; INR receipts are as they are.
        Assert.Equal(8_000m, received.Single(r => r.DocumentId == 9405).Amount);
        Assert.Equal(11_800m, received.Single(r => r.DocumentId == 9401).Amount);
        Assert.Equal(2_000m, received.Single(r => r.DocumentId == 9403).Amount);
    }

    [Fact]
    public void An_allocation_that_is_not_a_money_document_is_taken_as_it_is_and_adds_to_the_receipts()
    {
        List<ReceivedAmount> received = ReceivedFor(Request());

        Assert.Equal([10_000m, 1_180m], received.Where(r => r.DocumentId == 9402).Select(r => r.Amount));
    }

    [Fact]
    public void A_voided_settlement_and_one_after_the_to_date_hold_nothing()
    {
        Assert.DoesNotContain(ReceivedFor(Request()), r => r.DocumentId == 9404);
    }

    [Fact]
    public void A_settlement_effective_on_the_to_date_counts_and_one_the_day_after_does_not()
    {
        Assert.Contains(ReceivedFor(Request(to: new DateOnly(2026, 10, 12))), r => r.DocumentId == 9402);
        Assert.DoesNotContain(ReceivedFor(Request(to: new DateOnly(2026, 10, 11))), r => r.DocumentId == 9402);
    }

    [Fact]
    public void A_credit_note_is_settled_at_the_source_end_and_what_was_applied_is_negative()
    {
        ReceivedAmount applied = Assert.Single(ReceivedFor(Request(credits: true)), r => r.DocumentId == 9421);

        Assert.Equal(-1_180m, applied.Amount);
        Assert.DoesNotContain(ReceivedFor(Request(credits: true)), r => r.DocumentId == 9422);
    }

    // ---- The rows ---------------------------------------------------------------------

    [Fact]
    public void An_invoice_shows_what_was_received_and_what_is_due_as_at_the_to_date()
    {
        List<ReceivableInvoiceRow> rows = RowsFor(Request());

        Assert.Equal((11_800m, 0m), (Row(rows, "INV", 9401).Received, Row(rows, "INV", 9401).Due));
        Assert.Equal((11_180m, 12_420m), (Row(rows, "INV", 9402).Received, Row(rows, "INV", 9402).Due));
        Assert.Equal((2_000m, 3_900m), (Row(rows, "INV", 9403).Received, Row(rows, "INV", 9403).Due));
        Assert.Equal((0m, 9_440m), (Row(rows, "INV", 9404).Received, Row(rows, "INV", 9404).Due));
        Assert.Equal((8_000m, 39_200m), (Row(rows, "INV", 9405).Received, Row(rows, "INV", 9405).Due));
    }

    [Fact]
    public void The_total_less_what_was_received_is_what_is_due_on_every_row()
    {
        foreach (ReceivableInvoiceRow row in RowsFor(Request(credits: true)).Where(r => r.Status != ReceivableInvoiceStatus.Void))
        {
            Assert.Equal(row.Total - row.Received, row.Due);
        }
    }

    [Fact]
    public void Each_status_is_told_from_the_total_what_was_received_and_the_due_date()
    {
        List<ReceivableInvoiceRow> rows = RowsFor(Request());

        Assert.Equal(ReceivableInvoiceStatus.Paid, Row(rows, "INV", 9401).Status);
        Assert.Equal(ReceivableInvoiceStatus.Overdue, Row(rows, "INV", 9402).Status);
        Assert.Equal(ReceivableInvoiceStatus.PartlyPaid, Row(rows, "INV", 9403).Status);
        Assert.Equal(ReceivableInvoiceStatus.AwaitingPayment, Row(rows, "INV", 9404).Status);
        Assert.Equal(ReceivableInvoiceStatus.PartlyPaid, Row(rows, "INV", 9405).Status);
        Assert.Equal(ReceivableInvoiceStatus.Void, Row(rows, "INV", 9406).Status);
    }

    [Fact]
    public void Overdue_wins_over_partly_paid()
    {
        // INV-9402 has 11,180 received and 12,420 still owed past its due date.
        ReceivableInvoiceRow row = Row(RowsFor(Request()), "INV", 9402);

        Assert.True(row.Received > 0m);
        Assert.Equal(ReceivableInvoiceStatus.Overdue, row.Status);
    }

    [Fact]
    public void An_invoice_due_on_the_to_date_is_not_yet_overdue_and_one_due_the_day_before_is()
    {
        // INV-9402 is due on 15 October.
        Assert.Equal(ReceivableInvoiceStatus.AwaitingPayment, Row(RowsFor(Request(to: new DateOnly(2026, 10, 11))), "INV", 9402).Status);
        Assert.Equal(ReceivableInvoiceStatus.PartlyPaid, Row(RowsFor(Request(to: new DateOnly(2026, 10, 15))), "INV", 9402).Status);
        Assert.Equal(ReceivableInvoiceStatus.Overdue, Row(RowsFor(Request(to: new DateOnly(2026, 10, 16))), "INV", 9402).Status);
    }

    [Fact]
    public void Received_and_due_are_as_at_the_to_date_so_an_earlier_one_reads_as_it_did_then()
    {
        List<ReceivableInvoiceRow> rows = RowsFor(Request(to: new DateOnly(2026, 10, 11)));

        // Before the 12 October receipt and the 26 October credit note.
        Assert.Equal((0m, 23_600m, ReceivableInvoiceStatus.AwaitingPayment), (Row(rows, "INV", 9402).Received, Row(rows, "INV", 9402).Due, Row(rows, "INV", 9402).Status));
        Assert.Equal(ReceivableInvoiceStatus.Paid, Row(rows, "INV", 9401).Status);
    }

    [Fact]
    public void An_invoice_with_nothing_left_owed_or_more_than_it_came_to_is_paid()
    {
        InvoiceRead[] invoices = [Invoice(1, October1, new DateOnly(2026, 10, 5), Zenith, "INR", 1m, 100m, 0m, 0m, 0m)];
        TransactionRatioRead[] ratios = [Ratio("CRN", 5, "INV", 1, 150m, October1)];

        IQueryable<ReceivableInvoiceFigure> figures = ReceivableInvoiceSummaryReportRepository.Figures(invoices.AsQueryable(), CreditNotes.AsQueryable(), Request(), October1, October31);
        ReceivableInvoiceRow row = Assert.Single(ReceivableInvoiceSummaryReportRepository.Rows(
            figures, ReceivableInvoiceSummaryReportRepository.ReceivedAmounts(ratios.AsQueryable(), figures, October31), October31));

        // Over-applied: nothing owed is paid, even past its due date.
        Assert.Equal(ReceivableInvoiceStatus.Paid, row.Status);
        Assert.Equal(-50m, row.Due);
    }

    [Fact]
    public void An_invoice_with_no_due_date_is_never_overdue()
    {
        InvoiceRead[] invoices = [Invoice(1, October1, null, Zenith, "INR", 1m, 100m, 0m, 0m, 0m)];

        IQueryable<ReceivableInvoiceFigure> figures = ReceivableInvoiceSummaryReportRepository.Figures(invoices.AsQueryable(), CreditNotes.AsQueryable(), Request(), October1, October31);
        ReceivableInvoiceRow row = Assert.Single(ReceivableInvoiceSummaryReportRepository.Rows(
            figures, ReceivableInvoiceSummaryReportRepository.ReceivedAmounts(Array.Empty<TransactionRatioRead>().AsQueryable(), figures, October31), October31));

        Assert.Equal(ReceivableInvoiceStatus.AwaitingPayment, row.Status);
    }

    [Fact]
    public void A_voided_invoice_settles_nothing_and_owes_nothing()
    {
        ReceivableInvoiceRow voided = Row(RowsFor(Request()), "INV", 9406);

        Assert.Equal((1_180m, 0m, 0m), (voided.Total, voided.Received, voided.Due));
    }

    [Fact]
    public void A_credit_note_applied_in_full_is_paid_and_one_applied_to_nothing_is_awaiting_with_a_negative_due()
    {
        List<ReceivableInvoiceRow> rows = RowsFor(Request(credits: true));

        ReceivableInvoiceRow applied = Row(rows, "CRN", 9421);
        Assert.Equal((-1_180m, -1_180m, 0m, ReceivableInvoiceStatus.Paid), (applied.Total, applied.Received, applied.Due, applied.Status));

        ReceivableInvoiceRow open = Row(rows, "CRN", 9422);
        Assert.Equal((-2_360m, 0m, -2_360m, ReceivableInvoiceStatus.AwaitingPayment), (open.Total, open.Received, open.Due, open.Status));
    }

    [Fact]
    public void A_credit_note_applied_to_an_invoice_counts_as_received_on_the_invoice_even_when_credit_notes_are_not_listed()
    {
        Assert.Equal(11_180m, Row(RowsFor(Request(credits: false)), "INV", 9402).Received);
    }

    [Fact]
    public void Without_a_status_the_void_ones_are_left_out_and_asked_for_by_name_they_are_the_only_ones()
    {
        List<ReceivableInvoiceRow> rows = RowsFor(Request());

        List<ReceivableInvoiceRow> all = [.. ReceivableInvoiceSummaryReportRepository.WithStatus(rows.AsQueryable(), null)];
        Assert.Equal(5, all.Count);
        Assert.DoesNotContain(all, r => r.Status == ReceivableInvoiceStatus.Void);

        ReceivableInvoiceRow only = Assert.Single(ReceivableInvoiceSummaryReportRepository.WithStatus(rows.AsQueryable(), ReceivableInvoiceStatus.Void));
        Assert.Equal(9406L, only.DocumentId);
    }

    [Fact]
    public void A_status_asked_for_lists_only_the_documents_with_it()
    {
        List<ReceivableInvoiceRow> rows = RowsFor(Request());

        Assert.Equal([9403L, 9405L], ReceivableInvoiceSummaryReportRepository.WithStatus(rows.AsQueryable(), ReceivableInvoiceStatus.PartlyPaid).Select(r => r.DocumentId).Order());
        Assert.Equal([9402L], ReceivableInvoiceSummaryReportRepository.WithStatus(rows.AsQueryable(), ReceivableInvoiceStatus.Overdue).Select(r => r.DocumentId));
    }

    // ---- The figures and the page -------------------------------------------------------

    private static ReceivableInvoiceSummaryTotalsDto TotalsOf(IEnumerable<ReceivableInvoiceRow> rows)
    {
        List<ReceivableInvoiceRow> list = [.. rows];
        return new()
        {
            Taxable = list.Sum(r => r.Taxable),
            Tax = list.Sum(r => r.Cgst + r.Sgst + r.Igst + r.Cess),
            Total = list.Sum(r => r.Total),
            Received = list.Sum(r => r.Received),
            Due = list.Sum(r => r.Due),
        };
    }

    [Fact]
    public void The_periods_figures_add_up_as_worked_by_hand()
    {
        ReceivableInvoiceSummaryTotalsDto totals = TotalsOf(
            ReceivableInvoiceSummaryReportRepository.WithStatus(RowsFor(Request()).AsQueryable(), null));

        Assert.Equal((83_000m, 14_940m, 97_940m, 32_980m, 64_960m), (totals.Taxable, totals.Tax, totals.Total, totals.Received, totals.Due));
        Assert.Equal(totals.Total - totals.Received, totals.Due);
        Assert.Equal(totals.Taxable + totals.Tax, totals.Total);
    }

    [Fact]
    public void With_credit_notes_the_figures_take_their_negatives_off()
    {
        ReceivableInvoiceSummaryTotalsDto totals = TotalsOf(
            ReceivableInvoiceSummaryReportRepository.WithStatus(RowsFor(Request(credits: true)).AsQueryable(), null));

        Assert.Equal((80_000m, 14_400m, 94_400m, 31_800m, 62_600m), (totals.Taxable, totals.Tax, totals.Total, totals.Received, totals.Due));
    }

    private static ReceivableInvoiceSummaryReportResultDto Compose(
        ReceivableInvoiceSummaryReportRequest request, IReadOnlyList<ReceivableInvoiceRow>? page = null,
        IReadOnlyList<StatusCount>? counts = null, ReceivableInvoiceSummaryTotalsDto? totals = null)
    {
        List<ReceivableInvoiceRow> rows = page is null
            ? [.. ReceivableInvoiceSummaryReportRepository.WithStatus(RowsFor(request).AsQueryable(), request.Status).OrderByDescending(r => r.DocumentDate)]
            : [.. page];

        return ReceivableInvoiceSummaryReportRepository.Compose(
            request,
            rows,
            rows.Count,
            [
                new() { ContactId = Acme, Code = "ACME", Name = "Acme Exports" },
                new() { ContactId = Zenith, Code = "ZENITH", Name = "Zenith Traders" },
            ],
            counts ?? [],
            totals ?? TotalsOf(rows),
            "INR");
    }

    [Fact]
    public void A_row_carries_its_customer_its_figures_in_base_and_its_own_currency_total()
    {
        ReceivableInvoiceSummaryRowDto usd = Compose(Request()).Invoices.Single(r => r.DocumentId == 9405);

        Assert.Equal(("ACME", "Acme Exports", "USD"), (usd.ContactCode, usd.ContactName, usd.CurrencyCode));
        Assert.Equal((40_000m, 7_200m, 47_200m, 590m), (usd.Taxable, usd.Tax, usd.Total, usd.TotalInCurrency));
        Assert.Equal((8_000m, 39_200m, ReceivableInvoiceStatus.PartlyPaid), (usd.Received, usd.Due, usd.Status));
    }

    [Fact]
    public void The_tax_is_the_four_taxes_added_up_and_the_components_are_kept()
    {
        ReceivableInvoiceSummaryRowDto row = Compose(Request()).Invoices.Single(r => r.DocumentId == 9401);

        Assert.Equal((900m, 900m, 0m, 0m, 1_800m), (row.Cgst, row.Sgst, row.Igst, row.Cess, row.Tax));
    }

    [Fact]
    public void Days_overdue_are_given_only_to_an_overdue_invoice_and_run_from_its_due_date_to_the_to_date()
    {
        ReceivableInvoiceSummaryReportResultDto report = Compose(Request());

        Assert.Equal(16, report.Invoices.Single(r => r.DocumentId == 9402).DaysOverdue);
        Assert.All(report.Invoices.Where(r => r.DocumentId != 9402), r => Assert.Null(r.DaysOverdue));
    }

    [Fact]
    public void Every_status_has_a_count_and_a_nought_is_kept_so_a_tab_can_say_nothing_is_in_it()
    {
        ReceivableInvoiceSummaryReportResultDto report = Compose(
            Request(), counts: [new() { Status = ReceivableInvoiceStatus.Paid, Count = 3 }]);

        Assert.Equal(Enum.GetValues<ReceivableInvoiceStatus>().Length, report.StatusCounts.Count);
        Assert.Equal(3, report.StatusCounts[ReceivableInvoiceStatus.Paid]);
        Assert.Equal(0, report.StatusCounts[ReceivableInvoiceStatus.Overdue]);
    }

    [Fact]
    public void The_page_the_period_and_the_totals_of_every_match_are_passed_on()
    {
        ReceivableInvoiceSummaryTotalsDto totals = new() { Taxable = 1m, Tax = 2m, Total = 3m, Received = 4m, Due = 5m };
        var request = Request();
        request.PageNumber = 2;
        request.PageSize = 25;

        ReceivableInvoiceSummaryReportResultDto report = Compose(request, page: [], totals: totals);

        Assert.Equal((October1, October31, "INR"), (report.FromDate, report.ToDate, report.BaseCurrencyCode));
        Assert.Equal((2, 25), (report.PageNumber, report.PageSize));
        Assert.Same(totals, report.Totals);
    }

    [Fact]
    public void A_customer_the_names_do_not_know_is_still_listed_by_its_number()
    {
        ReceivableInvoiceRow[] page = [new() { TypeCode = "INV", DocumentId = 1, ContactId = 77, DocumentDate = October1 }];

        ReceivableInvoiceSummaryRowDto row = Assert.Single(ReceivableInvoiceSummaryReportRepository.Compose(Request(), page, 1, [], [], new(), "INR").Invoices);

        Assert.Equal("Customer 77", row.ContactName);
    }

    // ---- Lookups ----------------------------------------------------------------------

    [Fact]
    public void The_customers_offered_are_the_aged_receivables_list()
    {
        ContactRead[] contacts =
        [
            new() { ContactId = 2, ContactCode = "ZED", DisplayName = "Zed Traders", IsCustomer = true, CurrencyCode = "INR" },
            new() { ContactId = 3, ContactCode = "VEN", DisplayName = "Vendor Only", IsCustomer = false, CurrencyCode = "INR" },
        ];

        Assert.Equal(["ZED"], AgedReceivablesSummaryReportRepository.CustomerChoices(contacts.AsQueryable()).Select(c => c.ContactCode));
    }

    // ---- The request and the guards ---------------------------------------------------

    [Fact]
    public void The_two_dates_are_required_and_nothing_else_is()
    {
        Assert.Equal(
            [nameof(ReceivableInvoiceSummaryReportRequest.FromDate), nameof(ReceivableInvoiceSummaryReportRequest.ToDate)],
            typeof(ReceivableInvoiceSummaryReportRequest).GetProperties()
                .Where(p => p.GetCustomAttribute<RequiredAttribute>() is not null)
                .Select(p => p.Name));
    }

    [Fact]
    public void A_missing_date_is_refused_at_its_field_by_the_request_itself()
    {
        var request = new ReceivableInvoiceSummaryReportRequest();
        var results = new List<ValidationResult>();

        Assert.False(Validator.TryValidateObject(request, new ValidationContext(request), results, validateAllProperties: true));
        Assert.Equal(
            ["From date is required.", "To date is required."],
            results.Select(r => r.ErrorMessage).OrderBy(m => m, StringComparer.Ordinal));
    }

    [Fact]
    public void A_page_size_above_a_hundred_is_refused_by_the_request_itself()
    {
        var request = new ReceivableInvoiceSummaryReportRequest { FromDate = October1, ToDate = October31, PageSize = 500 };
        var results = new List<ValidationResult>();

        Assert.False(Validator.TryValidateObject(request, new ValidationContext(request), results, validateAllProperties: true));
        Assert.Equal(["Page size must be between 1 and 100."], results.Select(r => r.ErrorMessage));
    }

    [Fact]
    public void A_period_that_ends_before_it_begins_is_refused_at_the_to_date()
    {
        Assert.Equal(
            [(nameof(ReceivableInvoiceSummaryReportRequest.ToDate), "To date cannot be before the from date.")],
            ReceivableInvoiceSummaryReportRepository.Refusals(Request(from: October31, to: October1)));

        Assert.Empty(ReceivableInvoiceSummaryReportRepository.Refusals(Request(from: October1, to: October1)));
    }

    [Fact]
    public void A_currency_that_is_not_three_letters_is_refused_at_its_field_and_a_blank_one_is_not()
    {
        Assert.Equal(
            [(nameof(ReceivableInvoiceSummaryReportRequest.CurrencyCode), "Currency must be a 3-letter code.")],
            ReceivableInvoiceSummaryReportRepository.Refusals(Request(currency: "US")));

        Assert.Empty(ReceivableInvoiceSummaryReportRepository.Refusals(Request(currency: "")));
        Assert.Empty(ReceivableInvoiceSummaryReportRepository.Refusals(Request(currency: "usd")));
    }

    [Fact]
    public void A_status_that_is_not_one_of_the_five_is_refused_at_its_field()
    {
        Assert.Equal(
            [(nameof(ReceivableInvoiceSummaryReportRequest.Status), "Status must be one of the statuses offered.")],
            ReceivableInvoiceSummaryReportRepository.Refusals(Request(status: (ReceivableInvoiceStatus)9)));
    }

    [Fact]
    public async Task Another_branch_in_the_request_is_forbidden_not_hidden()
    {
        var controller = new ReceivableInvoiceSummaryReportController(null!)
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
            new ReceivableInvoiceSummaryReportRequest { FromDate = October1, ToDate = October31, OrgId = Guid.NewGuid() }, default);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public void The_endpoint_needs_the_reports_module_sales_view_and_a_trade_on_each_action()
    {
        Type controller = typeof(ReceivableInvoiceSummaryReportController);

        Assert.Equal("api/statements/receivable-invoice-summary", controller.GetCustomAttribute<RouteAttribute>()!.Template);
        Assert.Equal("reports", controller.GetCustomAttribute<RequireModulePermissionAttribute>()!.Module);
        Assert.NotNull(controller.GetCustomAttribute<RequireBusinessTypeAttribute>());

        foreach (string action in new[]
        {
            nameof(ReceivableInvoiceSummaryReportController.Run),
            nameof(ReceivableInvoiceSummaryReportController.Customers),
            nameof(ReceivableInvoiceSummaryReportController.Currencies),
        })
        {
            Assert.Contains(
                "sales.view",
                controller.GetMethod(action)!.GetCustomAttributes<RequirePermissionAttribute>().Select(a => a.Permission));
        }
    }

    [Fact]
    public void The_lookups_are_sub_routes_of_the_report()
    {
        Type controller = typeof(ReceivableInvoiceSummaryReportController);

        Assert.Equal("customers", controller.GetMethod(nameof(ReceivableInvoiceSummaryReportController.Customers))!.GetCustomAttribute<HttpGetAttribute>()!.Template);
        Assert.Equal("currencies", controller.GetMethod(nameof(ReceivableInvoiceSummaryReportController.Currencies))!.GetCustomAttribute<HttpGetAttribute>()!.Template);
    }

    // ---- The catalog source, held to the same period ----------------------------------

    [Fact]
    public void The_catalog_source_declares_both_dates_required_and_keeps_its_columns()
    {
        var source = new ReceivableInvoiceSummarySourceRepository();

        Assert.Equal(["from", "to"], source.Parameters.Select(p => p.Name));
        Assert.All(source.Parameters, p => Assert.True(p.IsRequired));
        Assert.Equal(
            ["documentNo", "documentDate", "contactName", "totalAmount", "amountDue"],
            source.Columns.Select(c => c.Key));
    }
}
