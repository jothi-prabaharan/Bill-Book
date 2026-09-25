using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Kernel.Apps;
using Shared.Kernel.Internal;
using Student.Api.Services;
using Student.Entity.Models;

namespace Student.Api.Controllers;

/// <summary>
/// The parent portal's view of a guardian's children and their published marks
/// (S9, TK-69). The caller is a guardian contact holding a School portal token,
/// not staff, so the guard is <see cref="RequirePortalAccessAttribute"/>; the
/// contact comes from the token, never from the route.
/// </summary>
[ApiController]
[Authorize]
[RequirePortalAccess]
[RequireApp(App.School)]
[Route("api/portal/school/children")]
public sealed class PortalSchoolController : ControllerBase
{
    private readonly PortalService _portal;

    public PortalSchoolController(PortalService portal) => _portal = portal;

    private long ContactId => long.Parse(User.FindFirst(RequirePortalAccessAttribute.ContactClaim)!.Value);

    [HttpGet]
    public async Task<IActionResult> Children(CancellationToken ct) => Ok(await _portal.ChildrenAsync(ContactId, ct));

    /// <summary>A child that is not this guardian's is not found, as if it did not exist.</summary>
    [HttpGet("{studentId:long}/marks")]
    public async Task<IActionResult> Marks(long studentId, CancellationToken ct) =>
        await _portal.MarksAsync(ContactId, studentId, ct) is List<PortalExamView> exams ? Ok(exams) : NotFound();
}
