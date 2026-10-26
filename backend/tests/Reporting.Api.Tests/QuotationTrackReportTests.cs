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
/// The Quotation Track Report (TK-231), on quotes worked by hand. The branch's base currency is INR, the period is
/// October 2026 and today is 26 October 2026:
/// <list type="bullet">
/// <item><b>QTE-9801</b> (Zenith), 10,000, valid until 15 October, no answer: <b>expired</b>.</item>
/// <item><b>QTE-9802</b> (Zenith), 5,000, valid until 30 November: <b>open</b>.</item>
/// <item><b>QTE-9803</b> (Acme), USD 250 at 80 = 20,000, accepted on the portal: <b>accepted</b>.</item>
/// <item><b>QTE-9804</b> (Zenith), 8,000, rejected on the portal: <b>declined</b>.</item>
/// <item><b>QTE-9805</b> (Zenith), 12,000: sales order SOR-9502 raised on 8 October, two days on: <b>converted to an order</b>.</item>
/// <item><b>QTE-9806</b> (Zenith), 20,000, accepted and then invoiced as INV-9404 on 15 October, eight days on:
/// <b>converted to an invoice</b>, whatever it was answered.</item>
/// <item><b>QTE-9807</b> (Acme), USD 300 at 80 = 24,000: both the order SOR-9503 (12 October) and the invoice
/// INV-9405 (18 October) came from it, and the invoice is the one named, nine days on.</item>
/// <item><b>QTE-9811</b> (Zenith), 4,000, valid until 25 October: <b>expired</b> the day after. <b>QTE-9812</b>
/// (Zenith), 6,000, valid until 26 October: still <b>open</b> today. <b>QTE-9813</b> (Zenith), 7,000, whose only
/// order is voided: <b>open</b>. <b>QTE-9814</b> (Zenith), 9,000, valid until 20 October, whose only order is a
/// draft: <b>expired</b>.</item>
/// <item>A voided quote (<b>void</b>, 3,000), a draft, and a quote dated in September, which are not in the default list.</item>
/// </list>
/// Raised is 11 quotes at 125,000 (3 open at 18,000, 3 expired at 23,000, 1 accepted at 20,000, 1 declined at 8,000, 1
/// converted to an order at 12,000 and 2 to an invoice at 44,000); 3 are converted at 56,000, a conversion rate of
/// 27.3% by count and 44.8% by value. The builders run on lists through the same expressions the repository runs
/// against the read models; the composition is pure. Nothing here needs PostgreSQL.
/// </summary>
public sealed class QuotationTrackReportTests
{
    private const long Acme = 9101, Zenith = 9103;

    private static readonly DateOnly October1 = new(2026, 10, 1);
    private static readonly DateOnly October31 = new(2026, 10, 31);
    private static readonly DateOnly Today = new(2026, 10, 26);

    private static QuoteRead Quote(
        long id, DateOnly date, DateOnly validUntil, long contact, string currency, decimal rate, decimal total,
        DocumentStatus status = DocumentStatus.Posted, QuoteResponse response = QuoteResponse.None) =>
        new()
        {
            QuoteId = id, TransactionTypeCode = "QTE", DocumentNo = $"QTE-{id}", DocumentDate = date, ValidUntil = validUntil, ContactId = contact,
            CurrencyCode = currency, ExchangeRate = rate, TotalAmount = total, Status = status, CustomerResponse = response,
        };

