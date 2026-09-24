using Accounting.Api.Services;
using Accounting.Entity.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Kernel.Internal;
using Shared.Kernel.Ordering;
using Shared.Kernel.Apps;

namespace Accounting.Api.Controllers;

/// <summary>
/// Settings › Numbering series. Owned by Accounting for the same reason Tax
/// Master is: it is configuration several services read, and it needs one home
/// rather than a copy per service.
///
/// <b>Guarded by <c>settings</c>, not <c>accounting</c></b> (TK-44). Every app
/// numbers its documents from this table (<c>EMP</c>, <c>PAY</c>, <c>ADM</c>…),
/// so the page is a shared settings page. The menu already offered it on
/// <c>settings.view</c> while the route and this controller asked for
/// <c>accounting.*</c>, so the two disagreed; they now agree on settings.
/// </summary>
[ApiController]
[Authorize]
[RequireModulePermission("settings")]
[Route("api/numbering-series")]
[RequireApp(App.All)]
public sealed class NumberingSeriesController : ControllerBase
{
    private readonly NumberingSeriesService _series;

    public NumberingSeriesController(NumberingSeriesService series) => _series = series;

    /// <summary><paramref name="seriesFor"/> filters to Master or Document; omit for both.</summary>
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] bool includeInactive,
        [FromQuery] string? seriesFor,
        CancellationToken ct) =>
        Ok(await _series.ListAsync(includeInactive, seriesFor, ct));

    [HttpGet("{numberingSeriesId:long}")]
    public async Task<IActionResult> Get(long numberingSeriesId, CancellationToken ct)
    {
        NumberingSeriesListItem? row = await _series.GetAsync(numberingSeriesId, ct);
        return row is null ? NotFound() : Ok(row);
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] SaveNumberingSeriesRequest request, CancellationToken ct)
    {
        SaveSeriesResult result = await _series.CreateAsync(request, ct);
        return Respond(result.Outcome, () => CreatedAtAction(
            nameof(Get),
            new { numberingSeriesId = result.NumberingSeriesId },
            new { numberingSeriesId = result.NumberingSeriesId }));
    }

    [HttpPut("{numberingSeriesId:long}")]
    public async Task<IActionResult> Update(
        long numberingSeriesId,
        [FromBody] SaveNumberingSeriesRequest request,
        CancellationToken ct)
    {
        SaveSeriesResult result = await _series.UpdateAsync(numberingSeriesId, request, ct);
        return Respond(result.Outcome, NoContent);
    }

    /// <summary>
    /// Moves the counter. Separate from the format edit: this is the one action
    /// that can make the next generated code collide with a code already issued.
    /// </summary>
    [HttpPut("{numberingSeriesId:long}/next-number")]
    public async Task<IActionResult> SetNextNumber(
        long numberingSeriesId, [FromBody] SetNextNumberRequest request, CancellationToken ct)
    {
        SetNextNumberResult result = await _series.SetNextNumberAsync(
            numberingSeriesId, request.NextNumber, ct);

        return Respond(result.Outcome, () => result.Lowered
            ? Ok(new MessageResponse
            {
                Message = "Counter moved back. Numbers between the new value and the old one may "
                    + "already be in use, and the next save will be refused if one collides.",
            })
            : NoContent());
    }

    [HttpPut("{numberingSeriesId:long}/default")]
    public async Task<IActionResult> SetDefault(long numberingSeriesId, CancellationToken ct) =>
        Respond(await _series.SetDefaultAsync(numberingSeriesId, ct), NoContent);

    [HttpPatch("reorder")]
    public async Task<IActionResult> Reorder([FromBody] ReorderRequest request, CancellationToken ct) =>
        Respond(await _series.ReorderAsync(request, ct), NoContent);

    [HttpDelete("{numberingSeriesId:long}")]
    public async Task<IActionResult> Deactivate(long numberingSeriesId, CancellationToken ct) =>
        Respond(await _series.DeactivateAsync(numberingSeriesId, ct), NoContent);

    private IActionResult Respond(SaveSeriesOutcome outcome, Func<IActionResult> onOk) =>
        outcome switch
        {
            SaveSeriesOutcome.Ok => onOk(),
            SaveSeriesOutcome.NotFound => NotFound(),
            SaveSeriesOutcome.DuplicateName => BadRequest(new MessageResponse
            {
                Message = "Another series already uses that name.",
            }),
            SaveSeriesOutcome.SeriesCodeLocked => BadRequest(new MessageResponse
            {
                Message = "The code of a built-in series cannot be changed — it is what the rest "
                    + "of the system looks the series up by. The name and format are editable.",
            }),
            SaveSeriesOutcome.ManualOverrideOnDocument => BadRequest(new MessageResponse
            {
                Message = "Document numbers must run consecutively, so a manual override cannot "
                    + "be allowed on a document series.",
            }),
            SaveSeriesOutcome.BranchCodeRequired => BadRequest(new MessageResponse
            {
                Message = "Enter a branch code, or turn off including it in the number.",
            }),
            SaveSeriesOutcome.NextNumberBelowStart => BadRequest(new MessageResponse
            {
                Message = "The next number cannot be lower than the series start number.",
            }),
            SaveSeriesOutcome.LastActiveSeries => BadRequest(new MessageResponse
            {
                Message = "This is the only active series for its code. Add another before "
                    + "deactivating this one, or records using it cannot be saved.",
            }),
            SaveSeriesOutcome.InvalidValue => BadRequest(new MessageResponse
            {
                Message = "One of the selected options is not a recognised value.",
            }),
            _ => StatusCode(StatusCodes.Status500InternalServerError),
        };
}
