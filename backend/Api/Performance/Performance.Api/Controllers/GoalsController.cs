using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Performance.Api.Services;
using Performance.Entity.Models;
using Shared.Kernel.Apps;
using Shared.Kernel.Internal;

namespace Performance.Api.Controllers;

[ApiController]
[Authorize]
[RequireModulePermission("performance")]
[RequireApp(App.Hrms)]
[Route("api/prf/goals")]
public sealed class GoalsController : ControllerBase
{
    private readonly PerformanceService _service;

    public GoalsController(PerformanceService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] long cycleId, [FromQuery] long employeeId, CancellationToken ct) =>
        Ok(await _service.GetGoalsAsync(cycleId, employeeId, ct));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SaveGoalRequest req, CancellationToken ct)
    {
        var goal = await _service.SaveGoalAsync(req, ct);
        return Ok(goal);
    }
}
