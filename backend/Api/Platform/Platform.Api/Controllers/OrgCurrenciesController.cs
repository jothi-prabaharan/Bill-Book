using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Platform.Api.Services;
using Platform.Entity.Models;
using Shared.Kernel.Internal;

namespace Platform.Api.Controllers;

[ApiController]
/// <summary>
/// The currencies a branch trades in.
///
/// The organization comes off the URL, and `plt` is the master database — it
/// holds every customer's rows with no query filter and no row-level security
/// to fall back on. The claim check is the entire boundary here, which is why it
/// is an attribute on the controller rather than a line in each action.
/// </summary>
[Authorize]
[OrgRouteMustMatchToken]
[Route("api/organizations/{orgId:guid}/currencies")]
public sealed class OrgCurrenciesController : ControllerBase
{
    private readonly OrgCurrencyService _service;

    public OrgCurrenciesController(OrgCurrencyService service) => _service = service;

    /// <summary>The org's currencies — active only unless includeInactive is set.</summary>
    [HttpGet]
    public async Task<IActionResult> List(
        Guid orgId, [FromQuery] bool includeInactive, CancellationToken ct)
    {
        IReadOnlyList<OrgCurrencyDto> rows = await _service.ListAsync(orgId, includeInactive, ct);
        return Ok(rows);
    }

    /// <summary>Currencies not yet added — the Add dropdown.</summary>
    [HttpGet("available")]
    public async Task<IActionResult> Available(Guid orgId, CancellationToken ct)
    {
        IReadOnlyList<MasterCurrency> rows = await _service.AvailableAsync(orgId, ct);
        return Ok(rows);
    }

    [HttpPost]
    public async Task<IActionResult> Add(
        Guid orgId, [FromBody] AddOrgCurrencyRequest request, CancellationToken ct)
    {
        bool added = await _service.AddAsync(orgId, request.CurrencyId, ct);
        return added
            ? CreatedAtAction(nameof(List), new { orgId }, null)
            : Conflict(new MessageResponse { Message = "That currency is already enabled." });
    }

    [HttpPut("{orgCurrencyId:guid}/active")]
    public async Task<IActionResult> SetActive(
        Guid orgId, Guid orgCurrencyId, [FromBody] SetOrgCurrencyActiveRequest request, CancellationToken ct)
    {
        SetActiveResult result = await _service.SetActiveAsync(orgId, orgCurrencyId, request.IsActive, ct);
        return result switch
        {
            SetActiveResult.Ok => NoContent(),
            SetActiveResult.NotFound => NotFound(),
            SetActiveResult.BaseCurrencyLocked => BadRequest(new MessageResponse
            {
                Message = "The base currency cannot be deactivated.",
            }),
            _ => StatusCode(StatusCodes.Status500InternalServerError),
        };
    }
}

/// <summary>Shared simple message payload.</summary>
public class MessageResponse
{
    public string Message { get; set; } = null!;
}
