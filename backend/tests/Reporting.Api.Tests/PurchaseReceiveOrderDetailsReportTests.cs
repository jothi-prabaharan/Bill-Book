using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Reporting.Api.Controllers;
using Reporting.Entity.Models;
using Reporting.Repository.ReadModels;
using Reporting.Repository.Repositories;
using Shared.Kernel.Documents;
using Shared.Kernel.Internal;
using Xunit;

namespace Reporting.Api.Tests;

/// <summary>
/// The Purchase Receive Order Details (TK-241), on receipts worked by hand. The branch's base currency is INR and the
/// period is October 2026. Widget A costs 400, Gadget B 700, and the ring USD 150:
/// <list type="bullet">
/// <item><b>GRN-9801</b> (Zenith, against POR-9811, vendor delivery note DN-77): Widget A, 10 delivered, 2 rejected and 8
/// accepted at 4,000, into the main warehouse from batch BT-1, worth 3,200; and Gadget B, 5 accepted at 3,500 into the
/// cold store, worth 3,500, 3 of it billed. Receipt value 6,700.</item>
/// <item><b>GRN-9802</b> (Acme, no order, US dollars at 80): Widget A, 5 at USD 500, and a ring, 2 at USD 300, with
/// serials S-1 and S-2, both into the main warehouse. Receipt value USD 800, so 64,000 in base.</item>
/// <item><b>GRN-9803</b> (Zenith, against POR-9812): Gadget B, 4 at 2,800, all billed. Receipt value 2,800.</item>
/// <item><b>GRN-9807</b> (Zenith): Gadget B, 3 delivered and 1 rejected at 1,000, so a unit costs 333.333333 and the two
/// accepted are worth 666.67.</item>
/// <item>A draft, a voided receipt and one dated in September, which are not in the list.</item>
/// </list>
/// The four receipts in the list have six lines worth 74,166.67 in base. Bills against the receipt lines: a posted one
/// for 3 of GRN-9801's Gadget B and a posted one for GRN-9803's 4; a voided bill and a draft bill against the first bill
/// nothing. The builders run on lists through the same expressions the repository runs against the read models; the
/// composition is pure. Nothing here needs PostgreSQL.
/// </summary>
public sealed class PurchaseReceiveOrderDetailsReportTests
{
    private const long Acme = 9201, Zenith = 9203, WidgetA = 9701, GadgetB = 9702, Ring = 9703, Main = 9301, Cold = 9302;

    private static readonly DateOnly October1 = new(2026, 10, 1);
    private static readonly DateOnly October31 = new(2026, 10, 31);

    private static GoodsReceiptRead Receipt(
        long id, DateOnly date, long contact, string currency, decimal rate, long? order = null,
        DocumentStatus status = DocumentStatus.Posted) =>
        new()
        {
            GoodsReceiptId = id, TransactionTypeCode = "GRN", DocumentNo = $"GRN-{id}", DocumentDate = date, ContactId = contact, CurrencyCode = currency,
            ExchangeRate = rate, PurchaseOrderId = order, Status = status,
        };

    private static readonly List<GoodsReceiptRead> Receipts =
    [
        new()
        {
            GoodsReceiptId = 9801, TransactionTypeCode = "GRN", DocumentNo = "GRN-9801", DocumentDate = new(2026, 10, 5), ContactId = Zenith, CurrencyCode = "INR",
            ExchangeRate = 1m, PurchaseOrderId = 9811, VendorDeliveryNoteNo = "DN-77", VendorDeliveryNoteDate = new(2026, 10, 4), Status = DocumentStatus.Posted,
        },
        Receipt(9802, new(2026, 10, 12), Acme, "USD", 80m),
        Receipt(9803, new(2026, 10, 20), Zenith, "INR", 1m, order: 9812),
        Receipt(9804, new(2026, 10, 22), Zenith, "INR", 1m, order: 9813, status: DocumentStatus.Draft),
        Receipt(9805, new(2026, 10, 23), Zenith, "INR", 1m, status: DocumentStatus.Void),
        Receipt(9806, new(2026, 9, 20), Zenith, "INR", 1m),
        Receipt(9807, new(2026, 10, 25), Zenith, "INR", 1m),
    ];

