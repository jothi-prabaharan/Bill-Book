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
/// The Sales Order Track Report (TK-230), on orders worked by hand. The branch's base currency is INR and the period
/// is October 2026. Widget A is sold at 600 and Gadget B at 1,000:
/// <list type="bullet">
/// <item><b>SOR-9501</b> (Zenith): Widget A 10 and Gadget B 5, nothing delivered or billed. <b>Open</b>, 11,000.</item>
/// <item><b>SOR-9502</b> (Zenith): Widget A 10 with 4 delivered and none billed, Gadget B 4 delivered and billed.
/// <b>Partly delivered</b>, 10,000, of which 6,400 is delivered and 4,000 billed.</item>
/// <item><b>SOR-9503</b> (Acme) in US dollars at 80: Widget A 5 at USD 100, delivered and billed. <b>Invoiced</b>, 40,000.</item>
/// <item><b>SOR-9504</b> (Zenith): Widget A 10 with 6 delivered and billed, closed. <b>Closed short</b>, 6,000.</item>
/// <item><b>SOR-9505</b> (Zenith): Gadget B 5, delivered and billed. <b>Invoiced</b>, 5,000.</item>
/// <item><b>SOR-9509</b> (Acme): Gadget B 8, all delivered and 3 billed. <b>Delivered</b>, 8,000.</item>
/// <item>A voided order (<b>cancelled</b>), a draft, and an order dated in September, which are not in the default list.</item>
/// </list>
/// The six orders in the default list come to 80,000 ordered, 63,000 delivered and 55,600 invoiced, so 17,000 is still
/// to deliver and 24,400 to bill. Invoices raised from the orders: INV-9405 (40,000) from SOR-9503 and INV-9403
/// (5,000) from SOR-9505. The builders run on lists through the same expressions the repository runs against the read
/// models; the composition is pure. Nothing here needs PostgreSQL.
/// </summary>
public sealed class SalesOrderTrackReportTests
{
    private const long Acme = 9101, Zenith = 9103, WidgetA = 9601, GadgetB = 9602;

    private static readonly DateOnly October1 = new(2026, 10, 1);
    private static readonly DateOnly October31 = new(2026, 10, 31);

    private static SalesOrderRead Order(
        long id, DateOnly date, long contact, string currency, decimal rate, FulfilmentStatus fulfilment,
        DocumentStatus status = DocumentStatus.Posted) =>
        new()
        {
            SalesOrderId = id, TransactionTypeCode = "SOR", DocumentNo = $"SOR-{id}", DocumentDate = date, ContactId = contact, CurrencyCode = currency,
            ExchangeRate = rate, FulfilmentStatus = fulfilment, Status = status,
        };

    private static readonly List<SalesOrderRead> Orders =
    [
        Order(9501, new(2026, 10, 3), Zenith, "INR", 1m, FulfilmentStatus.Open),
        Order(9502, new(2026, 10, 8), Zenith, "INR", 1m, FulfilmentStatus.PartlyDelivered),
        Order(9503, new(2026, 10, 12), Acme, "USD", 80m, FulfilmentStatus.Closed),
        Order(9504, new(2026, 10, 15), Zenith, "INR", 1m, FulfilmentStatus.Closed),
        Order(9505, new(2026, 10, 18), Zenith, "INR", 1m, FulfilmentStatus.Closed),
        Order(9506, new(2026, 10, 20), Zenith, "INR", 1m, FulfilmentStatus.Cancelled, DocumentStatus.Void),
        Order(9507, new(2026, 10, 22), Zenith, "INR", 1m, FulfilmentStatus.Open, DocumentStatus.Draft),
        Order(9508, new(2026, 9, 20), Zenith, "INR", 1m, FulfilmentStatus.Open),
        Order(9509, new(2026, 10, 25), Acme, "INR", 1m, FulfilmentStatus.Closed),
    ];

    private static SalesOrderDetailRead Line(long id, long order, int number, long item, decimal qty, decimal price, decimal delivered, decimal invoiced) =>
        new()
        {
            SalesOrderDetailId = id, SalesOrderId = order, LineNumber = number, ItemId = item, UomId = 40, Quantity = qty, UnitPrice = price,
            TaxableAmount = qty * price, DeliveredQuantity = delivered, InvoicedQuantity = invoiced,
        };