    private static readonly List<QuoteRead> Quotes =
    [
        Quote(9801, new(2026, 10, 1), new(2026, 10, 15), Zenith, "INR", 1m, 10_000m),
        Quote(9802, new(2026, 10, 2), new(2026, 11, 30), Zenith, "INR", 1m, 5_000m),
        Quote(9803, new(2026, 10, 3), new(2026, 11, 15), Acme, "USD", 80m, 250m, response: QuoteResponse.Accepted),
        Quote(9804, new(2026, 10, 5), new(2026, 10, 31), Zenith, "INR", 1m, 8_000m, response: QuoteResponse.Rejected),
        Quote(9805, new(2026, 10, 6), new(2026, 10, 20), Zenith, "INR", 1m, 12_000m),
        Quote(9806, new(2026, 10, 7), new(2026, 11, 7), Zenith, "INR", 1m, 20_000m, response: QuoteResponse.Accepted),
        Quote(9807, new(2026, 10, 9), new(2026, 11, 9), Acme, "USD", 80m, 300m),
        Quote(9808, new(2026, 10, 10), new(2026, 11, 10), Zenith, "INR", 1m, 3_000m, DocumentStatus.Void),
        Quote(9809, new(2026, 10, 11), new(2026, 11, 11), Zenith, "INR", 1m, 2_000m, DocumentStatus.Draft),
        Quote(9810, new(2026, 9, 25), new(2026, 10, 25), Zenith, "INR", 1m, 1_500m),
        Quote(9811, new(2026, 10, 14), new(2026, 10, 25), Zenith, "INR", 1m, 4_000m),
        Quote(9812, new(2026, 10, 16), new(2026, 10, 26), Zenith, "INR", 1m, 6_000m),
        Quote(9813, new(2026, 10, 17), new(2026, 11, 20), Zenith, "INR", 1m, 7_000m),
        Quote(9814, new(2026, 10, 12), new(2026, 10, 20), Zenith, "INR", 1m, 9_000m),
    ];

    private static InvoiceRead Invoice(long id, DateOnly date, long? quote, DocumentStatus status = DocumentStatus.Posted) =>
        new() { InvoiceId = id, TransactionTypeCode = "INV", DocumentNo = $"INV-{id}", DocumentDate = date, QuoteId = quote, Status = status };

    private static SalesOrderRead Order(long id, DateOnly date, long? quote, DocumentStatus status = DocumentStatus.Posted) =>
        new() { SalesOrderId = id, TransactionTypeCode = "SOR", DocumentNo = $"SOR-{id}", DocumentDate = date, QuoteId = quote, Status = status };

    private static readonly List<InvoiceRead> Invoices =
    [
        Invoice(9404, new(2026, 10, 15), 9806),
        Invoice(9405, new(2026, 10, 18), 9807),
        Invoice(9406, new(2026, 10, 20), 9813, DocumentStatus.Void),
        Invoice(9401, new(2026, 10, 2), null),
    ];

    private static readonly List<SalesOrderRead> Orders =
    [
        Order(9502, new(2026, 10, 8), 9805),
        Order(9503, new(2026, 10, 12), 9807),
        Order(9506, new(2026, 10, 20), 9813, DocumentStatus.Void),
        Order(9507, new(2026, 10, 22), 9814, DocumentStatus.Draft),
        Order(9501, new(2026, 10, 3), null),
    ];

    private static readonly List<CustomerRef> Customers =
    [
        new() { ContactId = Zenith, Code = "ZENITH", Name = "Zenith Traders" },
        new() { ContactId = Acme, Code = "ACME", Name = "Acme Exports" },
    ];

    private static QuotationTrackReportRequest Request(
        long? contact = null, QuoteOutcome? outcome = null, DateOnly? from = null, DateOnly? to = null) =>
        new() { FromDate = from ?? October1, ToDate = to ?? October31, ContactId = contact, Outcome = outcome };

    private static List<QuoteTrackRow> RowsFor(QuotationTrackReportRequest request, DateOnly? today = null)
    {
        IQueryable<QuoteRead> quotes = QuotationTrackReportRepository.Quotes(Quotes.AsQueryable(), request, request.FromDate!.Value, request.ToDate!.Value);
        return [.. QuotationTrackReportRepository.Rows(quotes, Invoices.AsQueryable(), Orders.AsQueryable(), today ?? Today)];
    }

    private static QuoteOutcome OutcomeOf(long id) => RowsFor(Request()).Single(r => r.QuoteId == id).Outcome;

