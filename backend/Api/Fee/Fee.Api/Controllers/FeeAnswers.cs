using Fee.Api.Services;
using Fee.Entity.Models;
using Microsoft.AspNetCore.Mvc;

namespace Fee.Api.Controllers;

/// <summary>
/// One mapping from a fee outcome to a status and a sentence. A row outside the
/// caller's branch is not found: the query filter and RLS hide it. Accounting's
/// refusal is logged with its detail and answered with a sentence that names no
/// account or figure (hard rule 14).
/// </summary>
internal static class FeeAnswers
{
    public static IActionResult Answer(this ControllerBase controller, FeeResult result) => result.Outcome switch
    {
        FeeOutcome.Ok => controller.Ok(result.Body ?? new { id = result.Id }),
        FeeOutcome.NotFound => controller.NotFound(),
        FeeOutcome.Duplicate => controller.Conflict(new FeeMessage(result.Detail ?? "That already exists.")),
        FeeOutcome.Unavailable => controller.StatusCode(StatusCodes.Status503ServiceUnavailable,
            new FeeMessage("The students, contacts or accounts could not be reached just now. Try again shortly.")),
        FeeOutcome.LedgerRefused => controller.UnprocessableEntity(
            new FeeMessage("The accounts refused this posting. Check that the fee heads' accounts and the guardian's ledger are set up, then try again.")),
        _ => controller.UnprocessableEntity(new FeeMessage(result.Detail ?? "That change is not allowed.")),
    };
}
