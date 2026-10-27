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
/// The Inventory Item Summary (TK-243), on stock worked by hand. The branch's base currency is INR and the period is
/// October 2026. Five stocked items, in pieces:
/// <list type="bullet">
/// <item><b>Steel bar</b>: bought 100 for 5,000 and sold 20 for 1,000 in September, so it opens at 80 worth 4,000. In October
/// it buys 40 for 2,200 and sends 5 back for 275 (purchased 35, worth 1,925), sells 30 costing 1,575 and takes 2 back costing
/// 105 (sold 28, costing 1,470), writes 3 off costing 150 (adjusted -3), and moves 10 between warehouses for 525 each way (which
/// nets to nothing). It closes at 84 worth 4,305. Invoiced at 80 a piece: 30 on an invoice and 2 on a credit note, 2,240 net, so
/// a profit of 770.</item>
/// <item><b>Copper wire</b>: opens at 50 worth 1,000, buys 20 for 440 and sells 15 costing 315, for 450 on an invoice and 40 at the
/// till (490 net, a profit of 175), closing at 55 worth 1,125. A receipt on 5 November is after the period.</item>
/// <item><b>Brass fitting</b>: never moved.</item>
/// <item><b>Old stock</b>: an opening entry of 10 worth 1,000 in August and nothing since.</item>
/// <item><b>Cost pending</b>: buys 10 for 500, and sells 4 for 600 whose movement has not been costed: its cost of sales is
/// unknown, so it has no profit, and it closes at 6 worth 500, short by what the 4 cost.</item>
/// </list>
/// Opening values come to 6,000, purchases 2,865, sales 3,330 and cost of sales 1,785, adjustments -150, so the closing values
/// come to 6,930. A service item is not stocked and a draft invoice sells nothing. The builders run on lists through the same
/// expressions the repository runs against the read models; the composition is pure. Nothing here needs PostgreSQL.
/// </summary>
public sealed class InventoryItemSummaryReportTests
{
    private const long Steel = 9631, Copper = 9632, Brass = 9633, OldStock = 9634, Pending = 9635, Service = 9636;

    private static readonly DateOnly October1 = new(2026, 10, 1);
    private static readonly DateOnly October31 = new(2026, 10, 31);

    private static ItemRead Item(long id, string code, string name, long category, CostingType costing = CostingType.WeightedAverage, bool tracked = true, ItemType type = ItemType.Goods) =>
        new() { ItemId = id, ItemCode = code, ItemName = name, ItemCategoryId = category, CostingType = costing, TrackInventory = tracked, ItemType = type, InventoryUomId = 40 };

    private static readonly List<ItemRead> Items =
    [
        Item(Steel, "STK-A", "Steel bar", 9701),
        Item(Copper, "STK-B", "Copper wire", 9701),
        Item(Brass, "STK-C", "Brass fitting", 9702),
        Item(OldStock, "STK-D", "Old stock", 9701, CostingType.Fifo),
        Item(Pending, "STK-E", "Cost pending", 9701),
        Item(Service, "SVC-1", "Installation", 9702, CostingType.None, tracked: false, type: ItemType.Service),
    ];

    private static readonly List<ItemCategoryRead> Categories =
    [
        new() { ItemCategoryId = 9701, CategoryCode = "HW", CategoryName = "Hardware" },
        new() { ItemCategoryId = 9702, CategoryCode = "EL", CategoryName = "Electronics" },
    ];

    private static readonly List<UnitOfMeasureRead> Units = [new() { UomId = 40, UomCode = "PCS", UomName = "Pieces" }];

    private static StockMovementRead Move(
        long id, long item, StockMovementType type, StockDirection direction, DateOnly date, decimal qty, decimal? cost, CostingStatus status = CostingStatus.Costed) =>
        new()
        {
            StockMovementId = id, ItemId = item, MovementType = type, Direction = direction, MovementDate = date, Quantity = qty, TotalCost = cost, CostingStatus = status,
        };

