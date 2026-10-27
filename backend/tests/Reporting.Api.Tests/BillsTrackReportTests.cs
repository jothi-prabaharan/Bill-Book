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
using Shared.Kernel.Approvals;
using Shared.Kernel.Documents;
using Shared.Kernel.Internal;
using Xunit;

namespace Reporting.Api.Tests;

/// <summary>
/// The Bills Track Report (TK-237), on the Payable Invoice Summary's books worked by hand, the branch's base
/// currency INR and the period October 2026, so what is paid and due is as at 31 October:
/// <list type="bullet">
/// <item><b>BIL-9701</b> (11,800, Steelco): all paid on 12 October. Paid, last paid 12 October; it went through an
/// approval workflow and was approved.</item>
/// <item><b>BIL-9702</b> (23,600, due 4 November, Boltworks): 10,000 paid on 20 October and a debit note of 2,360 applied on
/// 26 October, so 12,360 paid and 11,240 due. Partly paid, last settled 26 October.</item>
/// <item><b>BIL-9703</b> (5,900, due 20 October, Steelco): a settlement voided and one in November do not count. Overdue by
/// 11 days, never paid.</item>
/// <item><b>BIL-9704</b> (59,000, due 10 November, Boltworks): TDS of 1,000 withheld, so the vendor is owed 58,000 and none
/// is paid. Awaiting payment, 58,000 due.</item>
/// <item><b>BIL-9705</b> (9,440, due 12 November, Steelco), reverse charge: the vendor is owed 8,000, of which 3,000 is paid
/// on 25 October. Partly paid, 5,000 due.</item>
/// <item><b>BIL-9706</b> in US dollars at 80 (USD 100, 8,000): a payment of USD 40 is 3,200 paid on 22 October. Partly paid.</item>
/// <item><b>BIL-9707</b> voided; <b>BIL-9708</b> a draft (2,360) in approval, waiting on Level 2; <b>BIL-9709</b> ready to
/// post (1,180, Boltworks), approved; and a bill dated in September.</item>
/// </list>
/// The posted ones the default list holds come to 117,740 with 30,360 paid and 84,940 due, the summary's figures; the
/// drafts are left out of the totals. The builders run on lists through the same expressions the repository runs against
/// the read models; the composition is pure. Nothing here needs PostgreSQL.
/// </summary>
public sealed class BillsTrackReportTests
{
    private const long Steelco = 9111, Boltworks = 9112;

    private static readonly DateOnly October1 = new(2026, 10, 1);
    private static readonly DateOnly October31 = new(2026, 10, 31);

    private static BillRead Bill(
        long id, DateOnly date, DateOnly due, long contact, string currency, decimal rate, decimal taxable,
        decimal cgst, decimal sgst, decimal igst, decimal tds = 0m, bool reverseCharge = false, DocumentStatus status = DocumentStatus.Posted,
        ApprovalStatus? approval = null, string? step = null)
    {
        decimal total = taxable + cgst + sgst + igst;
        return new()
        {
            BillId = id, TransactionTypeCode = "BIL", DocumentNo = $"BIL-{id}", VendorBillNo = $"V/{id}", DocumentDate = date, DueDate = due,
            ContactId = contact, CurrencyCode = currency, ExchangeRate = rate, TaxableAmount = taxable, CgstAmount = cgst, SgstAmount = sgst,
            IgstAmount = igst, TdsAmount = tds, IsReverseCharge = reverseCharge, TotalAmount = total, TotalAmountBase = Math.Round(total * rate, 2),
            Status = status, ApprovalStatus = approval, CurrentStepLabel = step,
            PostedAt = status is DocumentStatus.Posted or DocumentStatus.Void ? new DateTimeOffset(date.ToDateTime(new TimeOnly(9, 0)), TimeSpan.Zero) : null,
        };
    }

