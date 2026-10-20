using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Reporting.Entity.Models;
using Reporting.Repository.Repositories;
using Shared.Kernel.Internal;

namespace Reporting.Api.Controllers;

/// <summary>
/// The Profit &amp; Loss (TK-200): its own endpoint, request and answer, for its
/// own page. The address is the one the page and the gateway already used
/// (<c>api/statements/profit-and-loss</c>); what moved is the work, out of
/// <c>StatementsController</c> and into <see cref="ProfitAndLossReportRepository"/>.
///
/// The same guards as before: the reports module and <c>accounting.view</c>,
/// on a branch with a trade. The rows are the token's branch's through the
/// query filter; a request naming another branch is refused with 403.
/// </summary>
[ApiController]
[Authorize]
[Route("api/statements/profit-and-loss")]
[RequireBusinessType]
[RequireModulePermission("reports")]
public sealed class ProfitAndLossReportController : ControllerBase
{
    private readonly ProfitAndLossReportRepository _report;

    public ProfitAndLossReportController(ProfitAndLossReportRepository report) => _report = report;

    /// <summary>The profit and loss for a period, and for a comparison period when asked.</summary>
    [HttpGet]
    [RequirePermission("accounting.view")]
    public async Task<IActionResult> Run([FromQuery] ProfitAndLossReportRequest request, CancellationToken ct)
    {
        if (request.OrgId is Guid orgId
            && !(Guid.TryParse(User.FindFirstValue(OrgRouteMustMatchTokenAttribute.ClaimType), out Guid tokenOrg)
                && tokenOrg == orgId))
        {
            return Forbid();
        }

        foreach ((string field, string message) in ProfitAndLossReportRepository.DateRefusals(request))
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