    private static readonly List<SalesOrderDetailRead> Details =
    [
        Line(9511, 9501, 1, WidgetA, 10m, 600m, 0m, 0m),
        Line(9512, 9501, 2, GadgetB, 5m, 1_000m, 0m, 0m),
        Line(9521, 9502, 1, WidgetA, 10m, 600m, 4m, 0m),
        Line(9522, 9502, 2, GadgetB, 4m, 1_000m, 4m, 4m),
        Line(9531, 9503, 1, WidgetA, 5m, 100m, 5m, 5m),
        Line(9541, 9504, 1, WidgetA, 10m, 600m, 6m, 6m),
        Line(9551, 9505, 1, GadgetB, 5m, 1_000m, 5m, 5m),
        Line(9561, 9506, 1, WidgetA, 1m, 1_000m, 0m, 0m),
        Line(9571, 9507, 1, WidgetA, 1m, 1_000m, 0m, 0m),
        Line(9581, 9508, 1, WidgetA, 1m, 1_000m, 0m, 0m),
        Line(9591, 9509, 1, GadgetB, 8m, 1_000m, 8m, 3m),
    ];

    private static readonly List<ItemRead> Items =
    [
        new() { ItemId = WidgetA, ItemCode = "WIDGET-A", ItemName = "Widget A" },
        new() { ItemId = GadgetB, ItemCode = "GADGET-B", ItemName = "Gadget B" },
    ];

    private static readonly List<UnitOfMeasureRead> Units = [new() { UomId = 40, UomCode = "PCS" }];

    private static readonly List<InvoiceRead> Invoices =
    [
        new() { InvoiceId = 9405, SalesOrderId = 9503, TaxableAmount = 500m, ExchangeRate = 80m, Status = DocumentStatus.Posted },
        new() { InvoiceId = 9403, SalesOrderId = 9505, TaxableAmount = 5_000m, ExchangeRate = 1m, Status = DocumentStatus.Posted },
        new() { InvoiceId = 9406, SalesOrderId = 9505, TaxableAmount = 700m, ExchangeRate = 1m, Status = DocumentStatus.Void },
        new() { InvoiceId = 9401, SalesOrderId = null, TaxableAmount = 10_000m, ExchangeRate = 1m, Status = DocumentStatus.Posted },
    ];

    private static readonly List<CustomerRef> Customers =
    [
        new() { ContactId = Zenith, Code = "ZENITH", Name = "Zenith Traders" },
        new() { ContactId = Acme, Code = "ACME", Name = "Acme Exports" },
    ];

    private static SalesOrderTrackReportRequest Request(
        long? contact = null, SalesOrderProgress? progress = null, bool outstanding = false, DateOnly? from = null, DateOnly? to = null) =>
        new() { FromDate = from ?? October1, ToDate = to ?? October31, ContactId = contact, Progress = progress, OutstandingOnly = outstanding };

    private static List<OrderTrackRow> RowsFor(SalesOrderTrackReportRequest request)
    {
        IQueryable<SalesOrderRead> orders = SalesOrderTrackReportRepository.Orders(Orders.AsQueryable(), request, request.FromDate!.Value, request.ToDate!.Value);
        return [.. SalesOrderTrackReportRepository.Rows(orders, SalesOrderTrackReportRepository.Figures(orders, Details.AsQueryable()))];
    }

    private static OrderTrackRow RowOf(IEnumerable<OrderTrackRow> rows, long id) => rows.Single(r => r.SalesOrderId == id);

    // ---- Which orders ----------------------------------------------------------------------

    [Fact]
    public void Posted_and_voided_orders_in_the_period_are_listed_and_a_draft_and_another_month_are_not()
    {
        Assert.Equal([9501L, 9502, 9503, 9504, 9505, 9506, 9509], RowsFor(Request()).Select(r => r.SalesOrderId).Order());
    }

    [Fact]
    public void The_customer_narrows_the_orders()
    {
        Assert.Equal([9503L, 9509], RowsFor(Request(contact: Acme)).Select(r => r.SalesOrderId).Order());
    }

    // ---- Progress ---------------------------------------------------------------------------

    [Theory]
    [InlineData(9501L, SalesOrderProgress.Open)]
    [InlineData(9502L, SalesOrderProgress.PartlyDelivered)]
    [InlineData(9503L, SalesOrderProgress.Invoiced)]
    [InlineData(9504L, SalesOrderProgress.ClosedShort)]
    [InlineData(9505L, SalesOrderProgress.Invoiced)]
    [InlineData(9506L, SalesOrderProgress.Cancelled)]
    [InlineData(9509L, SalesOrderProgress.Delivered)]
    public void An_orders_progress_is_worked_out_from_every_line(long id, SalesOrderProgress expected)
    {
        Assert.Equal(expected, RowOf(RowsFor(Request()), id).Progress);
    }

