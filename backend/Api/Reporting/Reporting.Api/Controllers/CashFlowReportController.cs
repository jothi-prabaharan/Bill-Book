using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Reporting.Entity.Models;
using Reporting.Repository.Repositories;
using Shared.Kernel.Internal;

namespace Reporting.Api.Controllers;

/// <summary>
/// The Cash Flow Statement – Direct (TK-201): its own endpoint, request and
/// answer, for its own page at <c>/reports/cash-flow-statement-direct</c>. The
/// gateway routes this exact path to Reporting, as it does the other statements.
///
/// The same guards as the Balance Sheet and the Profit &amp; Loss: the reports
/// module and <c>accounting.view</c>, on a branch with a trade. The rows are the
/// token's branch's through the query filter; a request naming another branch
/// is refused with 403.
/// </summary>
[ApiController]
[Authorize]
[Route("api/statements/cash-flow")]
[RequireBusinessType]
[RequireModulePermission("reports")]
public sealed class CashFlowReportController : ControllerBase
{
    private readonly CashFlowReportRepository _report;

    public CashFlowReportController(CashFlowReportRepository report) => _report = report;

    /// <summary>The cash flow statement for a period, and for a comparison period when asked.</summary>
    [HttpGet]
    [RequirePermission("accounting.view")]
    public async Task<IActionResult> Run([FromQuery] CashFlowReportRequest request, CancellationToken ct)
    {
        if (request.OrgId is Guid orgId
            && !(Guid.TryParse(User.FindFirstValue(OrgRouteMustMatchTokenAttribute.ClaimType), out Guid tokenOrg)
                && tokenOrg == orgId))
        {
            return Forbid();
        }

        foreach ((string field, string message) in CashFlowReportRepository.DateRefusals(request))
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