    private static readonly List<BillRead> Bills =
    [
        Bill(9701, new(2026, 10, 2), new(2026, 10, 17), Steelco, "INR", 1m, 10_000m, 0m, 0m, 1_800m, approval: ApprovalStatus.Approved),
        Bill(9702, new(2026, 10, 5), new(2026, 11, 4), Boltworks, "INR", 1m, 20_000m, 1_800m, 1_800m, 0m),
        Bill(9703, new(2026, 10, 8), new(2026, 10, 20), Steelco, "INR", 1m, 5_000m, 0m, 0m, 900m),
        Bill(9704, new(2026, 10, 10), new(2026, 11, 10), Boltworks, "INR", 1m, 50_000m, 0m, 0m, 9_000m, tds: 1_000m),
        Bill(9705, new(2026, 10, 12), new(2026, 11, 12), Steelco, "INR", 1m, 8_000m, 0m, 0m, 1_440m, reverseCharge: true),
        Bill(9706, new(2026, 10, 15), new(2026, 11, 15), Boltworks, "USD", 80m, 100m, 0m, 0m, 0m),
        Bill(9707, new(2026, 10, 18), new(2026, 11, 18), Steelco, "INR", 1m, 1_000m, 90m, 90m, 0m, status: DocumentStatus.Void),
        Bill(9708, new(2026, 10, 22), default, Steelco, "INR", 1m, 2_000m, 180m, 180m, 0m, status: DocumentStatus.Draft, approval: ApprovalStatus.InApproval, step: "Level 2"),
        Bill(9709, new(2026, 10, 28), default, Boltworks, "INR", 1m, 1_000m, 90m, 90m, 0m, status: DocumentStatus.ReadyToPost, approval: ApprovalStatus.Approved),
        Bill(9710, new(2026, 9, 20), new(2026, 10, 20), Steelco, "INR", 1m, 4_000m, 360m, 360m, 0m),
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
        Ratio("BIL", 9701, "SPM", 9711, 11_800m, new(2026, 10, 12)),
        Ratio("BIL", 9702, "SPM", 9712, 10_000m, new(2026, 10, 20)),
        Ratio("BIL", 9702, "DBN", 9721, 2_360m, new(2026, 10, 26)),
        Ratio("BIL", 9705, "SPM", 9715, 3_000m, new(2026, 10, 25)),
        // USD 40 in the payment's own currency: the bill's rate takes it to 3,200.
        Ratio("BIL", 9706, "SPM", 9716, 40m, new(2026, 10, 22)),
        // Neither counts on 31 October: one voided, one effective in November.
        Ratio("BIL", 9703, "SPM", 9713, 5_900m, new(2026, 10, 28), voided: true),
        Ratio("BIL", 9703, "SPM", 9714, 5_900m, new(2026, 11, 2)),
    ];

    private static BillsTrackReportRequest Request(BillTrackStatus? status = null, long? contact = null, DateOnly? from = null, DateOnly? to = null) =>
        new() { FromDate = from ?? October1, ToDate = to ?? October31, Status = status, ContactId = contact };

    /// <summary>Every entry for a request, through the same builders the repository uses, before any status filter.</summary>
    private static List<BillTrackEntry> EntriesFor(BillsTrackReportRequest request) =>
    [
        .. BillsTrackReportRepository.Entries(Bills.AsQueryable(), Ratios.AsQueryable(), request, request.FromDate!.Value, request.ToDate!.Value),
    ];

    private static BillTrackEntry Entry(IEnumerable<BillTrackEntry> entries, long id) => entries.Single(e => e.DocumentId == id);

    private static readonly List<VendorRef> Vendors =
    [
        new() { ContactId = Steelco, Code = "STEELCO", Name = "Steelco Metals" },
        new() { ContactId = Boltworks, Code = "BOLTWORKS", Name = "Boltworks Imports" },
    ];

    // ---- Which bills and what status ---------------------------------------------------

    [Fact]
    public void Every_bill_in_the_period_is_an_entry_the_drafts_and_the_voided_one_included_and_other_months_are_not()
    {
        List<BillTrackEntry> entries = EntriesFor(Request());

        Assert.Equal([9701L, 9702, 9703, 9704, 9705, 9706, 9707, 9708, 9709], entries.Select(e => e.DocumentId).Order());
    }

