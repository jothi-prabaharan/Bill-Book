using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Reporting.Entity.Models;
using Reporting.Repository.Repositories;
using Shared.Kernel.Internal;

namespace Reporting.Api.Controllers;

/// <summary>
/// The Delivery Order Track Report (TK-232): its own endpoint, request and answer, for its own page at
/// <c>/reports/delivery-order-track-report</c>. Each delivery challan dated in a period, newest first, with
/// how far it has been billed (not invoiced, partly invoiced or invoiced), its lines with what was
/// delivered and invoiced of each and what is still to bill, the sales order it was delivered against and
/// the invoices raised from it, and the totals of every challan the filters match. The customers its
/// filter offers are under <c>…/customers</c>. The gateway routes the exact paths to Reporting.
///
/// The reports module and <c>sales.view</c>, on a branch with a trade. A request naming another branch is
/// refused with 403; a missing or reversed date, an unknown status or challan type, or a customer that is
/// not the branch's is a field error.
/// </summary>
[ApiController]
[Authorize]
[Route("api/statements/delivery-order-track-report")]
[RequireBusinessType]
[RequireModulePermission("reports")]
public sealed class DeliveryOrderTrackReportController : ControllerBase
{
    private readonly DeliveryOrderTrackReportRepository _report;

    public DeliveryOrderTrackReportController(DeliveryOrderTrackReportRepository report) => _report = report;

    /// <summary>A page of the period's challans with their billing and lines, how many are at each status, and the totals of all of them.</summary>
    [HttpGet]
    [RequirePermission("sales.view")]
    public async Task<IActionResult> Run([FromQuery] DeliveryOrderTrackReportRequest request, CancellationToken ct)
    {
        if (request.OrgId is Guid orgId
            && !(Guid.TryParse(User.FindFirstValue(OrgRouteMustMatchTokenAttribute.ClaimType), out Guid tokenOrg)
                && tokenOrg == orgId))
        {
            return Forbid();
        }

        foreach ((string field, string message) in DeliveryOrderTrackReportRepository.Refusals(request))
        {
            ModelState.AddModelError(field, message);
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        // The query filter decides which customers are this branch's; another branch's is a refusal at the field.
        if (request.ContactId is long contactId && !await _report.ContactExistsAsync(contactId, ct))
        {
            ModelState.AddModelError(nameof(DeliveryOrderTrackReportRequest.ContactId), "That customer is not on this branch.");
            return ValidationProblem(ModelState);
        }

        return Ok(await _report.RunAsync(request, ct));
    }

    /// <summary>The branch's customers, for the filter.</summary>
    [HttpGet("customers")]
    [RequirePermission("sales.view")]
    public async Task<IActionResult> Customers(CancellationToken ct) => Ok(await _report.CustomersAsync(ct));
}
