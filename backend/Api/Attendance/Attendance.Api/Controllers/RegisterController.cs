using Attendance.Api.Services;
using Attendance.Entity.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Kernel.Apps;
using Shared.Kernel.Internal;

namespace Attendance.Api.Controllers;

/// <summary>
/// The student attendance register (S3, TK-63). Reading takes
/// <c>attendance.view</c>; taking the register and locking a day take
/// <c>attendance.edit</c>, which teachers hold; unlocking, and changing a
/// locked day, take <c>attendance.unlock</c>.
/// </summary>
[ApiController]
[Authorize]
[RequireModulePermission("attendance")]
[RequireApp(App.School)]
[Route("api/student-attendance")]
public sealed class RegisterController : ControllerBase
{
    private readonly RegisterService _register;

    public RegisterController(RegisterService register) => _register = register;

    [HttpGet("register")]
    public async Task<IActionResult> Get([FromQuery] long sectionId, [FromQuery] DateOnly date, CancellationToken ct)
    {
        (RegisterResult result, RegisterView? view) = await _register.GetAsync(sectionId, date, ct);
        return view is not null ? Ok(view) : Answer(result);
    }

    [HttpPut("register")]
    public async Task<IActionResult> Save([FromBody] SaveRegisterRequest request, CancellationToken ct) =>
        Answer(await _register.SaveAsync(request, ct));

    [HttpPost("register/lock")]
    [PermissionAction("edit")]
    public async Task<IActionResult> Lock([FromBody] LockRequest request, CancellationToken ct) =>
        Answer(await _register.LockAsync(request, ct));

    [HttpPost("register/unlock")]
    [PermissionAction("edit")]
    [RequirePermission(RegisterService.UnlockPermission)]
    public async Task<IActionResult> Unlock([FromBody] LockRequest request, CancellationToken ct) =>
        Answer(await _register.UnlockAsync(request, ct));

    // A section outside the caller's branch is not found: Student's query filter
    // hides it, so "another branch's" and "none" are one answer.
    private IActionResult Answer(RegisterResult result) => result.Outcome switch
    {
        RegisterOutcome.Ok => NoContent(),
        RegisterOutcome.NotFound => NotFound(),
        RegisterOutcome.Locked => StatusCode(StatusCodes.Status423Locked, new AttendanceMessage(result.Detail!)),
        RegisterOutcome.Unavailable => StatusCode(StatusCodes.Status503ServiceUnavailable,
            new AttendanceMessage("The section's roll could not be read just now. Try again shortly.")),
        _ => UnprocessableEntity(new AttendanceMessage(result.Detail ?? "That change is not allowed.")),
    };
}
