using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Reporting.Entity.Models;
using Reporting.Repository.Repositories;
using Shared.Kernel.Internal;

namespace Reporting.Api.Controllers;

/// <summary>
/// The Inventory Item Detail (TK-244): its own endpoint, request and answer, for its own page at
/// <c>/reports/inventory-item-detail</c>. Stock card of each stocked item over a period, with opening
/// balance, movements in chronological order, document numbers, running balance, and closing balance.
/// </summary>
[ApiController]
[Authorize]
[Route("api/statements/inventory-item-detail")]
[RequireBusinessType]
[RequireModulePermission("reports")]
public sealed class InventoryItemDetailReportController : ControllerBase
{
    private readonly InventoryItemDetailReportRepository _report;

    public InventoryItemDetailReportController(InventoryItemDetailReportRepository report) => _report = report;

    /// <summary>A page of items with their opening balance, stock movements in the period, and closing balance.</summary>
    [HttpGet]
    [RequirePermission("inventory.view")]
    public async Task<IActionResult> Run([FromQuery] InventoryItemDetailReportRequest request, CancellationToken ct)
    {
        if (request.OrgId is Guid orgId
            && !(Guid.TryParse(User.FindFirstValue(OrgRouteMustMatchTokenAttribute.ClaimType), out Guid tokenOrg)
                && tokenOrg == orgId))
        {
            return Forbid();
        }

        foreach ((string field, string message) in InventoryItemDetailReportRepository.Refusals(request))
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

    /// <summary>The branch's warehouses, for the filter.</summary>
    [HttpGet("warehouses")]
    [RequirePermission("inventory.view")]
    public async Task<IActionResult> Warehouses(CancellationToken ct) => Ok(await _report.WarehousesAsync(ct));
}
