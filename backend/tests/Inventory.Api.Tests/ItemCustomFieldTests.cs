using System.Text.Json;
using Inventory.Api.Services;
using Inventory.Repository.Repositories;
using Inventory.Entity.Enums;
using Inventory.Entity.Models;
using Inventory.Entity.TableEntities;
using Inventory.Repository;
using Shared.Kernel.CustomFields;
using Xunit;

namespace Inventory.Api.Tests;

/// <summary>Custom fields on items (TK-111): searched when marked searchable, and returned on the list.</summary>
[Collection(nameof(PostgresCollection))]
public sealed class ItemCustomFieldTests
{
    private readonly PostgresFixture _postgres;

    public ItemCustomFieldTests(PostgresFixture postgres) => _postgres = postgres;

    private sealed class Definitions(bool searchable) : ICustomFieldDefinitions
    {
        public Task<IReadOnlyList<CustomFieldDefinitionDto>?> GetAsync(CustomFieldEntityKind kind, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<CustomFieldDefinitionDto>?>(
            [
                new CustomFieldDefinitionDto
                {
                    EntityKind = CustomFieldEntityKind.Item,
                    Key = "shelf",
                    Label = "Shelf",
                    DataType = CustomFieldDataType.Text,
                    IsSearchable = searchable,
                },
            ]);
    }

    private async Task<InventoryDbContext> BranchAsync()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        Guid orgId = Guid.NewGuid();
        InventoryDbContext db = _postgres.CreateContext(Guid.NewGuid(), orgId);

        var uomType = new UomType { OrgId = orgId, UomTypeName = "Count", UomTypeSystemName = "COUNT", IsActive = true };
        db.UomTypes.Add(uomType);
        await db.SaveChangesAsync();
        var uom = new UnitOfMeasure { OrgId = orgId, UomTypeId = uomType.UomTypeId, UomCode = "PCS", UomName = "Pieces", ConversionToBase = 1m, IsBaseUnit = true, IsActive = true };
        db.UnitOfMeasures.Add(uom);
        await db.SaveChangesAsync();

        Item Make(string code, string name, string? shelf) => new()
        {
            OrgId = orgId,
            ItemCode = code,
            ItemName = name,
            UomTypeId = uomType.UomTypeId,
            InventoryUomId = uom.UomId,
            SalesUomId = uom.UomId,
            PurchaseUomId = uom.UomId,
            ReportUomId = uom.UomId,
            CostingType = CostingType.WeightedAverage,
            IsActive = true,
            CustomFields = shelf is null ? null : JsonDocument.Parse($$"""{ "shelf": "{{shelf}}" }"""),
        };

        db.Items.AddRange(Make("SOAP-1", "Sandal soap", "AISLE-7"), Make("SHMP-1", "Shampoo", null));
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        return db;
    }

    [SkippableFact]
    public async Task An_item_is_found_by_a_searchable_custom_value_and_the_value_comes_back_on_the_list()
    {
        await using InventoryDbContext db = await BranchAsync();

        IReadOnlyList<ItemListItem> found = await new ItemRepository(db, null!, TimeProvider.System, new Definitions(searchable: true))
            .ListAsync("aisle-7", null, null, false, default);

        ItemListItem soap = Assert.Single(found);
        Assert.Equal("SOAP-1", soap.ItemCode);
        Assert.Equal("AISLE-7", soap.CustomFields!["shelf"].GetString());
    }

    [SkippableFact]
    public async Task A_field_not_marked_searchable_is_not_searched()
    {
        await using InventoryDbContext db = await BranchAsync();

        Assert.Empty(await new ItemRepository(db, null!, TimeProvider.System, new Definitions(searchable: false))
            .ListAsync("aisle-7", null, null, false, default));
    }
}
