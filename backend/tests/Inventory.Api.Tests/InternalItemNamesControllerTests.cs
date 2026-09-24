using Inventory.Api.Controllers;
using Inventory.Entity.Enums;
using Inventory.Entity.TableEntities;
using Inventory.Repository;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Shared.Kernel.Documents;
using Shared.Kernel.Tenancy;
using Xunit;

namespace Inventory.Api.Tests;

/// <summary>
/// <c>POST internal/items/names</c> resolves item codes and names in the branch
/// the request names.
///
/// Sales and Purchase send only the internal key, so before TK-06 the route read
/// <c>inv.Items</c> with no tenant and every document line showed an id where
/// the item should have been. The context is registered the way the host builds
/// it, from the request's <see cref="TenantContext"/>, so the controller must
/// set the tenant before it resolves the context.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class InternalItemNamesControllerTests
{
    private readonly PostgresFixture _postgres;

    public InternalItemNamesControllerTests(PostgresFixture postgres) => _postgres = postgres;

    private async Task<long> SeedItemAsync(Guid customerId, Guid orgId)
    {
        await using InventoryDbContext db = _postgres.CreateContext(customerId, orgId);

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

        var item = new Item
        {
            OrgId = orgId,
            ItemCode = "ITM-1",
            ItemName = "Named over the wire",
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
        return item.ItemId;
    }

    private (InternalItemNamesController Controller, ServiceProvider Services) Controller(TenantContext tenant)
    {
        var services = new ServiceCollection();
        services.AddScoped(_ => _postgres.CreateContext(tenant));

        ServiceProvider provider = services.BuildServiceProvider();
        return (new InternalItemNamesController(tenant, provider), provider);
    }

    [SkippableFact]
    public async Task A_branch_named_in_the_body_gets_its_item_names()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        Guid customerId = Guid.NewGuid();
        Guid orgId = Guid.NewGuid();
        long itemId = await SeedItemAsync(customerId, orgId);

        (InternalItemNamesController controller, ServiceProvider services) = Controller(new TenantContext());
        await using ServiceProvider _ = services;

        IActionResult result = await controller.Names(
            new NameLookupRequest { Ids = [itemId], CustomerId = customerId, OrgId = orgId },
            CancellationToken.None);

        List<NamedRef> names = Assert.IsType<List<NamedRef>>(Assert.IsType<OkObjectResult>(result).Value);
        Assert.Equal(new NamedRef(itemId, "ITM-1", "Named over the wire"), Assert.Single(names));
    }

    [SkippableFact]
    public async Task Another_branch_gets_nothing_back()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        Guid customerId = Guid.NewGuid();
        long itemId = await SeedItemAsync(customerId, Guid.NewGuid());

        (InternalItemNamesController controller, ServiceProvider services) = Controller(new TenantContext());
        await using ServiceProvider _ = services;

        IActionResult result = await controller.Names(
            new NameLookupRequest { Ids = [itemId], CustomerId = customerId, OrgId = Guid.NewGuid() },
            CancellationToken.None);

        Assert.Empty(Assert.IsType<List<NamedRef>>(Assert.IsType<OkObjectResult>(result).Value));
    }

    [Fact]
    public async Task No_branch_anywhere_is_a_400()
    {
        var controller = new InternalItemNamesController(
            new TenantContext(), new ServiceCollection().BuildServiceProvider());

        IActionResult result = await controller.Names(
            new NameLookupRequest { Ids = [1] }, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }
}