    private static readonly List<StockMovementRead> Movements =
    [
        Move(1, Steel, StockMovementType.Receipt, StockDirection.In, new(2026, 9, 10), 100m, 5_000m),
        Move(2, Steel, StockMovementType.Issue, StockDirection.Out, new(2026, 9, 20), 20m, 1_000m),
        Move(3, Copper, StockMovementType.Receipt, StockDirection.In, new(2026, 9, 5), 50m, 1_000m),
        Move(4, OldStock, StockMovementType.Opening, StockDirection.In, new(2026, 8, 1), 10m, 1_000m),
        Move(5, Steel, StockMovementType.Receipt, StockDirection.In, new(2026, 10, 3), 40m, 2_200m),
        Move(6, Steel, StockMovementType.PurchaseReturn, StockDirection.Out, new(2026, 10, 4), 5m, 275m),
        Move(7, Steel, StockMovementType.Issue, StockDirection.Out, new(2026, 10, 10), 30m, 1_575m),
        Move(8, Steel, StockMovementType.SalesReturn, StockDirection.In, new(2026, 10, 12), 2m, 105m),
        Move(9, Steel, StockMovementType.Adjustment, StockDirection.Out, new(2026, 10, 15), 3m, 150m),
        Move(10, Steel, StockMovementType.TransferOut, StockDirection.Out, new(2026, 10, 20), 10m, 525m),
        Move(11, Steel, StockMovementType.TransferIn, StockDirection.In, new(2026, 10, 20), 10m, 525m),
        Move(12, Copper, StockMovementType.Receipt, StockDirection.In, new(2026, 10, 6), 20m, 440m),
        Move(13, Copper, StockMovementType.Issue, StockDirection.Out, new(2026, 10, 18), 15m, 315m),
        Move(14, Pending, StockMovementType.Receipt, StockDirection.In, new(2026, 10, 8), 10m, 500m),
        Move(15, Pending, StockMovementType.Issue, StockDirection.Out, new(2026, 10, 20), 4m, null, CostingStatus.Pending),
        Move(16, Copper, StockMovementType.Receipt, StockDirection.In, new(2026, 11, 5), 10m, 220m),
        // A movement of an item that is not stocked, which the report never reads.
        Move(17, Service, StockMovementType.Receipt, StockDirection.In, new(2026, 10, 5), 1m, 99m),
    ];

    private static InvoiceRead Invoice(long id, DateOnly date, string type = "INV", decimal rate = 1m, DocumentStatus status = DocumentStatus.Posted) =>
        new() { InvoiceId = id, TransactionTypeCode = type, DocumentNo = $"{type}-{id}", DocumentDate = date, ExchangeRate = rate, Status = status };

    private static InvoiceDetailRead InvoiceLine(long id, long invoice, long item, decimal taxable) =>
        new() { InvoiceDetailId = id, InvoiceId = invoice, ItemId = item, TaxableAmount = taxable };

    private static readonly List<InvoiceRead> Invoices =
    [
        Invoice(9981, new(2026, 10, 10)),
        Invoice(9982, new(2026, 10, 20)),
        Invoice(9983, new(2026, 10, 22), status: DocumentStatus.Draft),
        Invoice(9984, new(2026, 9, 25)),
        Invoice(9985, new(2026, 10, 28), "POS"),
    ];

    private static readonly List<InvoiceDetailRead> InvoiceDetails =
    [
        InvoiceLine(1, 9981, Steel, 2_400m),
        InvoiceLine(2, 9981, Copper, 450m),
        InvoiceLine(3, 9982, Pending, 600m),
        InvoiceLine(4, 9983, Steel, 9_999m),
        InvoiceLine(5, 9984, Steel, 800m),
        InvoiceLine(6, 9985, Service, 300m),
        InvoiceLine(7, 9985, Copper, 40m),
    ];

    private static readonly List<CreditNoteRead> CreditNotes =
    [
        new() { CreditNoteId = 9981, TransactionTypeCode = "CRN", DocumentNo = "CRN-9981", DocumentDate = new(2026, 10, 12), ExchangeRate = 1m, Status = DocumentStatus.Posted },
        new() { CreditNoteId = 9982, TransactionTypeCode = "CRN", DocumentNo = "CRN-9982", DocumentDate = new(2026, 10, 13), ExchangeRate = 1m, Status = DocumentStatus.Void },
    ];

