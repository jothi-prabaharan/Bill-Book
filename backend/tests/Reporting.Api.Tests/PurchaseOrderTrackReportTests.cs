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
/// The Purchase Order Track Report (TK-240), on orders worked by hand. The branch's base currency is INR and the period
/// is October 2026. Widget A is bought at 400 and Gadget B at 700:
/// <list type="bullet">
/// <item><b>POR-9701</b> (Zenith): Widget A 10 and Gadget B 5, nothing received or billed. <b>Open</b>, 7,500.</item>
/// <item><b>POR-9702</b> (Zenith): Widget A 10 with 4 received and none billed, Gadget B 4 received and billed.
/// <b>Partly received</b>, 6,800, of which 4,400 is received and 2,800 billed.</item>
/// <item><b>POR-9703</b> (Acme) in US dollars at 80: Widget A 5 at USD 100, received, and billed through the receipt
/// line on a bill at 82. <b>Billed</b>, 40,000.</item>
/// <item><b>POR-9704</b> (Zenith): Widget A 10 with 6 received and billed, closed. <b>Closed short</b>, 4,000.</item>
/// <item><b>POR-9705</b> (Zenith): Gadget B 5, received, and billed on two bills of 3 and 2. <b>Billed</b>, 3,500.</item>
/// <item><b>POR-9709</b> (Acme): Gadget B 8, all received and 3 billed. <b>Received</b>, 5,600.</item>
/// <item>A voided order (<b>cancelled</b>), a draft, and an order dated in September, which are not in the default list.</item>
/// </list>
/// The six orders in the default list come to 67,400 ordered, 55,900 received and 50,800 billed, so 11,500 is still
/// to receive and 16,600 to bill. Bills against the orders: BIL-9803 (2,800) and the rest named on each line below; a
/// voided bill, a draft bill and a bill with no order behind it bill nothing against an order. The builders run on lists
/// through the same expressions the repository runs against the read models; the composition is pure. Nothing here
/// needs PostgreSQL.
/// </summary>
public sealed class PurchaseOrderTrackReportTests
{
    private const long Acme = 9201, Zenith = 9203, WidgetA = 9701, GadgetB = 9702;

    private static readonly DateOnly October1 = new(2026, 10, 1);
    private static readonly DateOnly October31 = new(2026, 10, 31);

    private static PurchaseOrderRead Order(
        long id, DateOnly date, long contact, string currency, decimal rate, PurchaseFulfilmentStatus fulfilment,
        DocumentStatus status = DocumentStatus.Posted) =>
        new()
        {
            PurchaseOrderId = id, TransactionTypeCode = "POR", DocumentNo = $"POR-{id}", DocumentDate = date, ContactId = contact, CurrencyCode = currency,
            ExchangeRate = rate, FulfilmentStatus = fulfilment, Status = status, ExpectedDate = date.AddDays(14),
        };

    private static readonly List<PurchaseOrderRead> Orders =
    [
        Order(9701, new(2026, 10, 3), Zenith, "INR", 1m, PurchaseFulfilmentStatus.Open),
        Order(9702, new(2026, 10, 8), Zenith, "INR", 1m, PurchaseFulfilmentStatus.PartlyReceived),
        Order(9703, new(2026, 10, 12), Acme, "USD", 80m, PurchaseFulfilmentStatus.Closed),
        Order(9704, new(2026, 10, 15), Zenith, "INR", 1m, PurchaseFulfilmentStatus.Closed),
        Order(9705, new(2026, 10, 18), Zenith, "INR", 1m, PurchaseFulfilmentStatus.Closed),
        Order(9706, new(2026, 10, 20), Zenith, "INR", 1m, PurchaseFulfilmentStatus.Cancelled, DocumentStatus.Void),
        Order(9707, new(2026, 10, 22), Zenith, "INR", 1m, PurchaseFulfilmentStatus.Open, DocumentStatus.Draft),
        Order(9708, new(2026, 9, 20), Zenith, "INR", 1m, PurchaseFulfilmentStatus.Open),
        Order(9709, new(2026, 10, 25), Acme, "INR", 1m, PurchaseFulfilmentStatus.Closed),
    ];

