using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Reporting.Entity.Models;
using Reporting.Repository.Repositories;
using Shared.Kernel.Internal;

namespace Reporting.Api.Controllers;

/// <summary>
/// The Invoice Track Report (TK-226): its own endpoint, request and answer, for its own page at
/// <c>/reports/invoice-track-report</c>. Where each sales invoice dated in a period is in its life
/// (a draft, ready to post, awaiting payment, partly paid, overdue, paid or void), with the date
/// it was posted, its due date, the last settlement of it and what is still due, a page at a time.
/// The customers its filter offers are under <c>…/customers</c>. The gateway routes the exact
/// paths to Reporting.
///
/// The reports module and <c>sales.view</c>, on a branch with a trade. A request naming another
/// branch is refused with 403; a missing or reversed date, an unknown status, or a customer that
/// is not the branch's is a field error.
/// </summary>
[ApiController]
[Authorize]
[Route("api/statements/invoice-track-report")]
[RequireBusinessType]
[RequireModulePermission("reports")]
public sealed class InvoiceTrackReportController : ControllerBase
{
    private readonly InvoiceTrackReportRepository _report;

    public InvoiceTrackReportController(InvoiceTrackReportRepository report) => _report = report;

    /// <summary>A page of the period's invoices with where each stands, how many have each status, and the totals of the posted ones.</summary>
    [HttpGet]
    [RequirePermission("sales.view")]
    public async Task<IActionResult> Run([FromQuery] InvoiceTrackReportRequest request, CancellationToken ct)
    {
        if (request.OrgId is Guid orgId
            && !(Guid.TryParse(User.FindFirstValue(OrgRouteMustMatchTokenAttribute.ClaimType), out Guid tokenOrg)
                && tokenOrg == orgId))
        {
            return Forbid();
        }

        foreach ((string field, string message) in InvoiceTrackReportRepository.Refusals(request))
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
            ModelState.AddModelError(nameof(InvoiceTrackReportRequest.ContactId), "That customer is not on this branch.");
            return ValidationProblem(ModelState);
        }

        return Ok(await _report.RunAsync(request, ct));
    }

    /// <summary>The branch's customers, for the filter.</summary>
    [HttpGet("customers")]
    [RequirePermission("sales.view")]
    public async Task<IActionResult> Customers(CancellationToken ct) => Ok(await _report.CustomersAsync(ct));
}
