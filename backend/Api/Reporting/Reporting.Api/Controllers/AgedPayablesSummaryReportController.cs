using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Reporting.Entity.Models;
using Reporting.Repository.Repositories;
using Shared.Kernel.Internal;

namespace Reporting.Api.Controllers;

/// <summary>
/// The Aged Payables Summary (TK-233): its own endpoint, request and answer, for its own page at
/// <c>/reports/aged-payables-summary</c>. What the branch owed each vendor on a date, spread over Current and
/// four overdue columns, in the branch's base currency. The vendors its filter offers are under
/// <c>…/vendors</c>. The gateway routes the exact paths to Reporting.
///
/// The reports module and <c>purchase.view</c>, on a branch with a trade. A request naming another branch is
/// refused with 403; a missing as-at date, a bucket width other than 7, 15 or 30, or a vendor that is not the
/// branch's is a field error.
/// </summary>
[ApiController]
[Authorize]
[Route("api/statements/aged-payables-summary")]
[RequireBusinessType]
[RequireModulePermission("reports")]
public sealed class AgedPayablesSummaryReportController : ControllerBase
{
    private readonly AgedPayablesSummaryReportRepository _report;

    public AgedPayablesSummaryReportController(AgedPayablesSummaryReportRepository report) => _report = report;

    /// <summary>Each vendor's balance on a date, by how overdue it is, with the totals.</summary>
    [HttpGet]
    [RequirePermission("purchase.view")]
    public async Task<IActionResult> Run([FromQuery] AgedPayablesSummaryReportRequest request, CancellationToken ct)
    {
        if (request.OrgId is Guid orgId
            && !(Guid.TryParse(User.FindFirstValue(OrgRouteMustMatchTokenAttribute.ClaimType), out Guid tokenOrg)
                && tokenOrg == orgId))
        {
            return Forbid();
        }

        foreach ((string field, string message) in AgedPayablesSummaryReportRepository.Refusals(request))
        {
            ModelState.AddModelError(field, message);
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        // The query filter decides which vendors are this branch's; another branch's is a refusal at the field.
        if (request.ContactId is long contactId && !await _report.ContactExistsAsync(contactId, ct))
        {
            ModelState.AddModelError(nameof(AgedPayablesSummaryReportRequest.ContactId), "That vendor is not on this branch.");
            return ValidationProblem(ModelState);
        }

        return Ok(await _report.RunAsync(request, ct));
    }

    /// <summary>The branch's vendors, for the filter.</summary>
    [HttpGet("vendors")]
    [RequirePermission("purchase.view")]
    public async Task<IActionResult> Vendors(CancellationToken ct) => Ok(await _report.VendorsAsync(ct));
}
