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
/// The Payable Invoice Summary (TK-235), on books worked by hand. The branch's base currency is INR and the period is October
/// 2026, so paid and due are as at 31 October:
/// <list type="bullet">
/// <item><b>BIL-9601</b> (2 October, due 17 October, Steelco): taxable 10,000 and IGST 1,800, total 11,800, all of it paid on 12
/// October. Paid.</item>
/// <item><b>BIL-9602</b> (5 October, due 4 November, Boltworks): taxable 20,000 and CGST and SGST of 1,800 each, total 23,600.
/// 10,000 paid on 20 October and a debit note of 2,360 applied on 21 October, so 12,360 is paid and 11,240 due. Partly paid.</item>
/// <item><b>BIL-9603</b> (8 October, due 20 October, Steelco): total 5,900, nothing paid on the date: a settlement voided and one
/// effective in November do not count. Overdue by 11 days.</item>
/// <item><b>BIL-9604</b> (10 October, due 10 November, Boltworks): taxable 50,000 and IGST 9,000, total 59,000, <b>TDS of 1,000
/// withheld</b>, so the vendor is owed 58,000 and nothing is paid. Awaiting payment.</item>
/// <item><b>BIL-9605</b> (12 October, due 12 November, Steelco), <b>reverse charge</b>: taxable 8,000 and IGST 1,440 the branch pays
/// itself, total 9,440, so the vendor is owed 8,000, of which 3,000 is paid. Partly paid, 5,000 due.</item>
/// <item><b>BIL-9606</b> (15 October, due 15 November, Boltworks) in US dollars at 80: USD 100, or 8,000, of which a payment of USD
/// 40 is 3,200. Partly paid.</item>
/// <item>A voided bill of 3,000, a draft, and a bill dated in September, which are not in the default list.</item>
/// <item>The debit note <b>DBN-9621</b> of 2,360 against BIL-9602, applied to it in full.</item>
/// </list>
/// Without debit notes the six bills come to taxable 101,000, tax 16,740, TDS 1,000, total 117,740, payable 115,300, paid 30,360 and
/// due 84,940. The builders run on lists through the same expressions the repository runs against the read models; the composition
/// is pure. Nothing here needs PostgreSQL.
/// </summary>
public sealed class PayableInvoiceSummaryReportTests
{
    private const long Steelco = 9111, Boltworks = 9112;

    private static readonly DateOnly October1 = new(2026, 10, 1);
    private static readonly DateOnly October31 = new(2026, 10, 31);

    private static BillRead Bill(
        long id, DateOnly date, DateOnly due, long contact, string currency, decimal rate, decimal taxable,
        decimal cgst, decimal sgst, decimal igst, decimal tds = 0m, bool reverseCharge = false, DocumentStatus status = DocumentStatus.Posted)
    {
        decimal total = taxable + cgst + sgst + igst;
        return new()
        {
            BillId = id, TransactionTypeCode = "BIL", DocumentNo = $"BIL-{id}", VendorBillNo = $"PI-{id}", DocumentDate = date, DueDate = due,
            ContactId = contact, CurrencyCode = currency, ExchangeRate = rate, TaxableAmount = taxable, CgstAmount = cgst, SgstAmount = sgst,
            IgstAmount = igst, TdsAmount = tds, IsReverseCharge = reverseCharge, TotalAmount = total, TotalAmountBase = Math.Round(total * rate, 2),
            Status = status,
        };
    }

