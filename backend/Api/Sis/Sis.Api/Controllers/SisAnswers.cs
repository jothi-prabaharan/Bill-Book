using Microsoft.AspNetCore.Mvc;
using Sis.Api.Services;
using Sis.Entity.Models;

namespace Sis.Api.Controllers;

/// <summary>
/// One mapping from a sis outcome to a status and a sentence, for every sis
/// controller. A row outside the caller's branch is not found: the query filter
/// and RLS hide it, so "another branch's" and "none" are one answer.
/// </summary>
internal static class SisAnswers
{
    public static IActionResult Answer(this ControllerBase controller, SisResult result) => result.Outcome switch
    {
        SisOutcome.Ok => controller.Ok(new { id = result.Id }),
        SisOutcome.NotFound => controller.NotFound(),
        SisOutcome.Duplicate => controller.Conflict(new SisMessage(result.Detail ?? "Another entry in this branch already uses that code or name.")),
        SisOutcome.Unavailable => controller.StatusCode(StatusCodes.Status503ServiceUnavailable,
            new SisMessage("The guardian contacts could not be checked just now. Try again shortly.")),
        _ => controller.UnprocessableEntity(new SisMessage(result.Detail ?? "That change is not allowed.")),
    };
}
