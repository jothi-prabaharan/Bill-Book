using Inventory.Api.Services;
using Inventory.Entity.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Kernel.Ordering;

namespace Inventory.Api.Controllers;

/// <summary>
/// The item master. Saves as an aggregate — item, its vertical extension and its
/// barcodes in one payload — because the extension is only meaningful alongside
/// the profile that names it.
/// </summary>
[ApiController]
[Authorize]
[Route("api/items")]
public sealed class ItemsController : ControllerBase
{
    private readonly ItemService _items;

    public ItemsController(ItemService items) => _items = items;

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? search,
        [FromQuery] string? profile,
        [FromQuery] long? categoryId,
        [FromQuery] bool includeInactive,
        CancellationToken ct) =>
        Ok(await _items.ListAsync(search, profile, categoryId, includeInactive, ct));

    [HttpGet("{itemId:long}")]
    public async Task<IActionResult> Get(long itemId, CancellationToken ct)
    {
        ItemDetail? detail = await _items.GetAsync(itemId, ct);
        return detail is null ? NotFound() : Ok(detail);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SaveItemRequest request, CancellationToken ct)
    {
        SaveItemResult result = await _items.CreateAsync(request, ct);
        return InventoryResponses.From(this, result.Outcome, () => CreatedAtAction(
            nameof(Get),
            new { itemId = result.ItemId },
            new { itemId = result.ItemId, itemCode = result.ItemCode }));
    }

    [HttpPut("{itemId:long}")]
    public async Task<IActionResult> Update(
        long itemId, [FromBody] SaveItemRequest request, CancellationToken ct)
    {
        SaveItemResult result = await _items.UpdateAsync(itemId, request, ct);
        return InventoryResponses.From(this, result.Outcome, NoContent);
    }

    [HttpDelete("{itemId:long}")]
    public async Task<IActionResult> Deactivate(long itemId, CancellationToken ct) =>
        InventoryResponses.From(this, await _items.DeactivateAsync(itemId, ct), NoContent);

    [HttpPatch("reorder")]
    public async Task<IActionResult> Reorder([FromBody] ReorderRequest request, CancellationToken ct) =>
        InventoryResponses.From(this, await _items.ReorderAsync(request, ct), NoContent);
}
