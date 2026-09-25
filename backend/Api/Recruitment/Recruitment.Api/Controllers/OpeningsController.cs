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
[Route("api/rec/openings")]
public sealed class OpeningsController : ControllerBase
{
    private readonly RecruitmentService _recruitment;

    public OpeningsController(RecruitmentService recruitment) => _recruitment = recruitment;

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] OpeningStatus? status = null,
        CancellationToken ct = default) =>
        Ok(await _recruitment.ListOpeningsAsync(page, pageSize, status, ct));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Get(long id, CancellationToken ct)
    {
        var opening = await _recruitment.GetOpeningByIdAsync(id, ct);
        return opening is null ? NotFound() : Ok(opening);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateJobOpeningRequest request, CancellationToken ct)
    {
        var opening = await _recruitment.CreateOpeningAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = opening.JobOpeningId }, opening);
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, [FromBody] UpdateJobOpeningRequest request, CancellationToken ct)
    {
        var opening = await _recruitment.UpdateOpeningAsync(id, request, ct);
        return opening is null ? NotFound() : Ok(opening);
    }
}
