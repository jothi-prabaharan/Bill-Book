using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Reporting.Entity.Models;
using Reporting.Repository.Repositories;
using Shared.Kernel.Internal;

namespace Reporting.Api.Controllers;

/// <summary>
/// The Sales Order Track Report (TK-230): its own endpoint, request and answer, for its own page at
/// <c>/reports/sales-order-track-report</c>. Each sales order dated in a period, newest first, with how
/// far it has got (open, partly delivered, delivered, invoiced, closed short or cancelled), its lines
/// with what was ordered, delivered and invoiced of each and what is still to deliver or bill, and the
/// totals of every order the filters match. The customers its filter offers are under
/// <c>…/customers</c>. The gateway routes the exact paths to Reporting.
///
/// The reports module and <c>sales.view</c>, on a branch with a trade. A request naming another
/// branch is refused with 403; a missing or reversed date, an unknown progress, or a customer that is
/// not the branch's is a field error.
/// </summary>
[ApiController]
[Authorize]
[Route("api/statements/sales-order-track-report")]
[RequireBusinessType]
[RequireModulePermission("reports")]
public sealed class SalesOrderTrackReportController : ControllerBase
{
    private readonly SalesOrderTrackReportRepository _report;

    public SalesOrderTrackReportController(SalesOrderTrackReportRepository report) => _report = report;

    /// <summary>A page of the period's orders with their progress and lines, how many are at each progress, and the totals of all of them.</summary>
    [HttpGet]
    [RequirePermission("sales.view")]
    public async Task<IActionResult> Run([FromQuery] SalesOrderTrackReportRequest request, CancellationToken ct)
    {
        if (request.OrgId is Guid orgId
            && !(Guid.TryParse(User.FindFirstValue(OrgRouteMustMatchTokenAttribute.ClaimType), out Guid tokenOrg)
                && tokenOrg == orgId))
        {
            return Forbid();
        }

        foreach ((string field, string message) in SalesOrderTrackReportRepository.Refusals(request))
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
            ModelState.AddModelError(nameof(SalesOrderTrackReportRequest.ContactId), "That customer is not on this branch.");
            return ValidationProblem(ModelState);
        }

        return Ok(await _report.RunAsync(request, ct));
    }

    /// <summary>The branch's customers, for the filter.</summary>
    [HttpGet("customers")]
    [RequirePermission("sales.view")]
    public async Task<IActionResult> Customers(CancellationToken ct) => Ok(await _report.CustomersAsync(ct));
}
