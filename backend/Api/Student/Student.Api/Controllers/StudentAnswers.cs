using Microsoft.AspNetCore.Mvc;
using Student.Api.Services;
using Student.Entity.Models;

namespace Student.Api.Controllers;

/// <summary>
/// One mapping from a sis outcome to a status and a sentence, for every sis
/// controller. A row outside the caller's branch is not found: the query filter
/// and RLS hide it, so "another branch's" and "none" are one answer.
/// </summary>
internal static class StudentAnswers
{
    public static IActionResult Answer(this ControllerBase controller, StudentResult result) => result.Outcome switch
    {
        StudentOutcome.Ok => controller.Ok(new { id = result.Id }),
        StudentOutcome.NotFound => controller.NotFound(),
        StudentOutcome.Duplicate => controller.Conflict(new StudentMessage(result.Detail ?? "Another entry in this branch already uses that code or name.")),
        StudentOutcome.Unavailable => controller.StatusCode(StatusCodes.Status503ServiceUnavailable,
            new StudentMessage("The guardian contacts could not be checked just now. Try again shortly.")),
        _ => controller.UnprocessableEntity(new StudentMessage(result.Detail ?? "That change is not allowed.")),
    };
}
