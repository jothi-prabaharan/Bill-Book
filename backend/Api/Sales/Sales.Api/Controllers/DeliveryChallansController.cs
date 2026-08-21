using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sales.Api.Services;
using Sales.Entity.Models;
using Shared.Kernel.Internal;

namespace Sales.Api.Controllers;

/// <summary>
/// Sales › Delivery challans. <c>DLC</c> — the document the goods actually leave on.
///
/// <b>Stock moves here, not on the invoice.</b> The challan takes the goods out
/// and releases the order's reservation; the invoice that follows bills what was
/// delivered and moves nothing. Skipping it is normal — an invoice raised
/// directly still issues its own stock.
///
/// <b>What it posts depends on <c>ChallanType</c>.</b> Only <c>Sale</c> reaches
/// the ledger, as <c>Dr Goods Delivered Not Invoiced / Cr Inventory</c>: the
/// goods have left but nothing has been earned, so booking cost of sale here
/// would put an expense against revenue that does not exist yet. Job work,
/// approval, branch transfer and sample post nothing at all — stock moves,
/// ownership does not.
///
/// <b>The route was <c>DeliveryChallans</c>.</b> It is <c>api/sales/…</c> now,
/// matching quotes and sales orders — and matching what the frontend was already
/// calling, which is why this screen could not have worked as it stood.
/// </summary>
[ApiController]
[Authorize]
[RequireModulePermission("sales")]
[Route("api/sales/delivery-challans")]
public sealed class DeliveryChallansController : ControllerBase
{
    private readonly DeliveryChallanService _service;

    public DeliveryChallansController(DeliveryChallanService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken ct)
    {
        var list = await _service.ListAsync(from, to, ct);
        return Ok(list);
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Get(long id, CancellationToken ct)
    {
        var view = await _service.GetAsync(id, ct);
        if (view is null)
            return NotFound();

        return Ok(view);
    }

    [HttpPost]
    public async Task<IActionResult> Save(
        [FromBody] SaveDeliveryChallanRequest request, CancellationToken ct)
    {
        var id = await _service.SaveAsync(null, request, ct);
        return Ok(new { DeliveryChallanId = id });
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(
        long id, [FromBody] SaveDeliveryChallanRequest request, CancellationToken ct)
    {
        await _service.SaveAsync(id, request, ct);
        return Ok();
    }

    /// <summary>Dispatches: issues the stock, releases the reservation, posts if it is a sale.</summary>
    [HttpPost("{id:long}/post")]
    [PermissionAction("approve")]
    public async Task<IActionResult> Post(long id, CancellationToken ct)
    {
        await _service.PostAsync(id, ct);
        return Ok();
    }

    /// <summary>
    /// Withdraws a draft. The reason is required by the database, not just by
    /// convention — see <see cref="VoidDeliveryChallanRequest"/>.
    /// </summary>
    [HttpPost("{id:long}/void")]
    [PermissionAction("void")]
    public async Task<IActionResult> Void(
        long id, [FromBody] VoidDeliveryChallanRequest request, CancellationToken ct)
    {
        await _service.VoidAsync(id, request.Reason, ct);
        return NoContent();
    }
}