    private static PurchaseOrderDetailRead Line(long id, long order, int number, long item, decimal qty, decimal price, decimal received) =>
        new()
        {
            PurchaseOrderDetailId = id, PurchaseOrderId = order, LineNumber = number, ItemId = item, UomId = 40, Quantity = qty,
            TaxableAmount = qty * price, ReceivedQuantity = received,
        };

    private static readonly List<PurchaseOrderDetailRead> Details =
    [
        Line(9711, 9701, 1, WidgetA, 10m, 400m, 0m),
        Line(9712, 9701, 2, GadgetB, 5m, 700m, 0m),
        Line(9721, 9702, 1, WidgetA, 10m, 400m, 4m),
        Line(9722, 9702, 2, GadgetB, 4m, 700m, 4m),
        Line(9731, 9703, 1, WidgetA, 5m, 100m, 5m),
        Line(9741, 9704, 1, WidgetA, 10m, 400m, 6m),
        Line(9751, 9705, 1, GadgetB, 5m, 700m, 5m),
        Line(9761, 9706, 1, WidgetA, 1m, 1_000m, 0m),
        Line(9771, 9707, 1, WidgetA, 1m, 1_000m, 0m),
        Line(9781, 9708, 1, WidgetA, 1m, 1_000m, 0m),
        Line(9791, 9709, 1, GadgetB, 8m, 700m, 8m),
    ];

    private static readonly List<ItemRead> Items =
    [
        new() { ItemId = WidgetA, ItemCode = "WIDGET-A", ItemName = "Widget A" },
        new() { ItemId = GadgetB, ItemCode = "GADGET-B", ItemName = "Gadget B" },
    ];

    private static readonly List<UnitOfMeasureRead> Units = [new() { UomId = 40, UomCode = "PCS" }];

    private static BillRead Bill(long id, decimal rate = 1m, DocumentStatus status = DocumentStatus.Posted) =>
        new() { BillId = id, DocumentNo = $"BIL-{id}", ExchangeRate = rate, Status = status };

    private static readonly List<BillRead> Bills =
    [
        Bill(9803),
        Bill(9802, rate: 82m),
        Bill(9804),
        Bill(9805),
        Bill(9806),
        Bill(9807),
        Bill(9808),
        Bill(9809),
        Bill(9810, status: DocumentStatus.Void),
        Bill(9811, status: DocumentStatus.Draft),
    ];

    private static BillDetailRead BillLine(long id, long bill, long? orderLine, long? receiptLine, decimal qty, decimal taxable) =>
        new() { BillDetailId = id, BillId = bill, PurchaseOrderDetailId = orderLine, GoodsReceiptDetailId = receiptLine, Quantity = qty, TaxableAmount = taxable };

    private static readonly List<BillDetailRead> BillDetails =
    [
        BillLine(1, 9803, 9722, null, 4m, 2_800m),
        BillLine(2, 9802, null, 9831, 5m, 500m),
        BillLine(3, 9804, 9741, null, 6m, 2_400m),
        BillLine(4, 9805, 9751, null, 3m, 2_100m),
        BillLine(5, 9806, 9751, null, 2m, 1_400m),
        BillLine(6, 9807, 9791, null, 3m, 2_100m),
        BillLine(7, 9808, null, null, 1m, 10_000m),
        BillLine(8, 9809, null, 9832, 1m, 999m),
        BillLine(9, 9810, 9751, null, 1m, 700m),
        BillLine(10, 9811, 9711, null, 10m, 4_000m),
    ];

    private static readonly List<GoodsReceiptDetailRead> ReceiptDetails =
    [
        new() { GoodsReceiptDetailId = 9831, PurchaseOrderDetailId = 9731 },
        new() { GoodsReceiptDetailId = 9832, PurchaseOrderDetailId = null },
    ];

    private static readonly List<VendorRef> Vendors =
    [
        new() { ContactId = Zenith, Code = "ZENITH", Name = "Zenith Components" },
        new() { ContactId = Acme, Code = "ACME", Name = "Acme Supplies" },
    ];

