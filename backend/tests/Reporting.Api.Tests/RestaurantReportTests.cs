using Reporting.Repository.ReadModels;
using Reporting.Repository.Repositories;
using Shared.Kernel.Apps;
using Shared.Kernel.Documents;
using Shared.Kernel.Internal;
using Shared.Kernel.Restaurant;
using Xunit;

namespace Reporting.Api.Tests;

/// <summary>
/// The restaurant reports (TK-150), over lists.
///
/// <b>What this proves and what it does not.</b> It proves the arithmetic: that
/// the day-end's total is the day's posted invoices, that item and category
/// sales add up to the day-end's item sales, how waiters, tables, food cost,
/// wastage and cancellations are counted, and which branches see which report.
/// It does not prove the queries translate, since LINQ to Objects will run
/// anything, so the eight reports still need running against real schemas.
/// </summary>
public sealed class RestaurantReportTests
{
    private static readonly DateOnly Day1 = new(2026, 10, 1);
    private static readonly DateOnly Day2 = new(2026, 10, 2);

    private static readonly Guid Asha = Guid.NewGuid();
    private static readonly Guid Ravi = Guid.NewGuid();
    private static readonly Guid Manager = Guid.NewGuid();

    private const long Paneer = 10;
    private const long Lassi = 11;
    private const long Mains = 100;
    private const long Drinks = 101;

    // ---- The day's books -------------------------------------------------------
    //
    // Day 1: invoice 1 is a restaurant bill (order 1, table T1, Asha) with a
    // 100 service charge and a 20 tip, paid 1,000 cash and 155 UPI; invoice 2
    // is a counter sale paid by card. Invoice 3 is a draft and invoice 4 was
    // voided: neither is sales. Day 2: invoice 5 bills order 2 (table T1, Ravi).

    private static readonly List<InvoiceRead> Invoices =
    [
        Invoice(1, Day1, 1_155m),
        Invoice(2, Day1, 210m),
        Invoice(3, Day1, 999m, DocumentStatus.Draft),
        Invoice(4, Day1, 888m, DocumentStatus.Void),
        Invoice(5, Day2, 525m),
    ];

    private static readonly List<InvoiceDetailRead> Details =
    [
        Line(1, 1, Paneer, quantity: 2, taxable: 1_000m, tax: 50m),
        Line(2, 1, null, quantity: 1, taxable: 100m, tax: 5m), // the service charge
        Line(3, 2, Lassi, quantity: 2, taxable: 200m, tax: 10m),
        Line(4, 3, Paneer, quantity: 9, taxable: 999m, tax: 0m),
        Line(5, 4, Paneer, quantity: 8, taxable: 888m, tax: 0m),
        Line(6, 5, Paneer, quantity: 1, taxable: 500m, tax: 25m),
    ];

    private static readonly List<RestaurantBillRead> Bills =
    [
        new() { RestaurantBillId = 1, InvoiceId = 1, ServiceChargeAmount = 100m, TipAmount = 20m },
        new() { RestaurantBillId = 2, InvoiceId = 5 },
    ];

    private static readonly List<InvoiceTenderRead> Tenders =
    [
        new() { InvoiceTenderId = 1, InvoiceId = 1, Mode = "Cash", Amount = 1_000m },
        new() { InvoiceTenderId = 2, InvoiceId = 1, Mode = "Upi", Amount = 155m },
        new() { InvoiceTenderId = 3, InvoiceId = 2, Mode = "Card", Amount = 210m },
        new() { InvoiceTenderId = 4, InvoiceId = 3, Mode = "Cash", Amount = 999m },
    ];

    private static readonly List<FloorRead> Floors = [new() { FloorId = 1, Name = "Ground" }];

    private static readonly List<RestaurantTableRead> Tables =
    [
        new() { RestaurantTableId = 1, FloorId = 1, Code = "T1", Seats = 4, IsActive = true },
        new() { RestaurantTableId = 2, FloorId = 1, Code = "T2", Seats = 2, IsActive = true },
        new() { RestaurantTableId = 3, FloorId = 1, Code = "T3", Seats = 6, IsActive = false },
    ];

