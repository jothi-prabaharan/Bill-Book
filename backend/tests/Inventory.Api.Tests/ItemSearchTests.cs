using Inventory.Api.Services;
using Inventory.Entity.Enums;
using Inventory.Entity.Models;
using Inventory.Entity.TableEntities;
using Inventory.Repository;
using Xunit;

namespace Inventory.Api.Tests;

/// <summary>
/// <c>GET /api/items?search=</c> finds an item by its barcode, and pages (TK-14).
/// The POS till needs both: a scanner types the whole code, and a branch's item
/// master is too long to send in one response.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class ItemSearchTests
{
    private readonly PostgresFixture _postgres;

    public ItemSearchTests(PostgresFixture postgres) => _postgres = postgres;

    /// <summary>
    /// Only the list paths are exercised, and neither numbers anything, so the
    /// service gets no number generator.
    /// </summary>
    private static ItemService Service(InventoryDbContext db) => new(db, null!, TimeProvider.System);

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
        InventoryDbContext db, Guid orgId, (long TypeId, long UomId) unit, string code, string name, params string[] barcodes)
    {
        var item = new Item
        {
            OrgId = orgId,
            ItemCode = code,
            ItemName = name,
            UomTypeId = unit.TypeId,
            InventoryUomId = unit.UomId,
            SalesUomId = unit.UomId,
            PurchaseUomId = unit.UomId,
            ReportUomId = unit.UomId,
            CostingType = CostingType.WeightedAverage,
            IsActive = true,
        };
        db.Items.Add(item);
        await db.SaveChangesAsync();

        db.ItemBarcodes.AddRange(barcodes.Select((b, i) => new ItemBarcode
        {
            OrgId = orgId,
            ItemId = item.ItemId,
            Barcode = b,
            IsPrimary = i == 0,
            IsActive = true,
        }));
        await db.SaveChangesAsync();

        return item.ItemId;
    }

    [SkippableFact]
    public async Task An_exact_barcode_returns_that_one_item()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        Guid orgId = Guid.NewGuid();
        await using InventoryDbContext db = _postgres.CreateContext(Guid.NewGuid(), orgId);
        var unit = await SeedUnitAsync(db, orgId);

        long soap = await SeedItemAsync(db, orgId, unit, "SOAP-1", "Sandal soap", "8901030865278");
        await SeedItemAsync(db, orgId, unit, "SHMP-1", "Shampoo", "8901030865285");

        IReadOnlyList<ItemListItem> found = await Service(db).ListAsync("8901030865278", null, null, false, default);

        Assert.Equal(soap, Assert.Single(found).ItemId);
    }

    [SkippableFact]
    public async Task A_barcode_matches_exactly_never_in_part()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        Guid orgId = Guid.NewGuid();
        await using InventoryDbContext db = _postgres.CreateContext(Guid.NewGuid(), orgId);
        var unit = await SeedUnitAsync(db, orgId);

        await SeedItemAsync(db, orgId, unit, "SOAP-1", "Sandal soap", "8901030865278");

        // Half a scan finds nothing; a scanner types the whole code.
        Assert.Empty(await Service(db).ListAsync("89010308", null, null, false, default));
    }

    [SkippableFact]
    public async Task A_scanned_barcode_ranks_above_a_name_that_contains_the_same_digits()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        Guid orgId = Guid.NewGuid();
        await using InventoryDbContext db = _postgres.CreateContext(Guid.NewGuid(), orgId);
        var unit = await SeedUnitAsync(db, orgId);

        // Alphabetically first, and its name contains the code — which is what
        // would have put it at the top of a till's list.
        await SeedItemAsync(db, orgId, unit, "A-1", "A 1234567 promo pack");
        long scanned = await SeedItemAsync(db, orgId, unit, "Z-1", "Zinc cream", "1234567");

        IReadOnlyList<ItemListItem> found = await Service(db).ListAsync("1234567", null, null, false, default);

        Assert.Equal(2, found.Count);
        Assert.Equal(scanned, found[0].ItemId);
    }

    [SkippableFact]
    public async Task Paging_returns_the_right_total_and_the_requested_slice()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        Guid orgId = Guid.NewGuid();
        await using InventoryDbContext db = _postgres.CreateContext(Guid.NewGuid(), orgId);
        var unit = await SeedUnitAsync(db, orgId);

        for (int i = 1; i <= 7; i++)
        {
            await SeedItemAsync(db, orgId, unit, $"PEN-{i}", $"Pen {i:00}");
        }

        await SeedItemAsync(db, orgId, unit, "INK-1", "Ink bottle");

        ItemListPage page = await Service(db).PageAsync("pen", null, null, false, skip: 5, take: 5, default);

        Assert.Equal(7, page.Total);
        Assert.Equal(5, page.Skip);
        Assert.Equal(5, page.Take);
        Assert.Equal(["Pen 06", "Pen 07"], page.Rows.Select(r => r.ItemName));
    }

    [SkippableFact]
    public async Task Out_of_range_paging_is_clamped_rather_than_trusted()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        Guid orgId = Guid.NewGuid();
        await using InventoryDbContext db = _postgres.CreateContext(Guid.NewGuid(), orgId);
        var unit = await SeedUnitAsync(db, orgId);
        await SeedItemAsync(db, orgId, unit, "PEN-1", "Pen");

        ItemListPage page = await Service(db).PageAsync(null, null, null, false, skip: -3, take: 1_000_000, default);

        Assert.Equal(0, page.Skip);
        Assert.Equal(200, page.Take);
        Assert.Single(page.Rows);
    }

    [SkippableFact]
    public async Task Another_branchs_item_never_appears_even_by_its_barcode()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        Guid customerId = Guid.NewGuid();
        Guid branchA = Guid.NewGuid();
        Guid branchB = Guid.NewGuid();

        await using (InventoryDbContext a = _postgres.CreateContext(customerId, branchA))
        {
            var unit = await SeedUnitAsync(a, branchA);
            await SeedItemAsync(a, branchA, unit, "SOAP-1", "Sandal soap", "8901030865278");
        }

        await using InventoryDbContext b = _postgres.CreateContext(customerId, branchB);

        Assert.Empty(await Service(b).ListAsync("8901030865278", null, null, false, default));
        Assert.Empty(await Service(b).ListAsync("soap", null, null, false, default));
        Assert.Equal(0, (await Service(b).PageAsync(null, null, null, false, 0, 50, default)).Total);
    }

    [SkippableFact]
    public async Task An_inactive_barcode_finds_nothing()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        Guid orgId = Guid.NewGuid();
        await using InventoryDbContext db = _postgres.CreateContext(Guid.NewGuid(), orgId);
        var unit = await SeedUnitAsync(db, orgId);
        await SeedItemAsync(db, orgId, unit, "SOAP-1", "Sandal soap", "8901030865278");

        ItemBarcode barcode = db.ItemBarcodes.Single();
        barcode.IsActive = false;
        await db.SaveChangesAsync();

        Assert.Empty(await Service(db).ListAsync("8901030865278", null, null, false, default));
    }
}