    private static PurchaseOrderTrackReportRequest Request(
        long? contact = null, PurchaseOrderProgress? progress = null, bool outstanding = false, DateOnly? from = null, DateOnly? to = null) =>
        new() { FromDate = from ?? October1, ToDate = to ?? October31, ContactId = contact, Progress = progress, OutstandingOnly = outstanding };

    private static IQueryable<PurchaseBilledLine> Billed() =>
        PurchaseOrderTrackReportRepository.BilledLines(Bills.AsQueryable(), BillDetails.AsQueryable(), ReceiptDetails.AsQueryable());

    private static IQueryable<PurchaseBilledSum> BilledSums() => PurchaseOrderTrackReportRepository.BilledSums(Billed());

    private static List<PurchaseOrderTrackEntry> RowsFor(PurchaseOrderTrackReportRequest request)
    {
        IQueryable<PurchaseOrderRead> orders = PurchaseOrderTrackReportRepository.Orders(Orders.AsQueryable(), request, request.FromDate!.Value, request.ToDate!.Value);
        return [.. PurchaseOrderTrackReportRepository.Rows(
            orders,
            PurchaseOrderTrackReportRepository.Figures(orders, Details.AsQueryable()),
            PurchaseOrderTrackReportRepository.BilledFigures(orders, Details.AsQueryable(), BilledSums()))];
    }

    private static PurchaseOrderTrackEntry RowOf(IEnumerable<PurchaseOrderTrackEntry> rows, long id) => rows.Single(r => r.PurchaseOrderId == id);

    // ---- Which orders ----------------------------------------------------------------------

    [Fact]
    public void Posted_and_voided_orders_in_the_period_are_listed_and_a_draft_and_another_month_are_not()
    {
        Assert.Equal([9701L, 9702, 9703, 9704, 9705, 9706, 9709], RowsFor(Request()).Select(r => r.PurchaseOrderId).Order());
    }

    [Fact]
    public void The_vendor_narrows_the_orders()
    {
        Assert.Equal([9703L, 9709], RowsFor(Request(contact: Acme)).Select(r => r.PurchaseOrderId).Order());
    }

    // ---- What has been billed -----------------------------------------------------------------

    [Fact]
    public void A_bill_line_counts_against_the_order_line_it_names_or_the_one_its_receipt_line_names()
    {
        Dictionary<long, decimal> billed = BilledSums().ToDictionary(s => s.PurchaseOrderDetailId, s => s.Quantity);

        Assert.Equal(4m, billed[9722]);
        Assert.Equal(5m, billed[9731]);
        Assert.Equal(6m, billed[9741]);
        Assert.Equal(5m, billed[9751]);
        Assert.Equal(3m, billed[9791]);
    }

    [Fact]
    public void A_voided_bill_a_draft_bill_a_bill_with_no_order_and_a_receipt_with_no_order_bill_nothing_against_an_order()
    {
        Dictionary<long, decimal> billed = BilledSums().ToDictionary(s => s.PurchaseOrderDetailId, s => s.Quantity);

        // 9751 is 3 + 2: the voided bill's 1 is not there. The draft against 9711, the bill with no links and the receipt line with no order add nothing.
        Assert.Equal(5m, billed[9751]);
        Assert.DoesNotContain(9711L, billed.Keys);
        Assert.Equal([9722L, 9731, 9741, 9751, 9791], billed.Keys.Order());
    }

    [Fact]
    public void A_bill_line_naming_an_order_line_counts_there_even_when_it_also_names_a_receipt_line_of_another()
    {
        List<BillDetailRead> lines = [BillLine(20, 9803, 9722, 9831, 4m, 2_800m)];

        List<PurchaseBilledLine> billed = [.. PurchaseOrderTrackReportRepository.BilledLines(Bills.AsQueryable(), lines.AsQueryable(), ReceiptDetails.AsQueryable())];

        Assert.Equal([9722L], billed.Select(l => l.PurchaseOrderDetailId));
    }

    [Fact]
    public void A_bills_value_is_its_line_before_tax_in_base_at_the_bills_own_rate()
    {
        PurchaseBilledLine line = Billed().Single(l => l.PurchaseOrderDetailId == 9731);

        // USD 500 at 82.
        Assert.Equal((9802L, 5m, 41_000m), (line.BillId, line.Quantity, line.Value));
    }