    private static List<OutcomeFigure> FiguresFor(QuotationTrackReportRequest request) =>
        [.. QuotationTrackReportRepository.Figures(RowsFor(request).AsQueryable())];

    private static QuotationTrackReportResultDto Run(QuotationTrackReportRequest? request = null)
    {
        request ??= Request();
        List<QuoteTrackRow> rows = [.. QuotationTrackReportRepository.WithOutcome(RowsFor(Request(request.ContactId)).AsQueryable(), request.Outcome)
            .OrderByDescending(r => r.DocumentDate).ThenByDescending(r => r.QuoteId)];
        long[] ids = [.. rows.Select(r => r.QuoteId)];

        return QuotationTrackReportRepository.Compose(
            request, rows, [.. QuotationTrackReportRepository.Conversions(Invoices.AsQueryable(), Orders.AsQueryable(), ids)], Customers,
            FiguresFor(Request(request.ContactId)), "INR");
    }

    // ---- Which quotes -----------------------------------------------------------------------

    [Fact]
    public void Posted_and_voided_quotes_in_the_period_are_listed_and_a_draft_and_another_month_are_not()
    {
        Assert.Equal([9801L, 9802, 9803, 9804, 9805, 9806, 9807, 9808, 9811, 9812, 9813, 9814], RowsFor(Request()).Select(r => r.QuoteId).Order());
    }

    [Fact]
    public void The_customer_narrows_the_quotes()
    {
        Assert.Equal([9803L, 9807], RowsFor(Request(contact: Acme)).Select(r => r.QuoteId).Order());
    }

    [Fact]
    public void A_quotes_value_is_its_total_in_base_currency()
    {
        List<QuoteTrackRow> rows = RowsFor(Request());

        Assert.Equal((250m, 20_000m, "USD"), (rows.Single(r => r.QuoteId == 9803).TotalInCurrency, rows.Single(r => r.QuoteId == 9803).Total, rows.Single(r => r.QuoteId == 9803).CurrencyCode));
        Assert.Equal(24_000m, rows.Single(r => r.QuoteId == 9807).Total);
    }

    // ---- Outcomes ---------------------------------------------------------------------------

    [Theory]
    [InlineData(9801L, QuoteOutcome.Expired)]
    [InlineData(9802L, QuoteOutcome.Open)]
    [InlineData(9803L, QuoteOutcome.Accepted)]
    [InlineData(9804L, QuoteOutcome.Declined)]
    [InlineData(9805L, QuoteOutcome.ConvertedToOrder)]
    [InlineData(9806L, QuoteOutcome.ConvertedToInvoice)]
    [InlineData(9807L, QuoteOutcome.ConvertedToInvoice)]
    [InlineData(9808L, QuoteOutcome.Void)]
    [InlineData(9811L, QuoteOutcome.Expired)]
    [InlineData(9812L, QuoteOutcome.Open)]
    [InlineData(9813L, QuoteOutcome.Open)]
    [InlineData(9814L, QuoteOutcome.Expired)]
    public void Each_quote_has_the_outcome_worked_out_by_hand(long id, QuoteOutcome expected)
    {
        Assert.Equal(expected, OutcomeOf(id));
    }

    [Fact]
    public void A_quote_lapses_the_day_after_its_valid_until_date_and_not_on_it()
    {
        Assert.Equal(QuoteOutcome.Open, RowsFor(Request(), new DateOnly(2026, 10, 25)).Single(r => r.QuoteId == 9811).Outcome);
        Assert.Equal(QuoteOutcome.Expired, RowsFor(Request(), new DateOnly(2026, 10, 26)).Single(r => r.QuoteId == 9811).Outcome);
        Assert.Equal(QuoteOutcome.Open, RowsFor(Request(), new DateOnly(2026, 10, 26)).Single(r => r.QuoteId == 9812).Outcome);
        Assert.Equal(QuoteOutcome.Expired, RowsFor(Request(), new DateOnly(2026, 10, 27)).Single(r => r.QuoteId == 9812).Outcome);
    }