    private static readonly List<RestaurantOrderRead> Orders =
    [
        new() { RestaurantOrderId = 1, OrderNo = "ORD-1", OrderDate = Day1, OrderType = "DineIn", RestaurantTableId = 1, WaiterUserId = Asha, Guests = 4, Status = "Billed" },
        new() { RestaurantOrderId = 2, OrderNo = "ORD-2", OrderDate = Day2, OrderType = "DineIn", RestaurantTableId = 1, WaiterUserId = Ravi, Guests = 2, Status = "Billed" },
        new() { RestaurantOrderId = 3, OrderNo = "ORD-3", OrderDate = Day2, OrderType = "Takeaway", Status = "Open" },
    ];

    private static readonly List<RestaurantOrderLineRead> OrderLines =
    [
        new() { RestaurantOrderLineId = 1, RestaurantOrderId = 1, ItemId = Paneer, ItemName = "Paneer tikka", Station = "Kitchen", Quantity = 2, UnitPrice = 500m, KitchenStatus = "Served", RestaurantBillId = 1 },
        new() { RestaurantOrderLineId = 2, RestaurantOrderId = 1, ItemId = Lassi, ItemName = "Lassi", VariantName = "Large", Station = "Bar", Quantity = 1, UnitPrice = 300m, KitchenStatus = "Cancelled", CancelReason = "Guest changed mind" },
        new() { RestaurantOrderLineId = 3, RestaurantOrderId = 2, ItemId = Paneer, ItemName = "Paneer tikka", Station = "Kitchen", Quantity = 1, UnitPrice = 500m, KitchenStatus = "Served", RestaurantBillId = 2 },
        new() { RestaurantOrderLineId = 4, RestaurantOrderId = 3, ItemId = Paneer, ItemName = "Paneer tikka", Station = "Kitchen", Quantity = 1, UnitPrice = 500m, KitchenStatus = "Sent" },
    ];

    private static readonly List<ItemRead> Items =
    [
        new() { ItemId = Paneer, ItemCode = "PT", ItemName = "Paneer tikka", ItemCategoryId = Mains },
        new() { ItemId = Lassi, ItemCode = "LS", ItemName = "Lassi", ItemCategoryId = Drinks },
    ];

    private static readonly List<ItemCategoryRead> Categories =
    [
        new() { ItemCategoryId = Mains, CategoryCode = "MAIN", CategoryName = "Mains" },
        new() { ItemCategoryId = Drinks, CategoryCode = "DRNK", CategoryName = "Drinks" },
    ];

    // ---- Day-end ---------------------------------------------------------------

    [Fact]
    public void The_day_end_total_equals_the_days_posted_invoices()
    {
        List<RestaurantDayEndRow> rows = DayEnd(null, null);

        decimal posted = Invoices.Where(i => i.Status == DocumentStatus.Posted).Sum(i => i.TotalAmountBase);

        Assert.Equal(posted, rows.Sum(r => r.Total));
        Assert.All(rows, row => Assert.Equal(
            Invoices.Where(i => i.Status == DocumentStatus.Posted && i.DocumentDate == row.Date).Sum(i => i.TotalAmountBase),
            row.Total));
    }

    [Fact]
    public void The_day_end_splits_a_day_into_items_service_charge_tax_and_tenders()
    {
        RestaurantDayEndRow day = DayEnd(null, null).Single(r => r.Date == Day1);

        // Drafts and voided invoices are not sales: two bills, not four.
        Assert.Equal(2, day.Bills);
        Assert.Equal(4m, day.Covers);
        Assert.Equal(1_200m, day.ItemSales);
        Assert.Equal(100m, day.ServiceCharge);
        Assert.Equal(0m, day.OtherSales);
        Assert.Equal(65m, day.Tax);
        Assert.Equal(0m, day.RoundOff);
        Assert.Equal(1_365m, day.Total);
        Assert.Equal(1_000m, day.Cash);
        Assert.Equal(210m, day.Card);
        Assert.Equal(155m, day.Upi);
        Assert.Equal(0m, day.OnAccount);
        Assert.Equal(20m, day.Tips);
        Assert.Equal(682.50m, day.AverageBill);
        Assert.Equal(341.25m, day.AveragePerCover);
    }