    private static readonly List<CreditNoteDetailRead> CreditDetails =
    [
        new() { CreditNoteDetailId = 1, CreditNoteId = 9981, ItemId = Steel, TaxableAmount = 160m },
        new() { CreditNoteDetailId = 2, CreditNoteId = 9982, ItemId = Steel, TaxableAmount = 500m },
    ];

    private static InventoryItemSummaryReportRequest Request(
        long? category = null, long? item = null, bool showAll = false, DateOnly? from = null, DateOnly? to = null) =>
        new() { FromDate = from ?? October1, ToDate = to ?? October31, ItemCategoryId = category, ItemId = item, ShowItemsWithNoMovement = showAll };

    private static List<ItemFigure> FiguresFor(InventoryItemSummaryReportRequest request, IEnumerable<StockMovementRead>? movements = null)
    {
        IQueryable<ItemRead> items = InventoryItemSummaryReportRepository.Items(Items.AsQueryable(), request);
        return [.. InventoryItemSummaryReportRepository.ItemFigures(
            InventoryItemSummaryReportRepository.MovementFigures(items, (movements ?? Movements).AsQueryable(), request.FromDate!.Value, request.ToDate!.Value))];
    }

    private static ItemFigure FigureOf(IEnumerable<ItemFigure> figures, long item) => figures.Single(f => f.ItemId == item);

    private static List<ItemSale> SalesFor(InventoryItemSummaryReportRequest request)
    {
        IQueryable<ItemRead> items = InventoryItemSummaryReportRepository.Items(Items.AsQueryable(), request);
        return [.. InventoryItemSummaryReportRepository.ItemSales(
            items, Invoices.AsQueryable(), InvoiceDetails.AsQueryable(), CreditNotes.AsQueryable(), CreditDetails.AsQueryable(), request.FromDate!.Value, request.ToDate!.Value)];
    }

    private static List<InventoryItemSummaryEntry> RowsFor(InventoryItemSummaryReportRequest request)
    {
        IQueryable<ItemRead> items = InventoryItemSummaryReportRepository.Items(Items.AsQueryable(), request);
        return [.. InventoryItemSummaryReportRepository.Rows(
            items,
            Categories.AsQueryable(),
            Units.AsQueryable(),
            InventoryItemSummaryReportRepository.ItemFigures(InventoryItemSummaryReportRepository.MovementFigures(items, Movements.AsQueryable(), request.FromDate!.Value, request.ToDate!.Value)),
            InventoryItemSummaryReportRepository.ItemSales(items, Invoices.AsQueryable(), InvoiceDetails.AsQueryable(), CreditNotes.AsQueryable(), CreditDetails.AsQueryable(), request.FromDate!.Value, request.ToDate!.Value),
            request.ShowItemsWithNoMovement)];
    }

    private static InventoryItemSummaryEntry RowOf(IEnumerable<InventoryItemSummaryEntry> rows, long item) => rows.Single(r => r.ItemId == item);

    // ---- Which items --------------------------------------------------------------------------

    [Fact]
    public void Only_stocked_items_are_in_scope_and_the_category_and_item_narrow_them()
    {
        Assert.Equal([Steel, Copper, Brass, OldStock, Pending], InventoryItemSummaryReportRepository.Items(Items.AsQueryable(), Request()).Select(i => i.ItemId).Order());
        Assert.Equal([Brass], InventoryItemSummaryReportRepository.Items(Items.AsQueryable(), Request(category: 9702)).Select(i => i.ItemId));
        Assert.Equal([Copper], InventoryItemSummaryReportRepository.Items(Items.AsQueryable(), Request(item: Copper)).Select(i => i.ItemId));
    }

    [Fact]
    public void An_item_is_listed_when_it_had_stock_at_the_from_date_or_moved_in_the_period()
    {
        // Brass fitting never moved; Old stock only has what it opened with.
        Assert.Equal([Steel, Copper, OldStock, Pending], RowsFor(Request()).Select(r => r.ItemId).Order());
    }