    private static readonly List<BillRead> Bills =
    [
        Bill(9601, new(2026, 10, 2), new(2026, 10, 17), Steelco, "INR", 1m, 10_000m, 0m, 0m, 1_800m),
        Bill(9602, new(2026, 10, 5), new(2026, 11, 4), Boltworks, "INR", 1m, 20_000m, 1_800m, 1_800m, 0m),
        Bill(9603, new(2026, 10, 8), new(2026, 10, 20), Steelco, "INR", 1m, 5_000m, 0m, 0m, 900m),
        Bill(9604, new(2026, 10, 10), new(2026, 11, 10), Boltworks, "INR", 1m, 50_000m, 0m, 0m, 9_000m, tds: 1_000m),
        Bill(9605, new(2026, 10, 12), new(2026, 11, 12), Steelco, "INR", 1m, 8_000m, 0m, 0m, 1_440m, reverseCharge: true),
        Bill(9606, new(2026, 10, 15), new(2026, 11, 15), Boltworks, "USD", 80m, 100m, 0m, 0m, 0m),
        Bill(9607, new(2026, 10, 18), new(2026, 11, 18), Steelco, "INR", 1m, 3_000m, 0m, 0m, 0m, status: DocumentStatus.Void),
        Bill(9608, new(2026, 10, 19), new(2026, 11, 19), Steelco, "INR", 1m, 2_000m, 0m, 0m, 0m, status: DocumentStatus.Draft),
        Bill(9609, new(2026, 9, 25), new(2026, 10, 25), Steelco, "INR", 1m, 1_500m, 0m, 0m, 0m),
    ];

    private static readonly List<DebitNoteRead> DebitNotes =
    [
        new()
        {
            DebitNoteId = 9621, BillId = 9602, TransactionTypeCode = "DBN", DocumentNo = "DBN-9621", DocumentDate = new(2026, 10, 20), ContactId = Boltworks,
            CurrencyCode = "INR", ExchangeRate = 1m, TaxableAmount = 2_000m, CgstAmount = 180m, SgstAmount = 180m, TotalAmount = 2_360m,
            TotalAmountBase = 2_360m, Status = DocumentStatus.Posted,
        },
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
        // The bill is the credit side, so the source; the payment or debit note is the target.
        Ratio("BIL", 9601, "SPM", 9611, 11_800m, new(2026, 10, 12)),
        Ratio("BIL", 9602, "SPM", 9612, 10_000m, new(2026, 10, 20)),
        Ratio("BIL", 9602, "DBN", 9621, 2_360m, new(2026, 10, 21)),
        Ratio("BIL", 9605, "SPM", 9613, 3_000m, new(2026, 10, 25)),
        // A payment in dollars: the bill's rate takes it to base.
        Ratio("BIL", 9606, "SPM", 9614, 40m, new(2026, 10, 22)),
        // Neither counts on 31 October: one voided, one effective in November.
        Ratio("BIL", 9603, "SPM", 9615, 3_000m, new(2026, 10, 28), voided: true),
        Ratio("BIL", 9603, "SPM", 9616, 900m, new(2026, 11, 2)),
    ];

    private static PayableInvoiceSummaryReportRequest Request(
        long? contact = null, string? currency = null, PayableInvoiceStatus? status = null, bool debitNotes = false, DateOnly? from = null, DateOnly? to = null) =>
        new() { FromDate = from ?? October1, ToDate = to ?? October31, ContactId = contact, CurrencyCode = currency, Status = status, IncludeDebitNotes = debitNotes };

    private static List<PayableInvoiceFigure> FiguresFor(PayableInvoiceSummaryReportRequest request) =>
        [.. PayableInvoiceSummaryReportRepository.Figures(Bills.AsQueryable(), DebitNotes.AsQueryable(), request, request.FromDate!.Value, request.ToDate!.Value)];

    private static List<PaidAmount> PaidFor(PayableInvoiceSummaryReportRequest request) =>
        [.. PayableInvoiceSummaryReportRepository.PaidAmounts(Ratios.AsQueryable(), FiguresFor(request).AsQueryable(), request.ToDate!.Value)];

    private static List<PayableInvoiceRow> RowsFor(PayableInvoiceSummaryReportRequest request)
    {
        IQueryable<PayableInvoiceFigure> figures = FiguresFor(request).AsQueryable();
        return [.. PayableInvoiceSummaryReportRepository.Rows(figures, PayableInvoiceSummaryReportRepository.PaidAmounts(Ratios.AsQueryable(), figures, request.ToDate!.Value), request.ToDate!.Value)];
    }

    private static PayableInvoiceRow Row(IEnumerable<PayableInvoiceRow> rows, string type, long id) => rows.Single(r => r.TypeCode == type && r.DocumentId == id);

    // ---- Which documents --------------------------------------------------------------------

    [Fact]
    public void Posted_and_voided_bills_in_the_period_are_figures_and_drafts_and_other_months_are_not()
    {
        Assert.Equal([9601L, 9602, 9603, 9604, 9605, 9606, 9607], FiguresFor(Request()).Select(f => f.DocumentId).Order());
    }

