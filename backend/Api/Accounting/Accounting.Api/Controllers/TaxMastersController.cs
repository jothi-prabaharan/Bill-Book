using Accounting.Api.Services;
using Accounting.Entity.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Accounting.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/tax-masters")]
public sealed class TaxMastersController : ControllerBase
{
    private readonly TaxMasterService _taxes;

    public TaxMastersController(TaxMasterService taxes) => _taxes = taxes;

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] bool includeHistory,
        [FromQuery] bool includeInactive,
        CancellationToken ct) =>
        Ok(await _taxes.ListAsync(includeHistory, includeInactive, ct));

    [HttpGet("{taxMasterId:long}")]
    public async Task<IActionResult> Get(long taxMasterId, CancellationToken ct)
    {
        TaxMasterListItem? rate = await _taxes.GetAsync(taxMasterId, ct);
        return rate is null ? NotFound() : Ok(rate);
    }

    /// <summary>The rate a document dated onDate must use — never today's rate.</summary>
    [HttpGet("resolve/{taxGroupId:long}")]
    public async Task<IActionResult> Resolve(
        long taxGroupId, [FromQuery] DateOnly onDate, CancellationToken ct)
    {
        TaxMasterListItem? rate = await _taxes.ResolveAsync(taxGroupId, onDate, ct);
        return rate is null ? NotFound() : Ok(rate);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SaveTaxMasterRequest request, CancellationToken ct)
    {
        SaveTaxResult result = await _taxes.CreateAsync(request, ct);
        return Respond(result, () => CreatedAtAction(
            nameof(Get), new { taxMasterId = result.TaxMasterId }, new { taxMasterId = result.TaxMasterId }));
    }

    /// <summary>
    /// Supersedes a rate: closes the current version and inserts a successor.
    /// Rates are never edited in place — historical documents must keep
    /// resolving the rate that applied on their own date.
    /// </summary>
    [HttpPost("{taxMasterId:long}/revise")]
    public async Task<IActionResult> Revise(
        long taxMasterId, [FromBody] SaveTaxMasterRequest request, CancellationToken ct)
    {
        SaveTaxResult result = await _taxes.ReviseAsync(taxMasterId, request, ct);
        return Respond(result, () => Ok(new { taxMasterId = result.TaxMasterId }));
    }

    /// <summary>Display name only — allowed on seeded rates, whose split stays fixed.</summary>
    [HttpPut("{taxMasterId:long}/name")]
    public async Task<IActionResult> Rename(
        long taxMasterId, [FromBody] RenameTaxRequest request, CancellationToken ct)
    {
        SaveTaxOutcome outcome = await _taxes.RenameAsync(taxMasterId, request.TaxName, ct);
        return outcome == SaveTaxOutcome.Ok ? NoContent() : NotFound();
    }

    [HttpDelete("{taxMasterId:long}")]
    public async Task<IActionResult> Deactivate(long taxMasterId, CancellationToken ct)
    {
        SaveTaxOutcome outcome = await _taxes.DeactivateAsync(taxMasterId, ct);
        return outcome == SaveTaxOutcome.Ok ? NoContent() : NotFound();
    }

    private IActionResult Respond(SaveTaxResult result, Func<IActionResult> onOk) =>
        result.Outcome switch
        {
            SaveTaxOutcome.Ok => onOk(),
            SaveTaxOutcome.NotFound => NotFound(),
            SaveTaxOutcome.NoApplicability => BadRequest(new MessageResponse
            {
                Message = "A rate must apply to sales, purchases, or both.",
            }),
            SaveTaxOutcome.NotCurrentVersion => BadRequest(new MessageResponse
            {
                Message = "Only the version currently in force can be revised.",
            }),
            SaveTaxOutcome.EffectiveDateNotAfter => BadRequest(new MessageResponse
            {
                Message = "The new rate must take effect after the version it replaces.",
            }),
            _ => StatusCode(StatusCodes.Status500InternalServerError),
        };
}

public class RenameTaxRequest
{
    public string TaxName { get; set; } = null!;
}
