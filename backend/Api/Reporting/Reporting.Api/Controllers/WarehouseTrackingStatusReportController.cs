using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Reporting.Entity.Models;
using Reporting.Repository.Repositories;
using Shared.Kernel.Internal;

namespace Reporting.Api.Controllers;

/// <summary>
/// The Warehouse Tracking Status Report (TK-251): dedicated endpoint and page at
/// <c>/reports/warehouse-tracking-status-report</c>.
/// For each warehouse as at a date: quantities received, issued, and available, item by item.
/// </summary>
[ApiController]
[Authorize]
[Route("api/statements/warehouse-tracking-status-report")]
[RequireBusinessType]
[RequireModulePermission("reports")]
public sealed class WarehouseTrackingStatusReportController : ControllerBase
{
    private readonly WarehouseTrackingStatusReportRepository _report;

    public WarehouseTrackingStatusReportController(WarehouseTrackingStatusReportRepository report) => _report = report;

    /// <summary>Generates the warehouse tracking status report as at a required date.</summary>
    [HttpGet]
    [RequirePermission("inventory.view")]
    public async Task<IActionResult> Run([FromQuery] WarehouseTrackingStatusReportRequest request, CancellationToken ct)
    {
        if (request.OrgId is Guid orgId
            && !(Guid.TryParse(User.FindFirstValue(OrgRouteMustMatchTokenAttribute.ClaimType), out Guid tokenOrg)
                && tokenOrg == orgId))
        {
            return Forbid();
        }

        foreach (var (field, message) in WarehouseTrackingStatusReportRepository.Refusals(request))
        {
            ModelState.AddModelError(field, message);
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var lookupRefusals = await _report.LookupRefusalsAsync(request, ct);
        foreach (var (field, message) in lookupRefusals)
        {
            ModelState.AddModelError(field, message);
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        return Ok(await _report.RunAsync(request, ct));
    }

    /// <summary>Warehouses on this branch for the filter dropdown.</summary>
    [HttpGet("warehouses")]
    [RequirePermission("inventory.view")]
    public async Task<IActionResult> Warehouses(CancellationToken ct) =>
        Ok(await _report.WarehousesAsync(ct));

    /// <summary>Item categories on this branch for the filter dropdown.</summary>
    [HttpGet("categories")]
    [RequirePermission("inventory.view")]
    public async Task<IActionResult> Categories(CancellationToken ct) =>
        Ok(await _report.CategoriesAsync(ct));
}
