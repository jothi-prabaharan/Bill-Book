using Inventory.Api.Services;
using Inventory.Entity.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Inventory.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/warehouses")]
public sealed class WarehousesController : ControllerBase
{
    private readonly WarehouseService _warehouses;

    public WarehousesController(WarehouseService warehouses) => _warehouses = warehouses;

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] bool includeInactive, CancellationToken ct) =>
        Ok(await _warehouses.ListAsync(includeInactive, ct));

    [HttpGet("{warehouseId:long}")]
    public async Task<IActionResult> Get(long warehouseId, CancellationToken ct)
    {
        WarehouseListItem? row = await _warehouses.GetAsync(warehouseId, ct);
        return row is null ? NotFound() : Ok(row);
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] SaveWarehouseRequest request, CancellationToken ct) =>
        InventoryResponses.From(this, await _warehouses.CreateAsync(request, ct),
            () => StatusCode(StatusCodes.Status201Created));

    [HttpPut("{warehouseId:long}")]
    public async Task<IActionResult> Update(
        long warehouseId, [FromBody] SaveWarehouseRequest request, CancellationToken ct) =>
        InventoryResponses.From(this, await _warehouses.UpdateAsync(warehouseId, request, ct), NoContent);

    [HttpPut("{warehouseId:long}/default")]
    public async Task<IActionResult> SetDefault(long warehouseId, CancellationToken ct) =>
        InventoryResponses.From(this, await _warehouses.SetDefaultAsync(warehouseId, ct), NoContent);

    [HttpDelete("{warehouseId:long}")]
    public async Task<IActionResult> Deactivate(long warehouseId, CancellationToken ct) =>
        InventoryResponses.From(this, await _warehouses.DeactivateAsync(warehouseId, ct), NoContent);

    [HttpPatch("reorder")]
    public async Task<IActionResult> Reorder([FromBody] ReorderRequest request, CancellationToken ct) =>
        InventoryResponses.From(this, await _warehouses.ReorderAsync(request, ct), NoContent);
}