    [Fact]
    public void Asking_for_items_with_no_movement_lists_every_stocked_item()
    {
        Assert.Equal([Steel, Copper, Brass, OldStock, Pending], RowsFor(Request(showAll: true)).Select(r => r.ItemId).Order());
    }

    // ---- Movements ----------------------------------------------------------------------------

    [Fact]
    public void Opening_is_every_movement_before_the_from_date_of_every_type()
    {
        List<ItemFigure> figures = FiguresFor(Request());

        Assert.Equal((80m, 4_000m), (FigureOf(figures, Steel).OpeningQuantity, FigureOf(figures, Steel).OpeningValue));
        Assert.Equal((50m, 1_000m), (FigureOf(figures, Copper).OpeningQuantity, FigureOf(figures, Copper).OpeningValue));
        Assert.Equal((10m, 1_000m), (FigureOf(figures, OldStock).OpeningQuantity, FigureOf(figures, OldStock).OpeningValue));
        Assert.Equal((0m, 0m), (FigureOf(figures, Brass).OpeningQuantity, FigureOf(figures, Brass).OpeningValue));
    }

    [Fact]
    public void Purchased_is_goods_received_less_goods_sent_back_to_the_vendor()
    {
        ItemFigure steel = FigureOf(FiguresFor(Request()), Steel);

        // 40 for 2,200 less 5 for 275.
        Assert.Equal((35m, 1_925m), (steel.PurchasedQuantity, steel.PurchasedValue));
    }

    [Fact]
    public void Sold_is_goods_issued_less_goods_the_customer_sent_back_and_its_cost_is_what_they_cost()
    {
        ItemFigure steel = FigureOf(FiguresFor(Request()), Steel);

        // 30 costing 1,575 less 2 costing 105.
        Assert.Equal((28m, 1_470m), (steel.SoldQuantity, steel.SoldCost));
    }

    [Fact]
    public void Adjusted_is_every_other_movement_signed_and_a_transfer_nets_to_nothing()
    {
        ItemFigure steel = FigureOf(FiguresFor(Request()), Steel);

        // A write-off of 3 costing 150; the transfer out and in of 10 for 525 cancel.
        Assert.Equal((-3m, -150m), (steel.AdjustedQuantity, steel.AdjustedValue));
    }

    [Fact]
    public void A_movement_after_the_to_date_is_not_in_any_column()
    {
        ItemFigure copper = FigureOf(FiguresFor(Request()), Copper);

        // The receipt of 10 on 5 November is left out: 20 bought, 15 sold.
        Assert.Equal((20m, 440m, 15m, 315m), (copper.PurchasedQuantity, copper.PurchasedValue, copper.SoldQuantity, copper.SoldCost));
    }

    [Fact]
    public void A_movement_that_has_not_been_costed_is_counted_and_adds_no_value()
    {
        ItemFigure pending = FigureOf(FiguresFor(Request()), Pending);

        // The 4 issued are in the quantities, but cost nothing yet and are counted.
        Assert.Equal((10m, 500m, 4m, 0m), (pending.PurchasedQuantity, pending.PurchasedValue, pending.SoldQuantity, pending.SoldCost));
        Assert.Equal((1, 1), (pending.Uncosted, pending.SoldUncosted));
    }

    [Fact]
    public void A_movement_that_was_skipped_is_not_pending()
    {
        StockMovementRead[] moves = [Move(1, Steel, StockMovementType.Issue, StockDirection.Out, new(2026, 10, 5), 2m, null, CostingStatus.Skipped)];

        ItemFigure steel = FigureOf(FiguresFor(Request(), moves), Steel);

        Assert.Equal((2m, 0m, 0), (steel.SoldQuantity, steel.SoldCost, steel.Uncosted));
    }

