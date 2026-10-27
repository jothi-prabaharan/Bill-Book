using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Reporting.Entity.Models;
using Reporting.Repository.Repositories;
using Shared.Kernel.Internal;

namespace Reporting.Api.Controllers;

/// <summary>
/// The Purchase Analysis (TK-239): its own endpoint, request and answer, for its own page at
/// <c>/reports/purchase-analysis</c>. What was bought in a period, added up by item, item category or
/// vendor: quantity, gross value, discount, net purchases, the average cost of a unit and each group's
/// share of the whole, with an optional second period to compare net purchases with, a page of groups
/// at a time. The vendors and item categories its filters offer are under <c>…/vendors</c> and
/// <c>…/item-categories</c>. The gateway routes the exact paths to Reporting.
///
/// The reports module and <c>purchase.view</c>, on a branch with a trade. A request naming another
/// branch is refused with 403; a missing or reversed date, an incomplete comparison period, an
/// unknown grouping, or a vendor or category that is not the branch's is a field error.
/// </summary>
[ApiController]
[Authorize]
[Route("api/statements/purchase-analysis")]
[RequireBusinessType]
[RequireModulePermission("reports")]
public sealed class PurchaseAnalysisReportController : ControllerBase
{
    private readonly PurchaseAnalysisReportRepository _report;

    public PurchaseAnalysisReportController(PurchaseAnalysisReportRepository report) => _report = report;

    /// <summary>A page of the period's purchases added up by item, category or vendor, with the totals of all of them.</summary>
    [HttpGet]
    [RequirePermission("purchase.view")]
    public async Task<IActionResult> Run([FromQuery] PurchaseAnalysisReportRequest request, CancellationToken ct)
    {
        if (request.OrgId is Guid orgId
            && !(Guid.TryParse(User.FindFirstValue(OrgRouteMustMatchTokenAttribute.ClaimType), out Guid tokenOrg)
                && tokenOrg == orgId))
        {
            return Forbid();
        }

        foreach ((string field, string message) in PurchaseAnalysisReportRepository.Refusals(request))
        {
            ModelState.AddModelError(field, message);
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        // The query filter decides which of these are this branch's; another branch's is a refusal at the field.
        if (request.ContactId is long contactId && !await _report.ContactExistsAsync(contactId, ct))
        {
            ModelState.AddModelError(nameof(PurchaseAnalysisReportRequest.ContactId), "That vendor is not on this branch.");
        }

        if (request.ItemCategoryId is long categoryId && !await _report.CategoryExistsAsync(categoryId, ct))
        {
            ModelState.AddModelError(nameof(PurchaseAnalysisReportRequest.ItemCategoryId), "That item category is not on this branch.");
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

    /// <summary>The branch's item categories, for the filter.</summary>
    [HttpGet("item-categories")]
    [RequirePermission("purchase.view")]
    public async Task<IActionResult> ItemCategories(CancellationToken ct) => Ok(await _report.CategoriesAsync(ct));
}