    // ---- Progress ---------------------------------------------------------------------------

    [Theory]
    [InlineData(9701L, PurchaseOrderProgress.Open)]
    [InlineData(9702L, PurchaseOrderProgress.PartlyReceived)]
    [InlineData(9703L, PurchaseOrderProgress.Billed)]
    [InlineData(9704L, PurchaseOrderProgress.ClosedShort)]
    [InlineData(9705L, PurchaseOrderProgress.Billed)]
    [InlineData(9706L, PurchaseOrderProgress.Cancelled)]
    [InlineData(9709L, PurchaseOrderProgress.Received)]
    public void An_orders_progress_is_worked_out_from_every_line(long id, PurchaseOrderProgress expected)
    {
        Assert.Equal(expected, RowOf(RowsFor(Request()), id).Progress);
    }

    [Fact]
    public void One_line_not_yet_received_or_billed_keeps_the_order_from_being_received_or_billed()
    {
        // POR-9702 has a received and billed line, and a line with only part received: partly received, not received or billed.
        Assert.Equal(PurchaseOrderProgress.PartlyReceived, RowOf(RowsFor(Request()), 9702).Progress);
    }

    [Fact]
    public void A_cancelled_fulfilment_makes_an_order_cancelled_whatever_its_document_status()
    {
        PurchaseOrderRead[] orders = [Order(9800, new(2026, 10, 4), Zenith, "INR", 1m, PurchaseFulfilmentStatus.Cancelled)];
        PurchaseOrderDetailRead[] details = [Line(9801, 9800, 1, WidgetA, 2m, 100m, 0m)];

        PurchaseOrderTrackEntry row = PurchaseOrderTrackReportRepository.Rows(
            orders.AsQueryable(),
            PurchaseOrderTrackReportRepository.Figures(orders.AsQueryable(), details.AsQueryable()),
            PurchaseOrderTrackReportRepository.BilledFigures(orders.AsQueryable(), details.AsQueryable(), BilledSums())).Single();

        Assert.Equal(PurchaseOrderProgress.Cancelled, row.Progress);
    }

    [Fact]
    public void With_no_progress_chosen_every_order_but_the_cancelled_one_is_listed()
    {
        List<PurchaseOrderTrackEntry> listed = [.. PurchaseOrderTrackReportRepository.WithProgress(RowsFor(Request()).AsQueryable(), null)];

        Assert.Equal([9701L, 9702, 9703, 9704, 9705, 9709], listed.Select(r => r.PurchaseOrderId).Order());
    }

    [Theory]
    [InlineData(PurchaseOrderProgress.Open, new long[] { 9701 })]
    [InlineData(PurchaseOrderProgress.PartlyReceived, new long[] { 9702 })]
    [InlineData(PurchaseOrderProgress.Received, new long[] { 9709 })]
    [InlineData(PurchaseOrderProgress.Billed, new long[] { 9703, 9705 })]
    [InlineData(PurchaseOrderProgress.ClosedShort, new long[] { 9704 })]
    [InlineData(PurchaseOrderProgress.Cancelled, new long[] { 9706 })]
    public void A_progress_chosen_lists_only_the_orders_at_it(PurchaseOrderProgress progress, long[] expected)
    {
        List<PurchaseOrderTrackEntry> listed = [.. PurchaseOrderTrackReportRepository.WithProgress(RowsFor(Request()).AsQueryable(), progress)];

        Assert.Equal(expected, listed.Select(r => r.PurchaseOrderId).Order());
    }

    [Fact]
    public void Outstanding_only_keeps_the_orders_with_something_still_to_receive_or_bill()
    {
        List<PurchaseOrderTrackEntry> outstanding = [.. PurchaseOrderTrackReportRepository.OutstandingOnly(RowsFor(Request()).AsQueryable(), true)];

        // Open, partly received and received but not billed; not billed, closed short or cancelled.
        Assert.Equal([9701L, 9702, 9709], outstanding.Select(r => r.PurchaseOrderId).Order());
        Assert.Equal(7, PurchaseOrderTrackReportRepository.OutstandingOnly(RowsFor(Request()).AsQueryable(), false).Count());
    }