    [Fact]
    public void Opening_plus_purchased_less_sold_plus_adjusted_is_the_closing_quantity_and_value_of_every_item()
    {
        foreach (InventoryItemSummaryEntry row in RowsFor(Request(showAll: true)))
        {
            ItemFigure f = FigureOf(FiguresFor(Request()), row.ItemId);

            Assert.Equal(f.OpeningQuantity + f.PurchasedQuantity - f.SoldQuantity + f.AdjustedQuantity, row.ClosingQuantity);
            Assert.Equal(f.OpeningValue + f.PurchasedValue - f.SoldCost + f.AdjustedValue, row.ClosingValue);
        }

        // And as worked by hand: every costed movement up to the To date, signed.
        Assert.Equal((84m, 4_305m), (RowOf(RowsFor(Request()), Steel).ClosingQuantity, RowOf(RowsFor(Request()), Steel).ClosingValue));
        Assert.Equal((55m, 1_125m), (RowOf(RowsFor(Request()), Copper).ClosingQuantity, RowOf(RowsFor(Request()), Copper).ClosingValue));
        Assert.Equal((10m, 1_000m), (RowOf(RowsFor(Request()), OldStock).ClosingQuantity, RowOf(RowsFor(Request()), OldStock).ClosingValue));
        Assert.Equal((6m, 500m), (RowOf(RowsFor(Request()), Pending).ClosingQuantity, RowOf(RowsFor(Request()), Pending).ClosingValue));
    }

    // ---- Sales --------------------------------------------------------------------------------

    [Fact]
    public void Sales_are_the_posted_invoices_net_less_the_posted_credit_notes_by_item()
    {
        Dictionary<long, decimal> sales = SalesFor(Request()).ToDictionary(s => s.ItemId, s => s.Net);

        // Steel: 2,400 less the credit note's 160; the draft, the September invoice and the voided credit note are not in it.
        Assert.Equal((2_240m, 490m, 600m, 0m), (sales[Steel], sales[Copper], sales[Pending], sales[OldStock]));
    }

    [Fact]
    public void A_pos_sale_counts_and_a_line_for_an_item_not_stocked_does_not()
    {
        Dictionary<long, decimal> sales = SalesFor(Request()).ToDictionary(s => s.ItemId, s => s.Net);

        // Copper's 450 on the invoice and 40 at the till; the service item is not in scope.
        Assert.Equal(490m, sales[Copper]);
        Assert.DoesNotContain(Service, sales.Keys);
    }

    [Fact]
    public void Every_stocked_item_has_a_sales_row_even_with_none()
    {
        Assert.Equal([Steel, Copper, Brass, OldStock, Pending], SalesFor(Request()).Select(s => s.ItemId).Order());
    }

    [Fact]
    public void A_sale_in_another_currency_is_taken_to_base_at_its_documents_rate()
    {
        List<InvoiceRead> invoices = [Invoice(1, new(2026, 10, 5), rate: 80m)];
        List<InvoiceDetailRead> lines = [InvoiceLine(1, 1, Copper, 100m)];
        IQueryable<ItemRead> items = Items.AsQueryable();

        ItemSale copper = InventoryItemSummaryReportRepository
            .ItemSales(items, invoices.AsQueryable(), lines.AsQueryable(), Array.Empty<CreditNoteRead>().AsQueryable(), Array.Empty<CreditNoteDetailRead>().AsQueryable(), October1, October31)
            .Single(s => s.ItemId == Copper);

        Assert.Equal(8_000m, copper.Net);
    }

    // ---- The rows -----------------------------------------------------------------------------

    [Fact]
    public void A_row_names_its_item_category_costing_method_and_unit()
    {
        InventoryItemSummaryEntry steel = RowOf(RowsFor(Request()), Steel);

        Assert.Equal(("STK-A", "Steel bar", "Hardware", CostingType.WeightedAverage, "PCS"), (steel.ItemCode, steel.ItemName, steel.CategoryName, steel.CostingType, steel.UnitCode));
        Assert.Equal(CostingType.Fifo, RowOf(RowsFor(Request()), OldStock).CostingType);
    }

    // ---- Compose ------------------------------------------------------------------------------

