using Employee.Api.Services;
using Employee.Entity.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Kernel.Apps;
using Shared.Kernel.Internal;

namespace Employee.Api.Controllers;

/// <summary>
/// Announcements and policy documents (H1, TK-48). HRMS only, under the
/// <c>hrm</c> module: Payroll shares the employee master but not these.
/// </summary>
[ApiController]
[Authorize]
[RequireModulePermission("hrm")]
[RequireApp(App.Hrms)]
[Route("api/hrm")]
public sealed class NoticesController : ControllerBase
{
    private readonly NoticeService _notices;

    public NoticesController(NoticeService notices) => _notices = notices;

    [HttpGet("announcements")]
    public async Task<IActionResult> Announcements(CancellationToken ct) => Ok(await _notices.AnnouncementsAsync(ct));

    [HttpPost("announcements")]
    public async Task<IActionResult> CreateAnnouncement([FromBody] SaveAnnouncementRequest request, CancellationToken ct) =>
        Answer(await _notices.SaveAnnouncementAsync(null, request, ct));

    [HttpPut("announcements/{id:long}")]
    public async Task<IActionResult> UpdateAnnouncement(long id, [FromBody] SaveAnnouncementRequest request, CancellationToken ct) =>
        Answer(await _notices.SaveAnnouncementAsync(id, request, ct));

    [HttpGet("policy-documents")]
    public async Task<IActionResult> Policies(CancellationToken ct) => Ok(await _notices.PoliciesAsync(ct));

    [HttpPost("policy-documents")]
    public async Task<IActionResult> CreatePolicy([FromBody] SavePolicyDocumentRequest request, CancellationToken ct) =>
        Answer(await _notices.SavePolicyAsync(null, request, ct));

    [HttpPut("policy-documents/{id:long}")]
    public async Task<IActionResult> UpdatePolicy(long id, [FromBody] SavePolicyDocumentRequest request, CancellationToken ct) =>
        Answer(await _notices.SavePolicyAsync(id, request, ct));

    private IActionResult Answer(NoticeResult result) => result.Outcome switch
    {
        NoticeOutcome.Ok => Ok(new { id = result.Id }),
        NoticeOutcome.NotFound => NotFound(),
        NoticeOutcome.ExpiresBeforePublish => UnprocessableEntity(new EmployeeMessage("The expiry date cannot be before the publish date.")),
        _ => UnprocessableEntity(new EmployeeMessage("Choose the department, location or grade the announcement is for.")),
    };
}