    [Fact]
    public void A_posted_bills_status_is_worked_out_as_the_summary_does()
    {
        List<BillTrackEntry> entries = EntriesFor(Request());

        Assert.Equal(BillTrackStatus.Paid, Entry(entries, 9701).Status);
        Assert.Equal(BillTrackStatus.PartlyPaid, Entry(entries, 9702).Status);
        Assert.Equal(BillTrackStatus.Overdue, Entry(entries, 9703).Status);
        Assert.Equal(BillTrackStatus.AwaitingPayment, Entry(entries, 9704).Status);
        Assert.Equal(BillTrackStatus.PartlyPaid, Entry(entries, 9705).Status);
        Assert.Equal(BillTrackStatus.PartlyPaid, Entry(entries, 9706).Status);
        Assert.Equal(BillTrackStatus.Void, Entry(entries, 9707).Status);
    }

    [Fact]
    public void A_draft_and_a_ready_to_post_bill_have_their_own_status_no_due_date_and_owe_and_have_paid_nothing()
    {
        List<BillTrackEntry> entries = EntriesFor(Request());

        BillTrackEntry draft = Entry(entries, 9708);
        Assert.Equal((BillTrackStatus.Draft, 2_360m, 0m, 0m, 0m), (draft.Status, draft.Total, draft.Payable, draft.Paid, draft.Due));
        Assert.Null(draft.DueDate);
        BillTrackEntry ready = Entry(entries, 9709);
        Assert.Equal((BillTrackStatus.ReadyToPost, 1_180m, 0m, 0m), (ready.Status, ready.Total, ready.Paid, ready.Due));
        Assert.Null(ready.DueDate);
    }

    [Fact]
    public void What_was_paid_and_is_due_is_as_at_the_to_date_and_agrees_with_the_summary()
    {
        List<BillTrackEntry> entries = EntriesFor(Request());

        Assert.Equal((11_800m, 0m), (Entry(entries, 9701).Paid, Entry(entries, 9701).Due));
        // A payment and a debit note applied to the bill both count.
        Assert.Equal((12_360m, 11_240m), (Entry(entries, 9702).Paid, Entry(entries, 9702).Due));
        // A settlement that was voided, and one effective after the date, are not counted.
        Assert.Equal((0m, 5_900m), (Entry(entries, 9703).Paid, Entry(entries, 9703).Due));
        Assert.Equal((3_200m, 4_800m), (Entry(entries, 9706).Paid, Entry(entries, 9706).Due));
    }

    [Fact]
    public void What_the_vendor_is_owed_is_the_total_less_tds_and_on_a_reverse_charge_bill_the_tax()
    {
        List<BillTrackEntry> entries = EntriesFor(Request());

        BillTrackEntry tds = Entry(entries, 9704);
        Assert.Equal((59_000m, 58_000m, 0m, 58_000m), (tds.Total, tds.Payable, tds.Paid, tds.Due));
        BillTrackEntry reverse = Entry(entries, 9705);
        Assert.True(reverse.ReverseCharge);
        Assert.Equal((9_440m, 8_000m, 3_000m, 5_000m), (reverse.Total, reverse.Payable, reverse.Paid, reverse.Due));
    }

    [Fact]
    public void An_earlier_to_date_counts_only_what_was_settled_by_then_and_changes_the_status()
    {
        // On 20 October BIL-9702 had 10,000 paid and the debit note was not yet applied; BIL-9703 fell due that day, so was not yet late.
        List<BillTrackEntry> entries = EntriesFor(Request(to: new DateOnly(2026, 10, 20)));

        BillTrackEntry bill = Entry(entries, 9702);
        Assert.Equal((10_000m, 13_600m, BillTrackStatus.PartlyPaid), (bill.Paid, bill.Due, bill.Status));
        Assert.Equal(BillTrackStatus.AwaitingPayment, Entry(entries, 9703).Status);
        Assert.DoesNotContain(entries, e => e.DocumentId == 9706 && e.DocumentDate > new DateOnly(2026, 10, 20));
    }

    [Fact]
    public void A_foreign_bill_carries_its_own_total_and_its_total_in_base()
    {
        BillTrackEntry usd = Entry(EntriesFor(Request()), 9706);

        Assert.Equal(("USD", 100m, 8_000m), (usd.CurrencyCode, usd.TotalSource, usd.Total));
    }

