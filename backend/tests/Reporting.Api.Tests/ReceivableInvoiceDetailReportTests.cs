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
/// The Receivable Invoice Detail (TK-225), on books worked by hand. The branch's base currency is INR
/// and the period is October 2026. Two items, <b>Widget A</b> in Hardware and <b>Gadget B</b> in
/// Electronics, and these invoices, which are the Receivable Invoice Summary's:
/// <list type="bullet">
/// <item><b>INV-9401</b>: Widget A, 10 at 600 (taxable 6,000, tax 1,080 at 9% and 9%), and Gadget B, 4 at
/// 1,000 (4,000, 720). 10,000 and 1,800: 11,800.</item>
/// <item><b>INV-9402</b> (IGST): Gadget B, 10 at 1,500 less a discount of 1,000 (14,000, tax 2,520 at
/// 18%), and an <b>Installation</b> line with no item, 1 at 6,000 (6,000, 1,080). 20,000 and 3,600:
/// 23,600.</item>
/// <item><b>INV-9403</b>: Widget A, 5 at 1,000: 5,000, 900, 5,900.</item>
/// <item><b>INV-9405</b> in US dollars at 80: Widget A, 5 at USD 100: taxable USD 500 and IGST USD 90,
/// USD 590, which is 47,200.</item>
/// </list>
/// For the lines of Widget A alone, 51,000 is taxable and 9,180 tax, 60,180 in all; for Electronics,
/// 26,000, 4,680 and 30,680. The builders run on lists through the same expressions the repository runs
/// against the read models; the composition is pure. Nothing here needs PostgreSQL.
/// </summary>
public sealed class ReceivableInvoiceDetailReportTests
{
    private const long Zenith = 9103, Acme = 9101;
    private const long WidgetA = 9601, GadgetB = 9602, Hardware = 9701, Electronics = 9702;
    private const long Pcs = 40;

    private static readonly DateOnly October1 = new(2026, 10, 1);
    private static readonly DateOnly October31 = new(2026, 10, 31);

    private static ItemRead Item(long id, string code, string name, long category, bool active = true) =>
        new() { ItemId = id, ItemCode = code, ItemName = name, ItemCategoryId = category, IsActive = active };

    private static readonly List<ItemRead> Items =
    [
        Item(WidgetA, "WIDGET-A", "Widget A", Hardware),
        Item(GadgetB, "GADGET-B", "Gadget B", Electronics),
    ];

    private static long _id;

    private static InvoiceDetailRead Line(
        long invoice, int number, long? item, string? description, decimal quantity, decimal price, decimal discount,
        decimal taxable, decimal tax, long? unit = Pcs) =>
        new()
        {
            InvoiceDetailId = ++_id, InvoiceId = invoice, LineNumber = number, ItemId = item, Description = description, UomId = unit,
            Quantity = quantity, UnitPrice = price, DiscountAmount = discount, GrossAmount = quantity * price, TaxableAmount = taxable,
            TaxAmount = tax, LineTotal = taxable + tax,
        };

    private static readonly List<InvoiceDetailRead> Details =
    [
        Line(9401, 1, WidgetA, null, 10m, 600m, 0m, 6_000m, 1_080m),
        Line(9401, 2, GadgetB, null, 4m, 1_000m, 0m, 4_000m, 720m),
        Line(9402, 1, GadgetB, null, 10m, 1_500m, 1_000m, 14_000m, 2_520m),
        Line(9402, 2, null, "Installation", 1m, 6_000m, 0m, 6_000m, 1_080m, unit: null),
        Line(9403, 1, WidgetA, null, 5m, 1_000m, 0m, 5_000m, 900m),
        Line(9405, 1, WidgetA, null, 5m, 100m, 0m, 500m, 90m),
    ];

    private static ReceivableInvoiceRow InvoiceRow(
        long id, DateOnly date, long contact, string currency, decimal rate, decimal totalInCurrency,
        ReceivableInvoiceStatus status = ReceivableInvoiceStatus.Paid) =>
        new()
        {
            TypeCode = "INV", DocumentId = id, DocumentNo = $"INV-{id}", DocumentDate = date, DueDate = date.AddDays(14), ContactId = contact,
            CurrencyCode = currency, ExchangeRate = rate, TotalSource = totalInCurrency, Total = Math.Round(totalInCurrency * rate, 2), Status = status,
        };