    [Fact]
    public void A_period_is_by_the_bill_date_and_both_ends_are_in_it()
    {
        Assert.Equal([9602L], FiguresFor(Request(from: new DateOnly(2026, 10, 5), to: new DateOnly(2026, 10, 5))).Select(f => f.DocumentId));
    }

    [Fact]
    public void Debit_notes_are_figures_only_when_asked_for_and_are_negative()
    {
        Assert.DoesNotContain(FiguresFor(Request()), f => f.TypeCode == "DBN");

        PayableInvoiceFigure debit = FiguresFor(Request(debitNotes: true)).Single(f => f.TypeCode == "DBN");
        Assert.Equal((-1, -2_000m, -360m, -2_360m, -2_360m), (debit.Sign, debit.Taxable, debit.Cgst + debit.Sgst, debit.Total, debit.Payable));
    }

    [Fact]
    public void A_foreign_bill_is_in_base_currency_with_its_own_total_kept()
    {
        PayableInvoiceFigure dollars = FiguresFor(Request()).Single(f => f.DocumentId == 9606);

        Assert.Equal((8_000m, 8_000m, 100m, "USD"), (dollars.Taxable, dollars.Total, dollars.TotalSource, dollars.CurrencyCode));
    }

    [Fact]
    public void One_vendor_can_be_asked_for_and_one_currency_whatever_case_it_is_written_in()
    {
        Assert.Equal([9601L, 9603, 9605, 9607], FiguresFor(Request(contact: Steelco)).Select(f => f.DocumentId).Order());
        Assert.Equal([9606L], FiguresFor(Request(currency: " usd ")).Select(f => f.DocumentId));
        Assert.Equal([9602L, 9604, 9606], FiguresFor(Request(contact: Boltworks)).Select(f => f.DocumentId).Order());
    }

    // ---- What the vendor is owed -------------------------------------------------------------

    [Fact]
    public void What_the_vendor_is_owed_is_the_total_less_tds_and_on_a_reverse_charge_bill_the_tax()
    {
        List<PayableInvoiceFigure> figures = FiguresFor(Request());

        Assert.Equal(11_800m, figures.Single(f => f.DocumentId == 9601).Payable);
        Assert.Equal(58_000m, figures.Single(f => f.DocumentId == 9604).Payable);
        Assert.Equal(8_000m, figures.Single(f => f.DocumentId == 9605).Payable);
        Assert.Equal(1_000m, figures.Single(f => f.DocumentId == 9604).Tds);
        Assert.True(figures.Single(f => f.DocumentId == 9605).ReverseCharge);
    }

    // ---- What was paid -----------------------------------------------------------------------

    [Fact]
    public void A_payment_in_dollars_is_taken_to_base_at_the_bills_own_rate()
    {
        Assert.Equal(3_200m, PaidFor(Request()).Single(p => p.DocumentId == 9606).Amount);
    }

    [Fact]
    public void An_allocation_that_is_not_a_money_document_is_taken_as_it_is_and_adds_to_the_payments()
    {
        List<PaidAmount> onTheBill = [.. PaidFor(Request()).Where(p => p.DocumentId == 9602)];

        Assert.Equal([2_360m, 10_000m], onTheBill.Select(p => p.Amount).Order());
    }

    [Fact]
    public void A_voided_settlement_and_one_after_the_to_date_hold_nothing()
    {
        Assert.DoesNotContain(PaidFor(Request()), p => p.DocumentId == 9603);
    }

    [Fact]
    public void A_settlement_effective_on_the_to_date_counts_and_one_the_day_after_does_not()
    {
        Assert.Contains(PaidFor(Request(to: new DateOnly(2026, 10, 25))), p => p.DocumentId == 9605);
        Assert.DoesNotContain(PaidFor(Request(to: new DateOnly(2026, 10, 24))), p => p.DocumentId == 9605);
    }