    [Fact]
    public void The_vendor_filter_narrows_the_posted_and_the_draft_entries_alike()
    {
        List<BillTrackEntry> entries = EntriesFor(Request(contact: Boltworks));

        Assert.Equal([9702L, 9704, 9706, 9709], entries.Select(e => e.DocumentId).Order());
    }

    [Fact]
    public void An_entry_carries_the_vendors_own_bill_number()
    {
        Assert.Equal("V/9701", Entry(EntriesFor(Request()), 9701).VendorBillNo);
        Assert.Equal("V/9708", Entry(EntriesFor(Request()), 9708).VendorBillNo);
    }

    // ---- Status filter and totals -------------------------------------------------------

    [Fact]
    public void With_no_status_chosen_every_entry_but_the_void_one_is_listed()
    {
        List<BillTrackEntry> listed = [.. BillsTrackReportRepository.WithStatus(EntriesFor(Request()).AsQueryable(), null)];

        Assert.Equal([9701L, 9702, 9703, 9704, 9705, 9706, 9708, 9709], listed.Select(e => e.DocumentId).Order());
    }

    [Theory]
    [InlineData(BillTrackStatus.Draft, new long[] { 9708 })]
    [InlineData(BillTrackStatus.ReadyToPost, new long[] { 9709 })]
    [InlineData(BillTrackStatus.AwaitingPayment, new long[] { 9704 })]
    [InlineData(BillTrackStatus.PartlyPaid, new long[] { 9702, 9705, 9706 })]
    [InlineData(BillTrackStatus.Overdue, new long[] { 9703 })]
    [InlineData(BillTrackStatus.Paid, new long[] { 9701 })]
    [InlineData(BillTrackStatus.Void, new long[] { 9707 })]
    public void A_status_chosen_lists_only_the_bills_with_it(BillTrackStatus status, long[] expected)
    {
        List<BillTrackEntry> listed = [.. BillsTrackReportRepository.WithStatus(EntriesFor(Request()).AsQueryable(), status)];

        Assert.Equal(expected, listed.Select(e => e.DocumentId).Order());
    }

    [Fact]
    public void The_totals_add_up_the_posted_bills_only_and_the_due_equals_the_summarys()
    {
        List<BillTrackEntry> posted = [.. BillsTrackReportRepository.Posted(
            BillsTrackReportRepository.WithStatus(EntriesFor(Request()).AsQueryable(), null))];

        Assert.Equal([9701L, 9702, 9703, 9704, 9705, 9706], posted.Select(e => e.DocumentId).Order());
        Assert.Equal((117_740m, 30_360m, 84_940m), (posted.Sum(e => e.Total), posted.Sum(e => e.Paid), posted.Sum(e => e.Due)));
        // With TDS or reverse charge the due is what is owed to the vendor less what was paid, not the total less what was paid.
        Assert.All(posted, e => Assert.Equal(e.Payable - e.Paid, e.Due));
    }

    [Fact]
    public void A_voided_bill_is_not_a_posted_one_for_the_totals_and_a_draft_is_not_either()
    {
        List<BillTrackEntry> posted = [.. BillsTrackReportRepository.Posted(EntriesFor(Request()).AsQueryable())];

        Assert.DoesNotContain(posted, e => e.DocumentId is 9707 or 9708 or 9709);
        Assert.Equal(6, posted.Count);
    }

    // ---- The dates of its life and its approval ----------------------------------------

    [Fact]
    public void The_posting_moment_and_approval_are_read_for_the_bills_asked_for_and_a_draft_has_no_posting()
    {
        List<BillStamp> stamps = [.. BillsTrackReportRepository.Stamps(Bills.AsQueryable(), [9701L, 9708, 9709])];

        Assert.Equal([9701L, 9708, 9709], stamps.Select(s => s.DocumentId).Order());
        BillStamp approved = stamps.Single(s => s.DocumentId == 9701);
        Assert.NotNull(approved.PostedAt);
        Assert.Equal(ApprovalStatus.Approved, approved.ApprovalStatus);
        BillStamp draft = stamps.Single(s => s.DocumentId == 9708);
        Assert.Null(draft.PostedAt);
        Assert.Equal((ApprovalStatus.InApproval, "Level 2"), (draft.ApprovalStatus, draft.StepLabel));
    }