    private static GoodsReceiptDetailRead Line(
        long id, long receipt, int number, long item, long warehouse, decimal delivered, decimal accepted, decimal taxable, long? batch = null, string? reason = null) =>
        new()
        {
            GoodsReceiptDetailId = id, GoodsReceiptId = receipt, LineNumber = number, ItemId = item, WarehouseId = warehouse, UomId = 40, Quantity = delivered,
            AcceptedQuantity = accepted, RejectedQuantity = delivered - accepted, RejectionReason = reason, TaxableAmount = taxable, ItemBatchId = batch,
        };

    private static readonly List<GoodsReceiptDetailRead> Details =
    [
        Line(98011, 9801, 1, WidgetA, Main, 10m, 8m, 4_000m, batch: 9951, reason: "Damaged in transit"),
        Line(98012, 9801, 2, GadgetB, Cold, 5m, 5m, 3_500m),
        Line(98021, 9802, 1, WidgetA, Main, 5m, 5m, 500m),
        Line(98022, 9802, 2, Ring, Main, 2m, 2m, 300m),
        Line(98031, 9803, 1, GadgetB, Cold, 4m, 4m, 2_800m),
        Line(98041, 9804, 1, WidgetA, Main, 1m, 1m, 1_000m),
        Line(98051, 9805, 1, WidgetA, Main, 1m, 1m, 1_000m),
        Line(98061, 9806, 1, WidgetA, Main, 1m, 1m, 1_000m),
        Line(98071, 9807, 1, GadgetB, Cold, 3m, 2m, 1_000m),
    ];

    private static readonly List<ItemRef> Items =
    [
        new() { ItemId = WidgetA, Code = "WIDGET-A", Name = "Widget A" },
        new() { ItemId = GadgetB, Code = "GADGET-B", Name = "Gadget B" },
        new() { ItemId = Ring, Code = "RING-1", Name = "Gold ring" },
    ];

    private static readonly List<UnitRef> Units = [new() { UomId = 40, Code = "PCS" }];

    private static readonly List<ReceiptWarehouseRef> Warehouses =
    [
        new() { WarehouseId = Main, Name = "Main warehouse" },
        new() { WarehouseId = Cold, Name = "Cold store" },
    ];

    private static readonly List<ReceiptBatchRef> Batches = [new() { ItemBatchId = 9951, BatchNumber = "BT-1", ExpiryDate = new(2027, 3, 31) }];

    private static readonly List<ReceiptOrderRef> Orders =
    [
        new() { PurchaseOrderId = 9811, DocumentNo = "POR-9811" },
        new() { PurchaseOrderId = 9812, DocumentNo = "POR-9812" },
    ];

    private static readonly List<VendorRef> Vendors =
    [
        new() { ContactId = Zenith, Code = "ZENITH", Name = "Zenith Components" },
        new() { ContactId = Acme, Code = "ACME", Name = "Acme Supplies" },
    ];

    private static BillRead Bill(long id, DocumentStatus status = DocumentStatus.Posted) =>
        new() { BillId = id, DocumentNo = $"BIL-{id}", ExchangeRate = 1m, Status = status };

    private static readonly List<BillRead> Bills =
    [
        Bill(9901), Bill(9902), Bill(9903, DocumentStatus.Void), Bill(9904, DocumentStatus.Draft), Bill(9905),
    ];

    private static BillDetailRead BillLine(long id, long bill, long? receiptLine, decimal qty) =>
        new() { BillDetailId = id, BillId = bill, GoodsReceiptDetailId = receiptLine, Quantity = qty };

    private static readonly List<BillDetailRead> BillDetails =
    [
        BillLine(1, 9901, 98012, 3m),
        BillLine(2, 9902, 98031, 4m),
        BillLine(3, 9903, 98012, 2m),
        BillLine(4, 9904, 98012, 1m),
        BillLine(5, 9905, null, 9m),
    ];

