using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Reporting.Entity.Models;
using Reporting.Repository.Repositories;
using Shared.Kernel.Internal;

namespace Reporting.Api.Controllers;

/// <summary>
/// The Balance Sheet (TK-199): its own endpoint, request and answer, for its
/// own page. The address is the one the page and the gateway already used
/// (<c>api/statements/balance-sheet</c>); what moved is the work, out of
/// <c>StatementsController</c> and into <see cref="BalanceSheetReportRepository"/>.
///
/// The same guards as before: the reports module and <c>accounting.view</c>,
/// on a branch with a trade. The rows are the token's branch's through the
/// query filter; a request naming another branch is refused with 403.
/// </summary>
[ApiController]
[Authorize]
[Route("api/statements/balance-sheet")]
[RequireBusinessType]
[RequireModulePermission("reports")]
public sealed class BalanceSheetReportController : ControllerBase
{
    private readonly BalanceSheetReportRepository _report;

    public BalanceSheetReportController(BalanceSheetReportRepository report) => _report = report;

    /// <summary>The balance sheet at a date, and at a second date when asked.</summary>
    [HttpGet]
    [RequirePermission("accounting.view")]
    public async Task<IActionResult> Run([FromQuery] BalanceSheetReportRequest request, CancellationToken ct)
    {
        if (request.OrgId is Guid orgId
            && !(Guid.TryParse(User.FindFirstValue(OrgRouteMustMatchTokenAttribute.ClaimType), out Guid tokenOrg)
                && tokenOrg == orgId))
        {
            return Forbid();
        }

        return Ok(await _report.RunAsync(request, ct));
    }
}
