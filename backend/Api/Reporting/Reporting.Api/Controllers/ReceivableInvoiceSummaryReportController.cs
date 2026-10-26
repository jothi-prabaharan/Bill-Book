using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Reporting.Entity.Models;
using Reporting.Repository.Repositories;
using Shared.Kernel.Internal;

namespace Reporting.Api.Controllers;

/// <summary>
/// The Receivable Invoice Summary (TK-224): its own endpoint, request and answer, for its own
/// page at <c>/reports/receivable-invoice-summary</c>. Every posted sales invoice dated in a
/// period with its taxable value, tax and total, what was received and what is due, a page at a
/// time, in the branch's base currency. The customers and currencies its filters offer are under
/// <c>…/customers</c> and <c>…/currencies</c>. The gateway routes the exact paths to Reporting.
///
/// The reports module and <c>sales.view</c>, on a branch with a trade. A request naming another
/// branch is refused with 403; a missing or reversed date, a currency that is not three letters, or
/// a customer that is not the branch's is a field error.
/// </summary>
[ApiController]
[Authorize]
[Route("api/statements/receivable-invoice-summary")]
[RequireBusinessType]
[RequireModulePermission("reports")]
public sealed class ReceivableInvoiceSummaryReportController : ControllerBase
{
    private readonly ReceivableInvoiceSummaryReportRepository _report;

    public ReceivableInvoiceSummaryReportController(ReceivableInvoiceSummaryReportRepository report) => _report = report;

    /// <summary>A page of the period's invoices with their figures, how many have each status, and the totals of all of them.</summary>
    [HttpGet]
    [RequirePermission("sales.view")]
    public async Task<IActionResult> Run([FromQuery] ReceivableInvoiceSummaryReportRequest request, CancellationToken ct)
    {
        if (request.OrgId is Guid orgId
            && !(Guid.TryParse(User.FindFirstValue(OrgRouteMustMatchTokenAttribute.ClaimType), out Guid tokenOrg)
                && tokenOrg == orgId))
        {
            return Forbid();
        }

        foreach ((string field, string message) in ReceivableInvoiceSummaryReportRepository.Refusals(request))
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
            ModelState.AddModelError(nameof(ReceivableInvoiceSummaryReportRequest.ContactId), "That customer is not on this branch.");
            return ValidationProblem(ModelState);
        }

        return Ok(await _report.RunAsync(request, ct));
    }

    /// <summary>The branch's customers, for the filter.</summary>
    [HttpGet("customers")]
    [RequirePermission("sales.view")]
    public async Task<IActionResult> Customers(CancellationToken ct) => Ok(await _report.CustomersAsync(ct));

    /// <summary>The currencies the branch has invoiced in, for the filter.</summary>
    [HttpGet("currencies")]
    [RequirePermission("sales.view")]
    public async Task<IActionResult> Currencies(CancellationToken ct) => Ok(await _report.CurrenciesAsync(ct));
}
