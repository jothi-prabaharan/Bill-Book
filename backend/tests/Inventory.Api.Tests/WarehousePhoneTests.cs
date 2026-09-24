using Inventory.Api.Services;
using Inventory.Entity.Models;
using Inventory.Entity.TableEntities;
using Inventory.Repository;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Inventory.Api.Tests;

/// <summary>
/// A warehouse saved with blank phones stores NULL, not '' (D-04, TK-21).
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class WarehousePhoneTests
{
    private readonly PostgresFixture _postgres;

    public WarehousePhoneTests(PostgresFixture postgres) => _postgres = postgres;

    /// <summary>The request names its code, so nothing is numbered.</summary>
    private static WarehouseService Service(InventoryDbContext db) => new(db, null!, TimeProvider.System);

    [SkippableFact]
    public async Task Blank_phones_are_stored_as_null_and_real_ones_trimmed()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        Guid orgId = Guid.NewGuid();
        await using InventoryDbContext db = _postgres.CreateContext(Guid.NewGuid(), orgId);

        await Service(db).CreateAsync(new SaveWarehouseRequest
        {
            WarehouseCode = "MAIN",
            WarehouseName = "Main store",
            PhoneNumber = "   ",
            MobileNumber = " 9876543210 ",
        }, default);

        Warehouse saved = await db.Warehouses.AsNoTracking().SingleAsync();
        Assert.Null(saved.PhoneNumber);
        Assert.Equal("9876543210", saved.MobileNumber);

        await Service(db).UpdateAsync(saved.WarehouseId, new SaveWarehouseRequest
        {
            WarehouseCode = "MAIN",
            WarehouseName = "Main store",
            PhoneNumber = "",
            MobileNumber = "",
        }, default);

        Warehouse updated = await db.Warehouses.AsNoTracking().SingleAsync();
        Assert.Null(updated.PhoneNumber);
        Assert.Null(updated.MobileNumber);
    }
}
