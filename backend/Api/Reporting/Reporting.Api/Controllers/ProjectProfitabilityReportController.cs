using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Reporting.Entity.Models;
using Reporting.Repository.Repositories;
using Shared.Kernel.Internal;

namespace Reporting.Api.Controllers;

/// <summary>
/// Controller for Project Profitability report (TK-256).
/// Exposes endpoints to run the report and fetch lookup filters.
/// </summary>
[ApiController]
[Authorize]
[Route("api/statements/project-profitability")]
[RequireBusinessType]
[RequireModulePermission("reports")]
public sealed class ProjectProfitabilityReportController : ControllerBase
{
    private readonly ProjectProfitabilityReportRepository _repository;

    public ProjectProfitabilityReportController(ProjectProfitabilityReportRepository repository)
    {
        _repository = repository;
    }

    /// <summary>
    /// Generates the Project Profitability report for the given period and filters.
    /// </summary>
    [HttpGet]
    [RequirePermission("projects.view")]
    public async Task<IActionResult> Run(
        [FromQuery] ProjectProfitabilityReportRequest request,
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

        if (ProjectProfitabilityReportRepository.DateRefusal(request.From, request.To) is string dateRefusal)
        {
            ModelState.AddModelError(nameof(request.To), dateRefusal);
            return ValidationProblem(ModelState);
        }

        if (request.CustomerId.HasValue)
        {
            var customer = await _repository.FindCustomerAsync(request.CustomerId.Value, ct);
            if (ProjectProfitabilityReportRepository.CustomerRefusal(customer) is string customerRefusal)
            {
                ModelState.AddModelError(nameof(request.CustomerId), customerRefusal);
                return ValidationProblem(ModelState);
            }
        }

        if (request.ProjectId.HasValue)
        {
            var project = await _repository.FindProjectAsync(request.ProjectId.Value, ct);
            if (ProjectProfitabilityReportRepository.ProjectRefusal(project) is string projectRefusal)
            {
                ModelState.AddModelError(nameof(request.ProjectId), projectRefusal);
                return ValidationProblem(ModelState);
            }
        }

        var response = await _repository.RunAsync(request, ct);
        return Ok(response);
    }

    /// <summary>
    /// Lookups for the project profitability report filter selectors.
    /// </summary>
    [HttpGet("lookups")]
    [RequirePermission("projects.view")]
    public async Task<IActionResult> Lookups(CancellationToken ct)
    {
        var lookups = await _repository.GetLookupsAsync(ct);
        return Ok(lookups);
    }
}