    // ---- Values -----------------------------------------------------------------------------

    [Fact]
    public void An_orders_values_are_its_lines_before_tax_shared_out_at_the_orders_price()
    {
        PurchaseOrderTrackEntry row = RowOf(RowsFor(Request()), 9702);

        // 10 at 400 and 4 at 700: 4 of 10 received is 1,600, and 4 of 4 is 2,800.
        Assert.Equal((6_800m, 4_400m, 2_800m), (row.OrderedValue, row.ReceivedValue, row.BilledValue));
    }

    [Fact]
    public void An_order_in_another_currency_has_its_values_in_base_at_the_orders_rate_not_the_bills()
    {
        PurchaseOrderTrackEntry row = RowOf(RowsFor(Request()), 9703);

        // USD 500 at 80, all received and billed: 40,000 at the order's price, though the bill itself came to 41,000.
        Assert.Equal((40_000m, 40_000m, 40_000m), (row.OrderedValue, row.ReceivedValue, row.BilledValue));
    }

    [Fact]
    public void An_order_partly_billed_has_the_billed_share_of_its_value()
    {
        PurchaseOrderTrackEntry row = RowOf(RowsFor(Request()), 9709);

        // POR-9709: 3 of 8 at 700.
        Assert.Equal((5_600m, 5_600m, 2_100m), (row.OrderedValue, row.ReceivedValue, row.BilledValue));
    }

    [Fact]
    public void The_default_list_adds_up_as_worked_by_hand()
    {
        List<PurchaseOrderTrackEntry> listed = [.. PurchaseOrderTrackReportRepository.WithProgress(RowsFor(Request()).AsQueryable(), null)];

        Assert.Equal((67_400m, 55_900m, 50_800m), (listed.Sum(r => r.OrderedValue), listed.Sum(r => r.ReceivedValue), listed.Sum(r => r.BilledValue)));
    }

    // ---- Lines ------------------------------------------------------------------------------

    private static List<PurchaseOrderLineRow> LinesOf(params long[] orderIds) =>
        [.. PurchaseOrderTrackReportRepository.Lines(Orders.AsQueryable(), Details.AsQueryable(), Items.AsQueryable(), Units.AsQueryable(), orderIds)];

    private static List<PurchaseBilledFigure> BilledOf(params long[] orderIds) =>
        [.. PurchaseOrderTrackReportRepository.BilledLinesOf(Orders.AsQueryable(), Details.AsQueryable(), BilledSums(), orderIds)];

    [Fact]
    public void Only_the_lines_of_the_orders_asked_for_are_read_with_their_items_and_units()
    {
        List<PurchaseOrderLineRow> lines = LinesOf(9702);

        Assert.Equal([1, 2], lines.Select(l => l.LineNumber).Order());
        PurchaseOrderLineRow widget = lines.Single(l => l.LineNumber == 1);
        Assert.Equal(("WIDGET-A", "Widget A", "PCS", 10m, 4m), (widget.ItemCode, widget.ItemName, widget.UnitCode, widget.Ordered, widget.Received));
    }

    [Fact]
    public void A_lines_values_add_up_to_its_orders_and_ordered_less_received_is_what_is_to_receive()
    {
        List<PurchaseOrderLineRow> lines = LinesOf(9702);
        List<PurchaseBilledFigure> billed = BilledOf(9702);
        PurchaseOrderTrackEntry row = RowOf(RowsFor(Request()), 9702);

        Assert.Equal((row.OrderedValue, row.ReceivedValue, row.BilledValue), (lines.Sum(l => l.OrderedValue), lines.Sum(l => l.ReceivedValue), billed.Sum(f => f.BilledValue)));
    }

    // ---- Bills raised against the orders ----------------------------------------------------

    [Fact]
    public void The_bills_raised_against_an_order_are_the_posted_ones_added_up_at_the_prices_they_were_billed_at()
    {
        List<PurchaseOrderBillsRaised> raised = [.. PurchaseOrderTrackReportRepository.Raised(Details.AsQueryable(), Billed(), [9703L, 9705L, 9701L])];

        // The voided bill, the draft and the bill with no order are not counted; the dollar bill is 500 at 82; two bills make POR-9705's 3,500.
        Assert.Equal([(9703L, 1, 41_000m), (9705L, 2, 3_500m)], raised.OrderBy(r => r.PurchaseOrderId).Select(r => (r.PurchaseOrderId, r.Count, r.Billed)));
    }

