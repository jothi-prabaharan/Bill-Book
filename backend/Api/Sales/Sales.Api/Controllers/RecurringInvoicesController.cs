using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sales.Entity.Models;
using Sales.Repository.Repositories;
using Shared.Kernel.Apps;
using Shared.Kernel.Internal;

namespace Sales.Api.Controllers;

/// <summary>
/// Recurring invoice schedules (TK-139). A free add-on: any branch whose
/// business type allows it can switch it on.
/// </summary>
[ApiController]
[Authorize]
[RequireModulePermission("sales")]
[RequireAddOn(AddOn.RecurringBilling)]
[Route("api/sales/recurring-invoices")]
public sealed class RecurringInvoicesController : ControllerBase
{
    private readonly RecurringBillingRepository _schedules;

    public RecurringInvoicesController(RecurringBillingRepository schedules) => _schedules = schedules;

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) => Ok(await _schedules.ListAsync(ct));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Get(long id, CancellationToken ct) =>
        await _schedules.GetAsync(id, ct) is RecurringInvoiceScheduleView view ? Ok(view) : NotFound();

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SaveRecurringInvoiceScheduleRequest request, CancellationToken ct) =>
        Map(await _schedules.CreateAsync(request, ct));

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, [FromBody] SaveRecurringInvoiceScheduleRequest request, CancellationToken ct) =>
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
        RecurringScheduleOutcome.Ok => Ok(new { recurringInvoiceScheduleId = result.ScheduleId }),
        RecurringScheduleOutcome.NotFound => NotFound(),
        RecurringScheduleOutcome.Conflict => Conflict(new MessageResponse { Message = result.Detail ?? "The schedule cannot change that way." }),
        _ => BadRequest(new MessageResponse { Message = result.Detail ?? "The schedule was refused." }),
    };
}
