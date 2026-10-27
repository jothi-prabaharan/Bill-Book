using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Reporting.Entity.Models;
using Reporting.Repository.Repositories;
using Shared.Kernel.Internal;

namespace Reporting.Api.Controllers;

/// <summary>
/// The Batch Tracking Detail Report (TK-248): dedicated endpoint and page at
/// <c>/reports/batch-tracking-detail-report</c>.
/// Displays every movement of each batch in a period, grouped by batch.
/// </summary>
[ApiController]
[Authorize]
[Route("api/statements/batch-tracking-detail-report")]
[RequireBusinessType]
[RequireModulePermission("reports")]
public sealed class BatchTrackingDetailReportController : ControllerBase
{
    private readonly BatchTrackingDetailReportRepository _report;

    public BatchTrackingDetailReportController(BatchTrackingDetailReportRepository report) => _report = report;

    /// <summary>Generates the batch tracking detail report over a required date period.</summary>
    [HttpGet]
    [RequirePermission("inventory.view")]
    public async Task<IActionResult> Run([FromQuery] BatchTrackingDetailReportRequest request, CancellationToken ct)
    {
        if (request.OrgId is Guid orgId
            && !(Guid.TryParse(User.FindFirstValue(OrgRouteMustMatchTokenAttribute.ClaimType), out Guid tokenOrg)
                && tokenOrg == orgId))
        {
            return Forbid();
        }

        if (request.FromDate == null)
        {
            ModelState.AddModelError(nameof(request.FromDate), "From date is required.");
        }

        if (request.ToDate == null)
        {
            ModelState.AddModelError(nameof(request.ToDate), "To date is required.");
        }

        foreach (var (field, message) in BatchTrackingDetailReportRepository.Refusals(request))
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

    /// <summary>Batches for the filter dropdown.</summary>
    [HttpGet("batches")]
    [RequirePermission("inventory.view")]
    public async Task<IActionResult> Batches(CancellationToken ct) =>
        Ok(await _report.BatchesAsync(ct));

    /// <summary>Items for the filter dropdown.</summary>
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
