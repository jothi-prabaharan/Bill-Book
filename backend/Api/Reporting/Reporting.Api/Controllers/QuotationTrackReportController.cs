using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Reporting.Entity.Models;
using Reporting.Repository.Repositories;
using Shared.Kernel.Internal;

namespace Reporting.Api.Controllers;

/// <summary>
/// The Quotation Track Report (TK-231): its own endpoint, request and answer, for its own page at
/// <c>/reports/quotation-track-report</c>. Each quote dated in a period, newest first, with its outcome
/// (open, expired, accepted, converted to an order or an invoice, or declined) and the order or invoice
/// it became, how many quotes are at each outcome, and the conversion rate by count and by value. The
/// customers its filter offers are under <c>…/customers</c>. The gateway routes the exact paths to
/// Reporting.
///
/// The reports module and <c>sales.view</c>, on a branch with a trade. A request naming another
/// branch is refused with 403; a missing or reversed date, an unknown status, or a customer that is not
/// the branch's is a field error.
/// </summary>
[ApiController]
[Authorize]
[Route("api/statements/quotation-track-report")]
[RequireBusinessType]
[RequireModulePermission("reports")]
public sealed class QuotationTrackReportController : ControllerBase
{
    private readonly QuotationTrackReportRepository _report;

    public QuotationTrackReportController(QuotationTrackReportRepository report) => _report = report;

    /// <summary>A page of the period's quotes with their outcomes, how many are at each outcome, and the conversion rate.</summary>
    [HttpGet]
    [RequirePermission("sales.view")]
    public async Task<IActionResult> Run([FromQuery] QuotationTrackReportRequest request, CancellationToken ct)
    {
        if (request.OrgId is Guid orgId
            && !(Guid.TryParse(User.FindFirstValue(OrgRouteMustMatchTokenAttribute.ClaimType), out Guid tokenOrg)
                && tokenOrg == orgId))
        {
            return Forbid();
        }

        foreach ((string field, string message) in QuotationTrackReportRepository.Refusals(request))
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
            ModelState.AddModelError(nameof(QuotationTrackReportRequest.ContactId), "That customer is not on this branch.");
            return ValidationProblem(ModelState);
        }

        return Ok(await _report.RunAsync(request, ct));
    }

    /// <summary>The branch's customers, for the filter.</summary>
    [HttpGet("customers")]
    [RequirePermission("sales.view")]
    public async Task<IActionResult> Customers(CancellationToken ct) => Ok(await _report.CustomersAsync(ct));
}