    [Fact]
    public void Two_lines_of_one_bill_against_one_order_are_one_bill()
    {
        List<BillDetailRead> lines = [BillLine(30, 9803, 9721, null, 2m, 800m), BillLine(31, 9803, 9722, null, 1m, 700m)];
        IQueryable<PurchaseBilledLine> billed = PurchaseOrderTrackReportRepository.BilledLines(Bills.AsQueryable(), lines.AsQueryable(), ReceiptDetails.AsQueryable());

        PurchaseOrderBillsRaised raised = PurchaseOrderTrackReportRepository.Raised(Details.AsQueryable(), billed, [9702L]).Single();

        Assert.Equal((1, 1_500m), (raised.Count, raised.Billed));
    }

    // ---- Compose ------------------------------------------------------------------------------

    private static PurchaseOrderTrackReportResultDto Run(PurchaseOrderTrackReportRequest? request = null, IReadOnlyList<PurchaseOrderProgressCount>? counts = null)
    {
        request ??= Request();
        List<PurchaseOrderTrackEntry> rows = [.. PurchaseOrderTrackReportRepository.WithProgress(PurchaseOrderTrackReportRepository.OutstandingOnly(RowsFor(request).AsQueryable(), request.OutstandingOnly), request.Progress)
            .OrderByDescending(r => r.DocumentDate).ThenByDescending(r => r.PurchaseOrderId)];
        long[] ids = [.. rows.Select(r => r.PurchaseOrderId)];
        var totals = new PurchaseOrderTrackEntry { OrderedValue = rows.Sum(r => r.OrderedValue), ReceivedValue = rows.Sum(r => r.ReceivedValue), BilledValue = rows.Sum(r => r.BilledValue) };

        return PurchaseOrderTrackReportRepository.Compose(
            request, rows, rows.Count, LinesOf(ids), BilledOf(ids), [.. PurchaseOrderTrackReportRepository.Raised(Details.AsQueryable(), Billed(), ids)], Vendors, counts ?? [], totals, "INR");
    }

    private static PurchaseOrderTrackOrderDto OrderDto(PurchaseOrderTrackReportResultDto report, long id) => report.Orders.Single(o => o.PurchaseOrderId == id);

    [Fact]
    public void The_orders_are_newest_first_with_their_vendors_progress_and_dates()
    {
        PurchaseOrderTrackReportResultDto report = Run();

        Assert.Equal([9709L, 9705, 9704, 9703, 9702, 9701], report.Orders.Select(o => o.PurchaseOrderId));
        PurchaseOrderTrackOrderDto order = OrderDto(report, 9702);
        Assert.Equal(("ZENITH", "Zenith Components", PurchaseOrderProgress.PartlyReceived, new DateOnly(2026, 10, 8)), (order.ContactCode, order.ContactName, order.Progress, order.DocumentDate));
        Assert.Equal(new DateOnly(2026, 10, 22), order.ExpectedDate);
    }

    [Fact]
    public void An_order_lists_its_lines_in_line_order_with_what_is_to_receive_and_to_bill()
    {
        PurchaseOrderTrackOrderDto order = OrderDto(Run(), 9702);

        Assert.Equal([1, 2], order.Lines.Select(l => l.LineNumber));
        PurchaseOrderTrackLineDto widget = order.Lines[0];
        Assert.Equal((10m, 4m, 0m, 6m, 10m), (widget.Ordered, widget.Received, widget.Billed, widget.ToReceive, widget.ToBill));
        Assert.Equal((4_000m, 1_600m, 0m, 2_400m, 4_000m), (widget.OrderedValue, widget.ReceivedValue, widget.BilledValue, widget.ToReceiveValue, widget.ToBillValue));
    }