    private static readonly List<StockMovementRead> Movements =
    [
        new() { StockMovementId = 9961, SourceType = "GRN", SourceId = 9802, SourceLineId = 2 },
        new() { StockMovementId = 9962, SourceType = "BIL", SourceId = 9802, SourceLineId = 2 },
    ];

    private static readonly List<ItemSerialRead> SerialRows =
    [
        new() { ItemSerialId = 1, ItemId = Ring, SerialNumber = "S-2", ReceivedMovementId = 9961 },
        new() { ItemSerialId = 2, ItemId = Ring, SerialNumber = "S-1", ReceivedMovementId = 9961 },
        new() { ItemSerialId = 3, ItemId = Ring, SerialNumber = "S-9", ReceivedMovementId = 9962 },
        new() { ItemSerialId = 4, ItemId = Ring, SerialNumber = "S-X", ReceivedMovementId = null },
    ];

    private static PurchaseReceiveOrderDetailsReportRequest Request(
        long? contact = null, long? item = null, long? warehouse = null, long? order = null, DateOnly? from = null, DateOnly? to = null) =>
        new() { FromDate = from ?? October1, ToDate = to ?? October31, ContactId = contact, ItemId = item, WarehouseId = warehouse, PurchaseOrderId = order };

    private static List<GoodsReceiptRead> ReceiptsFor(PurchaseReceiveOrderDetailsReportRequest request)
    {
        IQueryable<GoodsReceiptRead> receipts = PurchaseReceiveOrderDetailsReportRepository.Receipts(Receipts.AsQueryable(), request, request.FromDate!.Value, request.ToDate!.Value);

        if (PurchaseReceiveOrderDetailsReportRepository.LinesAreFiltered(request))
        {
            IQueryable<long> withLines = PurchaseReceiveOrderDetailsReportRepository.Lines(Details.AsQueryable(), request).Select(d => d.GoodsReceiptId);
            receipts = receipts.Where(r => withLines.Contains(r.GoodsReceiptId));
        }

        return [.. receipts.OrderByDescending(r => r.DocumentDate).ThenByDescending(r => r.GoodsReceiptId)];
    }

    private static List<long> IdsFor(PurchaseReceiveOrderDetailsReportRequest request) => [.. ReceiptsFor(request).Select(r => r.GoodsReceiptId)];

    // ---- Which receipts ---------------------------------------------------------------------

    [Fact]
    public void Posted_receipts_in_the_period_are_listed_newest_first_and_a_draft_a_void_and_another_month_are_not()
    {
        Assert.Equal([9807L, 9803, 9802, 9801], IdsFor(Request()));
    }

    [Fact]
    public void The_vendor_and_the_purchase_order_narrow_the_receipts()
    {
        Assert.Equal([9807L, 9803, 9801], IdsFor(Request(contact: Zenith)));
        Assert.Equal([9801L], IdsFor(Request(order: 9811)));
        Assert.Equal([9803L], IdsFor(Request(contact: Zenith, order: 9812)));
    }

    [Fact]
    public void An_item_or_a_warehouse_lists_only_the_receipts_with_such_a_line_and_only_those_lines()
    {
        Assert.Equal([9802L, 9801], IdsFor(Request(item: WidgetA)));
        Assert.Equal([9807L, 9803, 9801], IdsFor(Request(warehouse: Cold)));
        Assert.Equal([9801L], IdsFor(Request(item: GadgetB, warehouse: Cold, contact: Zenith, order: 9811)));

        List<GoodsReceiptDetailRead> lines = [.. PurchaseReceiveOrderDetailsReportRepository.Lines(Details.AsQueryable(), Request(item: WidgetA, warehouse: Main))];
        Assert.Equal([98011L, 98021, 98041, 98051, 98061], lines.Select(d => d.GoodsReceiptDetailId).Order());
    }

