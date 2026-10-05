using Accounting.Api.Services;
using Accounting.Repository.Repositories;
using Accounting.Entity.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Kernel.Budgets;
using Shared.Kernel.Internal;
using Shared.Kernel.Apps;

namespace Accounting.Api.Controllers;

/// <summary>
/// Accounting › Journal entries. The manual journal — the one document a person
/// writes by naming two accounts directly.
///
/// <b>There is no update or delete past Draft.</b> Post and reverse are separate
/// routes rather than a status field on the save, because they are not edits:
/// one takes a number and writes to the general ledger, and the other creates a
/// second document. A PUT that could do either by accident is how a posted entry
/// gets silently rewritten.
/// </summary>
[ApiController]
[Authorize]
[RequireModulePermission("accounting")]
[Route("api/journals")]
[RequireBusinessType(BusinessType.Retail, BusinessType.Pharma, BusinessType.Jewellery, BusinessType.Restaurant, BusinessType.School, BusinessType.Service)]
public sealed class JournalsController : ControllerBase
{
    private readonly JournalRepository _journals;

    public JournalsController(JournalRepository journals) => _journals = journals;

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? status,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken ct) =>
        Ok(await _journals.ListAsync(status, from, to, ct));

    [HttpGet("{journalId:long}")]
    public async Task<IActionResult> Get(long journalId, CancellationToken ct)
    {
        JournalDetailView? journal = await _journals.GetAsync(journalId, ct);
        return journal is null ? NotFound() : Ok(journal);
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] SaveJournalRequest request, [FromServices] BudgetCheckRepository budgets, CancellationToken ct)
    {
        SaveJournalResult result = await _journals.CreateAsync(request, ct);
        List<string> warnings = result.Outcome == SaveJournalOutcome.Ok ? await BudgetWarningsAsync(request, budgets, ct) : [];

        return Respond(result, () => CreatedAtAction(
            nameof(Get),
            new { journalId = result.JournalId },
            new { journalId = result.JournalId, warnings }));
    }

    [HttpPut("{journalId:long}")]
    public async Task<IActionResult> Update(
        long journalId, [FromBody] SaveJournalRequest request, [FromServices] BudgetCheckRepository budgets, CancellationToken ct)
    {
        SaveJournalResult result = await _journals.UpdateAsync(journalId, request, ct);
        List<string> warnings = result.Outcome == SaveJournalOutcome.Ok ? await BudgetWarningsAsync(request, budgets, ct) : [];

        // An edit that took the journal out of approval says so (TK-101), and
        // one that passes the budget says that too (TK-109).
        return Respond(result, () => result.Detail is not null || warnings.Count > 0
            ? Ok(new { message = result.Detail, warnings })
            : NoContent());
    }

    [HttpPost("{journalId:long}/post")]
    public async Task<IActionResult> Post(long journalId, CancellationToken ct) =>
        Respond(await _journals.PostAsync(journalId, ct), NoContent);

    /// <summary>
    /// Creates the offsetting entry. Returns the reversal's id, not the
    /// original's — the reversal is a document in its own right and the caller
    /// will want to open it.
    /// </summary>
    [HttpPost("{journalId:long}/reverse")]
    public async Task<IActionResult> Reverse(
        long journalId, [FromBody] ReverseJournalRequest request, CancellationToken ct)
    {
        SaveJournalResult result = await _journals.ReverseAsync(journalId, request, ct);

        return Respond(result, () => Ok(new { journalId = result.JournalId }));
    }

    [HttpDelete("{journalId:long}")]
    public async Task<IActionResult> Delete(long journalId, CancellationToken ct) =>
        Respond(await _journals.DeleteAsync(journalId, ct), NoContent);

    /// <summary>
    /// Whether the saved lines pass the branch's approved budget: each line's
    /// debit less credit, in base currency. The save already stands; these are
    /// warnings the form shows (TK-109).
    /// </summary>
    private static Task<List<string>> BudgetWarningsAsync(SaveJournalRequest request, BudgetCheckRepository budgets, CancellationToken ct)
    {
        decimal rate = request.ExchangeRate is decimal r && r > 0m ? r : 1m;

        return budgets.WarningsAsync(request.JournalDate, request.Lines.Select(l => new BudgetSpendLine
        {
            AccountId = l.AccountId,
            ProjectId = l.ProjectId,
            Amount = rate == 1m
                ? l.DebitAmount - l.CreditAmount
                : Math.Round((l.DebitAmount - l.CreditAmount) / rate, 2, MidpointRounding.AwayFromZero),
        }), ct);
    }

    private IActionResult Respond(SaveJournalResult result, Func<IActionResult> onOk) =>
        result.Outcome switch
        {
            SaveJournalOutcome.Ok => onOk(),
            SaveJournalOutcome.NotFound => NotFound(),

            SaveJournalOutcome.NotDraft => BadRequest(new MessageResponse
            {
                Message = "A posted entry is never edited. Reverse it instead — the reversal "
                    + "stands beside it, and both stay in the ledger.",
            }),

            SaveJournalOutcome.NotPosted => BadRequest(new MessageResponse
            {
                Message = "Only a posted entry can be reversed. A draft is deleted.",
            }),

            SaveJournalOutcome.AlreadyReversed => BadRequest(new MessageResponse
            {
                Message = "This entry has already been reversed.",
            }),

            SaveJournalOutcome.NoLines => BadRequest(new MessageResponse
            {
                Message = "There is nothing to post — the entry has no lines.",
            }),

            // 409, not 400: the entry itself may be perfectly good, and dating it
            // later will post it. A validation error would tell the user to fix
            // the wrong thing.
            SaveJournalOutcome.PeriodClosed => Conflict(new MessageResponse
            {
                Message = result.Detail ?? "The books are closed for that date.",
            }),

            SaveJournalOutcome.AwaitingApproval => Conflict(new MessageResponse
            {
                Message = result.Detail ?? "This entry needs approval before it can be posted.",
            }),

            // Transient, and the caller should come back rather than assume a
            // currency. A 503 says so; a 400 would have the screen give up.
            SaveJournalOutcome.BaseCurrencyUnavailable => StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new MessageResponse
                {
                    Message = "The branch's base currency could not be read, so nothing was "
                        + "saved. Nothing has changed and this can be retried.",
                }),

            _ => BadRequest(new MessageResponse
            {
                Message = result.Detail ?? $"The entry was refused: {result.Outcome}.",
            }),
        };
}