    private static readonly List<ReceivableInvoiceRow> Page =
    [
        InvoiceRow(9405, new(2026, 10, 18), Acme, "USD", 80m, 590m, ReceivableInvoiceStatus.PartlyPaid),
        InvoiceRow(9403, new(2026, 10, 10), Zenith, "INR", 1m, 5_900m, ReceivableInvoiceStatus.PartlyPaid),
        InvoiceRow(9402, new(2026, 10, 5), Zenith, "INR", 1m, 23_600m, ReceivableInvoiceStatus.Overdue),
        InvoiceRow(9401, new(2026, 10, 2), Zenith, "INR", 1m, 11_800m),
    ];

    private static readonly List<LineTaxRate> TaxRates =
    [
        .. Details.Where(d => d.InvoiceId is 9401 or 9403).SelectMany(d => new LineTaxRate[]
        {
            new() { InvoiceDetailId = d.InvoiceDetailId, Rate = 9m },
            new() { InvoiceDetailId = d.InvoiceDetailId, Rate = 9m },
        }),
        .. Details.Where(d => d.InvoiceId is 9402 or 9405).Select(d => new LineTaxRate { InvoiceDetailId = d.InvoiceDetailId, Rate = 18m }),
    ];

    private static readonly List<UnitRef> Units = [new() { UomId = Pcs, Code = "PCS" }];

    private static readonly List<CustomerRef> Customers =
    [
        new() { ContactId = Zenith, Code = "ZENITH", Name = "Zenith Traders" },
        new() { ContactId = Acme, Code = "ACME", Name = "Acme Exports" },
    ];

    private static ReceivableInvoiceDetailReportRequest Request(
        long? item = null, long? category = null, ReceivableInvoiceStatus? status = null, long? contact = null) =>
        new() { FromDate = October1, ToDate = October31, ItemId = item, ItemCategoryId = category, Status = status, ContactId = contact };

    private static ReceivableInvoiceDetailReportResultDto Run(
        ReceivableInvoiceDetailReportRequest? request = null, IReadOnlyList<ReceivableInvoiceRow>? page = null,
        IReadOnlyList<InvoiceDetailRead>? lines = null, IReadOnlyList<ItemRef>? items = null,
        IReadOnlyList<StatusCount>? counts = null, ReceivableInvoiceDetailTotalsDto? totals = null)
    {
        request ??= Request();
        IReadOnlyList<ReceivableInvoiceRow> rows = page ?? Page;
        IReadOnlyList<InvoiceDetailRead> shown = lines ?? [.. ReceivableInvoiceDetailReportRepository.Lines(Details.AsQueryable(), Items.AsQueryable(), request)];
        IReadOnlyList<ItemRef> names = items ?? [.. Items.Select(i => new ItemRef { ItemId = i.ItemId, Code = i.ItemCode, Name = i.ItemName })];

        return ReceivableInvoiceDetailReportRepository.Compose(
            request, rows, rows.Count, shown, TaxRates, names, Units, Customers, counts ?? [], totals ?? new(), "INR");
    }

    private static ReceivableInvoiceDetailInvoiceDto Invoice(ReceivableInvoiceDetailReportResultDto report, long id) =>
        report.Invoices.Single(i => i.DocumentId == id);

    // ---- Which lines -------------------------------------------------------------------

    [Fact]
    public void With_no_item_or_category_every_line_is_a_line_of_the_report()
    {
        List<InvoiceDetailRead> lines = [.. ReceivableInvoiceDetailReportRepository.Lines(Details.AsQueryable(), Items.AsQueryable(), Request())];

        Assert.Equal(6, lines.Count);
        Assert.False(ReceivableInvoiceDetailReportRepository.LinesAreFiltered(Request()));
    }