    [Fact]
    public void The_day_end_keeps_to_its_period()
    {
        RestaurantDayEndRow only = Assert.Single(DayEnd(Day2, Day2));

        Assert.Equal(Day2, only.Date);
        Assert.Equal(525m, only.Total);
    }

    // ---- Item and category sales -----------------------------------------------

    [Fact]
    public void Item_sales_add_up_to_the_day_ends_item_sales()
    {
        List<RestaurantItemSalesRow> items = [.. RestaurantReports.ItemSales(
            Invoices.AsQueryable(), Details.AsQueryable(), Items.AsQueryable(), Categories.AsQueryable(), null, null)];

        RestaurantItemSalesRow paneer = items.Single(r => r.ItemId == Paneer);
        Assert.Equal(3m, paneer.Quantity);
        Assert.Equal(1_500m, paneer.NetSales);
        Assert.Equal(75m, paneer.Tax);
        Assert.Equal(1_575m, paneer.Total);
        Assert.Equal(500m, paneer.AveragePrice);
        Assert.Equal("Mains", paneer.Category);

        Assert.Equal(DayEnd(null, null).Sum(r => r.ItemSales), items.Sum(r => r.NetSales));
    }

    [Fact]
    public void Category_sales_are_the_item_sales_per_category()
    {
        List<RestaurantItemSalesRow> items = [.. RestaurantReports.ItemSales(
            Invoices.AsQueryable(), Details.AsQueryable(), Items.AsQueryable(), Categories.AsQueryable(), null, null)];
        List<RestaurantCategorySalesRow> categories = [.. RestaurantReports.CategorySales(
            Invoices.AsQueryable(), Details.AsQueryable(), Items.AsQueryable(), Categories.AsQueryable(), null, null)];

        Assert.Equal(2, categories.Count);
        Assert.All(categories, c => Assert.Equal(
            items.Where(i => i.Category == c.Category).Sum(i => i.NetSales),
            c.NetSales));
        Assert.Equal(200m, categories.Single(c => c.Category == "Drinks").NetSales);
    }

    // ---- Waiters and tables ----------------------------------------------------

    [Fact]
    public void Waiter_sales_count_billed_dishes_at_menu_price_and_their_cancellations()
    {
        List<RestaurantWaiterSalesRow> rows = [.. RestaurantReports.WaiterSales(
            Invoices.AsQueryable(), Bills.AsQueryable(), Orders.AsQueryable(), OrderLines.AsQueryable(), null, null)];

        RestaurantWaiterSalesRow asha = rows.Single(r => r.WaiterUserId == Asha);
        Assert.Equal(1, asha.Orders);
        Assert.Equal(4m, asha.Covers);
        Assert.Equal(2m, asha.Dishes);
        Assert.Equal(1_000m, asha.Sales);
        Assert.Equal(1m, asha.CancelledDishes);
        Assert.Equal(250m, asha.AveragePerCover);

        RestaurantWaiterSalesRow ravi = rows.Single(r => r.WaiterUserId == Ravi);
        Assert.Equal(500m, ravi.Sales);

        // The takeaway with no waiter billed nothing and cancelled nothing.
        Assert.DoesNotContain(rows, r => r.WaiterUserId == null);
    }

    [Fact]
    public void Table_turnover_counts_billed_orders_and_shows_idle_active_tables()
    {
        List<RestaurantTableTurnoverRow> rows = [.. RestaurantReports.TableTurnover(
            Floors.AsQueryable(), Tables.AsQueryable(), Orders.AsQueryable(), OrderLines.AsQueryable(), null, null)];

        RestaurantTableTurnoverRow t1 = rows.Single(r => r.Table == "T1");
        Assert.Equal(2, t1.Turns);
        Assert.Equal(6m, t1.Covers);
        Assert.Equal(3m, t1.AverageCovers);
        Assert.Equal(75m, t1.SeatUsePercent);
        Assert.Equal(1_500m, t1.Sales);
        Assert.Equal(750m, t1.SalesPerTurn);

        RestaurantTableTurnoverRow t2 = rows.Single(r => r.Table == "T2");
        Assert.Equal(0, t2.Turns);
        Assert.Null(t2.SeatUsePercent);

        Assert.DoesNotContain(rows, r => r.Table == "T3");
    }

