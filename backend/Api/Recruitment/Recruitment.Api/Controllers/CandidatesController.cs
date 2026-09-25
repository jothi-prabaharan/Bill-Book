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
[Route("api/rec/candidates")]
public sealed class CandidatesController : ControllerBase
{
    private readonly RecruitmentService _recruitment;

    public CandidatesController(RecruitmentService recruitment) => _recruitment = recruitment;

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        CancellationToken ct = default) =>
        Ok(await _recruitment.ListCandidatesAsync(page, pageSize, search, ct));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Get(long id, CancellationToken ct)
    {
        var candidate = await _recruitment.GetCandidateByIdAsync(id, ct);
        return candidate is null ? NotFound() : Ok(candidate);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCandidateRequest request, CancellationToken ct)
    {
        var candidate = await _recruitment.CreateCandidateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = candidate.CandidateId }, candidate);
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, [FromBody] UpdateCandidateRequest request, CancellationToken ct)
    {
        var candidate = await _recruitment.UpdateCandidateAsync(id, request, ct);
        return candidate is null ? NotFound() : Ok(candidate);
    }
}