    [Fact]
    public void A_debit_note_is_settled_at_the_target_end_and_what_was_applied_is_negative()
    {
        PaidAmount applied = PaidFor(Request(debitNotes: true)).Single(p => p is { TypeCode: "DBN", DocumentId: 9621 });

        Assert.Equal(-2_360m, applied.Amount);
        Assert.DoesNotContain(PaidFor(Request()), p => p.TypeCode == "DBN");
    }

    // ---- Paid, due and status ----------------------------------------------------------------

    [Fact]
    public void A_bill_shows_what_was_paid_and_what_is_due_as_at_the_to_date()
    {
        List<PayableInvoiceRow> rows = RowsFor(Request());

        Assert.Equal((11_800m, 0m), (Row(rows, "BIL", 9601).Paid, Row(rows, "BIL", 9601).Due));
        Assert.Equal((12_360m, 11_240m), (Row(rows, "BIL", 9602).Paid, Row(rows, "BIL", 9602).Due));
        Assert.Equal((0m, 5_900m), (Row(rows, "BIL", 9603).Paid, Row(rows, "BIL", 9603).Due));
        Assert.Equal((0m, 58_000m), (Row(rows, "BIL", 9604).Paid, Row(rows, "BIL", 9604).Due));
        Assert.Equal((3_000m, 5_000m), (Row(rows, "BIL", 9605).Paid, Row(rows, "BIL", 9605).Due));
        Assert.Equal((3_200m, 4_800m), (Row(rows, "BIL", 9606).Paid, Row(rows, "BIL", 9606).Due));
    }

    [Fact]
    public void What_the_vendor_is_owed_less_what_was_paid_is_what_is_due_on_every_row_and_is_the_total_less_paid_without_tds_or_reverse_charge()
    {
        List<PayableInvoiceRow> rows = RowsFor(Request(debitNotes: true));

        Assert.All(rows.Where(r => !r.Status.Equals(PayableInvoiceStatus.Void)), r => Assert.Equal(r.Payable - r.Paid, r.Due));
        Assert.All(rows.Where(r => r.Tds == 0m && !r.ReverseCharge && r.Status != PayableInvoiceStatus.Void), r => Assert.Equal(r.Total - r.Paid, r.Due));
    }

    [Theory]
    [InlineData(9601L, PayableInvoiceStatus.Paid)]
    [InlineData(9602L, PayableInvoiceStatus.PartlyPaid)]
    [InlineData(9603L, PayableInvoiceStatus.Overdue)]
    [InlineData(9604L, PayableInvoiceStatus.AwaitingPayment)]
    [InlineData(9605L, PayableInvoiceStatus.PartlyPaid)]
    [InlineData(9606L, PayableInvoiceStatus.PartlyPaid)]
    [InlineData(9607L, PayableInvoiceStatus.Void)]
    public void Each_status_is_told_from_what_is_owed_what_was_paid_and_the_due_date(long id, PayableInvoiceStatus expected)
    {
        Assert.Equal(expected, Row(RowsFor(Request()), "BIL", id).Status);
    }

    [Fact]
    public void Overdue_wins_over_partly_paid()
    {
        BillRead part = Bill(9700, new(2026, 10, 1), new(2026, 10, 10), Steelco, "INR", 1m, 1_000m, 0m, 0m, 0m);
        Bills.Add(part);
        Ratios.Add(Ratio("BIL", 9700, "SPM", 9701, 400m, new(2026, 10, 5)));
        try
        {
            PayableInvoiceRow row = Row(RowsFor(Request()), "BIL", 9700);

            Assert.Equal((400m, 600m, PayableInvoiceStatus.Overdue), (row.Paid, row.Due, row.Status));
        }
        finally
        {
            Bills.Remove(part);
            Ratios.RemoveAll(r => r.SourceTransactionId == 9700);
        }
    }

    [Fact]
    public void A_bill_due_on_the_to_date_is_not_yet_overdue_and_one_due_the_day_before_is()
    {
        Assert.Equal(PayableInvoiceStatus.AwaitingPayment, Row(RowsFor(Request(to: new DateOnly(2026, 11, 10))), "BIL", 9604).Status);
        Assert.Equal(PayableInvoiceStatus.Overdue, Row(RowsFor(Request(to: new DateOnly(2026, 11, 11))), "BIL", 9604).Status);
    }

