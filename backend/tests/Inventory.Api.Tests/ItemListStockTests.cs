using Inventory.Entity.Enums;
using Inventory.Entity.Models;
using Inventory.Entity.TableEntities;
using Inventory.Repository;
using Inventory.Repository.Repositories;
using Xunit;

namespace Inventory.Api.Tests;

/// <summary>
/// What the Items list shows for stock and HSN: how much is on hand and what it is worth, so the list's
/// On hand and Stock value columns carry figures rather than dashes. The value is the quantity at its
/// weighted-average cost, the figure the Stock page's list gives. Written by an AI and not run (section 0.5
/// of docs/Task_Details.md).
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class ItemListStockTests
{
    private readonly PostgresFixture _postgres;

    public ItemListStockTests(PostgresFixture postgres) => _postgres = postgres;

    private static ItemRepository Service(InventoryDbContext db) => new(db, null!, TimeProvider.System);

    private static async Task<(long UomTypeId, long UomId)> SeedUnitAsync(InventoryDbContext db, Guid orgId)
    {
        var uomType = new UomType { OrgId = orgId, UomTypeName = "Count", UomTypeSystemName = "COUNT", IsActive = true };
        db.UomTypes.Add(uomType);
        await db.SaveChangesAsync();

        var uom = new UnitOfMeasure
        {
            OrgId = orgId,
            UomTypeId = uomType.UomTypeId,
            UomCode = "PCS",
            UomName = "Pieces",
            ConversionToBase = 1m,
            IsBaseUnit = true,
            IsActive = true,
        };
        db.UnitOfMeasures.Add(uom);
        await db.SaveChangesAsync();

        return (uomType.UomTypeId, uom.UomId);
    }

    private static async Task<long> SeedItemAsync(
        InventoryDbContext db, Guid orgId, (long TypeId, long UomId) unit, string code, bool tracksInventory = true, int? hsnSacCodeId = null)
    {
        var item = new Item
        {
            OrgId = orgId,
            ItemCode = code,
            ItemName = "Item " + code,
            UomTypeId = unit.TypeId,
            InventoryUomId = unit.UomId,
            SalesUomId = unit.UomId,
            PurchaseUomId = unit.UomId,
            ReportUomId = unit.UomId,
            CostingType = CostingType.WeightedAverage,
            TrackInventory = tracksInventory,
            HsnSacCodeId = hsnSacCodeId,
            IsActive = true,
        };
        db.Items.Add(item);
        await db.SaveChangesAsync();
        return item.ItemId;
    }

    private static async Task SeedStockAsync(InventoryDbContext db, Guid orgId, long itemId, decimal quantity, decimal averageCost)
    {
        db.ItemStock.Add(new ItemStock
        {
            OrgId = orgId,
            ItemId = itemId,
            QuantityOnHand = quantity,
            WeightedAverageCost = averageCost,
        });
        await db.SaveChangesAsync();
    }

    [SkippableFact]
    public async Task A_stocked_item_lists_what_is_on_hand_and_what_it_is_worth()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        Guid orgId = Guid.NewGuid();
        await using InventoryDbContext db = _postgres.CreateContext(Guid.NewGuid(), orgId);
        var unit = await SeedUnitAsync(db, orgId);
        long itemId = await SeedItemAsync(db, orgId, unit, "BOLT-1");
        await SeedStockAsync(db, orgId, itemId, quantity: 25m, averageCost: 40m);

        ItemListItem row = Assert.Single(await Service(db).ListAsync(null, null, null, false, default));

        Assert.Equal(25m, row.QuantityOnHand);
        Assert.Equal(1000m, row.StockValue);
    }

    [SkippableFact]
    public async Task The_value_is_rounded_to_paise()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        Guid orgId = Guid.NewGuid();
        await using InventoryDbContext db = _postgres.CreateContext(Guid.NewGuid(), orgId);
        var unit = await SeedUnitAsync(db, orgId);
        long itemId = await SeedItemAsync(db, orgId, unit, "BOLT-2");
        await SeedStockAsync(db, orgId, itemId, quantity: 3m, averageCost: 33.333333m);

        ItemListItem row = Assert.Single(await Service(db).ListAsync(null, null, null, false, default));

        Assert.Equal(100m, row.StockValue);
    }

    [SkippableFact]
    public async Task A_stocked_item_that_has_never_moved_is_a_zero_not_a_dash()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        Guid orgId = Guid.NewGuid();
        await using InventoryDbContext db = _postgres.CreateContext(Guid.NewGuid(), orgId);
        var unit = await SeedUnitAsync(db, orgId);
        await SeedItemAsync(db, orgId, unit, "NEW-1");

        ItemListItem row = Assert.Single(await Service(db).ListAsync(null, null, null, false, default));

        Assert.Equal(0m, row.QuantityOnHand);
        Assert.Equal(0m, row.StockValue);
    }

    [SkippableFact]
    public async Task An_item_that_holds_no_stock_has_no_quantity_and_no_value()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        Guid orgId = Guid.NewGuid();
        await using InventoryDbContext db = _postgres.CreateContext(Guid.NewGuid(), orgId);
        var unit = await SeedUnitAsync(db, orgId);
        await SeedItemAsync(db, orgId, unit, "SERVICE-1", tracksInventory: false);

        ItemListItem row = Assert.Single(await Service(db).ListAsync(null, null, null, false, default));

        Assert.Null(row.QuantityOnHand);
        Assert.Null(row.StockValue);
    }

    [SkippableFact]
    public async Task The_list_carries_the_items_hsn_code_id()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        Guid orgId = Guid.NewGuid();
        await using InventoryDbContext db = _postgres.CreateContext(Guid.NewGuid(), orgId);
        var unit = await SeedUnitAsync(db, orgId);
        await SeedItemAsync(db, orgId, unit, "HSN-1", hsnSacCodeId: 73);
        await SeedItemAsync(db, orgId, unit, "HSN-2");

        IReadOnlyList<ItemListItem> rows = await Service(db).ListAsync(null, null, null, false, default);

        Assert.Equal(73, rows.Single(r => r.ItemCode == "HSN-1").HsnSacCodeId);
        Assert.Null(rows.Single(r => r.ItemCode == "HSN-2").HsnSacCodeId);
    }

    [SkippableFact]
    public async Task A_page_of_items_carries_the_same_figures()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        Guid orgId = Guid.NewGuid();
        await using InventoryDbContext db = _postgres.CreateContext(Guid.NewGuid(), orgId);
        var unit = await SeedUnitAsync(db, orgId);
        long itemId = await SeedItemAsync(db, orgId, unit, "PAGE-1");
        await SeedStockAsync(db, orgId, itemId, quantity: 4m, averageCost: 12.5m);

        ItemListPage page = await Service(db).PageAsync(null, null, null, false, 0, 10, default);

        ItemListItem row = Assert.Single(page.Rows);
        Assert.Equal(4m, row.QuantityOnHand);
        Assert.Equal(50m, row.StockValue);
    }
}