    [Fact]
    public void A_period_with_no_receipts_lists_none()
    {
        Assert.Empty(IdsFor(Request(from: new DateOnly(2025, 1, 1), to: new DateOnly(2025, 1, 31))));
    }

    // ---- Billed quantities ------------------------------------------------------------------

    [Fact]
    public void A_receipt_line_is_billed_for_what_the_posted_bills_naming_it_add_up_to()
    {
        List<ReceiptLineBilled> billed = [.. PurchaseReceiveOrderDetailsReportRepository.BilledQuantities(
            Bills.AsQueryable(), BillDetails.AsQueryable(), [98011L, 98012L, 98021L, 98022L, 98031L, 98071L])];

        // A voided bill, a draft bill and a bill line naming no receipt line bill nothing.
        Assert.Equal([(98012L, 3m), (98031L, 4m)], billed.OrderBy(b => b.GoodsReceiptDetailId).Select(b => (b.GoodsReceiptDetailId, b.Quantity)));
    }

    [Fact]
    public void Only_the_lines_asked_for_are_read_for_billing()
    {
        List<ReceiptLineBilled> billed = [.. PurchaseReceiveOrderDetailsReportRepository.BilledQuantities(Bills.AsQueryable(), BillDetails.AsQueryable(), [98031L])];

        Assert.Equal([98031L], billed.Select(b => b.GoodsReceiptDetailId));
    }

    // ---- Serial numbers ---------------------------------------------------------------------

    [Fact]
    public void A_serial_belongs_to_the_receipt_line_whose_stock_in_movement_brought_it_in()
    {
        List<ReceivedSerial> serials = [.. PurchaseReceiveOrderDetailsReportRepository.Serials(SerialRows.AsQueryable(), Movements.AsQueryable(), [9802L, 9801L])];

        // S-9 came in on a bill's movement, and S-X has none.
        Assert.Equal([(9802L, 2L, "S-1"), (9802L, 2L, "S-2")], serials.OrderBy(s => s.SerialNumber).Select(s => (s.GoodsReceiptId, s.LineNumber, s.SerialNumber)));
    }

    // ---- The purchase orders the filter offers -----------------------------------------------

    [Fact]
    public void The_filter_offers_the_newest_orders_a_posted_receipt_was_raised_against()
    {
        List<PurchaseOrderRead> orders =
        [
            new() { PurchaseOrderId = 9811, DocumentNo = "POR-9811", DocumentDate = new(2026, 10, 1), ContactId = Zenith, Status = DocumentStatus.Posted },
            new() { PurchaseOrderId = 9812, DocumentNo = "POR-9812", DocumentDate = new(2026, 10, 15), ContactId = Zenith, Status = DocumentStatus.Posted },
            new() { PurchaseOrderId = 9813, DocumentNo = "POR-9813", DocumentDate = new(2026, 10, 16), ContactId = Acme, Status = DocumentStatus.Posted },
            new() { PurchaseOrderId = 9814, DocumentNo = "POR-9814", DocumentDate = new(2026, 10, 17), ContactId = Acme, Status = DocumentStatus.Draft },
        ];
        List<ContactRead> contacts =
        [
            new() { ContactId = Zenith, DisplayName = "Zenith Components" },
            new() { ContactId = Acme, DisplayName = "Acme Supplies" },
        ];

        List<PurchaseReceiveOrderDetailsOrderChoiceDto> choices = [.. PurchaseReceiveOrderDetailsReportRepository.OrderChoices(orders.AsQueryable(), Receipts.AsQueryable(), contacts.AsQueryable())];

        // POR-9813's only receipt is a draft, and POR-9814 is not posted.
        Assert.Equal([(9812L, "POR-9812", "Zenith Components"), (9811L, "POR-9811", "Zenith Components")], choices.Select(c => (c.PurchaseOrderId, c.DocumentNo, c.ContactName)));
    }

    // ---- Values -----------------------------------------------------------------------------

