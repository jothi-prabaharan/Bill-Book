using Microsoft.EntityFrameworkCore;
using Inventory.Entity.TableEntities;
using Inventory.Repository;
using Shared.Kernel.Tenancy;
using Xunit;

namespace Inventory.Api.Tests;

/// <summary>
/// Asserts the inv schema's tenant isolation the same way Sales.Api.Tests does:
/// over the whole model and the whole schema, backed by a real PostgreSQL so the
/// RLS half — which the model knows nothing about — is actually checked.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class InventoryQueryFilterTests
{
    private readonly PostgresFixture _postgres;

    public InventoryQueryFilterTests(PostgresFixture postgres) => _postgres = postgres;

    [SkippableFact]
    public async Task Every_org_scoped_inventory_entity_has_a_query_filter()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        await using InventoryDbContext db = _postgres.CreateContext(Guid.NewGuid(), Guid.NewGuid());

        List<string> unfiltered = [.. db.Model.GetEntityTypes()
            .Where(e => typeof(OrgScopedEntity).IsAssignableFrom(e.ClrType))
            .Where(e => e.GetDeclaredQueryFilters() is not { Count: > 0 })
            .Select(e => e.ClrType.Name)
            .OrderBy(name => name)];

        Assert.Empty(unfiltered);
    }

    [SkippableFact]
    public async Task One_branch_cannot_read_another_branchs_warehouses()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        var customerId = Guid.NewGuid();
        await using InventoryDbContext mine = _postgres.CreateContext(customerId, Guid.NewGuid());
        await using InventoryDbContext theirs = _postgres.CreateContext(customerId, Guid.NewGuid());

        mine.Warehouses.Add(new Warehouse
        {
            WarehouseCode = "WH1",
            WarehouseName = "Branch-scoped warehouse",
        });

        await mine.SaveChangesAsync(CancellationToken.None);

        Assert.NotEmpty(await mine.Warehouses.ToListAsync(CancellationToken.None));
        Assert.Empty(await theirs.Warehouses.ToListAsync(CancellationToken.None));
    }

    [SkippableFact]
    public async Task One_customer_cannot_read_another_customers_warehouses()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        // Two different customers sharing the one test database — proving the
        // CustomerId column this migration added actually isolates them, not
        // just the OrgId that already did.
        await using InventoryDbContext mine = _postgres.CreateContext(Guid.NewGuid(), Guid.NewGuid());
        await using InventoryDbContext theirs = _postgres.CreateContext(Guid.NewGuid(), Guid.NewGuid());

        mine.Warehouses.Add(new Warehouse
        {
            WarehouseCode = "WH2",
            WarehouseName = "Customer-scoped warehouse",
        });

        await mine.SaveChangesAsync(CancellationToken.None);

        Assert.NotEmpty(await mine.Warehouses.ToListAsync(CancellationToken.None));
        Assert.Empty(await theirs.Warehouses.ToListAsync(CancellationToken.None));
    }

    [SkippableFact]
    public async Task Row_level_security_covers_every_table_in_the_schema()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        await using InventoryDbContext db = _postgres.CreateContext(Guid.NewGuid(), Guid.NewGuid());

        // PriceLists/PriceListItems had a policy defined but RLS never enabled on
        // the table itself, so the policy was inert — this is the check that
        // would have caught it.
        List<string> unprotected = [];
        await using (var command = db.Database.GetDbConnection().CreateCommand())
        {
            command.CommandText =
                "SELECT tablename FROM pg_tables WHERE schemaname = 'inv' AND NOT rowsecurity";

            await db.Database.OpenConnectionAsync(CancellationToken.None);
            await using var reader = await command.ExecuteReaderAsync(CancellationToken.None);

            while (await reader.ReadAsync(CancellationToken.None))
            {
                unprotected.Add(reader.GetString(0));
            }
        }

        Assert.Empty(unprotected);
    }
}
