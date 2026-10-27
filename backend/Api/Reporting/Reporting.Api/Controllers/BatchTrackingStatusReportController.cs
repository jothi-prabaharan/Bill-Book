using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Reporting.Entity.Models;
using Reporting.Repository.Repositories;
using Shared.Kernel.Internal;

namespace Reporting.Api.Controllers;

/// <summary>
/// The Batch Tracking Status Report (TK-247): dedicated endpoint and page at
/// <c>/reports/batch-tracking-status-report</c>.
/// Displays every batch with manufacture and expiry dates, received and available quantities.
/// </summary>
[ApiController]
[Authorize]
[Route("api/statements/batch-tracking-status-report")]
[RequireBusinessType]
[RequireModulePermission("reports")]
public sealed class BatchTrackingStatusReportController : ControllerBase
{
    private readonly BatchTrackingStatusReportRepository _report;

    public BatchTrackingStatusReportController(BatchTrackingStatusReportRepository report) => _report = report;

    /// <summary>Calculates batch tracking status as at a given date with optional item and warehouse filters.</summary>
    [HttpGet]
    [RequirePermission("inventory.view")]
    public async Task<IActionResult> Run([FromQuery] BatchTrackingStatusReportRequest request, CancellationToken ct)
    {
        if (request.OrgId is Guid orgId
            && !(Guid.TryParse(User.FindFirstValue(OrgRouteMustMatchTokenAttribute.ClaimType), out Guid tokenOrg)
                && tokenOrg == orgId))
        {
            return Forbid();
        }

        foreach (var (field, message) in BatchTrackingStatusReportRepository.Refusals(request))
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

    /// <summary>Items with batches for the filter dropdown.</summary>
    [HttpGet("items")]
    [RequirePermission("inventory.view")]
    public async Task<IActionResult> Items(CancellationToken ct) =>
        Ok(await _report.GetItemsAsync(ct));

    /// <summary>Warehouses for the filter dropdown.</summary>
    [HttpGet("warehouses")]
    [RequirePermission("inventory.view")]
    public async Task<IActionResult> Warehouses(CancellationToken ct) =>
        Ok(await _report.GetWarehousesAsync(ct));
}