    [Fact]
    public void A_conversion_wins_over_what_the_customer_answered_and_over_the_quote_lapsing()
    {
        Assert.Equal(QuoteOutcome.ConvertedToInvoice, OutcomeOf(9806));
        Assert.Equal(QuoteOutcome.ConvertedToOrder, OutcomeOf(9805));
    }

    [Fact]
    public void A_voided_or_draft_document_raised_from_a_quote_does_not_convert_it()
    {
        Assert.Equal(QuoteOutcome.Open, OutcomeOf(9813));
        Assert.Equal(QuoteOutcome.Expired, OutcomeOf(9814));
    }

    [Fact]
    public void A_voided_quote_is_void_even_when_a_document_was_raised_from_it()
    {
        Invoices.Add(Invoice(9499, new(2026, 10, 12), 9808));
        try
        {
            Assert.Equal(QuoteOutcome.Void, OutcomeOf(9808));
        }
        finally
        {
            Invoices.RemoveAll(i => i.InvoiceId == 9499);
        }
    }

    // ---- Tabs, the summary and the list -----------------------------------------------------

    [Fact]
    public void The_count_and_value_of_each_outcome_are_the_hand_worked_figures()
    {
        Dictionary<QuoteOutcome, (int Count, decimal Value)> figures = FiguresFor(Request()).ToDictionary(f => f.Outcome, f => (f.Count, f.Value));

        Assert.Equal((3, 18_000m), figures[QuoteOutcome.Open]);
        Assert.Equal((3, 23_000m), figures[QuoteOutcome.Expired]);
        Assert.Equal((1, 20_000m), figures[QuoteOutcome.Accepted]);
        Assert.Equal((1, 8_000m), figures[QuoteOutcome.Declined]);
        Assert.Equal((1, 12_000m), figures[QuoteOutcome.ConvertedToOrder]);
        Assert.Equal((2, 44_000m), figures[QuoteOutcome.ConvertedToInvoice]);
        Assert.Equal((1, 3_000m), figures[QuoteOutcome.Void]);
    }

    [Fact]
    public void With_no_outcome_asked_for_every_quote_but_the_void_ones_is_listed_and_the_void_ones_only_when_asked()
    {
        IQueryable<QuoteTrackRow> rows = RowsFor(Request()).AsQueryable();

        Assert.DoesNotContain(QuotationTrackReportRepository.WithOutcome(rows, null), r => r.Outcome == QuoteOutcome.Void);
        Assert.Equal(11, QuotationTrackReportRepository.WithOutcome(rows, null).Count());
        Assert.Equal([9808L], QuotationTrackReportRepository.WithOutcome(rows, QuoteOutcome.Void).Select(r => r.QuoteId));
        Assert.Equal([9806L, 9807], QuotationTrackReportRepository.WithOutcome(rows, QuoteOutcome.ConvertedToInvoice).Select(r => r.QuoteId).Order());
    }

    [Fact]
    public void The_summary_is_what_was_raised_what_was_converted_and_the_rate_by_count_and_by_value()
    {
        QuotationTrackSummaryDto summary = Run().Summary;

        Assert.Equal((11, 125_000m, 3, 56_000m), (summary.Raised, summary.RaisedValue, summary.Converted, summary.ConvertedValue));
        Assert.Equal((27.3m, 44.8m), (summary.ConversionRateByCount, summary.ConversionRateByValue));
    }

    [Fact]
    public void Raised_is_open_expired_accepted_converted_and_declined_added_up_and_void_is_in_none_of_them()
    {
        QuotationTrackReportResultDto report = Run();
        Dictionary<QuoteOutcome, int> counts = report.OutcomeCounts;

        Assert.Equal(
            counts[QuoteOutcome.Open] + counts[QuoteOutcome.Expired] + counts[QuoteOutcome.Accepted] + counts[QuoteOutcome.ConvertedToOrder]
                + counts[QuoteOutcome.ConvertedToInvoice] + counts[QuoteOutcome.Declined],
            report.Summary.Raised);
        Assert.Equal(report.TotalQuotes, report.Summary.Raised);
        Assert.Equal(report.TotalValue, report.Summary.RaisedValue);
        Assert.Equal(1, counts[QuoteOutcome.Void]);
    }

