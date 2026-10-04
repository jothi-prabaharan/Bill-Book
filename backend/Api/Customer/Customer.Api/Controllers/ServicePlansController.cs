using Customer.Entity.Enums;
using Customer.Entity.Models;
using Customer.Repository.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Kernel.Internal;

namespace Customer.Api.Controllers;

/// <summary>
/// Service plans (TK-140): a customer's recurring service, its completion, the
/// "Services due" list and the staff action that raises a visit.
///
/// Under the <c>support</c> module, like tickets, because a plan is after-sales
/// care and its visit is a ticket (D-48). Any branch with a business type may
/// use it; the reminders go to customers with an email address.
/// </summary>
[ApiController]
[Authorize]
[RequireModulePermission("support")]
[RequireBusinessType]
[Route("api/service-plans")]
public sealed class ServicePlansController : ControllerBase
{
    private readonly ServicePlanRepository _plans;

    public ServicePlansController(ServicePlanRepository plans) => _plans = plans;

    /// <summary>
    /// <paramref name="dueWithinDays"/> narrows to active plans due within that
    /// many days or already late: the dashboard's "Services due".
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] ServicePlanStatus? status, [FromQuery] int? dueWithinDays, [FromQuery] long? contactId, CancellationToken ct) =>
        Ok(await _plans.ListAsync(status, dueWithinDays is int d ? Math.Clamp(d, 0, 365) : null, contactId, ct));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Get(long id, CancellationToken ct) =>
        await _plans.GetAsync(id, ct) is ServicePlanView view ? Ok(view) : NotFound();

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SaveServicePlanRequest request, CancellationToken ct) =>
        Map(await _plans.CreateAsync(request, ct));

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, [FromBody] SaveServicePlanRequest request, CancellationToken ct) =>
        Map(await _plans.UpdateAsync(id, request, ct));

    [HttpPost("{id:long}/complete")]
    [PermissionAction("edit")]
    public async Task<IActionResult> Complete(long id, [FromBody] CompleteServiceRequest request, CancellationToken ct) =>
        Map(await _plans.CompleteAsync(id, request, ct));

    /// <summary>Raises a support ticket for the visit. 200 with the ticket's id.</summary>
    [HttpPost("{id:long}/visit")]
    [PermissionAction("create")]
    public async Task<IActionResult> RaiseVisit(long id, CancellationToken ct) =>
        Map(await _plans.RaiseTicketAsync(id, ct));

    [HttpPost("{id:long}/pause")]
    [PermissionAction("edit")]
    public async Task<IActionResult> Pause(long id, CancellationToken ct) => Map(await _plans.PauseAsync(id, ct));

    [HttpPost("{id:long}/resume")]
    [PermissionAction("edit")]
    public async Task<IActionResult> Resume(long id, CancellationToken ct) => Map(await _plans.ResumeAsync(id, ct));

    [HttpPost("{id:long}/end")]
    [PermissionAction("edit")]
    public async Task<IActionResult> End(long id, CancellationToken ct) => Map(await _plans.EndAsync(id, ct));

    private IActionResult Map(ServicePlanResult result) => result.Outcome switch
    {
        ServicePlanOutcome.Ok => Ok(new { id = result.Id }),
        ServicePlanOutcome.NotFound => NotFound(),
        ServicePlanOutcome.Conflict => Conflict(new { message = result.Detail ?? "That cannot be done now.", id = result.Id }),
        _ => BadRequest(new { message = result.Detail ?? "The plan was refused." }),
    };
}
