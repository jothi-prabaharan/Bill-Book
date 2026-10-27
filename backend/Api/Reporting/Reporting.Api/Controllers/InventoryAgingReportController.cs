using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Reporting.Entity.Models;
using Reporting.Repository.Repositories;
using Shared.Kernel.Internal;

namespace Reporting.Api.Controllers;

/// <summary>
/// The Inventory Aging Report (TK-246): dedicated endpoint and page at
/// <c>/reports/inventory-aging-report</c>.
/// Ages inventory into 0-30, 31-60, 61-90, 91-180, and 180+ day buckets.
/// </summary>
[ApiController]
[Authorize]
[Route("api/statements/inventory-aging-report")]
[RequireBusinessType]
[RequireModulePermission("reports")]
public sealed class InventoryAgingReportController : ControllerBase
{
    private readonly InventoryAgingReportRepository _report;

    public InventoryAgingReportController(InventoryAgingReportRepository report) => _report = report;

    /// <summary>Calculates inventory aging as at a given date with optional category and warehouse filters.</summary>
    [HttpGet]
    [RequirePermission("inventory.view")]
    public async Task<IActionResult> Run([FromQuery] InventoryAgingReportRequest request, CancellationToken ct)
    {
        if (request.OrgId is Guid orgId
            && !(Guid.TryParse(User.FindFirstValue(OrgRouteMustMatchTokenAttribute.ClaimType), out Guid tokenOrg)
                && tokenOrg == orgId))
        {
            return Forbid();
        }

        foreach (var (field, message) in InventoryAgingReportRepository.Refusals(request))
        {
            ModelState.AddModelError(field, message);
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var scopeRefusals = await _report.ValidateScopeAsync(request, ct);
        foreach (var (field, message) in scopeRefusals)
        {
            ModelState.AddModelError(field, message);
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        return Ok(await _report.GetReportAsync(request, ct));
    }

    /// <summary>Categories for the category filter dropdown.</summary>
    [HttpGet("item-categories")]
    [RequirePermission("inventory.view")]
    public async Task<IActionResult> Categories(CancellationToken ct) =>
        Ok(await _report.GetCategoriesAsync(ct));

    /// <summary>Warehouses for the warehouse filter dropdown.</summary>
    [HttpGet("warehouses")]
    [RequirePermission("inventory.view")]
    public async Task<IActionResult> Warehouses(CancellationToken ct) =>
        Ok(await _report.GetWarehousesAsync(ct));
}
