using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Reporting.Entity.Models;
using Reporting.Repository.Repositories;
using Shared.Kernel.Internal;

namespace Reporting.Api.Controllers;

/// <summary>
/// Business Performance (TK-203): its own endpoint, request and answer, for its
/// own page at <c>/reports/business-performance</c>. The gateway routes this
/// exact path to Reporting, as it does the financial statements; the catalog's
/// <c>business-performance</c> source stays for export and schedules, and both
/// share one arithmetic.
///
/// The same guards as the financial statements: the reports module and
/// <c>accounting.view</c>, on a branch with a trade. The rows are the token's
/// branch's through the query filter; a request naming another branch is
/// refused with 403.
/// </summary>
[ApiController]
[Authorize]
[Route("api/statements/business-performance")]
[RequireBusinessType]
[RequireModulePermission("reports")]
public sealed class BusinessPerformanceReportController : ControllerBase
{
    private readonly BusinessPerformanceReportRepository _report;

    public BusinessPerformanceReportController(BusinessPerformanceReportRepository report) => _report = report;

    /// <summary>The ratios for a period, and for a comparison period when asked.</summary>
    [HttpGet]
    [RequirePermission("accounting.view")]
    public async Task<IActionResult> Run([FromQuery] BusinessPerformanceReportRequest request, CancellationToken ct)
    {
        if (request.OrgId is Guid orgId
            && !(Guid.TryParse(User.FindFirstValue(OrgRouteMustMatchTokenAttribute.ClaimType), out Guid tokenOrg)
                && tokenOrg == orgId))
        {
            return Forbid();
        }

        foreach ((string field, string message) in BusinessPerformanceReportRepository.DateRefusals(request))
        {
            ModelState.AddModelError(field, message);
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        return Ok(await _report.RunAsync(request, ct));
    }
}
