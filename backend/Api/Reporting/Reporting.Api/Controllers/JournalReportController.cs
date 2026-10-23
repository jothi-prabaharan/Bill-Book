using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Reporting.Entity.Models;
using Reporting.Repository.Repositories;
using Shared.Kernel.Internal;

namespace Reporting.Api.Controllers;

/// <summary>
/// The Journal Report (TK-217): its own endpoint, request and answer, for its own
/// page at <c>/reports/journal-report</c>. The sources and accounts its filters
/// offer are Master's transaction types and Accounting's chart, which the page
/// already reads, so it has no lookups of its own. The gateway routes the exact
/// path to Reporting.
///
/// The reports module and <c>accounting.view</c>, on a branch with a trade. A
/// request naming another branch is refused with 403, a missing or reversed date
/// or an account that is not the branch's is a field error.
/// </summary>
[ApiController]
[Authorize]
[Route("api/statements/journal-report")]
[RequireBusinessType]
[RequireModulePermission("reports")]
public sealed class JournalReportController : ControllerBase
{
    private readonly JournalReportRepository _report;

    public JournalReportController(JournalReportRepository report) => _report = report;

    /// <summary>A page of the journals posted in a period, with their lines and the totals of all of them.</summary>
    [HttpGet]
    [RequirePermission("accounting.view")]
    public async Task<IActionResult> Run([FromQuery] JournalReportRequest request, CancellationToken ct)
    {
        if (request.OrgId is Guid orgId
            && !(Guid.TryParse(User.FindFirstValue(OrgRouteMustMatchTokenAttribute.ClaimType), out Guid tokenOrg)
                && tokenOrg == orgId))
        {
            return Forbid();
        }

        foreach ((string field, string message) in JournalReportRepository.DateRefusals(request))
        {
            ModelState.AddModelError(field, message);
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        // The query filter decides which accounts are this branch's; another branch's is a refusal at the field.
        if (request.AccountId is long accountId && !await _report.AccountExistsAsync(accountId, ct))
        {
            ModelState.AddModelError(nameof(JournalReportRequest.AccountId), "That account is not on this branch.");
            return ValidationProblem(ModelState);
        }

        return Ok(await _report.RunAsync(request, ct));
    }
}
