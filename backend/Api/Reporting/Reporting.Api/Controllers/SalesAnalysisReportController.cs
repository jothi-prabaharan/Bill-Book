using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Reporting.Entity.Models;
using Reporting.Repository.Repositories;
using Shared.Kernel.Internal;

namespace Reporting.Api.Controllers;

/// <summary>
/// The Sales Analysis (TK-228): its own endpoint, request and answer, for its own page at
/// <c>/reports/sales-analysis</c>. What sold in a period, added up by item, item category or
/// customer: quantity, gross value, discount, net sales, cost of sales and margin, with an optional
/// second period to compare net sales with, a page of groups at a time. The customers, item
/// categories and warehouses its filters offer are under <c>…/customers</c>,
/// <c>…/item-categories</c> and <c>…/warehouses</c>. The gateway routes the exact paths to Reporting.
///
/// The reports module and <c>sales.view</c>, on a branch with a trade. A request naming another
/// branch is refused with 403; a missing or reversed date, an incomplete comparison period, an
/// unknown grouping, or a customer, category or warehouse that is not the branch's is a field error.
/// </summary>
[ApiController]
[Authorize]
[Route("api/statements/sales-analysis")]
[RequireBusinessType]
[RequireModulePermission("reports")]
public sealed class SalesAnalysisReportController : ControllerBase
{
    private readonly SalesAnalysisReportRepository _report;

    public SalesAnalysisReportController(SalesAnalysisReportRepository report) => _report = report;

    /// <summary>A page of the period's sales added up by item, category or customer, with the totals of all of them.</summary>
    [HttpGet]
    [RequirePermission("sales.view")]
    public async Task<IActionResult> Run([FromQuery] SalesAnalysisReportRequest request, CancellationToken ct)
    {
        if (request.OrgId is Guid orgId
            && !(Guid.TryParse(User.FindFirstValue(OrgRouteMustMatchTokenAttribute.ClaimType), out Guid tokenOrg)
                && tokenOrg == orgId))
        {
            return Forbid();
        }

        foreach ((string field, string message) in SalesAnalysisReportRepository.Refusals(request))
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
            ModelState.AddModelError(nameof(SalesAnalysisReportRequest.ContactId), "That customer is not on this branch.");
        }

        if (request.ItemCategoryId is long categoryId && !await _report.CategoryExistsAsync(categoryId, ct))
        {
            ModelState.AddModelError(nameof(SalesAnalysisReportRequest.ItemCategoryId), "That item category is not on this branch.");
        }

        if (request.WarehouseId is long warehouseId && !await _report.WarehouseExistsAsync(warehouseId, ct))
        {
            ModelState.AddModelError(nameof(SalesAnalysisReportRequest.WarehouseId), "That warehouse is not on this branch.");
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

    /// <summary>The branch's item categories, for the filter.</summary>
    [HttpGet("item-categories")]
    [RequirePermission("sales.view")]
    public async Task<IActionResult> ItemCategories(CancellationToken ct) => Ok(await _report.CategoriesAsync(ct));

    /// <summary>The branch's warehouses, for the filter.</summary>
    [HttpGet("warehouses")]
    [RequirePermission("sales.view")]
    public async Task<IActionResult> Warehouses(CancellationToken ct) => Ok(await _report.WarehousesAsync(ct));
}
