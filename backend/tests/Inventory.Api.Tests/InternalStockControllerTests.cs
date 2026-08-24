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
}
