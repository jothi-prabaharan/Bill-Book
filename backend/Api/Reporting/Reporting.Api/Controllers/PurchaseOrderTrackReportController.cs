using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Reporting.Entity.Models;
using Reporting.Repository.Repositories;
using Shared.Kernel.Internal;

namespace Reporting.Api.Controllers;

/// <summary>
/// The Purchase Order Track Report (TK-240): its own endpoint, request and answer, for its own page at
/// <c>/reports/purchase-order-track-report</c>. Each purchase order dated in a period, newest first,
/// with how far it has got (open, partly received, received, billed, closed short or cancelled), its
/// lines with what was ordered, received and billed of each and what is still to receive or bill, and
/// the totals of every order the filters match. The vendors its filter offers are under
/// <c>…/vendors</c>. The gateway routes the exact paths to Reporting.
///
/// The reports module and <c>purchase.view</c>, on a branch with a trade. A request naming another
/// branch is refused with 403; a missing or reversed date, an unknown progress, or a vendor that is
/// not the branch's is a field error.
/// </summary>
[ApiController]
[Authorize]
[Route("api/statements/purchase-order-track-report")]
[RequireBusinessType]
[RequireModulePermission("reports")]
public sealed class PurchaseOrderTrackReportController : ControllerBase
{
    private readonly PurchaseOrderTrackReportRepository _report;

    public PurchaseOrderTrackReportController(PurchaseOrderTrackReportRepository report) => _report = report;

    /// <summary>A page of the period's orders with their progress and lines, how many are at each progress, and the totals of all of them.</summary>
    [HttpGet]
    [RequirePermission("purchase.view")]
    public async Task<IActionResult> Run([FromQuery] PurchaseOrderTrackReportRequest request, CancellationToken ct)
    {
        if (request.OrgId is Guid orgId
            && !(Guid.TryParse(User.FindFirstValue(OrgRouteMustMatchTokenAttribute.ClaimType), out Guid tokenOrg)
                && tokenOrg == orgId))
        {
            return Forbid();
        }

        foreach ((string field, string message) in PurchaseOrderTrackReportRepository.Refusals(request))
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
            ModelState.AddModelError(nameof(PurchaseOrderTrackReportRequest.ContactId), "That vendor is not on this branch.");
            return ValidationProblem(ModelState);
        }

        return Ok(await _report.RunAsync(request, ct));
    }

    /// <summary>The branch's vendors, for the filter.</summary>
    [HttpGet("vendors")]
    [RequirePermission("purchase.view")]
    public async Task<IActionResult> Vendors(CancellationToken ct) => Ok(await _report.VendorsAsync(ct));
}