    [Fact]
    public void Paid_and_due_are_as_at_the_to_date_so_an_earlier_one_reads_as_it_did_then()
    {
        List<PayableInvoiceRow> rows = RowsFor(Request(to: new DateOnly(2026, 10, 21)));

        Assert.Equal((12_360m, 11_240m), (Row(rows, "BIL", 9602).Paid, Row(rows, "BIL", 9602).Due));
        Assert.Equal((0m, 8_000m), (Row(rows, "BIL", 9605).Paid, Row(rows, "BIL", 9605).Due));
        Assert.Equal(PayableInvoiceStatus.Overdue, Row(rows, "BIL", 9603).Status);
    }

    [Fact]
    public void A_bill_with_nothing_left_owed_or_more_than_it_came_to_paid_is_paid()
    {
        BillRead over = Bill(9701, new(2026, 10, 3), new(2026, 11, 3), Steelco, "INR", 1m, 500m, 0m, 0m, 0m);
        Bills.Add(over);
        Ratios.Add(Ratio("BIL", 9701, "SPM", 9702, 800m, new(2026, 10, 4)));
        try
        {
            PayableInvoiceRow row = Row(RowsFor(Request()), "BIL", 9701);

            Assert.Equal((PayableInvoiceStatus.Paid, -300m), (row.Status, row.Due));
        }
        finally
        {
            Bills.Remove(over);
            Ratios.RemoveAll(r => r.SourceTransactionId == 9701);
        }
    }

    [Fact]
    public void A_voided_bill_settles_nothing_and_owes_nothing()
    {
        PayableInvoiceRow voided = Row(RowsFor(Request()), "BIL", 9607);

        Assert.Equal((0m, 0m, PayableInvoiceStatus.Void), (voided.Paid, voided.Due, voided.Status));
    }

    [Fact]
    public void A_debit_note_applied_in_full_is_paid_and_one_applied_to_nothing_is_awaiting_with_a_negative_due()
    {
        PayableInvoiceRow applied = Row(RowsFor(Request(debitNotes: true)), "DBN", 9621);

        Assert.Equal((PayableInvoiceStatus.Paid, -2_360m, 0m), (applied.Status, applied.Paid, applied.Due));

        Ratios.RemoveAll(r => r.TargetTransactionTypeCode == "DBN");
        try
        {
            PayableInvoiceRow open = Row(RowsFor(Request(debitNotes: true)), "DBN", 9621);

            Assert.Equal((PayableInvoiceStatus.AwaitingPayment, 0m, -2_360m), (open.Status, open.Paid, open.Due));
        }
        finally
        {
            Ratios.Add(Ratio("BIL", 9602, "DBN", 9621, 2_360m, new(2026, 10, 21)));
        }
    }

    [Fact]
    public void Without_a_status_the_void_ones_are_left_out_and_asked_for_by_name_they_are_the_only_ones()
    {
        IQueryable<PayableInvoiceRow> rows = RowsFor(Request()).AsQueryable();

        Assert.DoesNotContain(PayableInvoiceSummaryReportRepository.WithStatus(rows, null), r => r.Status == PayableInvoiceStatus.Void);
        Assert.Equal([9607L], PayableInvoiceSummaryReportRepository.WithStatus(rows, PayableInvoiceStatus.Void).Select(r => r.DocumentId));
    }

    [Fact]
    public void A_status_asked_for_lists_only_the_documents_with_it()
    {
        IQueryable<PayableInvoiceRow> rows = RowsFor(Request()).AsQueryable();

        Assert.Equal([9602L, 9605, 9606], PayableInvoiceSummaryReportRepository.WithStatus(rows, PayableInvoiceStatus.PartlyPaid).Select(r => r.DocumentId).Order());
        Assert.Equal([9603L], PayableInvoiceSummaryReportRepository.WithStatus(rows, PayableInvoiceStatus.Overdue).Select(r => r.DocumentId));
    }

    // ---- The figures ---------------------------------------------------------------------------

