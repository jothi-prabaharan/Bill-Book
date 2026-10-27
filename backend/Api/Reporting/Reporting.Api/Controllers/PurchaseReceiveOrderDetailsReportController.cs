using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Reporting.Entity.Models;
using Reporting.Repository.Repositories;
using Shared.Kernel.Internal;

namespace Reporting.Api.Controllers;

/// <summary>
/// The Purchase Receive Order Details (TK-241): its own endpoint, request and answer, for its own page
/// at <c>/reports/purchase-receive-order-details</c>. Every posted goods receipt dated in a period,
/// newest first, with its lines: the item, the warehouse, what was delivered, rejected, accepted and
/// billed, what a unit cost and what the accepted goods are worth, and the batch or serial numbers the
/// receipt created, with the totals of every receipt the filters match. The vendors, items, warehouses
/// and purchase orders its filters offer are under <c>…/vendors</c>, <c>…/items</c>,
/// <c>…/warehouses</c> and <c>…/purchase-orders</c>. The gateway routes the exact paths to Reporting.
///
/// The reports module and <c>purchase.view</c>, on a branch with a trade. A request naming another
/// branch is refused with 403; a missing or reversed date, or a vendor, item, warehouse or purchase
/// order that is not the branch's, is a field error.
/// </summary>
[ApiController]
[Authorize]
[Route("api/statements/purchase-receive-order-details")]
[RequireBusinessType]
[RequireModulePermission("reports")]
public sealed class PurchaseReceiveOrderDetailsReportController : ControllerBase
{
    private readonly PurchaseReceiveOrderDetailsReportRepository _report;

    public PurchaseReceiveOrderDetailsReportController(PurchaseReceiveOrderDetailsReportRepository report) => _report = report;

    /// <summary>A page of the period's goods receipts with their lines, and the totals of every receipt the filters match.</summary>
    [HttpGet]
    [RequirePermission("purchase.view")]
    public async Task<IActionResult> Run([FromQuery] PurchaseReceiveOrderDetailsReportRequest request, CancellationToken ct)
    {
        if (request.OrgId is Guid orgId
            && !(Guid.TryParse(User.FindFirstValue(OrgRouteMustMatchTokenAttribute.ClaimType), out Guid tokenOrg)
                && tokenOrg == orgId))
        {
            return Forbid();
        }

        foreach ((string field, string message) in PurchaseReceiveOrderDetailsReportRepository.Refusals(request))
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

    /// <summary>The branch's vendors, for the filter.</summary>
    [HttpGet("vendors")]
    [RequirePermission("purchase.view")]
    public async Task<IActionResult> Vendors(CancellationToken ct) => Ok(await _report.VendorsAsync(ct));

    /// <summary>The branch's items, for the filter.</summary>
    [HttpGet("items")]
    [RequirePermission("purchase.view")]
    public async Task<IActionResult> Items(CancellationToken ct) => Ok(await _report.ItemsAsync(ct));

    /// <summary>The branch's warehouses, for the filter.</summary>
    [HttpGet("warehouses")]
    [RequirePermission("purchase.view")]
    public async Task<IActionResult> Warehouses(CancellationToken ct) => Ok(await _report.WarehousesAsync(ct));

    /// <summary>The newest purchase orders a posted receipt was raised against, for the filter.</summary>
    [HttpGet("purchase-orders")]
    [RequirePermission("purchase.view")]
    public async Task<IActionResult> PurchaseOrders(CancellationToken ct) => Ok(await _report.OrdersAsync(ct));
}
