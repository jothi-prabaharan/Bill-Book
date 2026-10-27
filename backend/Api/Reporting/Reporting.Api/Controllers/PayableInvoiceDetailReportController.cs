using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Reporting.Entity.Models;
using Reporting.Repository.Repositories;
using Shared.Kernel.Internal;

namespace Reporting.Api.Controllers;

/// <summary>
/// The Payable Invoice Detail (TK-236): its own endpoint, request and answer, for its own page at
/// <c>/reports/payable-invoice-detail</c>. The Payable Invoice Summary line by line: each vendor bill
/// dated in a period, a page at a time, with its lines, and the totals of every line shown, in the
/// branch's base currency. The vendors, items and item categories its filters offer are under
/// <c>…/vendors</c>, <c>…/items</c> and <c>…/item-categories</c>. The gateway routes the exact paths to
/// Reporting.
///
/// The reports module and <c>purchase.view</c>, on a branch with a trade. A request naming another branch is
/// refused with 403; a missing or reversed date, or a vendor, item or category that is not the branch's,
/// is a field error.
/// </summary>
[ApiController]
[Authorize]
[Route("api/statements/payable-invoice-detail")]
[RequireBusinessType]
[RequireModulePermission("reports")]
public sealed class PayableInvoiceDetailReportController : ControllerBase
{
    private readonly PayableInvoiceDetailReportRepository _report;

    public PayableInvoiceDetailReportController(PayableInvoiceDetailReportRepository report) => _report = report;

    /// <summary>A page of the period's bills with their lines, how many have each status, and the totals of every line shown.</summary>
    [HttpGet]
    [RequirePermission("purchase.view")]
    public async Task<IActionResult> Run([FromQuery] PayableInvoiceDetailReportRequest request, CancellationToken ct)
    {
        if (request.OrgId is Guid orgId
            && !(Guid.TryParse(User.FindFirstValue(OrgRouteMustMatchTokenAttribute.ClaimType), out Guid tokenOrg)
                && tokenOrg == orgId))
        {
            return Forbid();
        }

        foreach ((string field, string message) in PayableInvoiceDetailReportRepository.Refusals(request))
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

    /// <summary>The branch's vendors, for the filter.</summary>
    [HttpGet("vendors")]
    [RequirePermission("purchase.view")]
    public async Task<IActionResult> Vendors(CancellationToken ct) => Ok(await _report.VendorsAsync(ct));

    /// <summary>The branch's items, for the filter.</summary>
    [HttpGet("items")]
    [RequirePermission("purchase.view")]
    public async Task<IActionResult> Items(CancellationToken ct) => Ok(await _report.ItemsAsync(ct));

    /// <summary>The branch's item categories, for the filter.</summary>
    [HttpGet("item-categories")]
    [RequirePermission("purchase.view")]
    public async Task<IActionResult> ItemCategories(CancellationToken ct) => Ok(await _report.CategoriesAsync(ct));
}