    private static PayableInvoiceSummaryTotalsDto TotalsOf(IEnumerable<PayableInvoiceRow> rows)
    {
        List<PayableInvoiceRow> listed = [.. PayableInvoiceSummaryReportRepository.WithStatus(rows.AsQueryable(), null)];
        return new PayableInvoiceSummaryTotalsDto
        {
            Taxable = listed.Sum(r => r.Taxable), Tax = listed.Sum(r => r.Cgst + r.Sgst + r.Igst + r.Cess), Tds = listed.Sum(r => r.Tds),
            Total = listed.Sum(r => r.Total), Payable = listed.Sum(r => r.Payable), Paid = listed.Sum(r => r.Paid), Due = listed.Sum(r => r.Due),
        };
    }

    [Fact]
    public void The_periods_figures_add_up_as_worked_by_hand()
    {
        PayableInvoiceSummaryTotalsDto totals = TotalsOf(RowsFor(Request()));

        Assert.Equal((101_000m, 16_740m, 1_000m, 117_740m), (totals.Taxable, totals.Tax, totals.Tds, totals.Total));
        Assert.Equal((115_300m, 30_360m, 84_940m), (totals.Payable, totals.Paid, totals.Due));
        Assert.Equal(totals.Payable - totals.Paid, totals.Due);
    }

    [Fact]
    public void With_debit_notes_the_figures_take_their_negatives_off()
    {
        PayableInvoiceSummaryTotalsDto totals = TotalsOf(RowsFor(Request(debitNotes: true)));

        Assert.Equal((99_000m, 16_380m, 115_380m), (totals.Taxable, totals.Tax, totals.Total));
        Assert.Equal((112_940m, 28_000m, 84_940m), (totals.Payable, totals.Paid, totals.Due));
    }

    [Fact]
    public void The_periods_taxable_total_is_the_sum_of_each_posted_bills_taxable_amount_in_base()
    {
        decimal expected = Bills
            .Where(b => b.Status == DocumentStatus.Posted && b.DocumentDate >= October1 && b.DocumentDate <= October31)
            .Sum(b => Math.Round(b.TaxableAmount * b.ExchangeRate, 2));

        Assert.Equal(expected, TotalsOf(RowsFor(Request())).Taxable);
    }

    // ---- Compose -------------------------------------------------------------------------------

    private static PayableInvoiceSummaryReportResultDto Compose(PayableInvoiceSummaryReportRequest? request = null, int page = 1, int size = 50)
    {
        request ??= Request();
        request.PageNumber = page;
        request.PageSize = size;
        List<PayableInvoiceRow> rows = RowsFor(Request(request.ContactId, request.CurrencyCode, null, request.IncludeDebitNotes));
        IQueryable<PayableInvoiceRow> matching = PayableInvoiceSummaryReportRepository.WithStatus(rows.AsQueryable(), request.Status);
        List<PayableInvoiceRow> ordered = [.. matching.OrderByDescending(r => r.DocumentDate).ThenByDescending(r => r.TypeCode).ThenByDescending(r => r.DocumentId)];

        return PayableInvoiceSummaryReportRepository.Compose(
            request,
            [.. ordered.Skip((page - 1) * size).Take(size)],
            ordered.Count,
            [
                new() { ContactId = Steelco, Code = "STEELCO", Name = "Steelco Supplies", IsMsme = true },
                new() { ContactId = Boltworks, Code = "BOLTWORKS", Name = "Boltworks Ltd" },
            ],
            [.. rows.GroupBy(r => r.Status).Select(g => new PayableStatusCount { Status = g.Key, Count = g.Count() })],
            TotalsOf(ordered),
            "INR");
    }

    [Fact]
    public void A_row_carries_its_vendor_its_figures_in_base_and_its_own_currency_total()
    {
        PayableInvoiceSummaryRowDto dollars = Compose().Invoices.Single(r => r.DocumentId == 9606);

        Assert.Equal(("BOLTWORKS", "Boltworks Ltd", "USD", 100m, 8_000m), (dollars.ContactCode, dollars.ContactName, dollars.CurrencyCode, dollars.TotalInCurrency, dollars.Total));
        Assert.Equal(("PI-9606", "BIL"), (dollars.VendorBillNo, dollars.TypeCode));
    }