    [Fact]
    public void The_summary_follows_the_customer_but_not_the_outcome_chosen()
    {
        QuotationTrackReportResultDto acme = Run(Request(contact: Acme));
        Assert.Equal((2, 44_000m, 1, 24_000m, 50m, 54.5m), (
            acme.Summary.Raised, acme.Summary.RaisedValue, acme.Summary.Converted, acme.Summary.ConvertedValue,
            acme.Summary.ConversionRateByCount, acme.Summary.ConversionRateByValue));

        QuotationTrackReportResultDto converted = Run(Request(outcome: QuoteOutcome.ConvertedToInvoice));
        Assert.Equal(11, converted.Summary.Raised);
        Assert.Equal((2, 44_000m), (converted.TotalQuotes, converted.TotalValue));
    }

    [Fact]
    public void Nothing_raised_leaves_the_rates_empty_rather_than_nought()
    {
        QuotationTrackReportResultDto report = QuotationTrackReportRepository.Compose(Request(), [], [], [], [], "INR");

        Assert.Equal((0, 0m, 0, 0m), (report.Summary.Raised, report.Summary.RaisedValue, report.Summary.Converted, report.Summary.ConvertedValue));
        Assert.Null(report.Summary.ConversionRateByCount);
        Assert.Null(report.Summary.ConversionRateByValue);
        Assert.Equal(0, report.TotalQuotes);
    }

    [Fact]
    public void Every_outcome_has_a_count_a_nought_included()
    {
        QuotationTrackReportResultDto report = QuotationTrackReportRepository.Compose(
            Request(), [], [], [], [new() { Outcome = QuoteOutcome.Accepted, Count = 4, Value = 400m }], "INR");

        Assert.Equal(Enum.GetValues<QuoteOutcome>().Length, report.OutcomeCounts.Count);
        Assert.Equal(4, report.OutcomeCounts[QuoteOutcome.Accepted]);
        Assert.Equal(0, report.OutcomeCounts[QuoteOutcome.Open]);
    }

    // ---- What a quote became ----------------------------------------------------------------

    [Fact]
    public void Every_converted_quote_names_a_document_that_exists_and_no_other_quote_does()
    {
        QuotationTrackReportResultDto report = Run();

        foreach (QuotationTrackRowDto quote in report.Quotes)
        {
            bool converted = quote.Outcome is QuoteOutcome.ConvertedToOrder or QuoteOutcome.ConvertedToInvoice;
            Assert.Equal(converted, quote.ConvertedTo is not null);
            Assert.Equal(converted, quote.DaysToConvert is not null);

            if (quote.ConvertedTo is { } document)
            {
                Assert.True(document.TypeCode == "INV"
                    ? Invoices.Any(i => i.InvoiceId == document.DocumentId && i.DocumentNo == document.DocumentNo && i.QuoteId == quote.QuoteId && i.Status == DocumentStatus.Posted)
                    : Orders.Any(o => o.SalesOrderId == document.DocumentId && o.DocumentNo == document.DocumentNo && o.QuoteId == quote.QuoteId && o.Status == DocumentStatus.Posted));
            }
        }
    }

