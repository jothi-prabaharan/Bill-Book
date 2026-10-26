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
/// The Invoice Track Report (TK-226), on the Receivable Invoice Summary's books worked by hand,
/// the branch's base currency INR and the period October 2026, so what is received and due is as at
/// 31 October:
/// <list type="bullet">
/// <item><b>INV-9401</b> (11,800): all received on 10 October. Paid, last paid 10 October.</item>
/// <item><b>INV-9402</b> (23,600, due 15 October): 10,000 received on 12 October and a credit note of
/// 1,180 applied on 26 October, so 11,180 received and 12,420 due, 16 days late. Overdue, last paid 26 October.</item>
/// <item><b>INV-9403</b> (5,900): 2,000 received on 20 October, 3,900 due. Partly paid.</item>
/// <item><b>INV-9404</b> (9,440): a settlement voided and one in November do not count. Awaiting payment, never paid.</item>
/// <item><b>INV-9405</b> in US dollars at 80 (USD 590, 47,200): a receipt of USD 100 is 8,000 received on 22 October,
/// 39,200 due. Partly paid.</item>
/// <item><b>INV-9406</b> voided; <b>INV-9407</b> a draft (3,540) and <b>INV-9409</b> ready to post (1,180, Acme),
/// which owe nothing; and an invoice dated in September.</item>
/// </list>
/// The posted ones the default list holds come to 97,940 with 32,980 received and 64,960 due, the
/// summary's figures; the drafts are left out of the totals. The builders run on lists through the same
/// expressions the repository runs against the read models; the composition is pure. Nothing here needs PostgreSQL.
/// </summary>
public sealed class InvoiceTrackReportTests
{
    private const long Acme = 9101, Zenith = 9103;

    private static readonly DateOnly October1 = new(2026, 10, 1);
    private static readonly DateOnly October31 = new(2026, 10, 31);

