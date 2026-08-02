using Inventory.Api.Services;
using Inventory.Entity.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Kernel.Ordering;

namespace Inventory.Api.Controllers;

/// <summary>
/// Settings › Inventory › Unit types. Types and their units are served and saved
/// through one controller because the screen edits them as one thing.
/// </summary>
[ApiController]
[Authorize]
[Route("api/uom-types")]
public sealed class UomController : ControllerBase
{
    private readonly UomService _uom;

    public UomController(UomService uom) => _uom = uom;

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] bool includeInactive, CancellationToken ct) =>
        Ok(await _uom.ListAsync(includeInactive, ct));

    [HttpPost]
    public async Task<IActionResult> CreateType(
        [FromBody] SaveUomTypeRequest request, CancellationToken ct) =>
        InventoryResponses.From(this, await _uom.CreateTypeAsync(request, ct),
            () => StatusCode(StatusCodes.Status201Created));

    [HttpPut("{uomTypeId:long}")]
    public async Task<IActionResult> UpdateType(
        long uomTypeId, [FromBody] SaveUomTypeRequest request, CancellationToken ct) =>
        InventoryResponses.From(this, await _uom.UpdateTypeAsync(uomTypeId, request, ct), NoContent);

    [HttpDelete("{uomTypeId:long}")]
    public async Task<IActionResult> DeleteType(long uomTypeId, CancellationToken ct) =>
        InventoryResponses.From(this, await _uom.DeleteTypeAsync(uomTypeId, ct), NoContent);

    [HttpPatch("reorder")]
    public async Task<IActionResult> ReorderTypes(
        [FromBody] ReorderRequest request, CancellationToken ct) =>
        InventoryResponses.From(this, await _uom.ReorderTypesAsync(request, ct), NoContent);

    [HttpPost("units")]
    public async Task<IActionResult> CreateUnit(
        [FromBody] SaveUnitOfMeasureRequest request, CancellationToken ct) =>
        InventoryResponses.From(this, await _uom.CreateUnitAsync(request, ct),
            () => StatusCode(StatusCodes.Status201Created));

    [HttpPut("units/{uomId:long}")]
    public async Task<IActionResult> UpdateUnit(
        long uomId, [FromBody] SaveUnitOfMeasureRequest request, CancellationToken ct) =>
        InventoryResponses.From(this, await _uom.UpdateUnitAsync(uomId, request, ct), NoContent);

    /// <summary>
    /// Moves the base unit of a type, rescaling every factor in it. Its own
    /// action because it rewrites the whole type rather than one row.
    /// </summary>
    [HttpPut("units/{uomId:long}/base")]
    public async Task<IActionResult> SetBaseUnit(long uomId, CancellationToken ct) =>
        InventoryResponses.From(this, await _uom.SetBaseUnitAsync(uomId, ct), NoContent);

    [HttpDelete("units/{uomId:long}")]
    public async Task<IActionResult> DeleteUnit(long uomId, CancellationToken ct) =>
        InventoryResponses.From(this, await _uom.DeleteUnitAsync(uomId, ct), NoContent);
}