    [Fact]
    public void An_item_narrows_the_lines_to_those_for_it()
    {
        List<InvoiceDetailRead> lines = [.. ReceivableInvoiceDetailReportRepository.Lines(Details.AsQueryable(), Items.AsQueryable(), Request(item: WidgetA))];

        Assert.Equal([9401L, 9403L, 9405L], lines.Select(d => d.InvoiceId).Order());
        Assert.All(lines, d => Assert.Equal(WidgetA, d.ItemId));
        Assert.True(ReceivableInvoiceDetailReportRepository.LinesAreFiltered(Request(item: WidgetA)));
    }

    [Fact]
    public void A_category_narrows_the_lines_to_those_for_an_item_in_it_and_a_line_with_no_item_is_not_in_one()
    {
        List<InvoiceDetailRead> lines = [.. ReceivableInvoiceDetailReportRepository.Lines(Details.AsQueryable(), Items.AsQueryable(), Request(category: Electronics))];

        Assert.Equal(3, lines.Count);
        Assert.All(lines, d => Assert.Equal(GadgetB, d.ItemId));
        Assert.DoesNotContain(lines, d => d.ItemId == null);
    }

    [Fact]
    public void An_item_and_a_category_together_keep_only_the_lines_that_are_both()
    {
        Assert.Equal(3, ReceivableInvoiceDetailReportRepository.Lines(Details.AsQueryable(), Items.AsQueryable(), Request(item: GadgetB, category: Electronics)).Count());
        Assert.Empty(ReceivableInvoiceDetailReportRepository.Lines(Details.AsQueryable(), Items.AsQueryable(), Request(item: GadgetB, category: Hardware)));
    }

    [Fact]
    public void The_request_the_summarys_builders_take_is_the_same_period_customer_and_status_with_no_credit_notes()
    {
        var request = Request(status: ReceivableInvoiceStatus.Overdue, contact: Zenith);

        ReceivableInvoiceSummaryReportRequest summary = ReceivableInvoiceDetailReportRepository.SummaryRequestOf(request);

        Assert.Equal((October1, October31, Zenith, ReceivableInvoiceStatus.Overdue, false), (summary.FromDate, summary.ToDate, summary.ContactId, summary.Status, summary.IncludeCreditNotes));
    }

    // ---- The totals --------------------------------------------------------------------

    [Fact]
    public void A_line_is_taken_to_base_at_its_invoices_rate()
    {
        List<LineFigure> figures = [.. ReceivableInvoiceDetailReportRepository.LineFigures(Details.AsQueryable(), Page.AsQueryable())];

        LineFigure usd = figures.Single(f => f.InvoiceId == 9405);

        // USD 500, USD 90 and USD 590 at 80.
        Assert.Equal((40_000m, 7_200m, 47_200m), (usd.Taxable, usd.Tax, usd.Total));
    }

    [Fact]
    public void A_line_in_base_currency_is_as_it_is_and_a_rate_with_places_is_rounded_to_two()
    {
        ReceivableInvoiceRow[] rows = [InvoiceRow(9402, October1, Zenith, "INR", 1m, 23_600m), InvoiceRow(9405, October1, Acme, "USD", 83.4567m, 590m)];

        List<LineFigure> figures = [.. ReceivableInvoiceDetailReportRepository.LineFigures(Details.AsQueryable(), rows.AsQueryable())];

        Assert.Equal([14_000m, 6_000m], figures.Where(f => f.InvoiceId == 9402).Select(f => f.Taxable));
        Assert.Equal(41_728.35m, figures.Single(f => f.InvoiceId == 9405).Taxable);
    }

    [Fact]
    public void The_lines_of_the_matching_invoices_add_up_to_the_summarys_taxable_value_tax_and_total()
    {
        List<LineFigure> figures = [.. ReceivableInvoiceDetailReportRepository.LineFigures(Details.AsQueryable(), Page.AsQueryable())];

        // 10,000 + 20,000 + 5,000 + 40,000, and 1,800 + 3,600 + 900 + 7,200, and the four invoices' totals.
        Assert.Equal(75_000m, figures.Sum(f => f.Taxable));
        Assert.Equal(13_500m, figures.Sum(f => f.Tax));
        Assert.Equal(88_500m, figures.Sum(f => f.Total));
        Assert.Equal(Page.Sum(r => r.Total), figures.Sum(f => f.Total));
    }

