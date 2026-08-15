using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Purchase.Api.Services;
using Purchase.Entity.Models;
using Shared.Kernel.Internal;

namespace Purchase.Api.Controllers;

/// <summary>
/// The debit note — <c>DBN</c>. The way back out of a purchase.
/// </summary>
[ApiController]
[Authorize]
[RequireModulePermission("purchase")]
[Route("api/purchase/debit-notes")]
public sealed class DebitNotesController : ControllerBase
{
    private readonly DebitNoteService _notes;

    public DebitNotesController(DebitNoteService notes) => _notes = notes;

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) => Ok(await _notes.ListAsync(ct));

    [HttpGet("{debitNoteId:long}")]
    public async Task<IActionResult> Get(long debitNoteId, CancellationToken ct)
    {
        DebitNoteView? note = await _notes.GetAsync(debitNoteId, ct);
        return note is null ? NotFound() : Ok(note);
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] SaveDebitNoteRequest request, CancellationToken ct)
    {
        DebitNoteResult result = await _notes.CreateAsync(request, ct);

        return result.Outcome == DebitNoteOutcome.Ok
            ? CreatedAtAction(nameof(Get), new { debitNoteId = result.DebitNoteId }, result)
            : Respond(result);
    }

    [HttpPut("{debitNoteId:long}")]
    public async Task<IActionResult> Update(
        long debitNoteId, [FromBody] SaveDebitNoteRequest request, CancellationToken ct)
    {
        DebitNoteResult result = await _notes.UpdateAsync(debitNoteId, request, ct);
        return result.Outcome == DebitNoteOutcome.Ok ? NoContent() : Respond(result);
    }

    /// <summary>Sends the goods back when the reason says so, and always reduces what is owed.</summary>
    [HttpPost("{debitNoteId:long}/post")]
    [PermissionAction("approve")]
    public async Task<IActionResult> Post(long debitNoteId, CancellationToken ct)
    {
        DebitNoteResult result = await _notes.PostAsync(debitNoteId, ct);
        return result.Outcome == DebitNoteOutcome.Ok ? NoContent() : Respond(result);
    }

    [HttpPost("{debitNoteId:long}/void")]
    [PermissionAction("void")]
    public async Task<IActionResult> Void(
        long debitNoteId, [FromBody] VoidDebitNoteRequest request, CancellationToken ct)
    {
        DebitNoteResult result = await _notes.VoidAsync(debitNoteId, request, ct);
        return result.Outcome == DebitNoteOutcome.Ok ? NoContent() : Respond(result);
    }

    private IActionResult Respond(DebitNoteResult result) =>
        result.Outcome switch
        {
            DebitNoteOutcome.NotFound => NotFound(),

            DebitNoteOutcome.LifecycleRefused => BadRequest(new MessageResponse
            {
                Message = result.Detail ?? "Action refused by the document lifecycle.",
            }),

            DebitNoteOutcome.LineInvalid => BadRequest(new MessageResponse
            {
                Message = result.Detail ?? "One or more lines are invalid.",
            }),

            DebitNoteOutcome.PlaceOfSupplyRefused => BadRequest(new MessageResponse
            {
                Message = result.Detail ?? "Place of supply could not be determined.",
            }),

            DebitNoteOutcome.BillInvalid => BadRequest(new MessageResponse
            {
                Message = result.Detail ?? "That bill cannot be debited against.",
            }),

            DebitNoteOutcome.OverReturned => Conflict(new MessageResponse
            {
                Message = result.Detail
                    ?? "More is being returned than that bill line has left.",
            }),

            DebitNoteOutcome.StockRefused => Conflict(new MessageResponse
            {
                Message = result.Detail ?? "Inventory refused the return.",
            }),

            DebitNoteOutcome.RatesUnavailable => StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new MessageResponse
                {
                    Message = result.Detail
                        ?? "Tax rates or the base currency are temporarily unavailable.",
                }),

            DebitNoteOutcome.PostingRefused => StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new MessageResponse
                {
                    Message = result.Detail
                        ?? "The ledger refused the posting. The note is still a draft — post "
                            + "it again.",
                }),

            _ => StatusCode(StatusCodes.Status500InternalServerError),
        };
}
