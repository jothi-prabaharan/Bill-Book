using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Reporting.Entity.Models;
using Reporting.Repository.Repositories;
using Shared.Kernel.Internal;

namespace Reporting.Api.Controllers;

/// <summary>
/// The Serial Tracking Status Report (TK-249): dedicated endpoint and page at
/// <c>/reports/serial-tracking-status-report</c>.
/// Shows every serial number as at a date: in stock, sold (to whom), returned or written off.
/// </summary>
[ApiController]
[Authorize]
[Route("api/statements/serial-tracking-status-report")]
[RequireBusinessType]
[RequireModulePermission("reports")]
public sealed class SerialTrackingStatusReportController : ControllerBase
{
    private readonly SerialTrackingStatusReportRepository _report;

    public SerialTrackingStatusReportController(SerialTrackingStatusReportRepository report) => _report = report;

    /// <summary>Generates the serial tracking status report as at a required date.</summary>
    [HttpGet]
    [RequirePermission("inventory.view")]
    public async Task<IActionResult> Run([FromQuery] SerialTrackingStatusReportRequest request, CancellationToken ct)
    {
        if (request.OrgId is Guid orgId
            && !(Guid.TryParse(User.FindFirstValue(OrgRouteMustMatchTokenAttribute.ClaimType), out Guid tokenOrg)
                && tokenOrg == orgId))
        {
            return Forbid();
        }

        foreach (var (field, message) in SerialTrackingStatusReportRepository.Refusals(request))
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

    /// <summary>Items with serial tracking for the filter dropdown.</summary>
    [HttpGet("items")]
    [RequirePermission("inventory.view")]
    public async Task<IActionResult> Items(CancellationToken ct) =>
        Ok(await _report.ItemsAsync(ct));

    /// <summary>Warehouses for the filter dropdown.</summary>
    [HttpGet("warehouses")]
    [RequirePermission("inventory.view")]
    public async Task<IActionResult> Warehouses(CancellationToken ct) =>
        Ok(await _report.WarehousesAsync(ct));
}