    [Fact]
    public void The_lines_for_one_item_or_one_category_add_up_as_worked_by_hand()
    {
        var widget = Request(item: WidgetA);
        List<LineFigure> forWidget = [.. ReceivableInvoiceDetailReportRepository.LineFigures(
            ReceivableInvoiceDetailReportRepository.Lines(Details.AsQueryable(), Items.AsQueryable(), widget), Page.AsQueryable())];
        Assert.Equal((51_000m, 9_180m, 60_180m), (forWidget.Sum(f => f.Taxable), forWidget.Sum(f => f.Tax), forWidget.Sum(f => f.Total)));

        var electronics = Request(category: Electronics);
        List<LineFigure> forCategory = [.. ReceivableInvoiceDetailReportRepository.LineFigures(
            ReceivableInvoiceDetailReportRepository.Lines(Details.AsQueryable(), Items.AsQueryable(), electronics), Page.AsQueryable())];
        Assert.Equal((26_000m, 4_680m, 30_680m), (forCategory.Sum(f => f.Taxable), forCategory.Sum(f => f.Tax), forCategory.Sum(f => f.Total)));
    }

    [Fact]
    public void A_line_of_an_invoice_that_is_not_in_the_matching_set_is_not_counted()
    {
        ReceivableInvoiceRow[] only = [Page.Single(r => r.DocumentId == 9403)];

        Assert.Equal([9403L], ReceivableInvoiceDetailReportRepository.LineFigures(Details.AsQueryable(), only.AsQueryable()).Select(f => f.InvoiceId));
    }

    // ---- An invoice and its lines --------------------------------------------------------

    [Fact]
    public void An_invoice_lists_its_lines_in_line_order_with_the_items_code_name_and_unit()
    {
        ReceivableInvoiceDetailInvoiceDto invoice = Invoice(Run(), 9401);

        Assert.Equal([1, 2], invoice.Lines.Select(l => l.LineNumber));
        ReceivableInvoiceDetailLineDto first = invoice.Lines[0];
        Assert.Equal(("WIDGET-A", "Widget A", "PCS"), (first.ItemCode, first.ItemName, first.UnitCode));
        Assert.Equal((10m, 600m, 6_000m, 1_080m, 7_080m), (first.Quantity, first.UnitPrice, first.Taxable, first.TaxAmount, first.LineTotal));
    }

    [Fact]
    public void A_line_with_no_item_reads_as_its_own_description_and_has_no_code_or_unit()
    {
        ReceivableInvoiceDetailLineDto service = Invoice(Run(), 9402).Lines.Single(l => l.LineNumber == 2);

        Assert.Equal((null, "Installation", null, null), (service.ItemCode, service.ItemName, service.UnitCode, service.Description));
        Assert.Null(service.ItemId);
    }

    [Fact]
    public void A_description_is_kept_only_when_it_says_more_than_the_items_name()
    {
        InvoiceDetailRead[] lines =
        [
            Line(9401, 1, WidgetA, "Widget A", 1m, 100m, 0m, 100m, 18m),
            Line(9401, 2, GadgetB, "Gadget B, blue", 1m, 100m, 0m, 100m, 18m),
        ];

        ReceivableInvoiceDetailInvoiceDto invoice = Invoice(Run(page: [Page[3]], lines: lines), 9401);

        Assert.Null(invoice.Lines[0].Description);
        Assert.Equal("Gadget B, blue", invoice.Lines[1].Description);
    }

    [Fact]
    public void A_line_for_an_item_the_names_do_not_know_falls_back_to_its_description()
    {
        InvoiceDetailRead[] lines = [Line(9401, 1, 777, "Old item", 1m, 100m, 0m, 100m, 18m)];

        ReceivableInvoiceDetailLineDto line = Invoice(Run(page: [Page[3]], lines: lines, items: []), 9401).Lines.Single();

        Assert.Equal((null, "Old item"), (line.ItemCode, line.ItemName));
    }

    [Fact]
    public void A_lines_tax_rate_is_the_rates_of_its_tax_rows_added_up()
    {
        ReceivableInvoiceDetailReportResultDto report = Run();

        // 9% and 9%, and a single 18% of IGST.
        Assert.Equal(18m, Invoice(report, 9401).Lines[0].TaxRate);
        Assert.Equal(18m, Invoice(report, 9402).Lines[0].TaxRate);
    }

