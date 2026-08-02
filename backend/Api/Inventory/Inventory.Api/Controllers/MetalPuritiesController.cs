using Inventory.Api.Services;
using Inventory.Entity.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Kernel.Internal;
using Shared.Kernel.Ordering;

namespace Inventory.Api.Controllers;

[ApiController]
[Authorize]
[RequireModulePermission("inventory")]
[Route("api/metal-purities")]
public sealed class MetalPuritiesController : ControllerBase
{
    private readonly MetalPurityService _purities;

    public MetalPuritiesController(MetalPurityService purities) => _purities = purities;

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? metalType, [FromQuery] bool includeInactive, CancellationToken ct) =>
        Ok(await _purities.ListAsync(metalType, includeInactive, ct));

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] SaveMetalPurityRequest request, CancellationToken ct) =>
        InventoryResponses.From(this, await _purities.CreateAsync(request, ct),
            () => StatusCode(StatusCodes.Status201Created));

    [HttpPut("{purityId:long}")]
    public async Task<IActionResult> Update(
        long purityId, [FromBody] SaveMetalPurityRequest request, CancellationToken ct) =>
        InventoryResponses.From(this, await _purities.UpdateAsync(purityId, request, ct), NoContent);

    [HttpDelete("{purityId:long}")]
    public async Task<IActionResult> Delete(long purityId, CancellationToken ct) =>
        InventoryResponses.From(this, await _purities.DeleteAsync(purityId, ct), NoContent);

    [HttpPatch("reorder")]
    public async Task<IActionResult> Reorder([FromBody] ReorderRequest request, CancellationToken ct) =>
        InventoryResponses.From(this, await _purities.ReorderAsync(request, ct), NoContent);
}
