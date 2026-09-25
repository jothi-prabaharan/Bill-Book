using Amc.Api.Services;
using Amc.Entity.Enums;
using Amc.Entity.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Kernel.Apps;
using Shared.Kernel.Internal;

namespace Amc.Api.Controllers;

/// <summary>
/// AMC contracts, their covered assets and visits (S8, TK-68). Reading takes
/// <c>amc.view</c>, adding a contract <c>amc.create</c>, and changing,
/// activating, terminating and recording visits <c>amc.edit</c>.
/// </summary>
[ApiController]
[Authorize]
[RequireModulePermission("amc")]
[RequireApp(App.School)]
[Route("api/amc")]
public sealed class AmcController : ControllerBase
{
    private readonly AmcService _amc;

    public AmcController(AmcService amc) => _amc = amc;

    [HttpGet("contracts")]
    public async Task<IActionResult> List([FromQuery] ContractStatus? status, CancellationToken ct) => Ok(await _amc.ListAsync(status, ct));

    [HttpGet("contracts/{id:long}")]
    public async Task<IActionResult> Get(long id, CancellationToken ct) =>
        await _amc.GetAsync(id, ct) is ContractView view ? Ok(view) : NotFound();

    [HttpPost("contracts")]
    public async Task<IActionResult> Create([FromBody] SaveContractRequest request, CancellationToken ct) =>
        Answer(await _amc.SaveAsync(null, request, ct));

    [HttpPut("contracts/{id:long}")]
    public async Task<IActionResult> Update(long id, [FromBody] SaveContractRequest request, CancellationToken ct) =>
        Answer(await _amc.SaveAsync(id, request, ct));

    [HttpPost("contracts/{id:long}/activate")]
    [PermissionAction("edit")]
    public async Task<IActionResult> Activate(long id, CancellationToken ct) => Answer(await _amc.ActivateAsync(id, ct));

    [HttpPost("contracts/{id:long}/terminate")]
    [PermissionAction("edit")]
    public async Task<IActionResult> Terminate(long id, [FromBody] TerminateContractRequest request, CancellationToken ct) =>
        Answer(await _amc.TerminateAsync(id, request.Reason, ct));

    [HttpGet("contracts/{id:long}/visits")]
    public async Task<IActionResult> Visits(long id, CancellationToken ct) => Ok(await _amc.VisitsAsync(id, ct));

    [HttpPost("contracts/{id:long}/visits")]
    [PermissionAction("edit")]
    public async Task<IActionResult> RecordVisit(long id, [FromBody] RecordVisitRequest request, CancellationToken ct) =>
        Answer(await _amc.RecordVisitAsync(id, request, ct));

    // A row outside the caller's branch is not found: the query filter and RLS hide it.
    private IActionResult Answer(AmcResult result) => result.Outcome switch
    {
        AmcOutcome.Ok => Ok(new { id = result.Id }),
        AmcOutcome.NotFound => NotFound(),
        AmcOutcome.StateRule => Conflict(new AmcMessage(result.Detail!)),
        AmcOutcome.Unavailable => StatusCode(StatusCodes.Status503ServiceUnavailable,
            new AmcMessage("A service this needs is not answering. Try again in a moment.")),
        _ => UnprocessableEntity(new AmcMessage(result.Detail ?? "That change is not allowed.")),
    };
}
