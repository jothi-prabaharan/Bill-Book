using Attendance.Api.Services;
using Attendance.Entity.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Kernel.Apps;
using Shared.Kernel.Internal;

namespace Attendance.Api.Controllers;

/// <summary>
/// A child's attendance in the parent portal (S9, TK-69). The contact comes
/// from the School portal token; a child who is not theirs is not found.
/// </summary>
[ApiController]
[Authorize]
[RequirePortalAccess]
[RequireApp(App.School)]
[Route("api/portal/school/attendance")]
public sealed class PortalAttendanceController : ControllerBase
{
    private readonly PortalAttendanceService _portal;
    private readonly TimeProvider _clock;

    public PortalAttendanceController(PortalAttendanceService portal, TimeProvider clock)
    {
        _portal = portal;
        _clock = clock;
    }

    /// <summary><paramref name="month"/> is any day in the month wanted; this month when omitted.</summary>
    [HttpGet("{studentId:long}")]
    public async Task<IActionResult> Month(long studentId, [FromQuery] DateOnly? month, CancellationToken ct)
    {
        long contactId = long.Parse(User.FindFirst(RequirePortalAccessAttribute.ContactClaim)!.Value);
        DateOnly wanted = month ?? DateOnly.FromDateTime(_clock.GetLocalNow().DateTime);

        (PortalAttendanceOutcome outcome, PortalAttendanceView? view) = await _portal.MonthAsync(contactId, studentId, wanted, ct);
        return outcome switch
        {
            PortalAttendanceOutcome.Ok => Ok(view),
            PortalAttendanceOutcome.NotFound => NotFound(),
            _ => StatusCode(StatusCodes.Status503ServiceUnavailable, new AttendanceMessage("A service this needs is not answering. Try again in a moment.")),
        };
    }
}