    // ---- Food cost and wastage -------------------------------------------------

    private static readonly List<WastageEntryRead> WastageEntries =
    [
        new() { WastageEntryId = 1, EntryDate = Day2, ItemId = Paneer, Quantity = 0.5m, UomId = 1, Reason = "Spoiled", StockAdjustmentId = 70 },
        new() { WastageEntryId = 2, EntryDate = Day2, ItemId = Lassi, Quantity = 1m, UomId = 1, Reason = "Dropped", StockAdjustmentId = 71 },
    ];

    private static readonly List<StockMovementRead> Movements =
    [
        Movement(1, Paneer, Day1, "RCN", 1, 300m),
        Movement(2, Lassi, Day1, "RCN", 1, null), // waiting on the costing engine
        Movement(3, Paneer, Day2, "STA", 70, 50m), // wastage entry 1
        Movement(4, Paneer, Day2, "STA", 99, 999m), // a stock count, not wastage
        Movement(5, Paneer, Day2, "INV", 5, 400m), // a sale's own issue, not a recipe's
    ];

    [Fact]
    public void Food_cost_is_ingredients_and_wastage_against_item_sales()
    {
        List<RestaurantFoodCostRow> rows = [.. RestaurantReports.FoodCost(
            Invoices.AsQueryable(), Details.AsQueryable(), Movements.AsQueryable(), WastageEntries.AsQueryable(), null, null)];

        RestaurantFoodCostRow day1 = rows.Single(r => r.Date == Day1);
        Assert.Equal(1_200m, day1.FoodSales);
        Assert.Equal(300m, day1.IngredientCost);
        Assert.Equal(0m, day1.WastageCost);
        Assert.Equal(25m, day1.FoodCostPercent);
        Assert.Equal(1, day1.Uncosted);

        RestaurantFoodCostRow day2 = rows.Single(r => r.Date == Day2);
        Assert.Equal(500m, day2.FoodSales);
        Assert.Equal(0m, day2.IngredientCost);
        Assert.Equal(50m, day2.WastageCost);
        Assert.Equal(450m, day2.GrossMargin);
        Assert.Equal(10m, day2.FoodCostPercent);
    }

    [Fact]
    public void Wastage_is_valued_at_its_adjustments_cost_and_says_when_it_is_not_costed()
    {
        List<UnitOfMeasureRead> units = [new() { UomId = 1, UomCode = "KG", UomName = "Kilogram", UqcCode = "KGS" }];

        List<RestaurantWastageRow> rows = [.. RestaurantReports.Wastage(
            WastageEntries.AsQueryable(), Items.AsQueryable(), Categories.AsQueryable(), units.AsQueryable(), Movements.AsQueryable(), null, null)];

        RestaurantWastageRow spoiled = rows.Single(r => r.WastageEntryId == 1);
        Assert.Equal(50m, spoiled.Cost);
        Assert.True(spoiled.IsCosted);
        Assert.Equal("KG", spoiled.Unit);

        // Its adjustment is waiting for approval: no movement yet.
        RestaurantWastageRow dropped = rows.Single(r => r.WastageEntryId == 2);
        Assert.Equal(0m, dropped.Cost);
        Assert.False(dropped.IsCosted);
    }

    // ---- KOT cancellations -----------------------------------------------------

    [Fact]
    public void Kot_cancellations_list_dishes_taken_back_after_the_ticket_went()
    {
        List<KotRead> kots = [new() { KotId = 1, RestaurantOrderId = 1, KotNo = "KOT-1", Station = "Bar" }];
        List<KotLineRead> kotLines =
        [
            new() { KotLineId = 1, KotId = 1, RestaurantOrderLineId = 2, Quantity = 1, Status = "Cancelled", CancelReason = "Guest changed mind", ModifiedBy = Manager },
            new() { KotLineId = 2, KotId = 1, RestaurantOrderLineId = 1, Quantity = 2, Status = "Served" },
        ];

        RestaurantKotCancellationRow row = Assert.Single(RestaurantReports.KotCancellations(
            kots.AsQueryable(), kotLines.AsQueryable(), Orders.AsQueryable(), OrderLines.AsQueryable(), Tables.AsQueryable(), null, null));

        Assert.Equal("KOT-1", row.KotNo);
        Assert.Equal("T1", row.Table);
        Assert.Equal("Lassi (Large)", row.Dish);
        Assert.Equal(300m, row.Value);
        Assert.Equal("Guest changed mind", row.Reason);
        Assert.Equal(Asha, row.WaiterUserId);
        Assert.Equal(Manager, row.CancelledByUserId);
    }