    [Fact]
    public void A_line_with_no_tax_rows_has_a_tax_rate_of_nothing()
    {
        InvoiceDetailRead[] lines = [Line(9401, 1, WidgetA, null, 1m, 100m, 0m, 100m, 0m)];

        Assert.Equal(0m, Invoice(Run(page: [Page[3]], lines: lines), 9401).Lines.Single().TaxRate);
    }

    [Fact]
    public void A_discount_is_shown_on_its_line()
    {
        ReceivableInvoiceDetailLineDto discounted = Invoice(Run(), 9402).Lines[0];

        Assert.Equal((1_000m, 14_000m), (discounted.DiscountAmount, discounted.Taxable));
    }

    [Fact]
    public void An_invoice_adds_up_its_lines_in_its_own_currency()
    {
        ReceivableInvoiceDetailInvoiceDto invoice = Invoice(Run(), 9402);

        Assert.Equal((20_000m, 3_600m, 23_600m), (invoice.LinesTaxable, invoice.LinesTax, invoice.LinesTotal));
    }

    [Fact]
    public void An_invoice_in_another_currency_carries_its_own_total_and_its_total_in_base()
    {
        ReceivableInvoiceDetailInvoiceDto usd = Invoice(Run(), 9405);

        Assert.Equal(("USD", 590m, 47_200m), (usd.CurrencyCode, usd.TotalInCurrency, usd.Total));
        Assert.Equal((500m, 90m, 590m), (usd.LinesTaxable, usd.LinesTax, usd.LinesTotal));
    }

    [Fact]
    public void With_every_line_shown_the_lines_and_the_round_off_make_the_total()
    {
        foreach (ReceivableInvoiceDetailInvoiceDto invoice in Run().Invoices)
        {
            Assert.NotNull(invoice.RoundOff);
            Assert.Equal(invoice.TotalInCurrency, invoice.LinesTotal + invoice.RoundOff!.Value);
        }
    }

    [Fact]
    public void A_round_off_is_what_the_total_has_over_the_lines()
    {
        // 23,600 on the invoice, 23,599.60 in its lines.
        InvoiceDetailRead[] lines = [Line(9402, 1, GadgetB, null, 1m, 20_000m, 0m, 20_000m, 3_599.60m)];

        Assert.Equal(0.40m, Invoice(Run(page: [Page[2]], lines: lines), 9402).RoundOff);
    }

    [Fact]
    public void When_an_item_or_category_narrows_the_lines_there_is_no_round_off_and_the_subtotals_are_of_the_lines_shown()
    {
        var request = Request(item: WidgetA);
        ReceivableInvoiceDetailReportResultDto report = Run(request, page: [Page[3]]);

        ReceivableInvoiceDetailInvoiceDto invoice = Invoice(report, 9401);
        Assert.True(report.LinesFiltered);
        Assert.Null(invoice.RoundOff);
        Assert.Equal([1], invoice.Lines.Select(l => l.LineNumber));
        Assert.Equal((6_000m, 1_080m, 7_080m), (invoice.LinesTaxable, invoice.LinesTax, invoice.LinesTotal));
        // The invoice's own total is still the whole invoice's.
        Assert.Equal(11_800m, invoice.TotalInCurrency);
    }

    [Fact]
    public void An_invoice_with_no_lines_is_still_listed_with_its_whole_total_as_round_off()
    {
        ReceivableInvoiceDetailInvoiceDto invoice = Invoice(Run(page: [Page[3]], lines: []), 9401);

        Assert.Empty(invoice.Lines);
        Assert.Equal((0m, 11_800m), (invoice.LinesTotal, invoice.RoundOff!.Value));
    }

    // ---- The page ----------------------------------------------------------------------