    [Fact]
    public void One_line_not_yet_delivered_or_billed_keeps_the_order_from_being_delivered_or_invoiced()
    {
        // SOR-9502 has a delivered and billed line, and a line with only part delivered: partly delivered, not delivered or invoiced.
        Assert.Equal(SalesOrderProgress.PartlyDelivered, RowOf(RowsFor(Request()), 9502).Progress);
    }

    [Fact]
    public void A_cancelled_fulfilment_makes_an_order_cancelled_whatever_its_document_status()
    {
        SalesOrderRead[] orders = [Order(9600, new(2026, 10, 4), Zenith, "INR", 1m, FulfilmentStatus.Cancelled)];
        SalesOrderDetailRead[] details = [Line(9601, 9600, 1, WidgetA, 2m, 100m, 0m, 0m)];

        OrderTrackRow row = SalesOrderTrackReportRepository.Rows(orders.AsQueryable(), SalesOrderTrackReportRepository.Figures(orders.AsQueryable(), details.AsQueryable())).Single();

        Assert.Equal(SalesOrderProgress.Cancelled, row.Progress);
    }

    [Fact]
    public void With_no_progress_chosen_every_order_but_the_cancelled_one_is_listed()
    {
        List<OrderTrackRow> listed = [.. SalesOrderTrackReportRepository.WithProgress(RowsFor(Request()).AsQueryable(), null)];

        Assert.Equal([9501L, 9502, 9503, 9504, 9505, 9509], listed.Select(r => r.SalesOrderId).Order());
    }

    [Theory]
    [InlineData(SalesOrderProgress.Open, new long[] { 9501 })]
    [InlineData(SalesOrderProgress.PartlyDelivered, new long[] { 9502 })]
    [InlineData(SalesOrderProgress.Delivered, new long[] { 9509 })]
    [InlineData(SalesOrderProgress.Invoiced, new long[] { 9503, 9505 })]
    [InlineData(SalesOrderProgress.ClosedShort, new long[] { 9504 })]
    [InlineData(SalesOrderProgress.Cancelled, new long[] { 9506 })]
    public void A_progress_chosen_lists_only_the_orders_at_it(SalesOrderProgress progress, long[] expected)
    {
        List<OrderTrackRow> listed = [.. SalesOrderTrackReportRepository.WithProgress(RowsFor(Request()).AsQueryable(), progress)];

        Assert.Equal(expected, listed.Select(r => r.SalesOrderId).Order());
    }

    [Fact]
    public void Outstanding_only_keeps_the_orders_with_something_still_to_deliver_or_bill()
    {
        List<OrderTrackRow> outstanding = [.. SalesOrderTrackReportRepository.OutstandingOnly(RowsFor(Request()).AsQueryable(), true)];

        // Open, partly delivered and delivered but not invoiced; not invoiced, closed short or cancelled.
        Assert.Equal([9501L, 9502, 9509], outstanding.Select(r => r.SalesOrderId).Order());
        Assert.Equal(7, SalesOrderTrackReportRepository.OutstandingOnly(RowsFor(Request()).AsQueryable(), false).Count());
    }

    // ---- Values -----------------------------------------------------------------------------

    [Fact]
    public void An_orders_values_are_its_lines_before_tax_shared_out_at_the_orders_price()
    {
        OrderTrackRow row = RowOf(RowsFor(Request()), 9502);

        // 10 at 600 and 4 at 1,000: 4 of 10 delivered is 2,400, and 4 of 4 is 4,000.
        Assert.Equal((10_000m, 6_400m, 4_000m), (row.OrderedValue, row.DeliveredValue, row.InvoicedValue));
    }

    [Fact]
    public void An_order_in_another_currency_has_its_values_in_base_at_the_orders_rate()
    {
        OrderTrackRow row = RowOf(RowsFor(Request()), 9503);

        // USD 500 at 80, all delivered and billed.
        Assert.Equal((40_000m, 40_000m, 40_000m), (row.OrderedValue, row.DeliveredValue, row.InvoicedValue));
    }

    [Fact]
    public void The_default_list_adds_up_as_worked_by_hand()
    {
        List<OrderTrackRow> listed = [.. SalesOrderTrackReportRepository.WithProgress(RowsFor(Request()).AsQueryable(), null)];

        Assert.Equal((80_000m, 63_000m, 55_600m), (listed.Sum(r => r.OrderedValue), listed.Sum(r => r.DeliveredValue), listed.Sum(r => r.InvoicedValue)));
    }

