using Accounting.Api.Services;
using Accounting.Entity.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Kernel.Internal;

namespace Accounting.Api.Controllers;

/// <summary>
/// The fixed asset register. Every rule lives in <see cref="FixedAssetService"/>
/// and <see cref="DepreciationService"/>; this only maps their outcomes.
/// </summary>
[ApiController]
[Route("api/accounting/fixed-assets")]
[Authorize]
[RequireModulePermission("accounting")]
public sealed class FixedAssetsController : ControllerBase
{
    private readonly FixedAssetService _assets;
    private readonly DepreciationService _depreciation;

    public FixedAssetsController(FixedAssetService assets, DepreciationService depreciation)
    {
        _assets = assets;
        _depreciation = depreciation;
    }

    [HttpGet]
    public async Task<IActionResult> GetFixedAssets(CancellationToken ct) =>
        Ok(await _assets.ListAsync(ct));

    [HttpPost]
    public async Task<IActionResult> RegisterAsset([FromBody] CreateFixedAssetRequest request, CancellationToken ct)
    {
        FixedAssetResult result = await _assets.RegisterAsync(request, ct);
        return Respond(result.Outcome, () => Ok(new { fixedAssetId = result.FixedAssetId }));
    }

    /// <summary>
    /// Puts an asset bought on a bill on the register. It posts nothing — the
    /// bill already did; see <see cref="FixedAssetService.CapitalizeAsync"/>.
    /// </summary>
    [HttpPost("capitalize")]
    public async Task<IActionResult> CapitalizeAsset([FromBody] CapitalizeAssetRequest request, CancellationToken ct)
    {
        FixedAssetResult result = await _assets.CapitalizeAsync(request, ct);
        return Respond(result.Outcome, () => Ok(new { fixedAssetId = result.FixedAssetId }));
    }

    /// <summary>
    /// Retires an asset. <c>approve</c>, not <c>create</c>: taking an asset off
    /// the books is a sign-off, the same separation a journal's post has.
    /// Another branch's asset answers 404 — see
    /// <see cref="FixedAssetService.DisposeAsync"/>.
    /// </summary>
    [HttpPost("{id:long}/dispose")]
    [PermissionAction("approve")]
    public async Task<IActionResult> DisposeAsset(long id, [FromBody] DisposeAssetRequest request, CancellationToken ct) =>
        Respond((await _assets.DisposeAsync(id, request, ct)).Outcome, NoContent);

    /// <summary>
    /// Charges the month <paramref name="runDate"/> falls in. Safe to repeat: a
    /// second run for the same month charges nothing and still answers 200.
    /// </summary>
    [HttpPost("depreciation-run")]
    [PermissionAction("approve")]
    public async Task<IActionResult> RunDepreciation([FromQuery] DateOnly runDate, CancellationToken ct)
    {
        DepreciationRunResult result = await _depreciation.RunDepreciationAsync(runDate, ct);

        if (result.Outcome == DepreciationRunOutcome.Ok)
        {
            return Ok(new { assetsCharged = result.AssetsCharged, journalId = result.JournalId });
        }

        // 409, as the journal screen answers: the run may be perfectly good,
        // and a later date will post it.
        if (result.JournalOutcome == SaveJournalOutcome.PeriodClosed)
        {
            return Conflict(new MessageResponse
            {
                Message = result.Detail ?? "The books are closed for that date.",
            });
        }

        return BadRequest(new MessageResponse
        {
            Message = result.Detail
                ?? "The depreciation journal was refused, so nothing was charged. "
                    + "Check that each category's accounts are open for posting.",
        });
    }

    private IActionResult Respond(FixedAssetOutcome outcome, Func<IActionResult> onOk) =>
        outcome switch
        {
            FixedAssetOutcome.Ok => onOk(),
            FixedAssetOutcome.NotFound => NotFound(),
            FixedAssetOutcome.CategoryMissing => BadRequest(new MessageResponse
            {
                Message = "Choose one of this branch's asset categories.",
            }),
            FixedAssetOutcome.DuplicateCode => BadRequest(new MessageResponse
            {
                Message = "Another asset in this branch already uses that code.",
            }),
            FixedAssetOutcome.InvalidSchedule => BadRequest(new MessageResponse
            {
                Message = "Each schedule needs a way to charge: straight line takes a useful life "
                    + "or a rate, written-down value a rate below 100, salvage cannot exceed cost, "
                    + "and an asset has at most one books and one tax schedule.",
            }),
            FixedAssetOutcome.NotActive => BadRequest(new MessageResponse
            {
                Message = "Only an asset in service can be disposed of.",
            }),
            FixedAssetOutcome.DisposalBeforePurchase => BadRequest(new MessageResponse
            {
                Message = "An asset cannot be disposed of before the date it was bought.",
            }),
            _ => StatusCode(StatusCodes.Status500InternalServerError),
        };
}
