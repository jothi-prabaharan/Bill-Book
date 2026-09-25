using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Performance.Api.Services;
using Performance.Entity.Enums;
using Performance.Entity.Models;
using Shared.Kernel.Apps;
using Shared.Kernel.Internal;

namespace Performance.Api.Controllers;

[ApiController]
[Authorize]
[RequireModulePermission("performance")]
[RequireApp(App.Hrms)]
[Route("api/prf/cycles")]
public sealed class ReviewCyclesController : ControllerBase
{
    private readonly PerformanceService _service;

    public ReviewCyclesController(PerformanceService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) =>
        Ok(await _service.GetReviewCyclesAsync(ct));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Get(long id, CancellationToken ct)
    {
        var cycle = await _service.GetReviewCycleByIdAsync(id, ct);
        return cycle is null ? NotFound() : Ok(cycle);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SaveReviewCycleRequest req, CancellationToken ct)
    {
        var cycle = await _service.CreateReviewCycleAsync(req, ct);
        return CreatedAtAction(nameof(Get), new { id = cycle.ReviewCycleId }, cycle);
    }

    [HttpPatch("{id:long}/status")]
    public async Task<IActionResult> UpdateStatus(long id, [FromBody] CycleStatus status, CancellationToken ct)
    {
        bool updated = await _service.UpdateCycleStatusAsync(id, status, ct);
        return updated ? NoContent() : NotFound();
    }

    [HttpPost("{id:long}/enroll")]
    public async Task<IActionResult> Enroll(long id, [FromBody] EnrollEmployeesRequest req, CancellationToken ct)
    {
        int enrolled = await _service.EnrollEmployeesAsync(id, req, ct);
        return Ok(new { enrolled });
    }

    [HttpPost("{id:long}/close")]
    public async Task<IActionResult> Close(long id, CancellationToken ct)
    {
        int closed = await _service.CloseCycleAsync(id, ct);
        return Ok(new { closed });
    }
}