    [Fact]
    public void The_last_payment_is_the_latest_settlement_on_or_before_the_date_and_a_debit_note_applied_counts()
    {
        List<BillLastPayment> last = [.. BillsTrackReportRepository.LastPayments(Ratios.AsQueryable(), [9701L, 9702, 9703, 9704, 9705, 9706], October31)];

        Assert.Equal(new DateOnly(2026, 10, 12), last.Single(p => p.DocumentId == 9701).Date);
        // 20 October by payment, 26 October by debit note: the later one.
        Assert.Equal(new DateOnly(2026, 10, 26), last.Single(p => p.DocumentId == 9702).Date);
        Assert.Equal(new DateOnly(2026, 10, 25), last.Single(p => p.DocumentId == 9705).Date);
        Assert.Equal(new DateOnly(2026, 10, 22), last.Single(p => p.DocumentId == 9706).Date);
        Assert.DoesNotContain(last, p => p.DocumentId is 9703 or 9704);
    }

    [Fact]
    public void A_voided_settlement_and_one_after_the_date_are_not_a_last_payment()
    {
        Assert.Empty(BillsTrackReportRepository.LastPayments(Ratios.AsQueryable(), [9703L], October31));
    }

    [Fact]
    public void An_earlier_date_leaves_out_a_later_settlement()
    {
        List<BillLastPayment> last = [.. BillsTrackReportRepository.LastPayments(Ratios.AsQueryable(), [9702L], new DateOnly(2026, 10, 22))];

        Assert.Equal(new DateOnly(2026, 10, 20), Assert.Single(last).Date);
    }

    // ---- Compose -------------------------------------------------------------------------

    private static BillsTrackReportResultDto Run(
        BillsTrackReportRequest? request = null, IReadOnlyList<BillTrackStatusCount>? counts = null, BillsTrackTotalsDto? totals = null)
    {
        request ??= Request();
        List<BillTrackEntry> page = [.. BillsTrackReportRepository.WithStatus(EntriesFor(request).AsQueryable(), request.Status)
            .OrderByDescending(e => e.DocumentDate).ThenByDescending(e => e.DocumentId)];
        long[] ids = [.. page.Select(e => e.DocumentId)];
        List<BillStamp> stamps = [.. BillsTrackReportRepository.Stamps(Bills.AsQueryable(), ids)];
        List<BillLastPayment> payments = [.. BillsTrackReportRepository.LastPayments(Ratios.AsQueryable(), ids, request.ToDate!.Value)];

        return BillsTrackReportRepository.Compose(request, page, page.Count, Vendors, stamps, payments, counts ?? [], totals ?? new(), "INR");
    }

    private static BillsTrackRowDto Row(BillsTrackReportResultDto report, long id) => report.Bills.Single(b => b.DocumentId == id);

    [Fact]
    public void The_bills_are_newest_first_with_their_vendors_and_dates()
    {
        BillsTrackReportResultDto report = Run();

        Assert.Equal([9709L, 9708, 9706, 9705, 9704, 9703, 9702, 9701], report.Bills.Select(b => b.DocumentId));
        BillsTrackRowDto overdue = Row(report, 9703);
        Assert.Equal(("STEELCO", "Steelco Metals", BillTrackStatus.Overdue), (overdue.ContactCode, overdue.ContactName, overdue.Status));
        Assert.Equal((new DateOnly(2026, 10, 8), new DateOnly(2026, 10, 20)), (overdue.DocumentDate, overdue.DueDate!.Value));
        Assert.Equal("V/9703", overdue.VendorBillNo);
    }

    [Fact]
    public void A_bill_shows_when_it_was_posted_and_when_it_was_last_paid_and_a_draft_shows_neither()
    {
        BillsTrackReportResultDto report = Run();

        BillsTrackRowDto paid = Row(report, 9701);
        Assert.NotNull(paid.PostedAt);
        Assert.Equal(new DateOnly(2026, 10, 12), paid.LastPaymentDate);
        BillsTrackRowDto draft = Row(report, 9708);
        Assert.Equal((null, null, null), (draft.PostedAt, draft.LastPaymentDate, draft.DueDate));
        // Posted and never paid: a posted-on date and no last payment.
        BillsTrackRowDto waiting = Row(report, 9704);
        Assert.NotNull(waiting.PostedAt);
        Assert.Null(waiting.LastPaymentDate);
    }

