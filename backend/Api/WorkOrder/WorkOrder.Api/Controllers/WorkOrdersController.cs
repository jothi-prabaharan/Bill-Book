using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Kernel.Apps;
using Shared.Kernel.Internal;
using WorkOrder.Api.Services;
using WorkOrder.Entity.Enums;
using WorkOrder.Entity.Models;

namespace WorkOrder.Api.Controllers;

/// <summary>
/// Work orders (S6, TK-66). Reading takes <c>workorder.view</c>, raising
/// <c>workorder.create</c>, editing, moving, ticking and issuing parts
/// <c>workorder.edit</c>, and closing a completed one <c>workorder.close</c> —
/// the sign-off a principal keeps when maintenance staff do the rest.
/// </summary>
[ApiController]
[Authorize]
[RequireModulePermission("workorder")]
[RequireApp(App.School)]
[Route("api/work-orders")]
public sealed class WorkOrdersController : ControllerBase
{
    private readonly WorkOrderService _orders;

    public WorkOrdersController(WorkOrderService orders) => _orders = orders;

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] WorkOrderStatus? status, [FromQuery] long? facilityAssetId, CancellationToken ct) =>
        Ok(await _orders.ListAsync(status, facilityAssetId, ct));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Get(long id, CancellationToken ct) =>
        await _orders.GetAsync(id, ct) is WorkOrderView view ? Ok(view) : NotFound();

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SaveWorkOrderRequest request, CancellationToken ct) =>
        Answer(await _orders.SaveAsync(null, request, ct));

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, [FromBody] SaveWorkOrderRequest request, CancellationToken ct) =>
        Answer(await _orders.SaveAsync(id, request, ct));

    /// <summary>Assign, start, hold, resume, complete or cancel. Closing has its own route and permission.</summary>
    [HttpPost("{id:long}/actions")]
    [PermissionAction("edit")]
    public async Task<IActionResult> Act(long id, [FromBody] WorkOrderActionRequest request, CancellationToken ct) =>
        Answer(await _orders.ActAsync(id, request, mayClose: false, ct));

    [HttpPost("{id:long}/close")]
    [PermissionAction("close")]
    public async Task<IActionResult> Close(long id, CancellationToken ct) =>
        Answer(await _orders.ActAsync(id, new WorkOrderActionRequest { Action = WorkOrderAction.Close }, mayClose: true, ct));

    [HttpPut("{id:long}/tasks/{taskId:long}")]
    public async Task<IActionResult> Tick(long id, long taskId, [FromBody] TaskTickRequest request, CancellationToken ct) =>
        Answer(await _orders.TickAsync(id, taskId, request.IsDone, ct));

    [HttpPost("{id:long}/parts")]
    [PermissionAction("edit")]
    public async Task<IActionResult> IssuePart(long id, [FromBody] IssuePartRequest request, CancellationToken ct) =>
        Answer(await _orders.IssuePartAsync(id, request, ct));

    /// <summary>The branch's stocked items, for the part picker. Inventory answers; nothing is stored here.</summary>
    [HttpGet("stock-items")]
    public async Task<IActionResult> StockItems([FromQuery] string? search, CancellationToken ct) =>
        Ok(await _orders.StockItemsAsync(string.IsNullOrWhiteSpace(search) ? null : search.Trim(), ct));

    [HttpGet("warehouses")]
    public async Task<IActionResult> Warehouses(CancellationToken ct) => Ok(await _orders.WarehousesAsync(ct));

    // A row outside the caller's branch is not found: the query filter and RLS hide it.
    private IActionResult Answer(WorkOrderResult result) => result.Outcome switch
    {
        WorkOrderOutcome.Ok => Ok(new { id = result.Id }),
        WorkOrderOutcome.NotFound => NotFound(),
        WorkOrderOutcome.StockRefused => Conflict(new WorkOrderMessage(result.Detail!)),
        WorkOrderOutcome.StateRule => Conflict(new WorkOrderMessage(result.Detail!)),
        WorkOrderOutcome.Unavailable => StatusCode(StatusCodes.Status503ServiceUnavailable,
            new WorkOrderMessage("A service this needs is not answering. Try again in a moment.")),
        _ => UnprocessableEntity(new WorkOrderMessage(result.Detail ?? "That change is not allowed.")),
    };
}
