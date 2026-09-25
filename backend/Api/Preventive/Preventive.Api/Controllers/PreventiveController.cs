using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Preventive.Api.Services;
using Preventive.Entity.Enums;
using Preventive.Entity.Models;
using Shared.Kernel.Apps;
using Shared.Kernel.Internal;

namespace Preventive.Api.Controllers;

/// <summary>
/// Preventive maintenance plans and their occurrences (S7, TK-67). Reading takes
/// <c>preventive.view</c>, adding a plan <c>preventive.create</c>, and changing a
/// plan, skipping an occurrence or generating now <c>preventive.edit</c>.
/// </summary>
[ApiController]
[Authorize]
[RequireModulePermission("preventive")]
[RequireApp(App.School)]
[Route("api/preventive")]
public sealed class PreventiveController : ControllerBase
{
    private readonly PreventiveService _plans;
    private readonly TimeProvider _clock;

    public PreventiveController(PreventiveService plans, TimeProvider clock)
    {
        _plans = plans;
        _clock = clock;
    }

    [HttpGet("plans")]
    public async Task<IActionResult> Plans(CancellationToken ct) => Ok(await _plans.PlansAsync(ct));

    [HttpPost("plans")]
    public async Task<IActionResult> CreatePlan([FromBody] SavePlanRequest request, CancellationToken ct) =>
        Answer(await _plans.SavePlanAsync(null, request, ct));

    [HttpPut("plans/{id:long}")]
    public async Task<IActionResult> UpdatePlan(long id, [FromBody] SavePlanRequest request, CancellationToken ct) =>
        Answer(await _plans.SavePlanAsync(id, request, ct));

    [HttpGet("occurrences")]
    public async Task<IActionResult> Occurrences([FromQuery] long? planId, [FromQuery] OccurrenceStatus? status, CancellationToken ct) =>
        Ok(await _plans.OccurrencesAsync(planId, status, ct));

    [HttpPut("occurrences/{id:long}")]
    public async Task<IActionResult> SetOccurrence(long id, [FromBody] OccurrenceActionRequest request, CancellationToken ct) =>
        Answer(await _plans.SetOccurrenceAsync(id, request.OccurrenceStatus, ct));

    /// <summary>Runs this branch's generation now rather than waiting for the hourly run. Safe to press twice.</summary>
    [HttpPost("generate")]
    [PermissionAction("edit")]
    public async Task<IActionResult> Generate(CancellationToken ct) =>
        Ok(await _plans.GenerateAsync(DateOnly.FromDateTime(_clock.GetLocalNow().DateTime), ct));

    // A row outside the caller's branch is not found: the query filter and RLS hide it.
    private IActionResult Answer(PreventiveResult result) => result.Outcome switch
    {
        PreventiveOutcome.Ok => Ok(new { id = result.Id }),
        PreventiveOutcome.NotFound => NotFound(),
        PreventiveOutcome.StateRule => Conflict(new PreventiveMessage(result.Detail!)),
        PreventiveOutcome.Unavailable => StatusCode(StatusCodes.Status503ServiceUnavailable,
            new PreventiveMessage("A service this needs is not answering. Try again in a moment.")),
        _ => UnprocessableEntity(new PreventiveMessage(result.Detail ?? "That change is not allowed.")),
    };
}
