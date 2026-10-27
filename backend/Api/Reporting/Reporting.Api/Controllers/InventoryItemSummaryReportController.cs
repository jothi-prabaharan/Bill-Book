using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Reporting.Entity.Models;
using Reporting.Repository.Repositories;
using Shared.Kernel.Internal;

namespace Reporting.Api.Controllers;

/// <summary>
/// The Inventory Item Summary (TK-243): its own endpoint, request and answer, for its own page at
/// <c>/reports/inventory-item-summary</c>. Each stocked item over a period, by name, with its opening
/// quantity and value, what was purchased, sold and adjusted, its closing quantity and value, and the
/// profit on what was sold, and the values of every item the filters match added up. The items and
/// categories its filters offer are under <c>…/items</c> and <c>…/item-categories</c>. The gateway routes
/// the exact paths to Reporting.
///
/// The reports module and <c>inventory.view</c>, on a branch with a trade. A request naming another branch
/// is refused with 403; a missing or reversed date, or an item or category that is not the branch's, is a
/// field error.
/// </summary>
[ApiController]
[Authorize]
[Route("api/statements/inventory-item-summary")]
[RequireBusinessType]
[RequireModulePermission("reports")]
public sealed class InventoryItemSummaryReportController : ControllerBase
{
    private readonly InventoryItemSummaryReportRepository _report;

    public InventoryItemSummaryReportController(InventoryItemSummaryReportRepository report) => _report = report;

    /// <summary>A page of the period's items with their stock movements and closing stock, and the values of every item the filters match.</summary>
    [HttpGet]
    [RequirePermission("inventory.view")]
    public async Task<IActionResult> Run([FromQuery] InventoryItemSummaryReportRequest request, CancellationToken ct)
    {
        if (request.OrgId is Guid orgId
            && !(Guid.TryParse(User.FindFirstValue(OrgRouteMustMatchTokenAttribute.ClaimType), out Guid tokenOrg)
                && tokenOrg == orgId))
        {
            return Forbid();
        }

        foreach ((string field, string message) in InventoryItemSummaryReportRepository.Refusals(request))
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

    /// <summary>The branch's stocked items, for the filter.</summary>
    [HttpGet("items")]
    [RequirePermission("inventory.view")]
    public async Task<IActionResult> Items(CancellationToken ct) => Ok(await _report.ItemsAsync(ct));

    /// <summary>The branch's item categories, for the filter.</summary>
    [HttpGet("item-categories")]
    [RequirePermission("inventory.view")]
    public async Task<IActionResult> ItemCategories(CancellationToken ct) => Ok(await _report.CategoriesAsync(ct));
}
