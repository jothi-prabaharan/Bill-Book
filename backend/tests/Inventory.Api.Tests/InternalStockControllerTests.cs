using Inventory.Api.Controllers;
using Inventory.Api.Services;
using Inventory.Entity.Enums;
using Inventory.Entity.Models;
using Inventory.Entity.TableEntities;
using Inventory.Repository;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Shared.Kernel.Tenancy;
using Xunit;

namespace Inventory.Api.Tests;

[Collection(nameof(PostgresCollection))]
public class InternalStockControllerTests
{
    private readonly PostgresFixture _postgres;

    public InternalStockControllerTests(PostgresFixture postgres) => _postgres = postgres;

    [SkippableFact]
    public async Task Failed_invoice_issue_rolls_back_released_reservation()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var db = _postgres.CreateContext(customerId, orgId);

        var uomType = new UomType { OrgId = orgId, UomTypeName = "Count", UomTypeSystemName = "COUNT", IsActive = true };
        db.UomTypes.Add(uomType);
        await db.SaveChangesAsync();

        var uom = new UnitOfMeasure { OrgId = orgId, UomTypeId = uomType.UomTypeId, UomCode = "PCS", UomName = "Pieces", ConversionToBase = 1m, IsBaseUnit = true, IsActive = true };
        db.UnitOfMeasures.Add(uom);
        await db.SaveChangesAsync();

        var savedUomType = await db.UomTypes.FirstAsync();
        var savedUom = await db.UnitOfMeasures.FirstAsync();

        var item = new Item
        {
            OrgId = orgId,
            ItemCode = "TEST",
            ItemName = "TEST",
            UomTypeId = savedUomType.UomTypeId,
            InventoryUomId = savedUom.UomId,
            SalesUomId = savedUom.UomId,
            PurchaseUomId = savedUom.UomId,
            ReportUomId = savedUom.UomId,
            TrackInventory = true,
            CostingType = CostingType.WeightedAverage,
            IsActive = true,
        };
        db.Items.Add(item);
        await db.SaveChangesAsync();

        // 10 on hand, 5 reserved
        db.ItemStock.Add(new ItemStock
        {
            OrgId = orgId,
            ItemId = item.ItemId,
            QuantityOnHand = 10m,
            QuantityReserved = 5m,
            WeightedAverageCost = 100m,
        });
        await db.SaveChangesAsync();

        var services = new ServiceCollection();
        services.AddScoped(_ => db);
        services.AddScoped<StockService>(sp => new StockService(db, new CostingService(db), TimeProvider.System));
        var sp = services.BuildServiceProvider();

        var tenant = new TenantContext();
        var controller = new InternalStockController(tenant, sp, NullLogger<InternalStockController>.Instance);

        var request = new IssueStockRequest
        {
            CustomerId = customerId,
            OrgId = orgId,
            SourceType = "INV",
            SourceId = 1,
            MovementDate = new DateOnly(2026, 8, 24),
            Lines =
            [
                // This line should successfully release 5, and try to issue 5.
                // BUT we are intentionally failing it by requesting to issue 99 (more than available)
                new IssueStockLine
                {
                    SourceLineId = 1,
                    ItemId = item.ItemId,
                    Quantity = 99m,
                    ReleaseReservation = true,
                }
            ]
        };

        var result = await controller.Issue(request, CancellationToken.None);
        var conflict = Assert.IsType<ConflictObjectResult>(result);
        var response = Assert.IsType<IssueStockResponse>(conflict.Value);
        Assert.False(response.Success);