    [Fact]
    public void The_invoices_are_in_the_order_given_with_their_customers_status_and_dates()
    {
        ReceivableInvoiceDetailReportResultDto report = Run();

        Assert.Equal([9405L, 9403L, 9402L, 9401L], report.Invoices.Select(i => i.DocumentId));
        ReceivableInvoiceDetailInvoiceDto overdue = Invoice(report, 9402);
        Assert.Equal(("ZENITH", "Zenith Traders", ReceivableInvoiceStatus.Overdue), (overdue.ContactCode, overdue.ContactName, overdue.Status));
        Assert.Equal((new DateOnly(2026, 10, 5), new DateOnly(2026, 10, 19)), (overdue.DocumentDate, overdue.DueDate!.Value));
    }

    [Fact]
    public void A_customer_the_names_do_not_know_is_still_listed_by_its_number()
    {
        ReceivableInvoiceRow[] page = [InvoiceRow(9401, October1, 77, "INR", 1m, 10m)];

        ReceivableInvoiceDetailReportResultDto report = ReceivableInvoiceDetailReportRepository.Compose(Request(), page, 1, [], [], [], [], [], [], new(), "INR");

        Assert.Equal("Customer 77", Assert.Single(report.Invoices).ContactName);
    }

    [Fact]
    public void Every_status_has_a_count_a_nought_included()
    {
        ReceivableInvoiceDetailReportResultDto report = Run(counts: [new() { Status = ReceivableInvoiceStatus.Overdue, Count = 4 }]);

        Assert.Equal(Enum.GetValues<ReceivableInvoiceStatus>().Length, report.StatusCounts.Count);
        Assert.Equal(4, report.StatusCounts[ReceivableInvoiceStatus.Overdue]);
        Assert.Equal(0, report.StatusCounts[ReceivableInvoiceStatus.Paid]);
    }

    [Fact]
    public void The_period_the_page_and_the_totals_of_every_match_are_passed_on()
    {
        var totals = new ReceivableInvoiceDetailTotalsDto { Taxable = 1m, Tax = 2m, Total = 3m };
        var request = Request();
        request.PageNumber = 3;
        request.PageSize = 10;

        ReceivableInvoiceDetailReportResultDto report = Run(request, totals: totals);

        Assert.Equal((October1, October31, "INR"), (report.FromDate, report.ToDate, report.BaseCurrencyCode));
        Assert.Equal((3, 10, 4), (report.PageNumber, report.PageSize, report.TotalInvoices));
        Assert.Same(totals, report.Totals);
    }

    // ---- Lookups -----------------------------------------------------------------------

    [Fact]
    public void The_items_offered_are_the_active_ones_by_name()
    {
        ItemRead[] items = [Item(1, "Z", "Zebra", Hardware), Item(2, "A", "Apple", Hardware), Item(3, "G", "Gone", Hardware, active: false)];

        Assert.Equal(["Apple", "Zebra"], ReceivableInvoiceDetailReportRepository.ItemChoices(items.AsQueryable()).Select(i => i.ItemName));
    }

    [Fact]
    public void The_categories_offered_are_the_active_ones_in_their_own_order()
    {
        ItemCategoryRead[] categories =
        [
            new() { ItemCategoryId = 1, CategoryCode = "B", CategoryName = "Second", DisplayOrder = 2, IsActive = true },
            new() { ItemCategoryId = 2, CategoryCode = "A", CategoryName = "First", DisplayOrder = 1, IsActive = true },
            new() { ItemCategoryId = 3, CategoryCode = "C", CategoryName = "Off", DisplayOrder = 0, IsActive = false },
        ];

        Assert.Equal(["First", "Second"], ReceivableInvoiceDetailReportRepository.CategoryChoices(categories.AsQueryable()).Select(c => c.CategoryName));
    }

    // ---- The request and the guards ---------------------------------------------------

    [Fact]
    public void The_two_dates_are_required_and_nothing_else_is()
    {
        Assert.Equal(
            [nameof(ReceivableInvoiceDetailReportRequest.FromDate), nameof(ReceivableInvoiceDetailReportRequest.ToDate)],
            typeof(ReceivableInvoiceDetailReportRequest).GetProperties()
                .Where(p => p.GetCustomAttribute<RequiredAttribute>() is not null)
                .Select(p => p.Name));
    }