    private static List<ReceiptLineFigure> FiguresFor(PurchaseReceiveOrderDetailsReportRequest request)
    {
        IQueryable<GoodsReceiptRead> receipts = PurchaseReceiveOrderDetailsReportRepository.Receipts(Receipts.AsQueryable(), request, request.FromDate!.Value, request.ToDate!.Value);
        return [.. PurchaseReceiveOrderDetailsReportRepository.LineFigures(PurchaseReceiveOrderDetailsReportRepository.Lines(Details.AsQueryable(), request), receipts)];
    }

    [Fact]
    public void The_lines_of_the_default_list_come_to_what_was_worked_out_by_hand_in_base_currency()
    {
        List<ReceiptLineFigure> figures = FiguresFor(Request());

        // 3,200 + 3,500 + 40,000 + 24,000 + 2,800 + 666.67
        Assert.Equal((6, 74_166.67m), (figures.Count, figures.Sum(f => f.ValueBase)));
    }

    [Fact]
    public void Each_receipts_lines_add_up_as_worked_by_hand()
    {
        Dictionary<long, decimal> byReceipt = FiguresFor(Request()).GroupBy(f => f.GoodsReceiptId).ToDictionary(g => g.Key, g => g.Sum(f => f.ValueBase));

        Assert.Equal((6_700m, 64_000m, 2_800m, 666.67m), (byReceipt[9801], byReceipt[9802], byReceipt[9803], byReceipt[9807]));
    }

    [Fact]
    public void The_narrowed_lines_are_what_the_totals_add_up()
    {
        List<ReceiptLineFigure> figures = FiguresFor(Request(item: WidgetA));

        // Widget A: 8 of 10 at 4,000 is 3,200, and USD 500 at 80 is 40,000.
        Assert.Equal((2, 43_200m), (figures.Count, figures.Sum(f => f.ValueBase)));
    }

    // ---- Compose ----------------------------------------------------------------------------

    private static PurchaseReceiveOrderDetailsReportResultDto Run(PurchaseReceiveOrderDetailsReportRequest? request = null, IReadOnlyList<ReceivedSerial>? serials = null)
    {
        request ??= Request();
        List<GoodsReceiptRead> page = ReceiptsFor(request);
        long[] ids = [.. page.Select(r => r.GoodsReceiptId)];
        List<GoodsReceiptDetailRead> lines =
        [
            .. PurchaseReceiveOrderDetailsReportRepository.Lines(Details.AsQueryable(), request).Where(d => ids.Contains(d.GoodsReceiptId)).OrderBy(d => d.GoodsReceiptId).ThenBy(d => d.LineNumber),
        ];
        List<ReceiptLineBilled> billed = [.. PurchaseReceiveOrderDetailsReportRepository.BilledQuantities(Bills.AsQueryable(), BillDetails.AsQueryable(), [.. lines.Select(d => d.GoodsReceiptDetailId)])];
        List<ReceivedSerial> received = serials is null
            ? [.. PurchaseReceiveOrderDetailsReportRepository.Serials(SerialRows.AsQueryable(), Movements.AsQueryable(), ids)]
            : [.. serials];
        List<ReceiptLineFigure> figures = FiguresFor(request);
        var totals = new PurchaseReceiveOrderDetailsTotalsDto { Lines = figures.Count, Value = figures.Sum(f => f.ValueBase) };

        return PurchaseReceiveOrderDetailsReportRepository.Compose(request, page, page.Count, lines, billed, Items, Units, Warehouses, Batches, received, Orders, Vendors, totals, "INR");
    }

    private static PurchaseReceiveOrderDetailsReceiptDto ReceiptDto(PurchaseReceiveOrderDetailsReportResultDto report, long id) => report.Receipts.Single(r => r.GoodsReceiptId == id);