    [Fact]
    public void Days_overdue_are_given_only_to_an_overdue_bill_counted_to_the_to_date()
    {
        BillsTrackReportResultDto report = Run();

        // The 20th to the 31st.
        Assert.Equal(11, Row(report, 9703).DaysOverdue);
        Assert.All(report.Bills.Where(b => b.Status != BillTrackStatus.Overdue), b => Assert.Null(b.DaysOverdue));
    }

    [Fact]
    public void A_bill_that_went_through_approval_says_where_it_stands_and_one_that_did_not_says_nothing()
    {
        BillsTrackReportResultDto report = Run();

        Assert.Equal(ApprovalStatus.Approved, Row(report, 9701).ApprovalStatus);
        Assert.Equal(ApprovalStatus.Approved, Row(report, 9709).ApprovalStatus);
        Assert.Null(Row(report, 9702).ApprovalStatus);
        Assert.All(report.Bills.Where(b => b.DocumentId is not (9701 or 9708 or 9709)), b => Assert.Null(b.ApprovalStatus));
    }

    [Fact]
    public void The_step_a_bill_waits_on_is_given_only_while_it_is_in_approval()
    {
        BillsTrackReportResultDto report = Run();

        BillsTrackRowDto waiting = Row(report, 9708);
        Assert.Equal((ApprovalStatus.InApproval, "Level 2", BillTrackStatus.Draft), (waiting.ApprovalStatus, waiting.ApprovalStep, waiting.Status));
        Assert.Null(Row(report, 9701).ApprovalStep);
        Assert.Null(Row(report, 9709).ApprovalStep);
    }

    [Fact]
    public void A_foreign_bill_row_carries_its_currency_both_totals_and_what_is_owed()
    {
        BillsTrackRowDto usd = Row(Run(), 9706);

        Assert.Equal(("USD", 100m, 8_000m, 8_000m, 3_200m, 4_800m), (usd.CurrencyCode, usd.TotalInCurrency, usd.Total, usd.Payable, usd.Paid, usd.Due));
    }

    [Fact]
    public void A_reverse_charge_bill_row_says_so_and_owes_less_than_its_total()
    {
        BillsTrackRowDto reverse = Row(Run(), 9705);

        Assert.True(reverse.ReverseCharge);
        Assert.Equal((9_440m, 8_000m, 5_000m), (reverse.Total, reverse.Payable, reverse.Due));
    }

    [Fact]
    public void A_vendor_the_names_do_not_know_is_still_listed_by_its_number()
    {
        BillTrackEntry[] page = [new() { TypeCode = "BIL", DocumentId = 1, DocumentNo = "BIL-1", DocumentDate = October1, ContactId = 77, CurrencyCode = "INR", Status = BillTrackStatus.Paid }];

        BillsTrackReportResultDto report = BillsTrackReportRepository.Compose(Request(), page, 1, [], [], [], [], new(), "INR");

        Assert.Equal("Vendor 77", Assert.Single(report.Bills).ContactName);
    }

    [Fact]
    public void Every_status_has_a_count_a_nought_included()
    {
        BillsTrackReportResultDto report = Run(counts: [new() { Status = BillTrackStatus.Overdue, Count = 4 }]);

        Assert.Equal(Enum.GetValues<BillTrackStatus>().Length, report.StatusCounts.Count);
        Assert.Equal(4, report.StatusCounts[BillTrackStatus.Overdue]);
        Assert.Equal(0, report.StatusCounts[BillTrackStatus.Draft]);
    }

    [Fact]
    public void The_period_the_page_and_the_totals_of_every_match_are_passed_on()
    {
        var totals = new BillsTrackTotalsDto { Total = 1m, Paid = 2m, Due = 3m };
        var request = Request();
        request.PageNumber = 3;
        request.PageSize = 10;

        BillsTrackReportResultDto report = Run(request, totals: totals);

        Assert.Equal((October1, October31, "INR"), (report.FromDate, report.ToDate, report.BaseCurrencyCode));
        Assert.Equal((3, 10, 8), (report.PageNumber, report.PageSize, report.TotalBills));
        Assert.Same(totals, report.Totals);
    }