    [Fact]
    public void A_quote_names_the_invoice_raised_from_it_before_the_order_and_counts_the_days_to_that()
    {
        QuotationTrackReportResultDto report = Run();
        QuotationTrackRowDto both = report.Quotes.Single(q => q.QuoteId == 9807);
        QuotationTrackRowDto invoiced = report.Quotes.Single(q => q.QuoteId == 9806);
        QuotationTrackRowDto ordered = report.Quotes.Single(q => q.QuoteId == 9805);

        Assert.Equal(("INV", "INV-9405", 9), (both.ConvertedTo!.TypeCode, both.ConvertedTo.DocumentNo, both.DaysToConvert));
        Assert.Equal(("INV", "INV-9404", 8), (invoiced.ConvertedTo!.TypeCode, invoiced.ConvertedTo.DocumentNo, invoiced.DaysToConvert));
        Assert.Equal(("SOR", "SOR-9502", 2), (ordered.ConvertedTo!.TypeCode, ordered.ConvertedTo.DocumentNo, ordered.DaysToConvert));
    }

    [Fact]
    public void With_two_documents_of_a_kind_the_earliest_is_named()
    {
        List<QuoteConversionRow> conversions =
        [
            new() { QuoteId = 1, TypeCode = "INV", DocumentId = 20, DocumentNo = "INV-20", DocumentDate = new(2026, 10, 20) },
            new() { QuoteId = 1, TypeCode = "INV", DocumentId = 10, DocumentNo = "INV-10", DocumentDate = new(2026, 10, 12) },
            new() { QuoteId = 1, TypeCode = "SOR", DocumentId = 5, DocumentNo = "SOR-5", DocumentDate = new(2026, 10, 2) },
        ];

        QuotationTrackReportResultDto report = QuotationTrackReportRepository.Compose(
            Request(),
            [new() { QuoteId = 1, DocumentNo = "QTE-1", DocumentDate = new(2026, 10, 1), ContactId = Zenith, Outcome = QuoteOutcome.ConvertedToInvoice }],
            conversions, Customers, [], "INR");

        Assert.Equal(("INV-10", 11), (report.Quotes[0].ConvertedTo!.DocumentNo, report.Quotes[0].DaysToConvert));
    }

    [Fact]
    public void The_conversions_of_a_page_are_the_posted_invoices_and_orders_raised_from_its_quotes_only()
    {
        List<QuoteConversionRow> conversions = [.. QuotationTrackReportRepository.Conversions(Invoices.AsQueryable(), Orders.AsQueryable(), [9805L, 9807, 9813])];

        Assert.Equal(["INV-9405", "SOR-9502", "SOR-9503"], conversions.Select(c => c.DocumentNo).Order());
    }

    [Fact]
    public void Quotes_are_listed_newest_first_with_their_customers()
    {
        QuotationTrackReportResultDto report = Run();

        Assert.Equal(
            [9813L, 9812, 9811, 9814, 9807, 9806, 9805, 9804, 9803, 9802, 9801],
            report.Quotes.Select(q => q.QuoteId));
        Assert.Equal(("ZENITH", "Zenith Traders"), (report.Quotes[0].ContactCode, report.Quotes[0].ContactName));
        Assert.Equal("Acme Exports", report.Quotes.Single(q => q.QuoteId == 9803).ContactName);
        Assert.Equal(new DateOnly(2026, 10, 15), report.Quotes.Single(q => q.QuoteId == 9801).ValidUntil);
    }

    [Fact]
    public void A_customer_the_names_do_not_know_is_still_listed_by_its_number()
    {
        QuotationTrackReportResultDto report = QuotationTrackReportRepository.Compose(
            Request(), [new() { QuoteId = 1, DocumentNo = "QTE-1", DocumentDate = October1, ContactId = 77, Outcome = QuoteOutcome.Open }], [], [], [], "INR");

        Assert.Equal("Customer 77", report.Quotes[0].ContactName);
    }

    [Fact]
    public void The_period_and_page_are_passed_on()
    {
        var request = Request();
        request.PageNumber = 3;
        request.PageSize = 10;

        QuotationTrackReportResultDto report = Run(request);

        Assert.Equal((October1, October31, "INR"), (report.FromDate, report.ToDate, report.BaseCurrencyCode));
        Assert.Equal((3, 10, 11), (report.PageNumber, report.PageSize, report.TotalQuotes));
    }

    // ---- The request and the guards ---------------------------------------------------