    private static InventoryItemSummaryReportResultDto Run(InventoryItemSummaryReportRequest? request = null)
    {
        request ??= Request();
        List<InventoryItemSummaryEntry> rows = [.. RowsFor(request).OrderBy(r => r.ItemName).ThenBy(r => r.ItemId)];
        var totals = new InventoryItemSummaryEntry
        {
            OpeningValue = rows.Sum(r => r.OpeningValue),
            PurchasedValue = rows.Sum(r => r.PurchasedValue),
            Sales = rows.Sum(r => r.Sales),
            SoldCost = rows.Sum(r => r.SoldCost),
            AdjustedValue = rows.Sum(r => r.AdjustedValue),
            ClosingValue = rows.Sum(r => r.ClosingValue),
            Uncosted = rows.Sum(r => r.Uncosted),
            SoldUncosted = rows.Sum(r => r.SoldUncosted),
        };

        return InventoryItemSummaryReportRepository.Compose(request, rows, rows.Count, totals, "INR");
    }

    private static InventoryItemSummaryItemDto ItemDto(InventoryItemSummaryReportResultDto report, long item) => report.Items.Single(i => i.ItemId == item);

    [Fact]
    public void The_items_are_by_name_with_their_stock_as_worked_by_hand()
    {
        InventoryItemSummaryReportResultDto report = Run();

        Assert.Equal([Copper, Pending, OldStock, Steel], report.Items.Select(i => i.ItemId));
        InventoryItemSummaryItemDto steel = ItemDto(report, Steel);
        Assert.Equal((80m, 4_000m, 35m, 1_925m, 28m, 2_240m), (steel.OpeningQuantity, steel.OpeningValue, steel.PurchasedQuantity, steel.PurchasedValue, steel.SoldQuantity, steel.SalesValue));
        Assert.Equal((1_470m, -3m, -150m, 84m, 4_305m), (steel.Cogs, steel.AdjustedQuantity, steel.AdjustedValue, steel.ClosingQuantity, steel.ClosingValue));
    }

    [Fact]
    public void Profit_is_sales_less_cost_of_sales()
    {
        InventoryItemSummaryReportResultDto report = Run();

        // Steel: 2,240 - 1,470. Copper: 490 - 315, the till sale included.
        Assert.Equal((770m, 175m), (ItemDto(report, Steel).Profit, ItemDto(report, Copper).Profit));
    }

    [Fact]
    public void An_item_whose_sales_have_a_movement_not_yet_costed_has_no_profit_and_says_cost_pending()
    {
        InventoryItemSummaryItemDto pending = ItemDto(Run(), Pending);

        Assert.Null(pending.Profit);
        Assert.True(pending.CostPending);
        Assert.False(ItemDto(Run(), Steel).CostPending);
    }

    [Fact]
    public void The_totals_add_up_the_values_as_worked_by_hand_and_have_no_profit_while_a_cost_is_pending()
    {
        InventoryItemSummaryTotalsDto totals = Run().Totals;

        Assert.Equal((6_000m, 2_865m, 3_330m, 1_785m, -150m, 6_930m), (totals.OpeningValue, totals.PurchasedValue, totals.SalesValue, totals.Cogs, totals.AdjustedValue, totals.ClosingValue));
        Assert.Null(totals.Profit);
        Assert.True(totals.CostPending);
    }

    [Fact]
    public void With_every_movement_costed_the_total_profit_is_sales_less_cost_of_sales()
    {
        InventoryItemSummaryReportResultDto report = InventoryItemSummaryReportRepository.Compose(
            Request(), [], 0, new InventoryItemSummaryEntry { Sales = 3_000m, SoldCost = 1_800m }, "INR");

        Assert.Equal((1_200m, false), (report.Totals.Profit, report.Totals.CostPending));
    }

    [Fact]
    public void The_period_and_page_are_passed_on()
    {
        var request = Request();
        request.PageNumber = 3;
        request.PageSize = 10;

        InventoryItemSummaryReportResultDto report = Run(request);

        Assert.Equal((October1, October31, "INR"), (report.FromDate, report.ToDate, report.BaseCurrencyCode));
        Assert.Equal((3, 10, 4), (report.PageNumber, report.PageSize, report.TotalItems));
    }

    // ---- The request and the guards ---------------------------------------------------