    [Fact]
    public void The_tax_is_the_four_taxes_added_up_and_the_components_are_kept_and_tds_and_reverse_charge_are_carried()
    {
        PayableInvoiceSummaryReportResultDto report = Compose();
        PayableInvoiceSummaryRowDto split = report.Invoices.Single(r => r.DocumentId == 9602);
        PayableInvoiceSummaryRowDto tds = report.Invoices.Single(r => r.DocumentId == 9604);
        PayableInvoiceSummaryRowDto rcm = report.Invoices.Single(r => r.DocumentId == 9605);

        Assert.Equal((1_800m, 1_800m, 0m, 3_600m), (split.Cgst, split.Sgst, split.Igst, split.Tax));
        Assert.Equal((1_000m, 58_000m, 59_000m), (tds.Tds, tds.Payable, tds.Total));
        Assert.Equal((true, 8_000m, 1_440m), (rcm.ReverseCharge, rcm.Payable, rcm.Tax));
    }

    [Fact]
    public void Days_overdue_are_given_only_to_an_overdue_bill_and_run_from_its_due_date_to_the_to_date()
    {
        PayableInvoiceSummaryReportResultDto report = Compose();

        Assert.Equal(11, report.Invoices.Single(r => r.DocumentId == 9603).DaysOverdue);
        Assert.All(report.Invoices.Where(r => r.DocumentId != 9603), r => Assert.Null(r.DaysOverdue));
    }

    [Fact]
    public void Every_status_has_a_count_and_a_nought_is_kept_so_a_tab_can_say_nothing_is_in_it()
    {
        PayableInvoiceSummaryReportResultDto report = PayableInvoiceSummaryReportRepository.Compose(
            Request(), [], 0, [], [new() { Status = PayableInvoiceStatus.Paid, Count = 4 }], new(), "INR");

        Assert.Equal(Enum.GetValues<PayableInvoiceStatus>().Length, report.StatusCounts.Count);
        Assert.Equal(4, report.StatusCounts[PayableInvoiceStatus.Paid]);
        Assert.Equal(0, report.StatusCounts[PayableInvoiceStatus.Overdue]);
    }

    [Fact]
    public void The_statuses_of_the_period_are_counted_as_worked_by_hand()
    {
        Dictionary<PayableInvoiceStatus, int> counts = Compose().StatusCounts;

        Assert.Equal((1, 3, 1, 1, 1), (counts[PayableInvoiceStatus.AwaitingPayment], counts[PayableInvoiceStatus.PartlyPaid], counts[PayableInvoiceStatus.Overdue], counts[PayableInvoiceStatus.Paid], counts[PayableInvoiceStatus.Void]));
    }

    [Fact]
    public void The_page_the_period_and_the_totals_of_every_match_are_passed_on()
    {
        PayableInvoiceSummaryReportResultDto report = Compose(page: 2, size: 4);

        Assert.Equal((October1, October31, "INR", 6), (report.FromDate, report.ToDate, report.BaseCurrencyCode, report.TotalInvoices));
        Assert.Equal((2, 4), (report.PageNumber, report.PageSize));
        Assert.Equal(2, report.Invoices.Count);
        Assert.Equal(84_940m, report.Totals.Due);
    }

    [Fact]
    public void A_vendor_the_names_do_not_know_is_still_listed_by_its_number()
    {
        PayableInvoiceSummaryReportResultDto report = PayableInvoiceSummaryReportRepository.Compose(
            Request(), [new() { TypeCode = "BIL", DocumentId = 1, ContactId = 77, DocumentDate = October1 }], 1, [], [], new(), "INR");

        Assert.Equal("Vendor 77", report.Invoices.Single().ContactName);
    }

    // ---- The request and the guards --------------------------------------------------------------

    [Fact]
    public void The_two_dates_are_required_and_nothing_else_is()
    {
        Assert.Equal(
            [nameof(PayableInvoiceSummaryReportRequest.FromDate), nameof(PayableInvoiceSummaryReportRequest.ToDate)],
            typeof(PayableInvoiceSummaryReportRequest).GetProperties()
                .Where(p => p.GetCustomAttribute<RequiredAttribute>() is not null)
                .Select(p => p.Name));
    }