    // ---- The request and the guards ---------------------------------------------------

    [Fact]
    public void The_two_dates_are_required_and_nothing_else_is()
    {
        Assert.Equal(
            [nameof(BillsTrackReportRequest.FromDate), nameof(BillsTrackReportRequest.ToDate)],
            typeof(BillsTrackReportRequest).GetProperties()
                .Where(p => p.GetCustomAttribute<RequiredAttribute>() is not null)
                .Select(p => p.Name));
    }

    [Fact]
    public void A_missing_date_is_refused_at_its_field_by_the_request_itself()
    {
        var request = new BillsTrackReportRequest();
        var results = new List<ValidationResult>();

        Assert.False(Validator.TryValidateObject(request, new ValidationContext(request), results, validateAllProperties: true));
        Assert.Equal(
            ["From date is required.", "To date is required."],
            results.Select(r => r.ErrorMessage).OrderBy(m => m, StringComparer.Ordinal));
    }

    [Fact]
    public void A_page_size_above_a_hundred_is_refused_by_the_request_itself()
    {
        var request = new BillsTrackReportRequest { FromDate = October1, ToDate = October31, PageSize = 101 };
        var results = new List<ValidationResult>();

        Assert.False(Validator.TryValidateObject(request, new ValidationContext(request), results, validateAllProperties: true));
        Assert.Equal(["Page size must be between 1 and 100."], results.Select(r => r.ErrorMessage));
    }

    [Fact]
    public void A_period_that_ends_before_it_begins_is_refused_at_the_to_date_and_an_unknown_status_at_its_field()
    {
        Assert.Equal(
            [(nameof(BillsTrackReportRequest.ToDate), "To date cannot be before the from date.")],
            BillsTrackReportRepository.Refusals(new BillsTrackReportRequest { FromDate = October31, ToDate = October1 }));

        Assert.Equal(
            [(nameof(BillsTrackReportRequest.Status), "Status must be one of the statuses offered.")],
            BillsTrackReportRepository.Refusals(Request(status: (BillTrackStatus)9)));

        Assert.Empty(BillsTrackReportRepository.Refusals(Request()));
    }

    [Fact]
    public async Task Another_branch_in_the_request_is_forbidden_not_hidden()
    {
        var controller = new BillsTrackReportController(null!)
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
            new BillsTrackReportRequest { FromDate = October1, ToDate = October31, OrgId = Guid.NewGuid() }, default);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public void The_endpoint_needs_the_reports_module_purchase_view_and_a_trade_on_each_action()
    {
        Type controller = typeof(BillsTrackReportController);

        Assert.Equal("api/statements/bills-track-report", controller.GetCustomAttribute<RouteAttribute>()!.Template);
        Assert.Equal("reports", controller.GetCustomAttribute<RequireModulePermissionAttribute>()!.Module);
        Assert.NotNull(controller.GetCustomAttribute<RequireBusinessTypeAttribute>());

        foreach (string action in new[] { nameof(BillsTrackReportController.Run), nameof(BillsTrackReportController.Vendors) })
        {
            Assert.Contains(
                "purchase.view",
                controller.GetMethod(action)!.GetCustomAttributes<RequirePermissionAttribute>().Select(a => a.Permission));
        }

        Assert.Equal("vendors", controller.GetMethod(nameof(BillsTrackReportController.Vendors))!.GetCustomAttribute<HttpGetAttribute>()!.Template);
    }

    // ---- The catalog source, held to the same period ----------------------------------

    [Fact]
    public void The_catalog_source_declares_both_dates_required_and_keeps_its_columns()
    {
        var source = new BillsTrackSourceRepository();

        Assert.Equal(["from", "to"], source.Parameters.Select(p => p.Name));
        Assert.All(source.Parameters, p => Assert.True(p.IsRequired));
        Assert.Equal(
            ["documentNo", "documentDate", "contactName", "status", "totalAmount", "amountDue", "billId"],
            source.Columns.Select(c => c.Key));
    }
}