    // ---- Lines ------------------------------------------------------------------------------

    private static List<LineRow> LinesOf(params long[] orderIds) =>
        [.. SalesOrderTrackReportRepository.Lines(Orders.AsQueryable(), Details.AsQueryable(), Items.AsQueryable(), Units.AsQueryable(), orderIds)];

    [Fact]
    public void Only_the_lines_of_the_orders_asked_for_are_read_with_their_items_and_units()
    {
        List<LineRow> lines = LinesOf(9502);

        Assert.Equal([1, 2], lines.Select(l => l.LineNumber).Order());
        LineRow widget = lines.Single(l => l.LineNumber == 1);
        Assert.Equal(("WIDGET-A", "Widget A", "PCS", 10m, 4m, 0m), (widget.ItemCode, widget.ItemName, widget.UnitCode, widget.Ordered, widget.Delivered, widget.Invoiced));
    }

    [Fact]
    public void A_lines_values_add_up_to_its_orders_and_ordered_less_delivered_is_what_is_to_deliver()
    {
        List<LineRow> lines = LinesOf(9502);
        OrderTrackRow row = RowOf(RowsFor(Request()), 9502);

        Assert.Equal((row.OrderedValue, row.DeliveredValue, row.InvoicedValue), (lines.Sum(l => l.OrderedValue), lines.Sum(l => l.DeliveredValue), lines.Sum(l => l.InvoicedValue)));
    }

    // ---- Invoices raised from the orders ----------------------------------------------------

    [Fact]
    public void The_invoices_raised_from_an_order_are_the_posted_ones_added_up_at_the_prices_they_were_raised_at()
    {
        List<RaisedInvoices> raised = [.. SalesOrderTrackReportRepository.Raised(Invoices.AsQueryable(), [9503L, 9505L, 9501L])];

        // A voided invoice and one not raised from an order are not counted; the dollar invoice is 500 at 80.
        Assert.Equal([(9503L, 1, 40_000m), (9505L, 1, 5_000m)], raised.OrderBy(r => r.SalesOrderId).Select(r => (r.SalesOrderId, r.Count, r.Billed)));
    }

    // ---- Compose ------------------------------------------------------------------------------

    private static SalesOrderTrackReportResultDto Run(SalesOrderTrackReportRequest? request = null, IReadOnlyList<ProgressCount>? counts = null)
    {
        request ??= Request();
        List<OrderTrackRow> rows = [.. SalesOrderTrackReportRepository.WithProgress(SalesOrderTrackReportRepository.OutstandingOnly(RowsFor(request).AsQueryable(), request.OutstandingOnly), request.Progress)
            .OrderByDescending(r => r.DocumentDate).ThenByDescending(r => r.SalesOrderId)];
        long[] ids = [.. rows.Select(r => r.SalesOrderId)];
        var totals = new OrderTrackRow { OrderedValue = rows.Sum(r => r.OrderedValue), DeliveredValue = rows.Sum(r => r.DeliveredValue), InvoicedValue = rows.Sum(r => r.InvoicedValue) };

        return SalesOrderTrackReportRepository.Compose(
            request, rows, rows.Count, LinesOf(ids), [.. SalesOrderTrackReportRepository.Raised(Invoices.AsQueryable(), ids)], Customers, counts ?? [], totals, "INR");
    }

    private static SalesOrderTrackOrderDto OrderDto(SalesOrderTrackReportResultDto report, long id) => report.Orders.Single(o => o.SalesOrderId == id);

    [Fact]
    public void The_orders_are_newest_first_with_their_customers_progress_and_dates()
    {
        SalesOrderTrackReportResultDto report = Run();

        Assert.Equal([9509L, 9505, 9504, 9503, 9502, 9501], report.Orders.Select(o => o.SalesOrderId));
        SalesOrderTrackOrderDto order = OrderDto(report, 9502);
        Assert.Equal(("ZENITH", "Zenith Traders", SalesOrderProgress.PartlyDelivered, new DateOnly(2026, 10, 8)), (order.ContactCode, order.ContactName, order.Progress, order.DocumentDate));
    }

