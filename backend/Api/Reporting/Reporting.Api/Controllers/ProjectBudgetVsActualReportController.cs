using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Reporting.Entity.Models;
using Reporting.Repository.Repositories;
using Shared.Kernel.Internal;

namespace Reporting.Api.Controllers;

/// <summary>
/// Controller for Project Budget vs Actual report (TK-257).
/// Exposes endpoints to evaluate each project's cost and hours budgets against actuals up to an as-at date.
/// </summary>
[ApiController]
[Authorize]
[Route("api/statements/project-budget-vs-actual")]
[RequireBusinessType]
[RequireModulePermission("reports")]
public sealed class ProjectBudgetVsActualReportController : ControllerBase
{
    private readonly ProjectBudgetVsActualReportRepository _repository;

    public ProjectBudgetVsActualReportController(ProjectBudgetVsActualReportRepository repository)
    {
        _repository = repository;
    }

    /// <summary>
    /// Generates the Project Budget vs Actual report as of the specified date.
    /// </summary>
    [HttpGet]
    [RequirePermission("projects.view")]
    public async Task<IActionResult> Run(
        [FromQuery] ProjectBudgetVsActualReportRequest request,
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

        if (ProjectBudgetVsActualReportRepository.AsOfDateRefusal(request.AsOfDate) is string dateRefusal)
        {
            ModelState.AddModelError(nameof(request.AsOfDate), dateRefusal);
            return ValidationProblem(ModelState);
        }

        if (request.CustomerId.HasValue)
        {
            var customer = await _repository.FindCustomerAsync(request.CustomerId.Value, ct);
            if (ProjectBudgetVsActualReportRepository.CustomerRefusal(customer) is string customerRefusal)
            {
                ModelState.AddModelError(nameof(request.CustomerId), customerRefusal);
                return ValidationProblem(ModelState);
            }
        }

        if (request.ProjectId.HasValue)
        {
            var project = await _repository.FindProjectAsync(request.ProjectId.Value, ct);
            if (ProjectBudgetVsActualReportRepository.ProjectRefusal(project) is string projectRefusal)
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
