using Inventory.Repository;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Documents;
using Shared.Kernel.Internal;
using Shared.Kernel.Stock;
using Shared.Kernel.Tenancy;

namespace Inventory.Api.Controllers;

/// <summary>
/// Resolves item ids to codes and names, in batches, for the services that hold
/// documents.
///
/// The mirror of Contacts' names endpoint, and there for the same reason: a
/// document line stores `ItemId` and deliberately not the code or the name, so
/// that renaming an item shows everywhere including on documents already raised.
/// Reading a document is then only cheap if its lines' names come back in one
/// call.
///
/// <b>POST with the ids in the body</b>, because a document with forty lines
/// resolves forty ids and a proxy's URL length limit is the thing that would
/// break first — silently, as a truncated list.
/// </summary>
[ApiController]
[AllowAnonymous]
[InternalOnly]
[Route("internal/items")]
public sealed class InternalItemNamesController : ControllerBase
{
    /// <summary>A cap, so one caller cannot ask for the whole catalogue at once.</summary>
    private const int MaxIds = 500;

    private readonly TenantContext _tenant;
    private readonly IServiceProvider _services;

    public InternalItemNamesController(TenantContext tenant, IServiceProvider services)
    {
        _tenant = tenant;
        _services = services;
    }

    [HttpPost("names")]
    public async Task<IActionResult> Names(
        [FromBody] NameLookupRequest request, CancellationToken ct)
    {
        // The branch comes from the body, or from the user's token when the
        // caller forwards one. Before TK-06 neither was read: the callers send
        // only the internal key, so the query filter saw no branch and every
        // name came back missing.
        switch (InternalTenant.Apply(_tenant, request.CustomerId, request.OrgId))
        {
            case InternalTenantOutcome.Missing:
                return BadRequest(new MessageResponse
                {
                    Message = "A customer and an organization are required to resolve names.",
                });
            case InternalTenantOutcome.Mismatch:
                return Forbid();
        }

        List<long> ids = [.. request.Ids.Distinct().Take(MaxIds)];

        if (ids.Count == 0)
        {
            return Ok(Array.Empty<NamedRef>());
        }

        // Resolved only now, after the tenant is set: the context takes its
        // connection and its query filter from the tenant when it is built.
        var db = _services.GetRequiredService<InventoryDbContext>();

        List<NamedRef> names = await db.Items
            .Where(i => ids.Contains(i.ItemId))
            .Select(i => new NamedRef(i.ItemId, i.ItemCode, i.ItemName))
            .ToListAsync(ct);

        return Ok(names);
    }

    /// <summary>
    /// Stock-tracked items by code or name, with what is on hand: School's work
    /// orders pick the parts they issue from this (TK-66), since their users
    /// hold no inventory permission.
    /// </summary>
    [HttpPost("search")]
    public async Task<IActionResult> Search([FromBody] StockItemSearchRequest request, CancellationToken ct)
    {
        if (InternalTenant.Apply(_tenant, request.CustomerId, request.OrgId) != InternalTenantOutcome.Applied)
        {
            return BadRequest(new MessageResponse { Message = "A customer and an organization are required." });
        }

        var db = _services.GetRequiredService<InventoryDbContext>();
        string? term = string.IsNullOrWhiteSpace(request.Search) ? null : $"%{request.Search.Trim()}%";

        List<StockItem> items = await db.Items.AsNoTracking()
            .Where(i => i.IsActive && i.TrackInventory
                && (term == null || EF.Functions.ILike(i.ItemName, term) || EF.Functions.ILike(i.ItemCode, term)))
            .OrderBy(i => i.ItemName)
            .Take(50)
            .Select(i => new StockItem
            {
                ItemId = i.ItemId,
                ItemCode = i.ItemCode,
                ItemName = i.ItemName,
                QuantityOnHand = db.ItemStock.Where(s => s.ItemId == i.ItemId).Select(s => s.QuantityOnHand).FirstOrDefault(),
            })
            .ToListAsync(ct);

        return Ok(items);
    }

    /// <summary>The branch's active warehouses, for the same pickers.</summary>
    [HttpPost("warehouses")]
    public async Task<IActionResult> Warehouses([FromBody] StockItemSearchRequest request, CancellationToken ct)
    {
        if (InternalTenant.Apply(_tenant, request.CustomerId, request.OrgId) != InternalTenantOutcome.Applied)
        {
            return BadRequest(new MessageResponse { Message = "A customer and an organization are required." });
        }

        var db = _services.GetRequiredService<InventoryDbContext>();
        return Ok(await db.Warehouses.AsNoTracking()
            .Where(w => w.IsActive)
            .OrderBy(w => w.WarehouseCode)
            .Select(w => new StockWarehouse { WarehouseId = w.WarehouseId, WarehouseCode = w.WarehouseCode, WarehouseName = w.WarehouseName })
            .ToListAsync(ct));
    }
}
