using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Reporting.Entity.Models;
using Reporting.Repository.Repositories;
using Shared.Kernel.Internal;

namespace Reporting.Api.Controllers;

/// <summary>
/// The Sales Analysis Detail Report (TK-229): its own endpoint, request and answer, for its own page
/// at <c>/reports/sales-analysis-detail-report</c>. The Sales Analysis before it is added up: one
/// row for each invoice or credit note line sold in a period, grouped by item, item category or
/// customer, each group with its subtotal, a page of lines at a time. The customers, items, item
/// categories and warehouses its filters offer are under <c>…/customers</c>, <c>…/items</c>,
/// <c>…/item-categories</c> and <c>…/warehouses</c>. The gateway routes the exact paths to Reporting.
///
/// The reports module and <c>sales.view</c>, on a branch with a trade. A request naming another
/// branch is refused with 403; a missing or reversed date, an unknown grouping, or a customer, item,
/// category or warehouse that is not the branch's is a field error.
/// </summary>
[ApiController]
[Authorize]
[Route("api/statements/sales-analysis-detail-report")]
[RequireBusinessType]
[RequireModulePermission("reports")]
public sealed class SalesAnalysisDetailReportController : ControllerBase
{
    private readonly SalesAnalysisDetailReportRepository _report;

    public SalesAnalysisDetailReportController(SalesAnalysisDetailReportRepository report) => _report = report;

    /// <summary>A page of the period's sales lines in their groups, each group with its subtotal, and the totals of all of them.</summary>
    [HttpGet]
    [RequirePermission("sales.view")]
    public async Task<IActionResult> Run([FromQuery] SalesAnalysisDetailReportRequest request, CancellationToken ct)
    {
        if (request.OrgId is Guid orgId
            && !(Guid.TryParse(User.FindFirstValue(OrgRouteMustMatchTokenAttribute.ClaimType), out Guid tokenOrg)
                && tokenOrg == orgId))
        {
            return Forbid();
        }

        foreach ((string field, string message) in SalesAnalysisDetailReportRepository.Refusals(request))
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
            ModelState.AddModelError(nameof(SalesAnalysisDetailReportRequest.ContactId), "That customer is not on this branch.");
        }

        if (request.ItemId is long itemId && !await _report.ItemExistsAsync(itemId, ct))
        {
            ModelState.AddModelError(nameof(SalesAnalysisDetailReportRequest.ItemId), "That item is not on this branch.");
        }

        if (request.ItemCategoryId is long categoryId && !await _report.CategoryExistsAsync(categoryId, ct))
        {
            ModelState.AddModelError(nameof(SalesAnalysisDetailReportRequest.ItemCategoryId), "That item category is not on this branch.");
        }

        if (request.WarehouseId is long warehouseId && !await _report.WarehouseExistsAsync(warehouseId, ct))
        {
            ModelState.AddModelError(nameof(SalesAnalysisDetailReportRequest.WarehouseId), "That warehouse is not on this branch.");
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

    /// <summary>The branch's active items, for the filter.</summary>
    [HttpGet("items")]
    [RequirePermission("sales.view")]
    public async Task<IActionResult> Items(CancellationToken ct) => Ok(await _report.ItemsAsync(ct));

    /// <summary>The branch's item categories, for the filter.</summary>
    [HttpGet("item-categories")]
    [RequirePermission("sales.view")]
    public async Task<IActionResult> ItemCategories(CancellationToken ct) => Ok(await _report.CategoriesAsync(ct));

    /// <summary>The branch's warehouses, for the filter.</summary>
    [HttpGet("warehouses")]
    [RequirePermission("sales.view")]
    public async Task<IActionResult> Warehouses(CancellationToken ct) => Ok(await _report.WarehousesAsync(ct));
}
