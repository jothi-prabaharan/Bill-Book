using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sales.Entity.Models;
using Sales.Repository.Repositories;
using Shared.Kernel.Apps;
using Shared.Kernel.Internal;

namespace Sales.Api.Controllers;

/// <summary>
/// Floors and tables (TK-145). Answers only while tables are switched on in
/// Settings › Restaurant; logic is in <see cref="RestaurantTableRepository"/>.
/// </summary>
[ApiController]
[Authorize]
[RequireBusinessType(BusinessType.Restaurant)]
[RequireModulePermission("restaurant")]
[RequireRestaurantFeature(RestaurantFeature.Tables)]
[Route("api/restaurant")]
public sealed class RestaurantTablesController : ControllerBase
{
    private readonly RestaurantTableRepository _tables;

    public RestaurantTablesController(RestaurantTableRepository tables) => _tables = tables;

    [HttpGet("floor-plan")]
    public async Task<IActionResult> FloorPlan([FromQuery] bool includeInactive, CancellationToken ct) =>
        Ok(await _tables.FloorPlanAsync(includeInactive, ct));

    [HttpPost("floors")]
    [PermissionAction("create")]
    public async Task<IActionResult> CreateFloor([FromBody] SaveFloorRequest request, CancellationToken ct) =>
        Respond(await _tables.SaveFloorAsync(null, request, ct), id => StatusCode(StatusCodes.Status201Created, new { floorId = id }));

    [HttpPut("floors/{floorId:long}")]
    public async Task<IActionResult> UpdateFloor(long floorId, [FromBody] SaveFloorRequest request, CancellationToken ct) =>
        Respond(await _tables.SaveFloorAsync(floorId, request, ct), _ => NoContent());

    [HttpDelete("floors/{floorId:long}")]
    public async Task<IActionResult> DeleteFloor(long floorId, CancellationToken ct) =>
        Respond(await _tables.DeleteFloorAsync(floorId, ct), _ => NoContent());

    [HttpPost("tables")]
    [PermissionAction("create")]
    public async Task<IActionResult> CreateTable([FromBody] SaveRestaurantTableRequest request, CancellationToken ct) =>
        Respond(await _tables.SaveTableAsync(null, request, ct), id => StatusCode(StatusCodes.Status201Created, new { restaurantTableId = id }));

    [HttpPut("tables/{tableId:long}")]
    public async Task<IActionResult> UpdateTable(long tableId, [FromBody] SaveRestaurantTableRequest request, CancellationToken ct) =>
        Respond(await _tables.SaveTableAsync(tableId, request, ct), _ => NoContent());

    [HttpDelete("tables/{tableId:long}")]
    public async Task<IActionResult> DeleteTable(long tableId, CancellationToken ct) =>
        Respond(await _tables.DeleteTableAsync(tableId, ct), _ => NoContent());

    /// <summary>Seat, bill, clear or free a table. Waiters do this all night, so it needs edit, not create.</summary>
    [HttpPut("tables/{tableId:long}/status")]
    public async Task<IActionResult> SetStatus(long tableId, [FromBody] SetTableStatusRequest request, CancellationToken ct) =>
        Respond(await _tables.SetStatusAsync(tableId, request.Status, ct), _ => NoContent());

    [HttpPost("tables/{tableId:long}/move")]
    public async Task<IActionResult> Move(long tableId, [FromBody] MoveTableRequest request, CancellationToken ct) =>
        Respond(await _tables.MoveAsync(tableId, request.ToTableId, ct), _ => NoContent());

    [HttpPost("tables/{tableId:long}/merge")]
    public async Task<IActionResult> Merge(long tableId, [FromBody] MergeTablesRequest request, CancellationToken ct) =>
        Respond(await _tables.MergeAsync(tableId, request.TableIds, ct), _ => NoContent());

    [HttpPost("tables/{tableId:long}/split")]
    public async Task<IActionResult> Split(long tableId, CancellationToken ct) =>
        Respond(await _tables.SplitAsync(tableId, ct), _ => NoContent());

    private IActionResult Respond<T>(TableResult<T> result, Func<T, IActionResult> onOk) => result.Outcome switch
    {
        TableOutcome.Ok => onOk(result.Value!),
        TableOutcome.NotFound => NotFound(),
        _ => BadRequest(new MessageResponse { Message = result.Message! }),
    };
}