    [Fact]
    public void The_two_dates_are_required_and_nothing_else_is()
    {
        Assert.Equal(
            [nameof(QuotationTrackReportRequest.FromDate), nameof(QuotationTrackReportRequest.ToDate)],
            typeof(QuotationTrackReportRequest).GetProperties()
                .Where(p => p.GetCustomAttribute<RequiredAttribute>() is not null)
                .Select(p => p.Name));
    }

    [Fact]
    public void A_missing_date_is_refused_at_its_field_by_the_request_itself()
    {
        var request = new QuotationTrackReportRequest();
        var results = new List<ValidationResult>();

        Assert.False(Validator.TryValidateObject(request, new ValidationContext(request), results, validateAllProperties: true));
        Assert.Equal(
            ["From date is required.", "To date is required."],
            results.Select(r => r.ErrorMessage).OrderBy(m => m, StringComparer.Ordinal));
    }

    [Fact]
    public void A_page_size_above_a_hundred_is_refused_by_the_request_itself()
    {
        var request = new QuotationTrackReportRequest { FromDate = October1, ToDate = October31, PageSize = 101 };
        var results = new List<ValidationResult>();

        Assert.False(Validator.TryValidateObject(request, new ValidationContext(request), results, validateAllProperties: true));
        Assert.Equal(["Page size must be between 1 and 100."], results.Select(r => r.ErrorMessage));
    }

    [Fact]
    public void A_period_that_ends_before_it_begins_and_an_unknown_outcome_are_refused_at_their_fields()
    {
        Assert.Equal(
            [(nameof(QuotationTrackReportRequest.ToDate), "To date cannot be before the from date.")],
            QuotationTrackReportRepository.Refusals(new QuotationTrackReportRequest { FromDate = October31, ToDate = October1 }));

        Assert.Equal(
            [(nameof(QuotationTrackReportRequest.Outcome), "Status must be one of the options offered.")],
            QuotationTrackReportRepository.Refusals(Request(outcome: (QuoteOutcome)9)));

        Assert.Empty(QuotationTrackReportRepository.Refusals(Request()));
    }

    [Fact]
    public async Task Another_branch_in_the_request_is_forbidden_not_hidden()
    {
        var controller = new QuotationTrackReportController(null!)
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
            new QuotationTrackReportRequest { FromDate = October1, ToDate = October31, OrgId = Guid.NewGuid() }, default);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public void The_endpoint_needs_the_reports_module_sales_view_and_a_trade_on_each_action()
    {
        Type controller = typeof(QuotationTrackReportController);

        Assert.Equal("api/statements/quotation-track-report", controller.GetCustomAttribute<RouteAttribute>()!.Template);
        Assert.Equal("reports", controller.GetCustomAttribute<RequireModulePermissionAttribute>()!.Module);
        Assert.NotNull(controller.GetCustomAttribute<RequireBusinessTypeAttribute>());

        foreach (string action in new[] { nameof(QuotationTrackReportController.Run), nameof(QuotationTrackReportController.Customers) })
        {
            Assert.Contains(
                "sales.view",
                controller.GetMethod(action)!.GetCustomAttributes<RequirePermissionAttribute>().Select(a => a.Permission));
        }

        Assert.Equal("customers", controller.GetMethod(nameof(QuotationTrackReportController.Customers))!.GetCustomAttribute<HttpGetAttribute>()!.Template);
    }

    // ---- The catalog source, held to the same period ----------------------------------

    [Fact]
    public void The_catalog_source_declares_both_dates_required_and_keeps_its_columns()
    {
        var source = new QuotationTrackSourceRepository();

        Assert.Equal(["from", "to"], source.Parameters.Select(p => p.Name));
        Assert.All(source.Parameters, p => Assert.True(p.IsRequired));
        Assert.Equal(["documentNo", "documentDate", "validUntil", "contactName", "status", "outcome", "totalAmount"], source.Columns.Select(c => c.Key));
    }
}