    [Fact]
    public void Ordered_less_received_and_ordered_less_billed_hold_on_every_line_and_every_order()
    {
        PurchaseOrderTrackReportResultDto report = Run();

        foreach (PurchaseOrderTrackOrderDto order in report.Orders)
        {
            Assert.Equal(order.OrderedValue - order.ReceivedValue, order.ToReceiveValue);
            Assert.Equal(order.OrderedValue - order.BilledValue, order.ToBillValue);
            Assert.All(order.Lines, l =>
            {
                Assert.Equal(l.Ordered - l.Received, l.ToReceive);
                Assert.Equal(l.Ordered - l.Billed, l.ToBill);
                Assert.Equal(l.OrderedValue - l.ReceivedValue, l.ToReceiveValue);
                Assert.Equal(l.OrderedValue - l.BilledValue, l.ToBillValue);
            });
            Assert.Equal(order.OrderedValue, order.Lines.Sum(l => l.OrderedValue));
            Assert.Equal(order.BilledValue, order.Lines.Sum(l => l.BilledValue));
        }
    }

    [Fact]
    public void An_order_carries_the_bills_raised_against_it_and_what_they_came_to()
    {
        PurchaseOrderTrackReportResultDto report = Run();

        Assert.Equal((2, 3_500m), (OrderDto(report, 9705).BillsRaised, OrderDto(report, 9705).BillTotal));
        Assert.Equal((1, 41_000m), (OrderDto(report, 9703).BillsRaised, OrderDto(report, 9703).BillTotal));
        Assert.Equal((0, 0m), (OrderDto(report, 9701).BillsRaised, OrderDto(report, 9701).BillTotal));
    }

    [Fact]
    public void A_line_for_no_item_reads_as_its_own_description()
    {
        PurchaseOrderTrackReportResultDto report = PurchaseOrderTrackReportRepository.Compose(
            Request(),
            [new() { PurchaseOrderId = 1, DocumentNo = "POR-1", DocumentDate = October1, ContactId = Zenith, CurrencyCode = "INR" }],
            1,
            [new() { PurchaseOrderId = 1, LineNumber = 1, Description = "Installation", Ordered = 1m }],
            [], [], Vendors, [], new(), "INR");

        Assert.Equal(("Installation", null), (report.Orders[0].Lines[0].ItemName, report.Orders[0].Lines[0].ItemCode));
    }

    [Fact]
    public void A_vendor_the_names_do_not_know_is_still_listed_by_its_number()
    {
        PurchaseOrderTrackReportResultDto report = PurchaseOrderTrackReportRepository.Compose(
            Request(), [new() { PurchaseOrderId = 1, DocumentNo = "POR-1", DocumentDate = October1, ContactId = 77, CurrencyCode = "INR" }], 1, [], [], [], [], [], new(), "INR");

        Assert.Equal("Vendor 77", report.Orders[0].ContactName);
    }

    [Fact]
    public void Every_progress_has_a_count_a_nought_included()
    {
        PurchaseOrderTrackReportResultDto report = Run(counts: [new() { Progress = PurchaseOrderProgress.Billed, Count = 4 }]);

        Assert.Equal(Enum.GetValues<PurchaseOrderProgress>().Length, report.ProgressCounts.Count);
        Assert.Equal(4, report.ProgressCounts[PurchaseOrderProgress.Billed]);
        Assert.Equal(0, report.ProgressCounts[PurchaseOrderProgress.Open]);
    }

    [Fact]
    public void The_totals_of_every_match_and_what_is_outstanding_are_worked_out_from_them()
    {
        PurchaseOrderTrackTotalsDto totals = Run().Totals;

        Assert.Equal((67_400m, 55_900m, 50_800m, 11_500m, 16_600m), (totals.OrderedValue, totals.ReceivedValue, totals.BilledValue, totals.ToReceiveValue, totals.ToBillValue));
    }

    [Fact]
    public void The_period_and_page_are_passed_on()
    {
        var request = Request();
        request.PageNumber = 3;
        request.PageSize = 10;

        PurchaseOrderTrackReportResultDto report = Run(request);

        Assert.Equal((October1, October31, "INR"), (report.FromDate, report.ToDate, report.BaseCurrencyCode));
        Assert.Equal((3, 10, 6), (report.PageNumber, report.PageSize, report.TotalOrders));
    }

    // ---- The request and the guards ---------------------------------------------------