    [Fact]
    public void An_order_lists_its_lines_in_line_order_with_what_is_to_deliver_and_to_bill()
    {
        SalesOrderTrackOrderDto order = OrderDto(Run(), 9502);

        Assert.Equal([1, 2], order.Lines.Select(l => l.LineNumber));
        SalesOrderTrackLineDto widget = order.Lines[0];
        Assert.Equal((10m, 4m, 0m, 6m, 10m), (widget.Ordered, widget.Delivered, widget.Invoiced, widget.ToDeliver, widget.ToBill));
        Assert.Equal((6_000m, 2_400m, 0m, 3_600m, 6_000m), (widget.OrderedValue, widget.DeliveredValue, widget.InvoicedValue, widget.ToDeliverValue, widget.ToBillValue));
    }

    [Fact]
    public void Ordered_less_delivered_and_ordered_less_invoiced_hold_on_every_line_and_every_order()
    {
        SalesOrderTrackReportResultDto report = Run();

        foreach (SalesOrderTrackOrderDto order in report.Orders)
        {
            Assert.Equal(order.OrderedValue - order.DeliveredValue, order.ToDeliverValue);
            Assert.Equal(order.OrderedValue - order.InvoicedValue, order.ToBillValue);
            Assert.All(order.Lines, l =>
            {
                Assert.Equal(l.Ordered - l.Delivered, l.ToDeliver);
                Assert.Equal(l.Ordered - l.Invoiced, l.ToBill);
                Assert.Equal(l.OrderedValue - l.DeliveredValue, l.ToDeliverValue);
                Assert.Equal(l.OrderedValue - l.InvoicedValue, l.ToBillValue);
            });
            Assert.Equal(order.OrderedValue, order.Lines.Sum(l => l.OrderedValue));
            Assert.Equal(order.InvoicedValue, order.Lines.Sum(l => l.InvoicedValue));
        }
    }

    [Fact]
    public void An_order_carries_the_invoices_raised_from_it_and_what_they_came_to()
    {
        SalesOrderTrackReportResultDto report = Run();

        Assert.Equal((1, 5_000m), (OrderDto(report, 9505).InvoicesRaised, OrderDto(report, 9505).Billed));
        Assert.Equal((1, 40_000m), (OrderDto(report, 9503).InvoicesRaised, OrderDto(report, 9503).Billed));
        Assert.Equal((0, 0m), (OrderDto(report, 9501).InvoicesRaised, OrderDto(report, 9501).Billed));
    }

    [Fact]
    public void A_line_for_no_item_reads_as_its_own_description()
    {
        SalesOrderTrackReportResultDto report = SalesOrderTrackReportRepository.Compose(
            Request(),
            [new() { SalesOrderId = 1, DocumentNo = "SOR-1", DocumentDate = October1, ContactId = Zenith, CurrencyCode = "INR" }],
            1,
            [new() { SalesOrderId = 1, LineNumber = 1, Description = "Installation", Ordered = 1m }],
            [], Customers, [], new(), "INR");

        Assert.Equal(("Installation", null), (report.Orders[0].Lines[0].ItemName, report.Orders[0].Lines[0].ItemCode));
    }

    [Fact]
    public void A_customer_the_names_do_not_know_is_still_listed_by_its_number()
    {
        SalesOrderTrackReportResultDto report = SalesOrderTrackReportRepository.Compose(
            Request(), [new() { SalesOrderId = 1, DocumentNo = "SOR-1", DocumentDate = October1, ContactId = 77, CurrencyCode = "INR" }], 1, [], [], [], [], new(), "INR");

        Assert.Equal("Customer 77", report.Orders[0].ContactName);
    }

    [Fact]
    public void Every_progress_has_a_count_a_nought_included()
    {
        SalesOrderTrackReportResultDto report = Run(counts: [new() { Progress = SalesOrderProgress.Invoiced, Count = 4 }]);

        Assert.Equal(Enum.GetValues<SalesOrderProgress>().Length, report.ProgressCounts.Count);
        Assert.Equal(4, report.ProgressCounts[SalesOrderProgress.Invoiced]);
        Assert.Equal(0, report.ProgressCounts[SalesOrderProgress.Open]);
    }

    [Fact]
    public void The_totals_of_every_match_and_what_is_outstanding_are_worked_out_from_them()
    {
        SalesOrderTrackTotalsDto totals = Run().Totals;

        Assert.Equal((80_000m, 63_000m, 55_600m, 17_000m, 24_400m), (totals.OrderedValue, totals.DeliveredValue, totals.InvoicedValue, totals.ToDeliverValue, totals.ToBillValue));
    }

