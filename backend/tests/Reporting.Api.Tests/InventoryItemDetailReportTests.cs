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
using Shared.Kernel.Internal;
using Xunit;

namespace Reporting.Api.Tests;

/// <summary>
/// The Inventory Item Detail (TK-244), on stock worked by hand. Five stocked items, in pieces:
/// <list type="bullet">
/// <item><b>Steel bar</b>: opens at 80 worth 4,000. In October it receives 40 for 2,200, sends 5 back for 275,
/// sells 30 costing 1,575, takes 2 back costing 105, writes 3 off costing 150, and transfers 10 between warehouses
/// netting to nothing. It closes at 84 worth 4,305. Exactly equals Inventory Item Summary (TK-243).</item>
/// <item><b>Copper wire</b>: opens at 50 worth 1,000, receives 20 for 440, sells 15 costing 315, closing at 55 worth 1,125.</item>
/// <item><b>Brass fitting</b>: never moved.</item>
/// <item><b>Old stock</b>: opens at 10 worth 1,000 and has no movement in the period.</item>
/// <item><b>Cost pending</b>: receives 10 for 500, sells 4 whose movement is uncosted: cost pending is true.</item>
/// </list>
/// Tests are written and not run by AI agents (rule 0.5).
/// </summary>
public sealed class InventoryItemDetailReportTests
{
    private const long Steel = 9631, Copper = 9632, Brass = 9633, OldStock = 9634, Pending = 9635, Service = 9636;
    private const long MainWh = 50, BranchWh = 51;

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

    private static StockMovementRead Move(
        long id, long item, StockMovementType type, StockDirection direction, DateOnly date, decimal qty, decimal? cost, CostingStatus status = CostingStatus.Costed, long? wh = MainWh, string? srcType = null, long? srcId = null) =>
        new()
        {
            StockMovementId = id, ItemId = item, MovementType = type, Direction = direction, MovementDate = date, Quantity = qty, TotalCost = cost, CostingStatus = status, WarehouseId = wh, SourceType = srcType, SourceId = srcId,
        };

    private static readonly List<StockMovementRead> Movements =
    [
        Move(1, Steel, StockMovementType.Receipt, StockDirection.In, new(2026, 9, 10), 100m, 5_000m),
        Move(2, Steel, StockMovementType.Issue, StockDirection.Out, new(2026, 9, 20), 20m, 1_000m),
        Move(3, Copper, StockMovementType.Receipt, StockDirection.In, new(2026, 9, 5), 50m, 1_000m),
        Move(4, OldStock, StockMovementType.Opening, StockDirection.In, new(2026, 8, 1), 10m, 1_000m),
        Move(5, Steel, StockMovementType.Receipt, StockDirection.In, new(2026, 10, 3), 40m, 2_200m, srcType: "GRN", srcId: 501),
        Move(6, Steel, StockMovementType.PurchaseReturn, StockDirection.Out, new(2026, 10, 4), 5m, 275m, srcType: "DBN", srcId: 601),
        Move(7, Steel, StockMovementType.Issue, StockDirection.Out, new(2026, 10, 10), 30m, 1_575m, srcType: "INV", srcId: 701),
        Move(8, Steel, StockMovementType.SalesReturn, StockDirection.In, new(2026, 10, 12), 2m, 105m, srcType: "CRN", srcId: 801),
        Move(9, Steel, StockMovementType.Adjustment, StockDirection.Out, new(2026, 10, 15), 3m, 150m, srcType: "STA", srcId: 901),
        Move(10, Steel, StockMovementType.TransferOut, StockDirection.Out, new(2026, 10, 20), 10m, 525m, wh: MainWh),
        Move(11, Steel, StockMovementType.TransferIn, StockDirection.In, new(2026, 10, 20), 10m, 525m, wh: BranchWh),
        Move(12, Copper, StockMovementType.Receipt, StockDirection.In, new(2026, 10, 6), 20m, 440m, srcType: "GRN", srcId: 502),
        Move(13, Copper, StockMovementType.Issue, StockDirection.Out, new(2026, 10, 18), 15m, 315m, srcType: "INV", srcId: 702),
        Move(14, Pending, StockMovementType.Receipt, StockDirection.In, new(2026, 10, 8), 10m, 500m),
        Move(15, Pending, StockMovementType.Issue, StockDirection.Out, new(2026, 10, 20), 4m, null, CostingStatus.Pending),
        Move(16, Copper, StockMovementType.Receipt, StockDirection.In, new(2026, 11, 5), 10m, 220m),
        Move(17, Service, StockMovementType.Receipt, StockDirection.In, new(2026, 10, 5), 1m, 99m),
    ];

    private static InventoryItemDetailReportRequest Request(
        long? category = null, long? item = null, long? wh = null, string? src = null, DateOnly? from = null, DateOnly? to = null) =>
        new() { FromDate = from ?? October1, ToDate = to ?? October31, ItemCategoryId = category, ItemId = item, WarehouseId = wh, Source = src };

    // ---- Builders: Items, Openings, Periods ----------------------------------------------------

    [Fact]
    public void Only_stocked_items_are_in_scope_and_the_category_and_item_narrow_them()
    {
        Assert.Equal([Steel, Copper, Brass, OldStock, Pending], InventoryItemDetailReportRepository.Items(Items.AsQueryable(), Request()).Select(i => i.ItemId).Order());
        Assert.Equal([Brass], InventoryItemDetailReportRepository.Items(Items.AsQueryable(), Request(category: 9702)).Select(i => i.ItemId));
        Assert.Equal([Copper], InventoryItemDetailReportRepository.Items(Items.AsQueryable(), Request(item: Copper)).Select(i => i.ItemId));
    }

