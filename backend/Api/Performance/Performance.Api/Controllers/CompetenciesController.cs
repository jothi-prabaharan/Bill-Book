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
[Route("api/prf/competencies")]
public sealed class CompetenciesController : ControllerBase
{
    private readonly PerformanceService _service;

    public CompetenciesController(PerformanceService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> ListGroups(CancellationToken ct) =>
        Ok(await _service.GetCompetencyGroupsAsync(ct));

    [HttpPost("groups")]
    public async Task<IActionResult> CreateGroup([FromBody] SaveCompetencyGroupRequest req, CancellationToken ct) =>
        Ok(await _service.SaveCompetencyGroupAsync(req, ct));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SaveCompetencyRequest req, CancellationToken ct) =>
        Ok(await _service.SaveCompetencyAsync(req, ct));
}
