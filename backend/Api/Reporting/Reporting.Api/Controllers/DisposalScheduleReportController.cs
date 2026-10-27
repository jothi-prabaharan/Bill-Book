using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Reporting.Entity.Models;
using Reporting.Repository.Repositories;
using Shared.Kernel.Apps;
using Shared.Kernel.Internal;

namespace Reporting.Api.Controllers;

/// <summary>
/// Disposal Schedule Report (TK-254): dedicated endpoint and page at
/// <c>/reports/disposal-schedule-report</c>.
/// Assets disposed of in a period: cost, accumulated depreciation, book value, proceeds and the gain or loss on each.
/// Guarded with AddOn.FixedAssets alone (no RequireBusinessType beside it).
/// </summary>
[ApiController]
[Authorize]
[Route("api/statements/disposal-schedule-report")]
[RequireAddOn(AddOn.FixedAssets)]
[RequireModulePermission("reports")]
public sealed class DisposalScheduleReportController : ControllerBase
{
    private readonly DisposalScheduleReportRepository _report;

    public DisposalScheduleReportController(DisposalScheduleReportRepository report) => _report = report;

    /// <summary>Runs the disposal schedule report for a required period.</summary>
    [HttpGet]
    [RequirePermission("accounting.view")]
    public async Task<IActionResult> Run([FromQuery] DisposalScheduleReportRequest request, CancellationToken ct)
    {
        if (request.OrgId is Guid orgId
            && !(Guid.TryParse(User.FindFirstValue(OrgRouteMustMatchTokenAttribute.ClaimType), out Guid tokenOrg)
                && tokenOrg == orgId))
        {
            return Forbid();
        }

        foreach (var (field, message) in DisposalScheduleReportRepository.Refusals(request))
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

    /// <summary>Lookups for the disposal schedule filters.</summary>
    [HttpGet("lookups")]
    [RequirePermission("accounting.view")]
    public async Task<IActionResult> Lookups(CancellationToken ct) =>
        Ok(await _report.GetLookupsAsync(ct));
}