    [Fact]
    public void The_two_dates_are_required_and_nothing_else_is()
    {
        Assert.Equal(
            [nameof(InventoryItemSummaryReportRequest.FromDate), nameof(InventoryItemSummaryReportRequest.ToDate)],
            typeof(InventoryItemSummaryReportRequest).GetProperties()
                .Where(p => p.GetCustomAttribute<RequiredAttribute>() is not null)
                .Select(p => p.Name));
    }

    [Fact]
    public void A_missing_date_is_refused_at_its_field_by_the_request_itself()
    {
        var request = new InventoryItemSummaryReportRequest();
        var results = new List<ValidationResult>();

        Assert.False(Validator.TryValidateObject(request, new ValidationContext(request), results, validateAllProperties: true));
        Assert.Equal(
            ["From date is required.", "To date is required."],
            results.Select(r => r.ErrorMessage).OrderBy(m => m, StringComparer.Ordinal));
    }

    [Fact]
    public void A_page_size_above_a_hundred_is_refused_by_the_request_itself()
    {
        var request = new InventoryItemSummaryReportRequest { FromDate = October1, ToDate = October31, PageSize = 101 };
        var results = new List<ValidationResult>();

        Assert.False(Validator.TryValidateObject(request, new ValidationContext(request), results, validateAllProperties: true));
        Assert.Equal(["Page size must be between 1 and 100."], results.Select(r => r.ErrorMessage));
    }

    [Fact]
    public void A_period_that_ends_before_it_begins_is_refused_at_its_field()
    {
        Assert.Equal(
            [(nameof(InventoryItemSummaryReportRequest.ToDate), "To date cannot be before the from date.")],
            InventoryItemSummaryReportRepository.Refusals(new InventoryItemSummaryReportRequest { FromDate = October31, ToDate = October1 }));

        Assert.Empty(InventoryItemSummaryReportRepository.Refusals(Request()));
    }

    [Fact]
    public async Task Another_branch_in_the_request_is_forbidden_not_hidden()
    {
        var controller = new InventoryItemSummaryReportController(null!)
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
            new InventoryItemSummaryReportRequest { FromDate = October1, ToDate = October31, OrgId = Guid.NewGuid() }, default);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public void The_endpoint_needs_the_reports_module_inventory_view_and_a_trade_on_each_action()
    {
        Type controller = typeof(InventoryItemSummaryReportController);

        Assert.Equal("api/statements/inventory-item-summary", controller.GetCustomAttribute<RouteAttribute>()!.Template);
        Assert.Equal("reports", controller.GetCustomAttribute<RequireModulePermissionAttribute>()!.Module);
        Assert.NotNull(controller.GetCustomAttribute<RequireBusinessTypeAttribute>());

        string[] actions =
        [
            nameof(InventoryItemSummaryReportController.Run),
            nameof(InventoryItemSummaryReportController.Items),
            nameof(InventoryItemSummaryReportController.ItemCategories),
        ];

        foreach (string action in actions)
        {
            Assert.Contains(
                "inventory.view",
                controller.GetMethod(action)!.GetCustomAttributes<RequirePermissionAttribute>().Select(a => a.Permission));
        }

        Assert.Equal(
            ["items", "item-categories"],
            actions.Skip(1).Select(a => controller.GetMethod(a)!.GetCustomAttribute<HttpGetAttribute>()!.Template));
    }

    // ---- The catalog source, held to the same period ----------------------------------

    [Fact]
    public void The_catalog_source_declares_both_dates_required_and_keeps_its_columns()
    {
        var source = new ItemSummarySourceRepository();

        Assert.Equal(["from", "to"], source.Parameters.Select(p => p.Name));
        Assert.All(source.Parameters, p => Assert.True(p.IsRequired));
        Assert.Equal(
            [
                "itemCode", "itemName", "itemGroup", "productCategory", "inventoryType", "costingMethod", "unitOfMeasurement", "openingQuantity", "openingBalance",
                "quantityPurchased", "purchases", "quantitySold", "sales", "quantityAdjusted", "adjustments", "cogs", "profit", "closingQuantity", "closingBalance",
                "inventoryAccount", "purchaseAccount", "salesAccount", "itemId",
            ],
            source.Columns.Select(c => c.Key));
    }
}