    [Fact]
    public void The_receipts_are_newest_first_with_their_vendor_order_and_delivery_note()
    {
        PurchaseReceiveOrderDetailsReportResultDto report = Run();

        Assert.Equal([9807L, 9803, 9802, 9801], report.Receipts.Select(r => r.GoodsReceiptId));
        PurchaseReceiveOrderDetailsReceiptDto receipt = ReceiptDto(report, 9801);
        Assert.Equal(("ZENITH", "Zenith Components", "POR-9811", "DN-77", new DateOnly(2026, 10, 5)), (receipt.ContactCode, receipt.ContactName, receipt.PurchaseOrderNo, receipt.VendorDeliveryNoteNo, receipt.DocumentDate));
        Assert.Equal(new DateOnly(2026, 10, 4), receipt.VendorDeliveryNoteDate);
        Assert.Null(ReceiptDto(report, 9802).PurchaseOrderNo);
    }

    [Fact]
    public void A_line_names_its_item_unit_warehouse_batch_and_what_was_delivered_rejected_and_accepted()
    {
        PurchaseReceiveOrderDetailsLineDto widget = ReceiptDto(Run(), 9801).Lines[0];

        Assert.Equal(("WIDGET-A", "Widget A", "PCS", "Main warehouse"), (widget.ItemCode, widget.ItemName, widget.UnitCode, widget.WarehouseName));
        Assert.Equal((10m, 2m, 8m), (widget.Delivered, widget.Rejected, widget.Accepted));
        Assert.Equal(("BT-1", new DateOnly(2027, 3, 31), "Damaged in transit"), (widget.BatchNumber, widget.BatchExpiryDate, widget.RejectionReason));
    }

    [Fact]
    public void A_unit_costs_the_taxable_value_over_what_was_delivered_and_the_accepted_goods_are_worth_that_much_each()
    {
        PurchaseReceiveOrderDetailsLineDto widget = ReceiptDto(Run(), 9801).Lines[0];

        // 4,000 over 10 delivered is 400 a unit; the 8 accepted are worth 3,200, not 4,000: rejecting two does not raise the cost of the rest.
        Assert.Equal((400m, 3_200m, 3_200m), (widget.UnitCost, widget.Value, widget.ValueBase));
    }

    [Fact]
    public void A_value_that_does_not_divide_evenly_is_rounded_to_two_places_as_the_receipt_posted_it()
    {
        PurchaseReceiveOrderDetailsLineDto gadget = ReceiptDto(Run(), 9807).Lines.Single();

        Assert.Equal((333.333333m, 666.67m, 666.67m), (gadget.UnitCost, gadget.Value, gadget.ValueBase));
    }

    [Fact]
    public void A_foreign_receipts_lines_are_in_its_own_currency_and_in_base_at_its_rate()
    {
        PurchaseReceiveOrderDetailsReceiptDto receipt = ReceiptDto(Run(), 9802);

        Assert.Equal(("USD", 800m, 64_000m), (receipt.CurrencyCode, receipt.Value, receipt.ValueBase));
        Assert.Equal((100m, 500m, 40_000m), (receipt.Lines[0].UnitCost, receipt.Lines[0].Value, receipt.Lines[0].ValueBase));
    }

    [Fact]
    public void A_line_is_billed_for_what_the_bills_naming_it_came_to()
    {
        PurchaseReceiveOrderDetailsReportResultDto report = Run();

        Assert.Equal(3m, ReceiptDto(report, 9801).Lines[1].Billed);
        Assert.Equal(0m, ReceiptDto(report, 9801).Lines[0].Billed);
        Assert.Equal(4m, ReceiptDto(report, 9803).Lines[0].Billed);
    }

    [Fact]
    public void A_line_carries_the_serial_numbers_its_receipt_created_in_order_and_how_many()
    {
        PurchaseReceiveOrderDetailsLineDto ring = ReceiptDto(Run(), 9802).Lines[1];

        Assert.Equal((new[] { "S-1", "S-2" }, 2), (ring.Serials.ToArray(), ring.SerialCount));
        Assert.Empty(ReceiptDto(Run(), 9802).Lines[0].Serials);
    }

