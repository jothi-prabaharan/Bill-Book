using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Reporting.Repository.Repositories;
using Shared.Kernel.Apps;
using Shared.Kernel.Internal;

namespace Reporting.Api.Controllers;

/// <summary>
/// Live real-time database figures for the Home Dashboard (TK-165).
/// </summary>
[ApiController]
[Authorize]
[RequireModulePermission("dashboard")]
[Route("api/reporting/dashboard")]
[Route("api/dashboard")]
[RequireBusinessType]
public sealed class DashboardController : ControllerBase
{
    private readonly DashboardRepository _repo;

    public DashboardController(DashboardRepository repo)
    {
        _repo = repo;
    }

    [HttpGet]
    public async Task<IActionResult> GetSummary(CancellationToken ct)
    {
        var summary = await _repo.GetSummaryAsync(ct);
        return Ok(summary);
    }
}
