using Accounting.Api.Services;
using Accounting.Entity.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Kernel.Internal;
using Shared.Kernel.Apps;

namespace Accounting.Api.Controllers;

/// <summary>
/// The fixed asset register. Every rule lives in <see cref="FixedAssetService"/>
/// and <see cref="DepreciationService"/>; this only maps their outcomes.
/// </summary>
[ApiController]
[Route("api/accounting/fixed-assets")]
[Authorize]
[RequireModulePermission("accounting")]
[RequireApp(App.RetailErp)]
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
        return Respond(result, () => Ok(new { fixedAssetId = result.FixedAssetId, journalId = result.JournalId }));
    }

    /// <summary>
    /// Puts an asset bought on a bill on the register by hand, for a bill posted
    /// before bills did it themselves, and reclassifies its cost out of the
    /// shared Fixed Asset account; see <see cref="FixedAssetService.CapitalizeAsync"/>.
    /// </summary>
    [HttpPost("capitalize")]
    public async Task<IActionResult> CapitalizeAsset([FromBody] CapitalizeAssetRequest request, CancellationToken ct)
    {
        FixedAssetResult result = await _assets.CapitalizeAsync(request, ct);
        return Respond(result, () => Ok(new { fixedAssetId = result.FixedAssetId, journalId = result.JournalId }));
    }

    /// <summary>
    /// Replaces an asset's depreciation schedules — how an asset a bill put on
    /// the register gets its life. Refused once depreciation has been charged.
    /// </summary>
    [HttpPut("{id:long}/schedules")]
    public async Task<IActionResult> SetSchedules(
        long id, [FromBody] SetDepreciationSchedulesRequest request, CancellationToken ct) =>
        Respond(await _assets.SetSchedulesAsync(id, request, ct), NoContent);

    /// <summary>
    /// Retires an asset. <c>approve</c>, not <c>create</c>: taking an asset off
    /// the books is a sign-off, the same separation a journal's post has.
    /// Another branch's asset answers 404 — see
    /// <see cref="FixedAssetService.DisposeAsync"/>.
    /// </summary>
    [HttpPost("{id:long}/dispose")]
    [PermissionAction("approve")]
    public async Task<IActionResult> DisposeAsset(long id, [FromBody] DisposeAssetRequest request, CancellationToken ct) =>
        Respond(await _assets.DisposeAsync(id, request, ct), NoContent);

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

    /// <summary>The outcome as a response. Public and static so the mapping is tested without a request.</summary>
    public static IActionResult Map(FixedAssetResult result, Func<IActionResult> onOk) =>
        result.Outcome switch
        {
            FixedAssetOutcome.Ok => onOk(),
            FixedAssetOutcome.NotFound => new NotFoundResult(),
            FixedAssetOutcome.CategoryMissing => Bad("Choose one of this branch's asset categories."),
            FixedAssetOutcome.DuplicateCode => Bad("Another asset in this branch already uses that code."),
            FixedAssetOutcome.InvalidSchedule => Bad(
                "Each schedule needs a way to charge: straight line takes a useful life "
                    + "or a rate, written-down value a rate below 100, salvage cannot exceed cost, "
                    + "and an asset has at most one books and one tax schedule."),
            FixedAssetOutcome.NotActive => Bad("Only an asset in service can be disposed of."),
            FixedAssetOutcome.DisposalBeforePurchase => Bad(
                "An asset cannot be disposed of before the date it was bought."),
            FixedAssetOutcome.AlreadyCapitalised => Bad(
                "That bill already put its assets on the register. Capitalising again would "
                    + "count their cost twice."),
            FixedAssetOutcome.ProceedsDestinationRequired => Bad(
                "Say where the sale proceeds went: the bank or cash account they were paid into, "
                    + "or the sales invoice raised to the buyer — one of the two."),
            FixedAssetOutcome.ProceedsAccountMissing => Bad(
                "Choose one of this branch's bank or cash accounts for the proceeds."),
            FixedAssetOutcome.InvoiceNotPosted => Bad(
                "That sales invoice has no posted sale in this branch's books. Post it first."),
            FixedAssetOutcome.SchedulesInUse => Bad(
                "Depreciation has already been charged on this asset's schedules, so they "
                    + "can no longer be changed."),

            // 409, as the journal screen answers: the entry may be perfectly
            // good, and a later date will post it.
            FixedAssetOutcome.PeriodClosed => new ConflictObjectResult(new MessageResponse
            {
                Message = result.Detail ?? "The books are closed for that date.",
            }),
            FixedAssetOutcome.SystemAccountMissing or FixedAssetOutcome.PostingRefused =>
                new ConflictObjectResult(new MessageResponse
                {
                    Message = result.Detail ?? "The ledger refused the entry, so nothing was saved.",
                }),
            _ => new StatusCodeResult(StatusCodes.Status500InternalServerError),
        };

    private IActionResult Respond(FixedAssetResult result, Func<IActionResult> onOk) => Map(result, onOk);

    private static BadRequestObjectResult Bad(string message) =>
        new(new MessageResponse { Message = message });
}