    [Fact]
    public void A_line_with_very_many_serials_gives_the_first_of_them_and_the_count_of_all()
    {
        List<ReceivedSerial> many = [.. Enumerable.Range(1, 60).Select(n => new ReceivedSerial { GoodsReceiptId = 9802, LineNumber = 2, SerialNumber = $"S-{n:000}" })];

        PurchaseReceiveOrderDetailsLineDto ring = ReceiptDto(Run(serials: many), 9802).Lines[1];

        Assert.Equal((PurchaseReceiveOrderDetailsReportRepository.MaxSerialsPerLine, 60, "S-001"), (ring.Serials.Count, ring.SerialCount, ring.Serials[0]));
    }

    [Fact]
    public void A_receipts_value_is_its_lines_and_the_lines_add_up_to_the_totals()
    {
        PurchaseReceiveOrderDetailsReportResultDto report = Run();

        Assert.Equal((6_700m, 6_700m), (ReceiptDto(report, 9801).Value, ReceiptDto(report, 9801).ValueBase));
        Assert.Equal(report.Totals.Value, report.Receipts.Sum(r => r.ValueBase));
        Assert.Equal((6, 74_166.67m), (report.Totals.Lines, report.Totals.Value));
    }

    [Fact]
    public void An_item_or_a_warehouse_shows_only_its_lines_and_says_so()
    {
        PurchaseReceiveOrderDetailsReportResultDto report = Run(Request(item: WidgetA));

        Assert.True(report.LinesFiltered);
        Assert.Equal([9802L, 9801], report.Receipts.Select(r => r.GoodsReceiptId));
        Assert.All(report.Receipts, r => Assert.All(r.Lines, l => Assert.Equal(WidgetA, l.ItemId)));
        Assert.Equal((3_200m, 40_000m), (ReceiptDto(report, 9801).ValueBase, ReceiptDto(report, 9802).ValueBase));
        Assert.False(Run().LinesFiltered);
    }

    [Fact]
    public void A_line_for_no_item_reads_as_its_own_description()
    {
        PurchaseReceiveOrderDetailsReportResultDto report = PurchaseReceiveOrderDetailsReportRepository.Compose(
            Request(),
            [Receipt(1, October1, Zenith, "INR", 1m)],
            1,
            [new() { GoodsReceiptDetailId = 11, GoodsReceiptId = 1, LineNumber = 1, Description = "Packing material", Quantity = 1m, AcceptedQuantity = 1m, TaxableAmount = 50m }],
            [], [], [], [], [], [], [], Vendors, new(), "INR");

        PurchaseReceiveOrderDetailsLineDto line = report.Receipts[0].Lines[0];
        Assert.Equal(("Packing material", null, 50m), (line.ItemName, line.ItemCode, line.Value));
    }

    [Fact]
    public void A_vendor_the_names_do_not_know_is_still_listed_by_its_number()
    {
        PurchaseReceiveOrderDetailsReportResultDto report = PurchaseReceiveOrderDetailsReportRepository.Compose(
            Request(), [Receipt(1, October1, 77, "INR", 1m)], 1, [], [], [], [], [], [], [], [], [], new(), "INR");

        Assert.Equal("Vendor 77", report.Receipts[0].ContactName);
    }

    [Fact]
    public void The_period_and_page_are_passed_on()
    {
        var request = Request();
        request.PageNumber = 3;
        request.PageSize = 10;

        PurchaseReceiveOrderDetailsReportResultDto report = Run(request);

        Assert.Equal((October1, October31, "INR"), (report.FromDate, report.ToDate, report.BaseCurrencyCode));
        Assert.Equal((3, 10, 4), (report.PageNumber, report.PageSize, report.TotalReceipts));
    }

    // ---- The request and the guards ---------------------------------------------------

    [Fact]
    public void The_two_dates_are_required_and_nothing_else_is()
    {
        Assert.Equal(
            [nameof(PurchaseReceiveOrderDetailsReportRequest.FromDate), nameof(PurchaseReceiveOrderDetailsReportRequest.ToDate)],
            typeof(PurchaseReceiveOrderDetailsReportRequest).GetProperties()
                .Where(p => p.GetCustomAttribute<RequiredAttribute>() is not null)
                .Select(p => p.Name));
    }

