using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Reporting.Entity.Models;
using Reporting.Repository.Repositories;
using Shared.Kernel.Internal;

namespace Reporting.Api.Controllers;

/// <summary>
/// The Receive Order Track Report (TK-242): its own endpoint, request and answer, for its own page at
/// <c>/reports/receive-order-track-report</c>. Each posted goods receipt dated in a period, newest first,
/// with what its accepted goods are worth, how much of that posted bills have billed, what is still to
/// bill and its status (not billed, partly billed, billed, or nothing to bill), and the totals of every
/// receipt the filters match. The vendors its filter offers are under <c>…/vendors</c>. The gateway
/// routes the exact paths to Reporting.
///
/// The reports module and <c>purchase.view</c>, on a branch with a trade. A request naming another
/// branch is refused with 403; a missing or reversed date, an unknown status, or a vendor that is not
/// the branch's is a field error.
/// </summary>
[ApiController]
[Authorize]
[Route("api/statements/receive-order-track-report")]
[RequireBusinessType]
[RequireModulePermission("reports")]
public sealed class ReceiveOrderTrackReportController : ControllerBase
{
    private readonly ReceiveOrderTrackReportRepository _report;

    public ReceiveOrderTrackReportController(ReceiveOrderTrackReportRepository report) => _report = report;

    /// <summary>A page of the period's receipts with their billing status, how many have each status, and the totals of all of them.</summary>
    [HttpGet]
    [RequirePermission("purchase.view")]
    public async Task<IActionResult> Run([FromQuery] ReceiveOrderTrackReportRequest request, CancellationToken ct)
    {
        if (request.OrgId is Guid orgId
            && !(Guid.TryParse(User.FindFirstValue(OrgRouteMustMatchTokenAttribute.ClaimType), out Guid tokenOrg)
                && tokenOrg == orgId))
        {
            return Forbid();
        }

        foreach ((string field, string message) in ReceiveOrderTrackReportRepository.Refusals(request))
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
            ModelState.AddModelError(nameof(ReceiveOrderTrackReportRequest.ContactId), "That vendor is not on this branch.");
            return ValidationProblem(ModelState);
        }

        return Ok(await _report.RunAsync(request, ct));
    }

    /// <summary>The branch's vendors, for the filter.</summary>
    [HttpGet("vendors")]
    [RequirePermission("purchase.view")]
    public async Task<IActionResult> Vendors(CancellationToken ct) => Ok(await _report.VendorsAsync(ct));
}
