using Accounting.Api.Services;
using Accounting.Entity.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Kernel.Internal;

namespace Accounting.Api.Controllers;

/// <summary>
/// Accounting › Opening balances. Where a branch's books begin.
///
/// <b>No id anywhere in these routes.</b> A branch has exactly one opening
/// balance — one moment it started trading, that every balance since is measured
/// from — so there is nothing to identify. A route taking an id would imply
/// otherwise, and the first thing someone would do is create a second.
///
/// <b>No delete after finalize, and no reverse at all.</b> Reversing an opening
/// balance does not restate a transaction, it deletes the ground everything
/// stands on. An error found afterwards is corrected with a journal entry.
/// </summary>
[ApiController]
[Authorize]
[RequireModulePermission("accounting")]
[Route("api/opening-balance")]
public sealed class OpeningBalancesController : ControllerBase
{
    private readonly OpeningBalanceService _openings;

    public OpeningBalancesController(OpeningBalanceService openings) => _openings = openings;

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        OpeningBalanceView? document = await _openings.GetAsync(ct);
        return document is null ? NotFound() : Ok(document);
    }

    /// <summary>
    /// Whether it may be finalized, and what is stopping it. Read continuously by
    /// the screen rather than only when Finalize is pressed — a migration is keyed
    /// over weeks, and finding at the end that it is out by ₹4,300 means auditing
    /// all of it instead of the line just typed.
    /// </summary>
    [HttpGet("readiness")]
    public async Task<IActionResult> Readiness(CancellationToken ct)
    {
        OpeningBalanceReadinessView? readiness = await _openings.ReadinessAsync(ct);
        return readiness is null ? NotFound() : Ok(readiness);
    }

    /// <summary>
    /// Every control account against the subledger beneath it, as at go-live.
    /// Read after finalize: the blockers say the migration <i>may</i> open, this
    /// says it landed. Double-entry cannot answer it — a receivable posted with
    /// no sub-account balances perfectly while nobody owes the money.
    /// </summary>
    [HttpGet("tie")]
    public async Task<IActionResult> Tie(CancellationToken ct)
    {
        SubLedgerTieView? tie = await _openings.TieAsync(ct);
        return tie is null ? NotFound() : Ok(tie);
    }

    /// <summary>
    /// Creates the branch's opening balance or replaces the draft. A PUT rather
    /// than a POST because there is one of them: saving twice does not make two.
    /// </summary>
    [HttpPut]
    public async Task<IActionResult> Save(
        [FromBody] SaveOpeningBalanceRequest request, CancellationToken ct) =>
        Respond(await _openings.SaveAsync(request, ct), NoContent);

    [HttpPost("finalize")]
    public async Task<IActionResult> Finalize(CancellationToken ct) =>
        Respond(await _openings.FinalizeAsync(ct), NoContent);

    [HttpDelete]
    public async Task<IActionResult> Delete(CancellationToken ct) =>
        Respond(await _openings.DeleteAsync(ct), NoContent);

    private IActionResult Respond(OpeningBalanceResult result, Func<IActionResult> onOk) =>
        result.Outcome switch
        {
            OpeningBalanceOutcome.Ok => onOk(),
            OpeningBalanceOutcome.NotFound => NotFound(),

            // 409, not 400: the request is well-formed and the document is real.
            // What is wrong is the state it is in, which no amount of editing the
            // request will change.
            OpeningBalanceOutcome.AlreadyFinalized or OpeningBalanceOutcome.AlreadyExists =>
                Conflict(new MessageResponse
                {
                    Message = result.Detail ?? "The books have already opened.",
                }),

            // Also 409: nothing is malformed, the figures simply do not tie yet.
            OpeningBalanceOutcome.NotReady or OpeningBalanceOutcome.SubLedgerMissing =>
                Conflict(new MessageResponse
                {
                    Message = result.Detail ?? "The opening balance does not tie yet.",
                }),

            // Transient, both of them. Nothing was posted and the caller should
            // come back rather than treat the document as refused.
            OpeningBalanceOutcome.InventoryUnavailable
                or OpeningBalanceOutcome.BaseCurrencyUnavailable => StatusCode(
                    StatusCodes.Status503ServiceUnavailable,
                    new MessageResponse { Message = result.Detail ?? "Please try again." }),

            _ => BadRequest(new MessageResponse
            {
                Message = result.Detail ?? $"The opening balance was refused: {result.Outcome}.",
            }),
        };
}