    [Fact]
    public void A_missing_date_is_refused_at_its_field_by_the_request_itself()
    {
        var request = new PurchaseReceiveOrderDetailsReportRequest();
        var results = new List<ValidationResult>();

        Assert.False(Validator.TryValidateObject(request, new ValidationContext(request), results, validateAllProperties: true));
        Assert.Equal(
            ["From date is required.", "To date is required."],
            results.Select(r => r.ErrorMessage).OrderBy(m => m, StringComparer.Ordinal));
    }

    [Fact]
    public void A_page_size_above_a_hundred_is_refused_by_the_request_itself()
    {
        var request = new PurchaseReceiveOrderDetailsReportRequest { FromDate = October1, ToDate = October31, PageSize = 101 };
        var results = new List<ValidationResult>();

        Assert.False(Validator.TryValidateObject(request, new ValidationContext(request), results, validateAllProperties: true));
        Assert.Equal(["Page size must be between 1 and 100."], results.Select(r => r.ErrorMessage));
    }

    [Fact]
    public void A_period_that_ends_before_it_begins_is_refused_at_its_field()
    {
        Assert.Equal(
            [(nameof(PurchaseReceiveOrderDetailsReportRequest.ToDate), "To date cannot be before the from date.")],
            PurchaseReceiveOrderDetailsReportRepository.Refusals(new PurchaseReceiveOrderDetailsReportRequest { FromDate = October31, ToDate = October1 }));

        Assert.Empty(PurchaseReceiveOrderDetailsReportRepository.Refusals(Request()));
    }

    [Fact]
    public async Task Another_branch_in_the_request_is_forbidden_not_hidden()
    {
        var controller = new PurchaseReceiveOrderDetailsReportController(null!)
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
            new PurchaseReceiveOrderDetailsReportRequest { FromDate = October1, ToDate = October31, OrgId = Guid.NewGuid() }, default);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public void The_endpoint_needs_the_reports_module_purchase_view_and_a_trade_on_each_action()
    {
        Type controller = typeof(PurchaseReceiveOrderDetailsReportController);

        Assert.Equal("api/statements/purchase-receive-order-details", controller.GetCustomAttribute<RouteAttribute>()!.Template);
        Assert.Equal("reports", controller.GetCustomAttribute<RequireModulePermissionAttribute>()!.Module);
        Assert.NotNull(controller.GetCustomAttribute<RequireBusinessTypeAttribute>());

        string[] actions =
        [
            nameof(PurchaseReceiveOrderDetailsReportController.Run),
            nameof(PurchaseReceiveOrderDetailsReportController.Vendors),
            nameof(PurchaseReceiveOrderDetailsReportController.Items),
            nameof(PurchaseReceiveOrderDetailsReportController.Warehouses),
            nameof(PurchaseReceiveOrderDetailsReportController.PurchaseOrders),
        ];

        foreach (string action in actions)
        {
            Assert.Contains(
                "purchase.view",
                controller.GetMethod(action)!.GetCustomAttributes<RequirePermissionAttribute>().Select(a => a.Permission));
        }

        Assert.Equal(
            ["vendors", "items", "warehouses", "purchase-orders"],
            actions.Skip(1).Select(a => controller.GetMethod(a)!.GetCustomAttribute<HttpGetAttribute>()!.Template));
    }

    // ---- The catalog source, held to the same period ----------------------------------

    [Fact]
    public void The_catalog_source_declares_both_dates_required_and_keeps_its_columns()
    {
        var source = new PurchaseReceiveOrderDetailsSourceRepository();

        Assert.Equal(["from", "to"], source.Parameters.Select(p => p.Name));
        Assert.All(source.Parameters, p => Assert.True(p.IsRequired));
        Assert.Equal(
            ["documentNo", "documentDate", "contactName", "itemCode", "quantity", "acceptedQuantity", "rejectedQuantity", "value", "goodsReceiptId", "goodsReceiptDetailId"],
            source.Columns.Select(c => c.Key));
    }
}
