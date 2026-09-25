using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Recruitment.Api.Services;
using Recruitment.Entity.Enums;
using Recruitment.Entity.Models;
using Shared.Kernel.Apps;
using Shared.Kernel.Internal;

namespace Recruitment.Api.Controllers;

[ApiController]
[Authorize]
[RequireApp(App.Hrms)]
[RequireModulePermission("recruitment")]
[Route("api/rec/applications")]
public sealed class ApplicationsController : ControllerBase
{
    private readonly RecruitmentService _recruitment;

    public ApplicationsController(RecruitmentService recruitment) => _recruitment = recruitment;

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] long? openingId = null,
        [FromQuery] ApplicationStage? stage = null,
        CancellationToken ct = default) =>
        Ok(await _recruitment.ListApplicationsAsync(page, pageSize, openingId, stage, ct));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Get(long id, CancellationToken ct)
    {
        var app = await _recruitment.GetApplicationByIdAsync(id, ct);
        return app is null ? NotFound() : Ok(app);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateApplicationRequest request, CancellationToken ct)
    {
        var app = await _recruitment.CreateApplicationAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = app.ApplicationId }, app);
    }

    [HttpPut("{id:long}/stage")]
    public async Task<IActionResult> UpdateStage(long id, [FromBody] UpdateApplicationStageRequest request, CancellationToken ct) =>
        await _recruitment.UpdateApplicationStageAsync(id, request, ct) ? Ok(new { success = true }) : NotFound();
}
