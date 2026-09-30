using System.Security.Claims;
using Accounting.Api.Services;
using Accounting.Repository.Repositories;
using Accounting.Entity.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Kernel.Apps;
using Shared.Kernel.Interfaces;
using Shared.Kernel.Internal;

namespace Accounting.Api.Controllers;

/// <summary>
/// The timesheet (TK-106), under <c>api/projects/time</c>. Logging time needs
/// only <c>projects.view</c>: a user logs their own hours, and the service
/// refuses anyone else's unless the user holds <c>projects.edit</c>.
/// </summary>
[ApiController]
[Authorize]
[RequireModulePermission("projects")]
[Route("api/projects/time")]
[RequireBusinessType(BusinessType.General, BusinessType.Pharma, BusinessType.Jewellery)]
public sealed class TimeEntriesController : ControllerBase
{
    private readonly TimeEntryRepository _time;
    private readonly ICurrentUser _user;

    public TimeEntriesController(TimeEntryRepository time, ICurrentUser user)
    {
        _time = time;
        _user = user;
    }

    private bool MayActForOthers => User.HasClaim("permission", "projects.edit");

    /// <summary>The signed-in user's entries for a week, or another user's for someone with <c>projects.edit</c>.</summary>
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] DateOnly from, [FromQuery] DateOnly to, [FromQuery] Guid? userId, CancellationToken ct)
    {
        if (_user.UserId is not Guid me)
        {
            return Forbid();
        }

        if (to < from || to.DayNumber - from.DayNumber > 92)
        {
            return BadRequest(new MessageResponse { Message = "Choose a range of at most three months." });
        }

        Guid? whose = userId is Guid other && other != me ? (MayActForOthers ? other : null) : me;
        if (whose is null)
        {
            return Forbid();
        }

        return Ok(await _time.ListAsync(whose, from, to, ct));
    }

    [HttpPost]
    [PermissionAction("view")]
    public async Task<IActionResult> Create([FromBody] SaveTimeEntryRequest request, CancellationToken ct) =>
        _user.UserId is Guid me
            ? Respond(await _time.SaveAsync(null, request, me, MayActForOthers, ct), created: true)
            : Forbid();

    [HttpPut("{id:long}")]
    [PermissionAction("view")]
    public async Task<IActionResult> Update(long id, [FromBody] SaveTimeEntryRequest request, CancellationToken ct) =>
        _user.UserId is Guid me
            ? Respond(await _time.SaveAsync(id, request, me, MayActForOthers, ct), created: false)
            : Forbid();

    [HttpDelete("{id:long}")]
    [PermissionAction("view")]
    public async Task<IActionResult> Delete(long id, CancellationToken ct) =>
        _user.UserId is Guid me
            ? Respond(await _time.DeleteAsync(id, me, MayActForOthers, ct), created: false)
            : Forbid();

    private IActionResult Respond(TimeEntryResult result, bool created) => result.Outcome switch
    {
        TimeEntryOutcome.Ok => created ? Ok(new { timeEntryId = result.TimeEntryId }) : NoContent(),
        TimeEntryOutcome.NotFound => NotFound(),
        TimeEntryOutcome.NotYours => StatusCode(StatusCodes.Status403Forbidden, new MessageResponse { Message = result.Detail! }),
        TimeEntryOutcome.Billed => Conflict(new MessageResponse { Message = result.Detail! }),
        _ => UnprocessableEntity(new MessageResponse { Message = result.Detail ?? "The entry was refused." }),
    };
}