        // Physically assert the reservation survived (was rolled back)
        var stock = await db.ItemStock.FirstAsync(s => s.ItemId == item.ItemId);
        Assert.Equal(10m, stock.QuantityOnHand);
        Assert.Equal(5m, stock.QuantityReserved); // The reservation of 5 should STILL be there!
    }

    /// <summary>
    /// A credit note's goods come back as a sales return, onto the layers they
    /// left from — and a receipt with no issue behind it stays a receipt.
    ///
    /// Every line used to be recorded as a receipt, so the id naming the issue
    /// was stored and never read: the costing engine only walks a return back to
    /// its layers, and a receipt opens a fresh layer at whatever cost it was sent
    /// — which from a credit note was the selling price (TK-13).
    /// </summary>
    [SkippableFact]
    public async Task A_receipt_naming_the_issue_it_reverses_is_recorded_as_a_sales_return()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var db = _postgres.CreateContext(customerId, orgId);

        var uomType = new UomType { OrgId = orgId, UomTypeName = "Count", UomTypeSystemName = "COUNT", IsActive = true };
        db.UomTypes.Add(uomType);
        await db.SaveChangesAsync();

        var uom = new UnitOfMeasure { OrgId = orgId, UomTypeId = uomType.UomTypeId, UomCode = "PCS", UomName = "Pieces", ConversionToBase = 1m, IsBaseUnit = true, IsActive = true };
        db.UnitOfMeasures.Add(uom);
        await db.SaveChangesAsync();

        var item = new Item
        {
            OrgId = orgId,
            ItemCode = "RET",
            ItemName = "Returned item",
            UomTypeId = uomType.UomTypeId,
            InventoryUomId = uom.UomId,
            SalesUomId = uom.UomId,
            PurchaseUomId = uom.UomId,
            ReportUomId = uom.UomId,
            TrackInventory = true,
            CostingType = CostingType.WeightedAverage,
            IsActive = true,
        };
        db.Items.Add(item);
        await db.SaveChangesAsync();

        db.ItemStock.Add(new ItemStock
        {
            OrgId = orgId,
            ItemId = item.ItemId,
            QuantityOnHand = 10m,
            WeightedAverageCost = 60m,
        });
        await db.SaveChangesAsync();

        var services = new ServiceCollection();
        services.AddScoped(_ => db);
        services.AddScoped<StockService>(sp => new StockService(db, new CostingService(db), TimeProvider.System));
        var sp = services.BuildServiceProvider();

        var controller = new InternalStockController(
            new TenantContext(), sp, NullLogger<InternalStockController>.Instance);

        // The sale: 4 out on invoice 1.
        var issued = Assert.IsType<OkObjectResult>(await controller.Issue(new IssueStockRequest
        {
            CustomerId = customerId,
            OrgId = orgId,
            SourceType = "INV",
            SourceId = 1,
            MovementDate = new DateOnly(2026, 8, 24),
            Lines = [new IssueStockLine { SourceLineId = 1, ItemId = item.ItemId, Quantity = 4m }],
        }, CancellationToken.None));
        long issueId = Assert.IsType<IssueStockResponse>(issued.Value).Lines.Single().StockMovementId!.Value;

        // The return: 2 back on credit note 5, naming the issue.
        var returned = Assert.IsType<OkObjectResult>(await controller.Receipt(new ReceiveStockRequest
        {
            CustomerId = customerId,
            OrgId = orgId,
            SourceType = "CRN",
            SourceId = 5,
            MovementDate = new DateOnly(2026, 8, 25),
            Lines =
            [
                new ReceiveStockLine
                {
                    SourceLineId = 1,
                    ItemId = item.ItemId,
                    Quantity = 2m,
                    UnitCost = 60m,
                    ReturnsStockMovementId = issueId,
                },
            ],
        }, CancellationToken.None));

        var response = Assert.IsType<ReceiveStockResponse>(returned.Value);

        // Valued and posted by the costing worker from the layers, so nothing
        // here for the caller to post a second time.
        Assert.Equal(0m, response.TotalValue);

        var movement = await db.StockMovements.SingleAsync(m => m.SourceType == "CRN" && m.SourceId == 5);
        Assert.Equal(StockMovementType.SalesReturn, movement.MovementType);
        Assert.Equal(issueId, movement.ReturnsStockMovementId);

        // And a receipt with no issue behind it is still a receipt, valued here.
        var received = Assert.IsType<OkObjectResult>(await controller.Receipt(new ReceiveStockRequest
        {
            CustomerId = customerId,
            OrgId = orgId,
            SourceType = "GRN",
            SourceId = 9,
            MovementDate = new DateOnly(2026, 8, 25),
            Lines = [new ReceiveStockLine { SourceLineId = 1, ItemId = item.ItemId, Quantity = 3m, UnitCost = 70m }],
        }, CancellationToken.None));

        Assert.Equal(210m, Assert.IsType<ReceiveStockResponse>(received.Value).TotalValue);
        var receipt = await db.StockMovements.SingleAsync(m => m.SourceType == "GRN" && m.SourceId == 9);
        Assert.Equal(StockMovementType.Receipt, receipt.MovementType);
    }
}