    private static InvoiceRead Invoice(
        long id, DateOnly date, DateOnly? due, long contact, string currency, decimal rate, decimal taxable,
        decimal cgst, decimal sgst, decimal igst, DocumentStatus status = DocumentStatus.Posted, DateTimeOffset? postedAt = null)
    {
        decimal total = taxable + cgst + sgst + igst;
        return new()
        {
            InvoiceId = id, TransactionTypeCode = "INV", DocumentNo = $"INV-{id}", DocumentDate = date, DueDate = due, ContactId = contact,
            CurrencyCode = currency, ExchangeRate = rate, TaxableAmount = taxable, CgstAmount = cgst, SgstAmount = sgst, IgstAmount = igst,
            TotalAmount = total, TotalAmountBase = Math.Round(total * rate, 2), Status = status,
            PostedAt = postedAt ?? (status is DocumentStatus.Posted or DocumentStatus.Void ? new DateTimeOffset(date.ToDateTime(new TimeOnly(9, 0)), TimeSpan.Zero) : null),
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
        Invoice(9407, new(2026, 10, 22), null, Zenith, "INR", 1m, 3_000m, 270m, 270m, 0m, DocumentStatus.Draft),
        Invoice(9408, new(2026, 9, 20), new(2026, 10, 20), Zenith, "INR", 1m, 4_000m, 360m, 360m, 0m),
        Invoice(9409, new(2026, 10, 28), null, Acme, "INR", 1m, 1_000m, 90m, 90m, 0m, DocumentStatus.ReadyToPost),
    ];

    private static readonly List<CreditNoteRead> CreditNotes = [];

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

    private static InvoiceTrackReportRequest Request(InvoiceTrackStatus? status = null, long? contact = null, DateOnly? from = null, DateOnly? to = null) =>
        new() { FromDate = from ?? October1, ToDate = to ?? October31, Status = status, ContactId = contact };

    /// <summary>Every entry for a request, through the same builders the repository uses, before any status filter.</summary>
    private static List<InvoiceTrackEntry> EntriesFor(InvoiceTrackReportRequest request) =>
    [
        .. InvoiceTrackReportRepository.Entries(
            Invoices.AsQueryable(), CreditNotes.AsQueryable(), Ratios.AsQueryable(), request, request.FromDate!.Value, request.ToDate!.Value),
    ];

    private static InvoiceTrackEntry Entry(IEnumerable<InvoiceTrackEntry> entries, long id) => entries.Single(e => e.DocumentId == id);

    private static readonly List<CustomerRef> Customers =
    [
        new() { ContactId = Zenith, Code = "ZENITH", Name = "Zenith Traders" },
        new() { ContactId = Acme, Code = "ACME", Name = "Acme Exports" },
    ];

    // ---- Which invoices and what status ------------------------------------------------

    [Fact]
    public void Every_invoice_in_the_period_is_an_entry_the_drafts_and_the_voided_one_included_and_other_months_are_not()
    {
        List<InvoiceTrackEntry> entries = EntriesFor(Request());

        Assert.Equal([9401L, 9402, 9403, 9404, 9405, 9406, 9407, 9409], entries.Select(e => e.DocumentId).Order());
    }

    [Fact]
    public void A_posted_invoices_status_is_worked_out_as_the_summary_does()
    {
        List<InvoiceTrackEntry> entries = EntriesFor(Request());

        Assert.Equal(InvoiceTrackStatus.Paid, Entry(entries, 9401).Status);
        // Overdue wins over partly paid: part of it was received.
        Assert.Equal(InvoiceTrackStatus.Overdue, Entry(entries, 9402).Status);
        Assert.Equal(InvoiceTrackStatus.PartlyPaid, Entry(entries, 9403).Status);
        Assert.Equal(InvoiceTrackStatus.AwaitingPayment, Entry(entries, 9404).Status);
        Assert.Equal(InvoiceTrackStatus.PartlyPaid, Entry(entries, 9405).Status);
        Assert.Equal(InvoiceTrackStatus.Void, Entry(entries, 9406).Status);
    }

    [Fact]
    public void A_draft_and_a_ready_to_post_invoice_have_their_own_status_and_owe_and_have_received_nothing()
    {
        List<InvoiceTrackEntry> entries = EntriesFor(Request());

        InvoiceTrackEntry draft = Entry(entries, 9407);
        Assert.Equal((InvoiceTrackStatus.Draft, 3_540m, 0m, 0m), (draft.Status, draft.Total, draft.Received, draft.Due));
        InvoiceTrackEntry ready = Entry(entries, 9409);
        Assert.Equal((InvoiceTrackStatus.ReadyToPost, 1_180m, 0m, 0m), (ready.Status, ready.Total, ready.Received, ready.Due));
    }

    [Fact]
    public void What_was_received_and_is_due_is_as_at_the_to_date_and_agrees_with_the_summary()
    {
        List<InvoiceTrackEntry> entries = EntriesFor(Request());

        Assert.Equal((11_800m, 0m), (Entry(entries, 9401).Received, Entry(entries, 9401).Due));
        Assert.Equal((11_180m, 12_420m), (Entry(entries, 9402).Received, Entry(entries, 9402).Due));
        Assert.Equal((2_000m, 3_900m), (Entry(entries, 9403).Received, Entry(entries, 9403).Due));
        // A settlement that was voided, and one effective after the date, are not counted.
        Assert.Equal((0m, 9_440m), (Entry(entries, 9404).Received, Entry(entries, 9404).Due));
        Assert.Equal((8_000m, 39_200m), (Entry(entries, 9405).Received, Entry(entries, 9405).Due));
    }

    [Fact]
    public void An_earlier_to_date_counts_only_what_was_settled_by_then_and_changes_the_status()
    {
        // On 15 October INV-9402 was not yet past its due date (the 15th), and only 10,000 had been received.
        List<InvoiceTrackEntry> entries = EntriesFor(Request(to: new DateOnly(2026, 10, 15)));

        InvoiceTrackEntry invoice = Entry(entries, 9402);
        Assert.Equal((10_000m, 13_600m, InvoiceTrackStatus.PartlyPaid), (invoice.Received, invoice.Due, invoice.Status));
        Assert.DoesNotContain(entries, e => e.DocumentId == 9405);
    }

    [Fact]
    public void A_foreign_invoice_carries_its_own_total_and_its_total_in_base()
    {
        InvoiceTrackEntry usd = Entry(EntriesFor(Request()), 9405);

        Assert.Equal(("USD", 590m, 47_200m), (usd.CurrencyCode, usd.TotalSource, usd.Total));
    }

    [Fact]
    public void The_customer_filter_narrows_the_posted_and_the_draft_entries_alike()
    {
        List<InvoiceTrackEntry> entries = EntriesFor(Request(contact: Acme));

        Assert.Equal([9405L, 9409], entries.Select(e => e.DocumentId).Order());
    }

    [Fact]
    public void Drafts_and_ready_to_post_invoices_are_listed_beside_the_posted_ones()
    {
        // A draft and a ready one are listed beside the posted.
        Assert.Equal(
            [InvoiceTrackStatus.Draft, InvoiceTrackStatus.ReadyToPost],
            EntriesFor(Request()).Select(e => e.Status).Where(s => s is InvoiceTrackStatus.Draft or InvoiceTrackStatus.ReadyToPost).Order());
    }

    // ---- Status filter and totals -------------------------------------------------------

    [Fact]
    public void With_no_status_chosen_every_entry_but_the_void_one_is_listed()
    {
        List<InvoiceTrackEntry> listed = [.. InvoiceTrackReportRepository.WithStatus(EntriesFor(Request()).AsQueryable(), null)];

        Assert.Equal([9401L, 9402, 9403, 9404, 9405, 9407, 9409], listed.Select(e => e.DocumentId).Order());
    }

    [Theory]
    [InlineData(InvoiceTrackStatus.Draft, new long[] { 9407 })]
    [InlineData(InvoiceTrackStatus.ReadyToPost, new long[] { 9409 })]
    [InlineData(InvoiceTrackStatus.AwaitingPayment, new long[] { 9404 })]
    [InlineData(InvoiceTrackStatus.PartlyPaid, new long[] { 9403, 9405 })]
    [InlineData(InvoiceTrackStatus.Overdue, new long[] { 9402 })]
    [InlineData(InvoiceTrackStatus.Paid, new long[] { 9401 })]
    [InlineData(InvoiceTrackStatus.Void, new long[] { 9406 })]
    public void A_status_chosen_lists_only_the_invoices_with_it(InvoiceTrackStatus status, long[] expected)
    {
        List<InvoiceTrackEntry> listed = [.. InvoiceTrackReportRepository.WithStatus(EntriesFor(Request()).AsQueryable(), status)];

        Assert.Equal(expected, listed.Select(e => e.DocumentId).Order());
    }

    [Fact]
    public void The_totals_add_up_the_posted_invoices_only_and_equal_the_summarys()
    {
        List<InvoiceTrackEntry> posted = [.. InvoiceTrackReportRepository.Posted(
            InvoiceTrackReportRepository.WithStatus(EntriesFor(Request()).AsQueryable(), null))];

        Assert.Equal([9401L, 9402, 9403, 9404, 9405], posted.Select(e => e.DocumentId).Order());
        Assert.Equal((97_940m, 32_980m, 64_960m), (posted.Sum(e => e.Total), posted.Sum(e => e.Received), posted.Sum(e => e.Due)));
        Assert.All(posted, e => Assert.Equal(e.Total - e.Received, e.Due));
    }

    [Fact]
    public void A_voided_invoice_is_not_a_posted_one_for_the_totals_and_a_draft_is_not_either()
    {
        List<InvoiceTrackEntry> posted = [.. InvoiceTrackReportRepository.Posted(EntriesFor(Request()).AsQueryable())];

        Assert.DoesNotContain(posted, e => e.DocumentId is 9406 or 9407 or 9409);
        Assert.Equal(5, posted.Count);
    }

    // ---- The dates of its life ----------------------------------------------------------

    [Fact]
    public void The_posted_on_moment_is_read_for_the_invoices_asked_for_and_is_empty_on_a_draft()
    {
        List<PostedStamp> stamps = [.. InvoiceTrackReportRepository.PostedStamps(Invoices.AsQueryable(), [9401L, 9407])];

        Assert.Equal([9401L, 9407], stamps.Select(s => s.DocumentId).Order());
        Assert.NotNull(stamps.Single(s => s.DocumentId == 9401).PostedAt);
        Assert.Null(stamps.Single(s => s.DocumentId == 9407).PostedAt);
    }

    [Fact]
    public void The_last_payment_is_the_latest_settlement_on_or_before_the_date_and_a_credit_note_applied_counts()
    {
        List<LastPayment> last = [.. InvoiceTrackReportRepository.LastPayments(Ratios.AsQueryable(), [9401L, 9402, 9403, 9404, 9405], October31)];

        Assert.Equal(new DateOnly(2026, 10, 10), last.Single(p => p.DocumentId == 9401).Date);
        // 12 October by receipt, 26 October by credit note: the later one.
        Assert.Equal(new DateOnly(2026, 10, 26), last.Single(p => p.DocumentId == 9402).Date);
        Assert.Equal(new DateOnly(2026, 10, 20), last.Single(p => p.DocumentId == 9403).Date);
        Assert.Equal(new DateOnly(2026, 10, 22), last.Single(p => p.DocumentId == 9405).Date);
    }

    [Fact]
    public void A_voided_settlement_and_one_after_the_date_are_not_a_last_payment()
    {
        List<LastPayment> last = [.. InvoiceTrackReportRepository.LastPayments(Ratios.AsQueryable(), [9404L], October31)];

        Assert.Empty(last);
    }

    [Fact]
    public void An_earlier_date_leaves_out_a_later_settlement()
    {
        List<LastPayment> last = [.. InvoiceTrackReportRepository.LastPayments(Ratios.AsQueryable(), [9402L], new DateOnly(2026, 10, 20))];

        Assert.Equal(new DateOnly(2026, 10, 12), Assert.Single(last).Date);
    }

    // ---- Compose -------------------------------------------------------------------------

    private static InvoiceTrackReportResultDto Run(
        InvoiceTrackReportRequest? request = null, IReadOnlyList<TrackStatusCount>? counts = null, InvoiceTrackTotalsDto? totals = null)
    {
        request ??= Request();
        List<InvoiceTrackEntry> page = [.. InvoiceTrackReportRepository.WithStatus(EntriesFor(request).AsQueryable(), request.Status)
            .OrderByDescending(e => e.DocumentDate).ThenByDescending(e => e.DocumentId)];
        long[] ids = [.. page.Select(e => e.DocumentId)];
        List<PostedStamp> stamps = [.. InvoiceTrackReportRepository.PostedStamps(Invoices.AsQueryable(), ids)];
        List<LastPayment> payments = [.. InvoiceTrackReportRepository.LastPayments(Ratios.AsQueryable(), ids, request.ToDate!.Value)];

        return InvoiceTrackReportRepository.Compose(request, page, page.Count, Customers, stamps, payments, counts ?? [], totals ?? new(), "INR");
    }

    private static InvoiceTrackRowDto Row(InvoiceTrackReportResultDto report, long id) => report.Invoices.Single(i => i.DocumentId == id);

    [Fact]
    public void The_invoices_are_newest_first_with_their_customers_and_dates()
    {
        InvoiceTrackReportResultDto report = Run();

        Assert.Equal([9409L, 9407, 9405, 9404, 9403, 9402, 9401], report.Invoices.Select(i => i.DocumentId));
        InvoiceTrackRowDto overdue = Row(report, 9402);
        Assert.Equal(("ZENITH", "Zenith Traders", InvoiceTrackStatus.Overdue), (overdue.ContactCode, overdue.ContactName, overdue.Status));
        Assert.Equal((new DateOnly(2026, 10, 5), new DateOnly(2026, 10, 15)), (overdue.DocumentDate, overdue.DueDate!.Value));
    }

    [Fact]
    public void An_invoice_shows_when_it_was_posted_and_when_it_was_last_paid_and_a_draft_shows_neither()
    {
        InvoiceTrackReportResultDto report = Run();

        InvoiceTrackRowDto paid = Row(report, 9401);
        Assert.NotNull(paid.PostedAt);
        Assert.Equal(new DateOnly(2026, 10, 10), paid.LastPaymentDate);
        InvoiceTrackRowDto draft = Row(report, 9407);
        Assert.Equal((null, null), (draft.PostedAt, draft.LastPaymentDate));
        // Posted and never paid: a posted-on date and no last payment.
        InvoiceTrackRowDto waiting = Row(report, 9404);
        Assert.NotNull(waiting.PostedAt);
        Assert.Null(waiting.LastPaymentDate);
    }

    [Fact]
    public void Days_overdue_are_given_only_to_an_overdue_invoice_counted_to_the_to_date()
    {
        InvoiceTrackReportResultDto report = Run();

        // The 15th to the 31st.
        Assert.Equal(16, Row(report, 9402).DaysOverdue);
        Assert.All(report.Invoices.Where(i => i.Status != InvoiceTrackStatus.Overdue), i => Assert.Null(i.DaysOverdue));
    }

    [Fact]
    public void A_foreign_invoice_row_carries_its_currency_and_both_totals()
    {
        InvoiceTrackRowDto usd = Row(Run(), 9405);

        Assert.Equal(("USD", 590m, 47_200m, 8_000m, 39_200m), (usd.CurrencyCode, usd.TotalInCurrency, usd.Total, usd.Received, usd.Due));
    }

    [Fact]
    public void A_customer_the_names_do_not_know_is_still_listed_by_its_number()
    {
        InvoiceTrackEntry[] page = [new() { TypeCode = "INV", DocumentId = 1, DocumentNo = "INV-1", DocumentDate = October1, ContactId = 77, CurrencyCode = "INR", Status = InvoiceTrackStatus.Paid }];

        InvoiceTrackReportResultDto report = InvoiceTrackReportRepository.Compose(Request(), page, 1, [], [], [], [], new(), "INR");

        Assert.Equal("Customer 77", Assert.Single(report.Invoices).ContactName);
    }

    [Fact]
    public void Every_status_has_a_count_a_nought_included()
    {
        InvoiceTrackReportResultDto report = Run(counts: [new() { Status = InvoiceTrackStatus.Overdue, Count = 4 }]);

        Assert.Equal(Enum.GetValues<InvoiceTrackStatus>().Length, report.StatusCounts.Count);
        Assert.Equal(4, report.StatusCounts[InvoiceTrackStatus.Overdue]);
        Assert.Equal(0, report.StatusCounts[InvoiceTrackStatus.Draft]);
    }

    [Fact]
    public void The_period_the_page_and_the_totals_of_every_match_are_passed_on()
    {
        var totals = new InvoiceTrackTotalsDto { Total = 1m, Received = 2m, Due = 3m };
        var request = Request();
        request.PageNumber = 3;
        request.PageSize = 10;

        InvoiceTrackReportResultDto report = Run(request, totals: totals);

        Assert.Equal((October1, October31, "INR"), (report.FromDate, report.ToDate, report.BaseCurrencyCode));
        Assert.Equal((3, 10, 7), (report.PageNumber, report.PageSize, report.TotalInvoices));
        Assert.Same(totals, report.Totals);
    }

    // ---- The request and the guards ---------------------------------------------------

    [Fact]
    public void The_two_dates_are_required_and_nothing_else_is()
    {
        Assert.Equal(
            [nameof(InvoiceTrackReportRequest.FromDate), nameof(InvoiceTrackReportRequest.ToDate)],
            typeof(InvoiceTrackReportRequest).GetProperties()
                .Where(p => p.GetCustomAttribute<RequiredAttribute>() is not null)
                .Select(p => p.Name));
    }

    [Fact]
    public void A_missing_date_is_refused_at_its_field_by_the_request_itself()
    {
        var request = new InvoiceTrackReportRequest();
        var results = new List<ValidationResult>();

        Assert.False(Validator.TryValidateObject(request, new ValidationContext(request), results, validateAllProperties: true));
        Assert.Equal(
            ["From date is required.", "To date is required."],
            results.Select(r => r.ErrorMessage).OrderBy(m => m, StringComparer.Ordinal));
    }

    [Fact]
    public void A_page_size_above_a_hundred_is_refused_by_the_request_itself()
    {
        var request = new InvoiceTrackReportRequest { FromDate = October1, ToDate = October31, PageSize = 101 };
        var results = new List<ValidationResult>();

        Assert.False(Validator.TryValidateObject(request, new ValidationContext(request), results, validateAllProperties: true));
        Assert.Equal(["Page size must be between 1 and 100."], results.Select(r => r.ErrorMessage));
    }

    [Fact]
    public void A_period_that_ends_before_it_begins_is_refused_at_the_to_date_and_an_unknown_status_at_its_field()
    {
        Assert.Equal(
            [(nameof(InvoiceTrackReportRequest.ToDate), "To date cannot be before the from date.")],
            InvoiceTrackReportRepository.Refusals(new InvoiceTrackReportRequest { FromDate = October31, ToDate = October1 }));

        Assert.Equal(
            [(nameof(InvoiceTrackReportRequest.Status), "Status must be one of the statuses offered.")],
            InvoiceTrackReportRepository.Refusals(Request(status: (InvoiceTrackStatus)9)));

        Assert.Empty(InvoiceTrackReportRepository.Refusals(Request()));
    }

    [Fact]
    public async Task Another_branch_in_the_request_is_forbidden_not_hidden()
    {
        var controller = new InvoiceTrackReportController(null!)
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
            new InvoiceTrackReportRequest { FromDate = October1, ToDate = October31, OrgId = Guid.NewGuid() }, default);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public void The_endpoint_needs_the_reports_module_sales_view_and_a_trade_on_each_action()
    {
        Type controller = typeof(InvoiceTrackReportController);

        Assert.Equal("api/statements/invoice-track-report", controller.GetCustomAttribute<RouteAttribute>()!.Template);
        Assert.Equal("reports", controller.GetCustomAttribute<RequireModulePermissionAttribute>()!.Module);
        Assert.NotNull(controller.GetCustomAttribute<RequireBusinessTypeAttribute>());

        foreach (string action in new[] { nameof(InvoiceTrackReportController.Run), nameof(InvoiceTrackReportController.Customers) })
        {
            Assert.Contains(
                "sales.view",
                controller.GetMethod(action)!.GetCustomAttributes<RequirePermissionAttribute>().Select(a => a.Permission));
        }

        Assert.Equal("customers", controller.GetMethod(nameof(InvoiceTrackReportController.Customers))!.GetCustomAttribute<HttpGetAttribute>()!.Template);
    }

    // ---- The catalog source, held to the same period ----------------------------------

    [Fact]
    public void The_catalog_source_declares_both_dates_required_and_keeps_its_columns()
    {
        var source = new InvoiceTrackSourceRepository();

        Assert.Equal(["from", "to"], source.Parameters.Select(p => p.Name));
        Assert.All(source.Parameters, p => Assert.True(p.IsRequired));
        Assert.Equal(
            ["documentNo", "documentDate", "contactName", "status", "totalAmount", "amountDue"],
            source.Columns.Select(c => c.Key));
    }
}