    [Fact]
    public void A_missing_date_is_refused_at_its_field_by_the_request_itself()
    {
        var request = new ReceivableInvoiceDetailReportRequest();
        var results = new List<ValidationResult>();

        Assert.False(Validator.TryValidateObject(request, new ValidationContext(request), results, validateAllProperties: true));
        Assert.Equal(
            ["From date is required.", "To date is required."],
            results.Select(r => r.ErrorMessage).OrderBy(m => m, StringComparer.Ordinal));
    }

    [Fact]
    public void A_page_size_above_a_hundred_is_refused_by_the_request_itself()
    {
        var request = new ReceivableInvoiceDetailReportRequest { FromDate = October1, ToDate = October31, PageSize = 101 };
        var results = new List<ValidationResult>();

        Assert.False(Validator.TryValidateObject(request, new ValidationContext(request), results, validateAllProperties: true));
        Assert.Equal(["Page size must be between 1 and 100."], results.Select(r => r.ErrorMessage));
    }

    [Fact]
    public void A_period_that_ends_before_it_begins_is_refused_at_the_to_date_and_an_unknown_status_at_its_field()
    {
        Assert.Equal(
            [(nameof(ReceivableInvoiceDetailReportRequest.ToDate), "To date cannot be before the from date.")],
            ReceivableInvoiceDetailReportRepository.Refusals(new ReceivableInvoiceDetailReportRequest { FromDate = October31, ToDate = October1 }));

        Assert.Equal(
            [(nameof(ReceivableInvoiceDetailReportRequest.Status), "Status must be one of the statuses offered.")],
            ReceivableInvoiceDetailReportRepository.Refusals(Request(status: (ReceivableInvoiceStatus)9)));

        Assert.Empty(ReceivableInvoiceDetailReportRepository.Refusals(Request()));
    }

    [Fact]
    public async Task Another_branch_in_the_request_is_forbidden_not_hidden()
    {
        var controller = new ReceivableInvoiceDetailReportController(null!)
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
            new ReceivableInvoiceDetailReportRequest { FromDate = October1, ToDate = October31, OrgId = Guid.NewGuid() }, default);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public void The_endpoint_needs_the_reports_module_sales_view_and_a_trade_on_each_action()
    {
        Type controller = typeof(ReceivableInvoiceDetailReportController);

        Assert.Equal("api/statements/receivable-invoice-detail", controller.GetCustomAttribute<RouteAttribute>()!.Template);
        Assert.Equal("reports", controller.GetCustomAttribute<RequireModulePermissionAttribute>()!.Module);
        Assert.NotNull(controller.GetCustomAttribute<RequireBusinessTypeAttribute>());

        foreach (string action in new[]
        {
            nameof(ReceivableInvoiceDetailReportController.Run),
            nameof(ReceivableInvoiceDetailReportController.Customers),
            nameof(ReceivableInvoiceDetailReportController.Items),
            nameof(ReceivableInvoiceDetailReportController.ItemCategories),
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
        Type controller = typeof(ReceivableInvoiceDetailReportController);

        Assert.Equal("customers", controller.GetMethod(nameof(ReceivableInvoiceDetailReportController.Customers))!.GetCustomAttribute<HttpGetAttribute>()!.Template);
        Assert.Equal("items", controller.GetMethod(nameof(ReceivableInvoiceDetailReportController.Items))!.GetCustomAttribute<HttpGetAttribute>()!.Template);
        Assert.Equal("item-categories", controller.GetMethod(nameof(ReceivableInvoiceDetailReportController.ItemCategories))!.GetCustomAttribute<HttpGetAttribute>()!.Template);
    }

    // ---- The catalog source, held to the same period ----------------------------------

    [Fact]
    public void The_catalog_source_declares_both_dates_required_and_keeps_its_columns()
    {
        var source = new ReceivableInvoiceDetailSourceRepository();

        Assert.Equal(["from", "to"], source.Parameters.Select(p => p.Name));
        Assert.All(source.Parameters, p => Assert.True(p.IsRequired));
        Assert.Equal(
            ["documentNo", "documentDate", "contactName", "itemCode", "itemName", "quantity", "unitPrice", "lineTotal"],
            source.Columns.Select(c => c.Key));
    }
}
