using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Recruitment.Api.Services;
using Recruitment.Entity.Models;
using Shared.Kernel.Apps;
using Shared.Kernel.Internal;

namespace Recruitment.Api.Controllers;

[ApiController]
[Authorize]
[RequireApp(App.Hrms)]
[RequireModulePermission("recruitment")]
[Route("api/rec/interviews")]
public sealed class InterviewsController : ControllerBase
{
    private readonly RecruitmentService _recruitment;

    public InterviewsController(RecruitmentService recruitment) => _recruitment = recruitment;

    [HttpGet("application/{applicationId:long}")]
    public async Task<IActionResult> ListByApplication(long applicationId, CancellationToken ct) =>
        Ok(await _recruitment.ListInterviewsByApplicationAsync(applicationId, ct));

    [HttpPost("schedule")]
    public async Task<IActionResult> Schedule([FromBody] ScheduleInterviewRoundRequest request, CancellationToken ct)
    {
        var round = await _recruitment.ScheduleInterviewRoundAsync(request, ct);
        return Ok(round);
    }

    [HttpPut("{id:long}/feedback")]
    public async Task<IActionResult> UpdateFeedback(long id, [FromBody] UpdateInterviewFeedbackRequest request, CancellationToken ct) =>
        await _recruitment.UpdateInterviewFeedbackAsync(id, request, ct) ? Ok(new { success = true }) : NotFound();
}
