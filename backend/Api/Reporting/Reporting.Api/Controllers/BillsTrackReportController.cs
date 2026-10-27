using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Reporting.Entity.Models;
using Reporting.Repository.Repositories;
using Shared.Kernel.Internal;

namespace Reporting.Api.Controllers;

/// <summary>
/// The Bills Track Report (TK-237): its own endpoint, request and answer, for its own page at
/// <c>/reports/bills-track-report</c>. Where each vendor bill dated in a period is in its life (a
/// draft, ready to post, awaiting payment, partly paid, overdue, paid or void), with the date it was
/// posted, its due date, the last settlement of it, what is still owed to the vendor and where its
/// approval stands, a page at a time. The vendors its filter offers are under <c>…/vendors</c>. The
/// gateway routes the exact paths to Reporting.
///
/// The reports module and <c>purchase.view</c>, on a branch with a trade. A request naming another
/// branch is refused with 403; a missing or reversed date, an unknown status, or a vendor that is
/// not the branch's is a field error.
/// </summary>
[ApiController]
[Authorize]
[Route("api/statements/bills-track-report")]
[RequireBusinessType]
[RequireModulePermission("reports")]
public sealed class BillsTrackReportController : ControllerBase
{
    private readonly BillsTrackReportRepository _report;

    public BillsTrackReportController(BillsTrackReportRepository report) => _report = report;

    /// <summary>A page of the period's bills with where each stands, how many have each status, and the totals of the posted ones.</summary>
    [HttpGet]
    [RequirePermission("purchase.view")]
    public async Task<IActionResult> Run([FromQuery] BillsTrackReportRequest request, CancellationToken ct)
    {
        if (request.OrgId is Guid orgId
            && !(Guid.TryParse(User.FindFirstValue(OrgRouteMustMatchTokenAttribute.ClaimType), out Guid tokenOrg)
                && tokenOrg == orgId))
        {
            return Forbid();
        }

        foreach ((string field, string message) in BillsTrackReportRepository.Refusals(request))
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
            ModelState.AddModelError(nameof(BillsTrackReportRequest.ContactId), "That vendor is not on this branch.");
            return ValidationProblem(ModelState);
        }

        return Ok(await _report.RunAsync(request, ct));
    }

    /// <summary>The branch's vendors, for the filter.</summary>
    [HttpGet("vendors")]
    [RequirePermission("purchase.view")]
    public async Task<IActionResult> Vendors(CancellationToken ct) => Ok(await _report.VendorsAsync(ct));
}
