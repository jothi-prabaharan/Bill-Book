using Inventory.Repository;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Documents;
using Shared.Kernel.Internal;

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

    private readonly InventoryDbContext _db;

    public InternalItemNamesController(InventoryDbContext db) => _db = db;

    [HttpPost("names")]
    public async Task<IActionResult> Names(
        [FromBody] NameLookupRequest request, CancellationToken ct)
    {
        List<long> ids = [.. request.Ids.Distinct().Take(MaxIds)];

        if (ids.Count == 0)
        {
            return Ok(Array.Empty<NamedRef>());
        }

        List<NamedRef> names = await _db.Items
            .Where(i => ids.Contains(i.ItemId))
            .Select(i => new NamedRef(i.ItemId, i.ItemCode, i.ItemName))
            .ToListAsync(ct);

        return Ok(names);
    }
}
