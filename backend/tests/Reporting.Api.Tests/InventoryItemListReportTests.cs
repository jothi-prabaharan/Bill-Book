using System.Reflection;
using Microsoft.AspNetCore.Authorization;
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

public sealed class InventoryItemListReportTests
{
    [Fact]
    public void Controller_HasExpectedSecurityAttributes()
    {
        var type = typeof(InventoryItemListReportController);

        Assert.NotNull(type.GetCustomAttribute<ApiControllerAttribute>());
        Assert.NotNull(type.GetCustomAttribute<AuthorizeAttribute>());
        Assert.NotNull(type.GetCustomAttribute<RequireBusinessTypeAttribute>());

        var moduleAttr = type.GetCustomAttribute<RequireModulePermissionAttribute>();
        Assert.NotNull(moduleAttr);
        Assert.Equal("reports", moduleAttr.Module);

        var runMethod = type.GetMethod(nameof(InventoryItemListReportController.Run));
        Assert.NotNull(runMethod);
        var permAttr = runMethod.GetCustomAttribute<RequirePermissionAttribute>();
        Assert.NotNull(permAttr);
        Assert.Equal("inventory.view", permAttr.Permission);
    }

    [Fact]
    public void FilterItems_AppliesCategoryStatusAndType()
    {
        var items = new List<ItemRead>
        {
            new() { ItemId = 1, ItemCode = "A1", ItemName = "Widget A", ItemCategoryId = 10, IsActive = true, ItemType = ItemType.Inventory },
            new() { ItemId = 2, ItemCode = "A2", ItemName = "Widget B", ItemCategoryId = 20, IsActive = false, ItemType = ItemType.Inventory },
            new() { ItemId = 3, ItemCode = "S1", ItemName = "Service One", ItemCategoryId = 10, IsActive = true, ItemType = ItemType.Service }
        }.AsQueryable();

        var req = new InventoryItemListReportRequest
        {
            ItemCategoryId = 10,
            Status = "Active",
            ItemType = "Inventory"
        };

        var filtered = InventoryItemListReportRepository.FilterItems(items, req).ToList();

        Assert.Single(filtered);
        Assert.Equal("A1", filtered[0].ItemCode);
    }

    [Fact]
    public void StockFigures_CalculatesQuantityAndValueUpToAsOfDate()
    {
        var asOf = new DateOnly(2026, 10, 15);

        var movements = new List<StockMovementRead>
        {
            // Within asOf
            new() { ItemId = 1, MovementDate = new DateOnly(2026, 10, 5), Direction = StockDirection.In, Quantity = 50, TotalCost = 1000m, CostingStatus = CostingStatus.Costed },
            new() { ItemId = 1, MovementDate = new DateOnly(2026, 10, 10), Direction = StockDirection.Out, Quantity = 20, TotalCost = 400m, CostingStatus = CostingStatus.Costed },
            // After asOf - should be ignored
            new() { ItemId = 1, MovementDate = new DateOnly(2026, 10, 20), Direction = StockDirection.In, Quantity = 100, TotalCost = 2000m, CostingStatus = CostingStatus.Costed },
        }.AsQueryable();

        var figures = InventoryItemListReportRepository.StockFigures(movements, asOf).ToList();

        Assert.Single(figures);
        Assert.Equal(30m, figures[0].QuantityOnHand);
        Assert.Equal(600m, figures[0].TotalValue);
        Assert.Equal(0, figures[0].UncostedMoves);
    }

    [Fact]
    public void PoFigures_CalculatesOpenPurchaseOrderQuantity()
    {
        var pos = new List<PurchaseOrderRead>
        {
            new() { PurchaseOrderId = 101, Status = DocumentStatus.Posted },
            new() { PurchaseOrderId = 102, Status = DocumentStatus.Draft }, // Draft ignored
        }.AsQueryable();

        var pods = new List<PurchaseOrderDetailRead>
        {
            new() { PurchaseOrderId = 101, ItemId = 1, Quantity = 100, ReceivedQuantity = 40 }, // Open 60
            new() { PurchaseOrderId = 102, ItemId = 1, Quantity = 50, ReceivedQuantity = 0 },
        }.AsQueryable();

        var figures = InventoryItemListReportRepository.PoFigures(pos, pods).ToList();

        Assert.Single(figures);
        Assert.Equal(60m, figures[0].QuantityOnOrder);
    }

    [Fact]
    public void SoFigures_CalculatesOpenSalesOrderQuantity()
    {
        var sos = new List<SalesOrderRead>
        {
            new() { SalesOrderId = 201, Status = DocumentStatus.Posted },
        }.AsQueryable();

        var sods = new List<SalesOrderDetailRead>
        {
            new() { SalesOrderId = 201, ItemId = 1, Quantity = 80, DeliveredQuantity = 30 }, // Open 50
        }.AsQueryable();

        var figures = InventoryItemListReportRepository.SoFigures(sos, sods).ToList();

        Assert.Single(figures);
        Assert.Equal(50m, figures[0].QuantityCommitted);
    }

    [Fact]
    public void FifoFigures_PicksOldestCostLayerWithRemainingQuantity()
    {
        var layers = new List<CostLayerRead>
        {
            new() { CostLayerId = 1, ItemId = 1, ReceivedOn = new DateOnly(2026, 9, 1), RemainingQuantity = 0, UnitCost = 15m },
            new() { CostLayerId = 2, ItemId = 1, ReceivedOn = new DateOnly(2026, 9, 15), RemainingQuantity = 10, UnitCost = 18m },
            new() { CostLayerId = 3, ItemId = 1, ReceivedOn = new DateOnly(2026, 10, 1), RemainingQuantity = 25, UnitCost = 20m },
        }.AsQueryable();

        var figures = InventoryItemListReportRepository.FifoFigures(layers).ToList();

        Assert.Single(figures);
        Assert.Equal(18m, figures[0].UnitCostPriceFifo);
    }
}
