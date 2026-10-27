using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Reporting.Entity.Models;
using Reporting.Repository.Repositories;
using Shared.Kernel.Internal;

namespace Reporting.Api.Controllers;

/// <summary>
/// The Serial Tracking Detail Report (TK-250): dedicated endpoint and page at
/// <c>/reports/serial-tracking-detail-report</c>.
/// Shows every movement of each serial number over a period, grouped by serial.
/// </summary>
[ApiController]
[Authorize]
[Route("api/statements/serial-tracking-detail-report")]
[RequireBusinessType]
[RequireModulePermission("reports")]
public sealed class SerialTrackingDetailReportController : ControllerBase
{
    private readonly SerialTrackingDetailReportRepository _report;

    public SerialTrackingDetailReportController(SerialTrackingDetailReportRepository report) => _report = report;

    /// <summary>Generates the serial tracking detail report for a required date range.</summary>
    [HttpGet]
    [RequirePermission("inventory.view")]
    public async Task<IActionResult> Run([FromQuery] SerialTrackingDetailReportRequest request, CancellationToken ct)
    {
        if (request.OrgId is Guid orgId
            && !(Guid.TryParse(User.FindFirstValue(OrgRouteMustMatchTokenAttribute.ClaimType), out Guid tokenOrg)
                && tokenOrg == orgId))
        {
            return Forbid();
        }

        foreach (var (field, message) in SerialTrackingDetailReportRepository.Refusals(request))
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

    /// <summary>Available serial numbers for the filter dropdown.</summary>
    [HttpGet("serials")]
    [RequirePermission("inventory.view")]
    public async Task<IActionResult> Serials(CancellationToken ct) =>
        Ok(await _report.SerialsAsync(ct));

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

    /// <summary>Contacts for the filter dropdown.</summary>
    [HttpGet("contacts")]
    [RequirePermission("inventory.view")]
    public async Task<IActionResult> Contacts(CancellationToken ct) =>
        Ok(await _report.ContactsAsync(ct));
}
