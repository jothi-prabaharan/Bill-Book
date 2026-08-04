using Banking.Api.Services;
using Banking.Entity.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Kernel.Internal;

namespace Banking.Api.Controllers;

/// <summary>
/// Banking › Spend money. A bill paid, a deposit put down with a supplier, a
/// customer refunded — six meanings on one document type, told apart by the
/// ledger source on each line.
///
/// <b>Post and void are their own routes</b>, not a status field on the save.
/// They are not edits: one takes a number and writes to the general ledger, the
/// other withdraws those rows. A PUT that could do either by accident is how a
/// posted document gets silently rewritten.
/// </summary>
[ApiController]
[Authorize]
[RequireModulePermission("banking")]
[Route("api/spend-money")]
public sealed class SpendMoneyController : ControllerBase
{
    private readonly SpendMoneyService _documents;

    public SpendMoneyController(SpendMoneyService documents) => _documents = documents;

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? status,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken ct) =>
        Ok(await _documents.ListAsync(status, from, to, ct));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Get(long id, CancellationToken ct)
    {
        MoneyDocumentView? document = await _documents.GetAsync(id, ct);
        return document is null ? NotFound() : Ok(document);
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] SaveMoneyDocumentRequest request, CancellationToken ct)
    {
        MoneyDocumentResult result = await _documents.CreateAsync(request, ct);

        return Respond(result, () => CreatedAtAction(
            nameof(Get), new { id = result.DocumentId }, new { id = result.DocumentId }));
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(
        long id, [FromBody] SaveMoneyDocumentRequest request, CancellationToken ct) =>
        Respond(await _documents.UpdateAsync(id, request, ct), NoContent);

    [HttpPost("{id:long}/post")]
    public async Task<IActionResult> Post(long id, CancellationToken ct) =>
        Respond(await _documents.PostAsync(id, ct), NoContent);

    [HttpPost("{id:long}/void")]
    public async Task<IActionResult> Void(
        long id, [FromBody] VoidMoneyDocumentRequest request, CancellationToken ct) =>
        Respond(await _documents.VoidAsync(id, request, ct), NoContent);

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken ct) =>
        Respond(await _documents.DeleteAsync(id, ct), NoContent);

    private IActionResult Respond(MoneyDocumentResult result, Func<IActionResult> onOk) =>
        result.Outcome switch
        {
            MoneyDocumentOutcome.Ok => onOk(),
            MoneyDocumentOutcome.NotFound => NotFound(),

            MoneyDocumentOutcome.NotDraft => BadRequest(new MessageResponse
            {
                Message = "A posted document is never edited. Void it instead — the document "
                    + "and its number stay, and its ledger rows are withdrawn.",
            }),

            MoneyDocumentOutcome.NotPosted => BadRequest(new MessageResponse
            {
                Message = "Only a posted document can be voided. A draft is deleted.",
            }),

            // 409, not 400: the document may be perfectly good, and dating it
            // later will post it. A validation error would point at the wrong
            // thing to fix.
            MoneyDocumentOutcome.PeriodClosed => Conflict(new MessageResponse
            {
                Message = result.Detail ?? "The books are closed for that date.",
            }),

            // Transient. The caller should come back rather than treat it as a
            // refusal — a lock that could not be read is not a lock that passed.
            MoneyDocumentOutcome.PeriodLockUnavailable
                or MoneyDocumentOutcome.SettlementRateUnavailable => StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new MessageResponse { Message = result.Detail ?? "Please try again." }),

            // A line pointing at the wrong kind of document, or at one another
            // line already settles. 409 rather than 400: the document is
            // well-formed, it just contradicts itself about what it pays.
            MoneyDocumentOutcome.MappingNotSettleable
                or MoneyDocumentOutcome.MappingRepeated => Conflict(new MessageResponse
                {
                    Message = result.Detail ?? "The lines disagree about what this settles.",
                }),

            _ => BadRequest(new MessageResponse
            {
                Message = result.Detail ?? $"The document was refused: {result.Outcome}.",
            }),
        };
}
