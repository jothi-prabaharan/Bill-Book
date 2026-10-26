using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Reporting.Entity.Models;
using Reporting.Repository.Repositories;
using Shared.Kernel.Internal;

namespace Reporting.Api.Controllers;

/// <summary>
/// The Aged Receivables Summary (TK-222): its own endpoint, request and answer, for its
/// own page at <c>/reports/aged-receivables-summary</c>. What each customer owed on a
/// date, spread over Current and four overdue columns, in the branch's base currency.
/// The customers its filter offers are under <c>…/customers</c>. The gateway routes the
/// exact paths to Reporting.
///
/// The reports module and <c>sales.view</c>, on a branch with a trade. A request naming
/// another branch is refused with 403; a missing as-at date, a bucket width other than
/// 7, 15 or 30, or a customer that is not the branch's is a field error.
/// </summary>
[ApiController]
[Authorize]
[Route("api/statements/aged-receivables-summary")]
[RequireBusinessType]
[RequireModulePermission("reports")]
public sealed class AgedReceivablesSummaryReportController : ControllerBase
{
    private readonly AgedReceivablesSummaryReportRepository _report;

    public AgedReceivablesSummaryReportController(AgedReceivablesSummaryReportRepository report) => _report = report;

    /// <summary>Each customer's balance on a date, by how overdue it is, with the totals.</summary>
    [HttpGet]
    [RequirePermission("sales.view")]
    public async Task<IActionResult> Run([FromQuery] AgedReceivablesSummaryReportRequest request, CancellationToken ct)
    {
        if (request.OrgId is Guid orgId
            && !(Guid.TryParse(User.FindFirstValue(OrgRouteMustMatchTokenAttribute.ClaimType), out Guid tokenOrg)
                && tokenOrg == orgId))
        {
            return Forbid();
        }

        foreach ((string field, string message) in AgedReceivablesSummaryReportRepository.Refusals(request))
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
            ModelState.AddModelError(nameof(AgedReceivablesSummaryReportRequest.ContactId), "That customer is not on this branch.");
            return ValidationProblem(ModelState);
        }

        return Ok(await _report.RunAsync(request, ct));
    }

    /// <summary>The branch's customers, for the filter.</summary>
    [HttpGet("customers")]
    [RequirePermission("sales.view")]
    public async Task<IActionResult> Customers(CancellationToken ct) => Ok(await _report.CustomersAsync(ct));
}