    // ---- Who sees which report -------------------------------------------------

    [Fact]
    public void A_restaurant_report_is_for_a_restaurant_branch_only()
    {
        RestaurantDayEndSource dayEnd = new();

        Assert.True(new ReportAudience(AddOn.None, BusinessType.Restaurant, Settings()).Allows(dayEnd));
        Assert.False(new ReportAudience(AddOn.All, BusinessType.General, null).Allows(dayEnd));
        Assert.False(new ReportAudience(AddOn.All, null, null).Allows(dayEnd));

        // Every other report is still there for a Restaurant branch.
        Assert.True(new ReportAudience(AddOn.None, BusinessType.Restaurant, Settings()).Allows(new ProjectProfitabilitySource()));
    }

    [Fact]
    public void A_restaurant_report_is_hidden_while_its_switch_is_off()
    {
        RestaurantTableTurnoverSource turnover = new();
        RestaurantFoodCostSource foodCost = new();
        RestaurantDayEndSource dayEnd = new();

        ReportAudience noTables = new(AddOn.None, BusinessType.Restaurant, Settings(tables: false));
        Assert.False(noTables.Allows(turnover));
        Assert.True(noTables.Allows(foodCost));
        Assert.True(noTables.Allows(dayEnd));

        Assert.False(new ReportAudience(AddOn.None, BusinessType.Restaurant, Settings(recipes: false)).Allows(foodCost));
        Assert.False(new ReportAudience(AddOn.None, BusinessType.Restaurant, Settings(kitchen: false)).Allows(new RestaurantKotCancellationsSource(null!)));

        // Settings Master could not supply hide every switched report, and only those.
        ReportAudience unread = new(AddOn.None, BusinessType.Restaurant, null);
        Assert.False(unread.Allows(turnover));
        Assert.True(unread.Allows(dayEnd));

        // A scheduled run was authorised when it was saved.
        Assert.True(ReportAudience.Unrestricted().Allows(turnover));
    }

    // ---- Helpers ---------------------------------------------------------------

    private static List<RestaurantDayEndRow> DayEnd(DateOnly? from, DateOnly? to) =>
        [.. RestaurantReports.DayEnd(
            Invoices.AsQueryable(), Details.AsQueryable(), Bills.AsQueryable(), Tenders.AsQueryable(), Orders.AsQueryable(), from, to)];

    private static RestaurantSettingsView Settings(bool tables = true, bool kitchen = true, bool recipes = true) =>
        new() { TablesEnabled = tables, KitchenEnabled = kitchen, RecipesEnabled = recipes };

    private static InvoiceRead Invoice(long id, DateOnly date, decimal total, DocumentStatus status = DocumentStatus.Posted) =>
        new()
        {
            InvoiceId = id,
            TransactionTypeCode = "INV",
            DocumentNo = $"INV-{id}",
            DocumentDate = date,
            ContactId = 1,
            CurrencyCode = "INR",
            ExchangeRate = 1m,
            TotalAmount = total,
            TotalAmountBase = total,
            Status = status,
        };

    private static InvoiceDetailRead Line(long id, long invoiceId, long? itemId, decimal quantity, decimal taxable, decimal tax) =>
        new()
        {
            InvoiceDetailId = id,
            InvoiceId = invoiceId,
            ItemId = itemId,
            Quantity = quantity,
            TaxableAmount = taxable,
            TaxAmount = tax,
            LineTotal = taxable + tax,
        };

    private static StockMovementRead Movement(long id, long itemId, DateOnly date, string source, long sourceId, decimal? cost) =>
        new()
        {
            StockMovementId = id,
            ItemId = itemId,
            MovementDate = date,
            SourceType = source,
            SourceId = sourceId,
            Quantity = 1m,
            TotalCost = cost,
        };
}