    [Fact]
    public void A_missing_date_is_refused_at_its_field_by_the_request_itself()
    {
        var request = new PayableInvoiceSummaryReportRequest();
        var results = new List<ValidationResult>();

        Assert.False(Validator.TryValidateObject(request, new ValidationContext(request), results, validateAllProperties: true));
        Assert.Equal(["From date is required.", "To date is required."], results.Select(r => r.ErrorMessage).OrderBy(m => m, StringComparer.Ordinal));
    }

    [Fact]
    public void A_page_size_above_a_hundred_is_refused_by_the_request_itself()
    {
        var request = new PayableInvoiceSummaryReportRequest { FromDate = October1, ToDate = October31, PageSize = 101 };
        var results = new List<ValidationResult>();

        Assert.False(Validator.TryValidateObject(request, new ValidationContext(request), results, validateAllProperties: true));
        Assert.Equal(["Page size must be between 1 and 100."], results.Select(r => r.ErrorMessage));
    }

    [Fact]
    public void A_period_that_ends_before_it_begins_a_currency_that_is_not_three_letters_and_an_unknown_status_are_refused_at_their_fields()
    {
        Assert.Equal(
            [(nameof(PayableInvoiceSummaryReportRequest.ToDate), "To date cannot be before the from date.")],
            PayableInvoiceSummaryReportRepository.Refusals(new PayableInvoiceSummaryReportRequest { FromDate = October31, ToDate = October1 }));
        Assert.Equal(
            [(nameof(PayableInvoiceSummaryReportRequest.CurrencyCode), "Currency must be a 3-letter code.")],
            PayableInvoiceSummaryReportRepository.Refusals(Request(currency: "US")));
        Assert.Equal(
            [(nameof(PayableInvoiceSummaryReportRequest.Status), "Status must be one of the statuses offered.")],
            PayableInvoiceSummaryReportRepository.Refusals(Request(status: (PayableInvoiceStatus)9)));
        Assert.Empty(PayableInvoiceSummaryReportRepository.Refusals(Request(currency: " ")));
    }

    [Fact]
    public async Task Another_branch_in_the_request_is_forbidden_not_hidden()
    {
        var controller = new PayableInvoiceSummaryReportController(null!)
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
            new PayableInvoiceSummaryReportRequest { FromDate = October1, ToDate = October31, OrgId = Guid.NewGuid() }, default);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public void The_endpoint_needs_the_reports_module_purchase_view_and_a_trade_on_each_action()
    {
        Type controller = typeof(PayableInvoiceSummaryReportController);

        Assert.Equal("api/statements/payable-invoice-summary", controller.GetCustomAttribute<RouteAttribute>()!.Template);
        Assert.Equal("reports", controller.GetCustomAttribute<RequireModulePermissionAttribute>()!.Module);
        Assert.NotNull(controller.GetCustomAttribute<RequireBusinessTypeAttribute>());

        foreach (string action in new[]
        {
            nameof(PayableInvoiceSummaryReportController.Run),
            nameof(PayableInvoiceSummaryReportController.Vendors),
            nameof(PayableInvoiceSummaryReportController.Currencies),
        })
        {
            Assert.Contains(
                "purchase.view",
                controller.GetMethod(action)!.GetCustomAttributes<RequirePermissionAttribute>().Select(a => a.Permission));
        }
    }

    [Fact]
    public void The_lookups_are_sub_routes_of_the_report()
    {
        Type controller = typeof(PayableInvoiceSummaryReportController);

        Assert.Equal("vendors", controller.GetMethod(nameof(PayableInvoiceSummaryReportController.Vendors))!.GetCustomAttribute<HttpGetAttribute>()!.Template);
        Assert.Equal("currencies", controller.GetMethod(nameof(PayableInvoiceSummaryReportController.Currencies))!.GetCustomAttribute<HttpGetAttribute>()!.Template);
    }

    // ---- The catalog source, held to the same period ------------------------------------------------

    [Fact]
    public void The_catalog_source_declares_both_dates_required_and_keeps_its_columns()
    {
        var source = new PayableInvoiceSummarySourceRepository();

        Assert.Equal(["from", "to"], source.Parameters.Select(p => p.Name));
        Assert.All(source.Parameters, p => Assert.True(p.IsRequired));
        Assert.Equal(["documentNo", "documentDate", "contactName", "totalAmount", "amountDue", "billId"], source.Columns.Select(c => c.Key));
    }
}
