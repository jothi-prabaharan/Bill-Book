using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Reporting.Entity.Models;
using Reporting.Repository.Repositories;
using Shared.Kernel.Internal;

namespace Reporting.Api.Controllers;

/// <summary>
/// The Inventory Item List (TK-245): its own endpoint, request and answer, for its own page at
/// <c>/reports/inventory-item-list</c>. Lists stocked and non-stocked items as at a given date with
/// settings, accounts, taxes, quantity on hand, average and FIFO cost, value, and open PO/SO quantities.
/// </summary>
[ApiController]
[Authorize]
[Route("api/statements/inventory-item-list")]
[RequireBusinessType]
[RequireModulePermission("reports")]
public sealed class InventoryItemListReportController : ControllerBase
{
    private readonly InventoryItemListReportRepository _report;

    public InventoryItemListReportController(InventoryItemListReportRepository report) => _report = report;

    /// <summary>A page of items as at a date with quantity on hand, costs, total value, and on-order/committed quantities.</summary>
    [HttpGet]
    [RequirePermission("inventory.view")]
    public async Task<IActionResult> Run([FromQuery] InventoryItemListReportRequest request, CancellationToken ct)
    {
        if (request.OrgId is Guid orgId
            && !(Guid.TryParse(User.FindFirstValue(OrgRouteMustMatchTokenAttribute.ClaimType), out Guid tokenOrg)
                && tokenOrg == orgId))
        {
            return Forbid();
        }

        if (!request.AsOfDate.HasValue)
        {
            ModelState.AddModelError("AsOfDate", "As at date is required.");
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

    /// <summary>Item types for the type filter dropdown.</summary>
    [HttpGet("item-types")]
    [RequirePermission("inventory.view")]
    public IActionResult ItemTypes() =>
        Ok(new[]
        {
            new { Key = "Inventory", Label = "Inventory Item" },
            new { Key = "NonInventory", Label = "Non-Inventory Item" },
            new { Key = "Service", Label = "Service" }
        });

    /// <summary>Item statuses for the status filter dropdown.</summary>
    [HttpGet("statuses")]
    [RequirePermission("inventory.view")]
    public IActionResult Statuses() =>
        Ok(new[]
        {
            new { Key = "Active", Label = "Active" },
            new { Key = "Inactive", Label = "Inactive" }
        });
}