    [Fact]
    public void The_period_and_page_are_passed_on()
    {
        var request = Request();
        request.PageNumber = 3;
        request.PageSize = 10;

        SalesOrderTrackReportResultDto report = Run(request);

        Assert.Equal((October1, October31, "INR"), (report.FromDate, report.ToDate, report.BaseCurrencyCode));
        Assert.Equal((3, 10, 6), (report.PageNumber, report.PageSize, report.TotalOrders));
    }

    // ---- The request and the guards ---------------------------------------------------

    [Fact]
    public void The_two_dates_are_required_and_nothing_else_is()
    {
        Assert.Equal(
            [nameof(SalesOrderTrackReportRequest.FromDate), nameof(SalesOrderTrackReportRequest.ToDate)],
            typeof(SalesOrderTrackReportRequest).GetProperties()
                .Where(p => p.GetCustomAttribute<RequiredAttribute>() is not null)
                .Select(p => p.Name));
    }

    [Fact]
    public void A_missing_date_is_refused_at_its_field_by_the_request_itself()
    {
        var request = new SalesOrderTrackReportRequest();
        var results = new List<ValidationResult>();

        Assert.False(Validator.TryValidateObject(request, new ValidationContext(request), results, validateAllProperties: true));
        Assert.Equal(
            ["From date is required.", "To date is required."],
            results.Select(r => r.ErrorMessage).OrderBy(m => m, StringComparer.Ordinal));
    }

    [Fact]
    public void A_page_size_above_a_hundred_is_refused_by_the_request_itself()
    {
        var request = new SalesOrderTrackReportRequest { FromDate = October1, ToDate = October31, PageSize = 101 };
        var results = new List<ValidationResult>();

        Assert.False(Validator.TryValidateObject(request, new ValidationContext(request), results, validateAllProperties: true));
        Assert.Equal(["Page size must be between 1 and 100."], results.Select(r => r.ErrorMessage));
    }

    [Fact]
    public void A_period_that_ends_before_it_begins_and_an_unknown_progress_are_refused_at_their_fields()
    {
        Assert.Equal(
            [(nameof(SalesOrderTrackReportRequest.ToDate), "To date cannot be before the from date.")],
            SalesOrderTrackReportRepository.Refusals(new SalesOrderTrackReportRequest { FromDate = October31, ToDate = October1 }));

        Assert.Equal(
            [(nameof(SalesOrderTrackReportRequest.Progress), "Progress must be one of the options offered.")],
            SalesOrderTrackReportRepository.Refusals(Request(progress: (SalesOrderProgress)9)));

        Assert.Empty(SalesOrderTrackReportRepository.Refusals(Request()));
    }

    [Fact]
    public async Task Another_branch_in_the_request_is_forbidden_not_hidden()
    {
        var controller = new SalesOrderTrackReportController(null!)
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
            new SalesOrderTrackReportRequest { FromDate = October1, ToDate = October31, OrgId = Guid.NewGuid() }, default);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public void The_endpoint_needs_the_reports_module_sales_view_and_a_trade_on_each_action()
    {
        Type controller = typeof(SalesOrderTrackReportController);

        Assert.Equal("api/statements/sales-order-track-report", controller.GetCustomAttribute<RouteAttribute>()!.Template);
        Assert.Equal("reports", controller.GetCustomAttribute<RequireModulePermissionAttribute>()!.Module);
        Assert.NotNull(controller.GetCustomAttribute<RequireBusinessTypeAttribute>());

        foreach (string action in new[] { nameof(SalesOrderTrackReportController.Run), nameof(SalesOrderTrackReportController.Customers) })
        {
            Assert.Contains(
                "sales.view",
                controller.GetMethod(action)!.GetCustomAttributes<RequirePermissionAttribute>().Select(a => a.Permission));
        }

        Assert.Equal("customers", controller.GetMethod(nameof(SalesOrderTrackReportController.Customers))!.GetCustomAttribute<HttpGetAttribute>()!.Template);
    }

    // ---- The catalog source, held to the same period ----------------------------------

    [Fact]
    public void The_catalog_source_declares_both_dates_required_and_keeps_its_columns()
    {
        var source = new SalesOrderTrackSourceRepository();

        Assert.Equal(["from", "to"], source.Parameters.Select(p => p.Name));
        Assert.All(source.Parameters, p => Assert.True(p.IsRequired));
        Assert.Equal(["documentNo", "documentDate", "contactName", "status", "fulfilmentStatus", "totalAmount"], source.Columns.Select(c => c.Key));
    }
}
