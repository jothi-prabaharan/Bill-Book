using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Purchase.Entity.Models;
using Purchase.Repository.Repositories;
using Shared.Kernel.Apps;
using Shared.Kernel.Internal;

namespace Purchase.Api.Controllers;

/// <summary>
/// Recurring bill schedules (TK-139). A free add-on: any branch whose
/// business type allows it can switch it on.
/// </summary>
[ApiController]
[Authorize]
[RequireModulePermission("purchase")]
[RequireAddOn(AddOn.RecurringBilling)]
[Route("api/purchase/recurring-bills")]
public sealed class RecurringBillsController : ControllerBase
{
    private readonly RecurringBillingRepository _schedules;

    public RecurringBillsController(RecurringBillingRepository schedules) => _schedules = schedules;

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) => Ok(await _schedules.ListAsync(ct));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Get(long id, CancellationToken ct) =>
        await _schedules.GetAsync(id, ct) is RecurringBillScheduleView view ? Ok(view) : NotFound();

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SaveRecurringBillScheduleRequest request, CancellationToken ct) =>
        Map(await _schedules.CreateAsync(request, ct));

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, [FromBody] SaveRecurringBillScheduleRequest request, CancellationToken ct) =>
        Map(await _schedules.UpdateAsync(id, request, ct));

    [HttpPost("{id:long}/pause")]
    [PermissionAction("edit")]
    public async Task<IActionResult> Pause(long id, CancellationToken ct) => Map(await _schedules.PauseAsync(id, ct));

    [HttpPost("{id:long}/resume")]
    [PermissionAction("edit")]
    public async Task<IActionResult> Resume(long id, CancellationToken ct) => Map(await _schedules.ResumeAsync(id, ct));

    [HttpPost("{id:long}/end")]
    [PermissionAction("edit")]
    public async Task<IActionResult> End(long id, CancellationToken ct) => Map(await _schedules.EndAsync(id, ct));

    internal IActionResult Map(RecurringScheduleResult result) => result.Outcome switch
    {
        RecurringScheduleOutcome.Ok => Ok(new { recurringBillScheduleId = result.ScheduleId }),
        RecurringScheduleOutcome.NotFound => NotFound(),
        RecurringScheduleOutcome.Conflict => Conflict(new MessageResponse { Message = result.Detail ?? "The schedule cannot change that way." }),
        _ => BadRequest(new MessageResponse { Message = result.Detail ?? "The schedule was refused." }),
    };
}
