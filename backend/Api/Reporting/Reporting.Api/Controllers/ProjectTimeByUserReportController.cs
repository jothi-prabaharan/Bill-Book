using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Reporting.Entity.Models;
using Reporting.Repository.Repositories;
using Shared.Kernel.Internal;

namespace Reporting.Api.Controllers;

/// <summary>
/// Controller for Project Time by User report (TK-259).
/// Exposes endpoints to analyze logged project hours by team member and project for a period.
/// </summary>
[ApiController]
[Authorize]
[Route("api/statements/project-time-by-user")]
[RequireBusinessType]
[RequireModulePermission("reports")]
public sealed class ProjectTimeByUserReportController : ControllerBase
{
    private readonly ProjectTimeByUserReportRepository _repository;

    public ProjectTimeByUserReportController(ProjectTimeByUserReportRepository repository)
    {
        _repository = repository;
    }

    /// <summary>
    /// Generates the Project Time by User report for the specified date range.
    /// </summary>
    [HttpGet]
    [RequirePermission("projects.view")]
    public async Task<IActionResult> Run(
        [FromQuery] ProjectTimeByUserReportRequest request,
        CancellationToken ct)
    {
        if (request.OrgId is Guid orgId
            && !(Guid.TryParse(User.FindFirstValue(OrgRouteMustMatchTokenAttribute.ClaimType), out Guid tokenOrg)
                && tokenOrg == orgId))
        {
            return Forbid();
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        if (ProjectTimeByUserReportRepository.DateRangeRefusal(request.FromDate, request.ToDate) is string dateRefusal)
        {
            ModelState.AddModelError(nameof(request.ToDate), dateRefusal);
            return ValidationProblem(ModelState);
        }

        if (request.ProjectId.HasValue)
        {
            var project = await _repository.FindProjectAsync(request.ProjectId.Value, ct);
            if (ProjectTimeByUserReportRepository.ProjectRefusal(project) is string projectRefusal)
            {
                ModelState.AddModelError(nameof(request.ProjectId), projectRefusal);
                return ValidationProblem(ModelState);
            }
        }

        var response = await _repository.RunAsync(request, ct);
        return Ok(response);
    }

    /// <summary>
    /// Lookups for filter dropdowns.
    /// </summary>
    [HttpGet("lookups")]
    [RequirePermission("projects.view")]
    public async Task<IActionResult> Lookups(CancellationToken ct)
    {
        var lookups = await _repository.GetLookupsAsync(ct);
        return Ok(lookups);
    }
}