    [Fact]
    public void The_two_dates_are_required_and_nothing_else_is()
    {
        Assert.Equal(
            [nameof(PurchaseOrderTrackReportRequest.FromDate), nameof(PurchaseOrderTrackReportRequest.ToDate)],
            typeof(PurchaseOrderTrackReportRequest).GetProperties()
                .Where(p => p.GetCustomAttribute<RequiredAttribute>() is not null)
                .Select(p => p.Name));
    }

    [Fact]
    public void A_missing_date_is_refused_at_its_field_by_the_request_itself()
    {
        var request = new PurchaseOrderTrackReportRequest();
        var results = new List<ValidationResult>();

        Assert.False(Validator.TryValidateObject(request, new ValidationContext(request), results, validateAllProperties: true));
        Assert.Equal(
            ["From date is required.", "To date is required."],
            results.Select(r => r.ErrorMessage).OrderBy(m => m, StringComparer.Ordinal));
    }

    [Fact]
    public void A_page_size_above_a_hundred_is_refused_by_the_request_itself()
    {
        var request = new PurchaseOrderTrackReportRequest { FromDate = October1, ToDate = October31, PageSize = 101 };
        var results = new List<ValidationResult>();

        Assert.False(Validator.TryValidateObject(request, new ValidationContext(request), results, validateAllProperties: true));
        Assert.Equal(["Page size must be between 1 and 100."], results.Select(r => r.ErrorMessage));
    }

    [Fact]
    public void A_period_that_ends_before_it_begins_and_an_unknown_progress_are_refused_at_their_fields()
    {
        Assert.Equal(
            [(nameof(PurchaseOrderTrackReportRequest.ToDate), "To date cannot be before the from date.")],
            PurchaseOrderTrackReportRepository.Refusals(new PurchaseOrderTrackReportRequest { FromDate = October31, ToDate = October1 }));

        Assert.Equal(
            [(nameof(PurchaseOrderTrackReportRequest.Progress), "Progress must be one of the options offered.")],
            PurchaseOrderTrackReportRepository.Refusals(Request(progress: (PurchaseOrderProgress)9)));

        Assert.Empty(PurchaseOrderTrackReportRepository.Refusals(Request()));
    }

    [Fact]
    public async Task Another_branch_in_the_request_is_forbidden_not_hidden()
    {
        var controller = new PurchaseOrderTrackReportController(null!)
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
            new PurchaseOrderTrackReportRequest { FromDate = October1, ToDate = October31, OrgId = Guid.NewGuid() }, default);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public void The_endpoint_needs_the_reports_module_purchase_view_and_a_trade_on_each_action()
    {
        Type controller = typeof(PurchaseOrderTrackReportController);

        Assert.Equal("api/statements/purchase-order-track-report", controller.GetCustomAttribute<RouteAttribute>()!.Template);
        Assert.Equal("reports", controller.GetCustomAttribute<RequireModulePermissionAttribute>()!.Module);
        Assert.NotNull(controller.GetCustomAttribute<RequireBusinessTypeAttribute>());

        foreach (string action in new[] { nameof(PurchaseOrderTrackReportController.Run), nameof(PurchaseOrderTrackReportController.Vendors) })
        {
            Assert.Contains(
                "purchase.view",
                controller.GetMethod(action)!.GetCustomAttributes<RequirePermissionAttribute>().Select(a => a.Permission));
        }

        Assert.Equal("vendors", controller.GetMethod(nameof(PurchaseOrderTrackReportController.Vendors))!.GetCustomAttribute<HttpGetAttribute>()!.Template);
    }

    // ---- The catalog source, held to the same period ----------------------------------

    [Fact]
    public void The_catalog_source_declares_both_dates_required_and_keeps_its_columns()
    {
        var source = new PurchaseOrderTrackSourceRepository();

        Assert.Equal(["from", "to"], source.Parameters.Select(p => p.Name));
        Assert.All(source.Parameters, p => Assert.True(p.IsRequired));
        Assert.Equal(
            ["documentNo", "documentDate", "contactName", "status", "fulfilmentStatus", "totalAmount", "purchaseOrderId"],
            source.Columns.Select(c => c.Key));
    }
}