    [Fact]
    public void Opening_figures_match_inventory_item_summary_opening()
    {
        IQueryable<ItemRead> scopedItems = InventoryItemDetailReportRepository.Items(Items.AsQueryable(), Request());
        List<ItemOpeningFigure> openings = [.. InventoryItemDetailReportRepository.OpeningFigures(scopedItems, Movements.AsQueryable(), October1, null)];

        ItemOpeningFigure steel = openings.Single(o => o.ItemId == Steel);
        Assert.Equal(80m, steel.OpeningQuantity);
        Assert.Equal(4_000m, steel.OpeningValue);

        ItemOpeningFigure copper = openings.Single(o => o.ItemId == Copper);
        Assert.Equal(50m, copper.OpeningQuantity);
        Assert.Equal(1_000m, copper.OpeningValue);

        ItemOpeningFigure oldStock = openings.Single(o => o.ItemId == OldStock);
        Assert.Equal(10m, oldStock.OpeningQuantity);
        Assert.Equal(1_000m, oldStock.OpeningValue);
    }

    [Fact]
    public void Period_figures_accurately_aggregate_in_and_out_movements()
    {
        IQueryable<ItemRead> scopedItems = InventoryItemDetailReportRepository.Items(Items.AsQueryable(), Request());
        List<ItemPeriodFigure> periods = [.. InventoryItemDetailReportRepository.PeriodFigures(scopedItems, Movements.AsQueryable(), October1, October31, null, null)];

        ItemPeriodFigure steel = periods.Single(p => p.ItemId == Steel);
        // In: 40 (Receipt) + 2 (SalesReturn) + 10 (TransferIn) = 52
        // In Value: 2200 + 105 + 525 = 2830
        Assert.Equal(52m, steel.InQuantity);
        Assert.Equal(2_830m, steel.InValue);

        // Out: 5 (PurchaseReturn) + 30 (Issue) + 3 (Adjustment) + 10 (TransferOut) = 48
        // Out Value: 275 + 1575 + 150 + 525 = 2525
        Assert.Equal(48m, steel.OutQuantity);
        Assert.Equal(2_525m, steel.OutValue);

        // Closing: 80 + 52 - 48 = 84 qty; 4000 + 2830 - 2525 = 4305 value! Exactly equals Summary!
        Assert.Equal(84m, 80m + steel.InQuantity - steel.OutQuantity);
        Assert.Equal(4_305m, 4_000m + steel.InValue - steel.OutValue);
    }

    [Fact]
    public void Warehouse_filter_narrows_opening_and_period_figures()
    {
        IQueryable<ItemRead> scopedItems = InventoryItemDetailReportRepository.Items(Items.AsQueryable(), Request(wh: BranchWh));
        List<ItemPeriodFigure> periods = [.. InventoryItemDetailReportRepository.PeriodFigures(scopedItems, Movements.AsQueryable(), October1, October31, BranchWh, null)];

        ItemPeriodFigure steel = periods.Single(p => p.ItemId == Steel);
        Assert.Equal(10m, steel.InQuantity);
        Assert.Equal(525m, steel.InValue);
        Assert.Equal(0m, steel.OutQuantity);
    }

    // ---- Refusals & Validation ----------------------------------------------------------------

    [Fact]
    public void Missing_or_reversed_dates_are_refused()
    {
        var reversed = new InventoryItemDetailReportRequest { FromDate = October31, ToDate = October1 };
        IReadOnlyList<(string Field, string Message)> refusals = InventoryItemDetailReportRepository.Refusals(reversed);
        Assert.Contains(refusals, r => r.Field == nameof(InventoryItemDetailReportRequest.ToDate));

        var valid = new InventoryItemDetailReportRequest { FromDate = October1, ToDate = October31 };
        Assert.Empty(InventoryItemDetailReportRepository.Refusals(valid));
    }

    // ---- Route & Controller Guards ------------------------------------------------------------

    [Fact]
    public void Controller_has_required_attributes()
    {
        Type controller = typeof(InventoryItemDetailReportController);

        Assert.NotNull(controller.GetCustomAttribute<ApiControllerAttribute>());
        Assert.NotNull(controller.GetCustomAttribute<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>());

        var route = controller.GetCustomAttribute<RouteAttribute>();
        Assert.NotNull(route);
        Assert.Equal("api/statements/inventory-item-detail", route.Template);

        Assert.NotNull(controller.GetCustomAttribute<RequireBusinessTypeAttribute>());
        Assert.NotNull(controller.GetCustomAttribute<RequireModulePermissionAttribute>());
    }

    [Fact]
    public void Actions_have_inventory_view_permission()
    {
        MethodInfo run = typeof(InventoryItemDetailReportController).GetMethod(nameof(InventoryItemDetailReportController.Run))!;
        var runPerm = run.GetCustomAttribute<RequirePermissionAttribute>();
        Assert.NotNull(runPerm);
        Assert.Equal("inventory.view", runPerm.Permission);

        MethodInfo items = typeof(InventoryItemDetailReportController).GetMethod(nameof(InventoryItemDetailReportController.Items))!;
        Assert.NotNull(items.GetCustomAttribute<RequirePermissionAttribute>());

        MethodInfo cats = typeof(InventoryItemDetailReportController).GetMethod(nameof(InventoryItemDetailReportController.ItemCategories))!;
        Assert.NotNull(cats.GetCustomAttribute<RequirePermissionAttribute>());

        MethodInfo whs = typeof(InventoryItemDetailReportController).GetMethod(nameof(InventoryItemDetailReportController.Warehouses))!;
        Assert.NotNull(whs.GetCustomAttribute<RequirePermissionAttribute>());
    }
}
