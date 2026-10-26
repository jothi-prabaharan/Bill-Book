using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Reporting.Entity.Models;
using Reporting.Repository.Repositories;
using Shared.Kernel.Internal;

namespace Reporting.Api.Controllers;

/// <summary>
/// The Receivable Invoice Detail (TK-225): its own endpoint, request and answer, for its own page at
/// <c>/reports/receivable-invoice-detail</c>. The Receivable Invoice Summary line by line: each invoice
/// dated in a period, a page at a time, with its lines, and the totals of every line shown, in the
/// branch's base currency. The customers, items and item categories its filters offer are under
/// <c>…/customers</c>, <c>…/items</c> and <c>…/item-categories</c>. The gateway routes the exact paths to
/// Reporting.
///
/// The reports module and <c>sales.view</c>, on a branch with a trade. A request naming another branch is
/// refused with 403; a missing or reversed date, or a customer, item or category that is not the branch's,
/// is a field error.
/// </summary>
[ApiController]
[Authorize]
[Route("api/statements/receivable-invoice-detail")]
[RequireBusinessType]
[RequireModulePermission("reports")]
public sealed class ReceivableInvoiceDetailReportController : ControllerBase
{
    private readonly ReceivableInvoiceDetailReportRepository _report;

    public ReceivableInvoiceDetailReportController(ReceivableInvoiceDetailReportRepository report) => _report = report;

    /// <summary>A page of the period's invoices with their lines, how many have each status, and the totals of every line shown.</summary>
    [HttpGet]
    [RequirePermission("sales.view")]
    public async Task<IActionResult> Run([FromQuery] ReceivableInvoiceDetailReportRequest request, CancellationToken ct)
    {
        if (request.OrgId is Guid orgId
            && !(Guid.TryParse(User.FindFirstValue(OrgRouteMustMatchTokenAttribute.ClaimType), out Guid tokenOrg)
                && tokenOrg == orgId))
        {
            return Forbid();
        }

        foreach ((string field, string message) in ReceivableInvoiceDetailReportRepository.Refusals(request))
        {
            ModelState.AddModelError(field, message);
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        foreach ((string field, string message) in await _report.LookupRefusalsAsync(request, ct))
        {
            ModelState.AddModelError(field, message);
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        return Ok(await _report.RunAsync(request, ct));
    }

    /// <summary>The branch's customers, for the filter.</summary>
    [HttpGet("customers")]
    [RequirePermission("sales.view")]
    public async Task<IActionResult> Customers(CancellationToken ct) => Ok(await _report.CustomersAsync(ct));

    /// <summary>The branch's items, for the filter.</summary>
    [HttpGet("items")]
    [RequirePermission("sales.view")]
    public async Task<IActionResult> Items(CancellationToken ct) => Ok(await _report.ItemsAsync(ct));

    /// <summary>The branch's item categories, for the filter.</summary>
    [HttpGet("item-categories")]
    [RequirePermission("sales.view")]
    public async Task<IActionResult> ItemCategories(CancellationToken ct) => Ok(await _report.CategoriesAsync(ct));
}
