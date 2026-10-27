using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Reporting.Entity.Models;
using Reporting.Repository.Repositories;
using Shared.Kernel.Apps;
using Shared.Kernel.Internal;

namespace Reporting.Api.Controllers;

/// <summary>
/// Depreciation Schedule Report (TK-253): dedicated endpoint and page at
/// <c>/reports/depreciation-schedule</c>.
/// Each asset's depreciation method, rate and life, and what it charged over a period.
/// Guarded with AddOn.FixedAssets alone (no RequireBusinessType beside it).
/// </summary>
[ApiController]
[Authorize]
[Route("api/statements/depreciation-schedule")]
[RequireAddOn(AddOn.FixedAssets)]
[RequireModulePermission("reports")]
public sealed class DepreciationScheduleReportController : ControllerBase
{
    private readonly DepreciationScheduleReportRepository _report;

    public DepreciationScheduleReportController(DepreciationScheduleReportRepository report) => _report = report;

    /// <summary>Runs the depreciation schedule for a required period.</summary>
    [HttpGet]
    [RequirePermission("accounting.view")]
    public async Task<IActionResult> Run([FromQuery] DepreciationScheduleReportRequest request, CancellationToken ct)
    {
        if (request.OrgId is Guid orgId
            && !(Guid.TryParse(User.FindFirstValue(OrgRouteMustMatchTokenAttribute.ClaimType), out Guid tokenOrg)
                && tokenOrg == orgId))
        {
            return Forbid();
        }

        foreach (var (field, message) in DepreciationScheduleReportRepository.Refusals(request))
        {
            ModelState.AddModelError(field, message);
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var lookupRefusals = await _report.LookupRefusalsAsync(request, ct);
        foreach (var (field, message) in lookupRefusals)
        {
            ModelState.AddModelError(field, message);
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        return Ok(await _report.RunAsync(request, ct));
    }

    /// <summary>Fixed asset categories on this branch for the filter dropdown.</summary>
    [HttpGet("categories")]
    [RequirePermission("accounting.view")]
    public async Task<IActionResult> Categories(CancellationToken ct) =>
        Ok(await _report.CategoriesAsync(ct));

    /// <summary>Lookups for the depreciation schedule filters.</summary>
    [HttpGet("lookups")]
    [RequirePermission("accounting.view")]
    public async Task<IActionResult> Lookups(CancellationToken ct) =>
        Ok(await _report.GetLookupsAsync(ct));
}
