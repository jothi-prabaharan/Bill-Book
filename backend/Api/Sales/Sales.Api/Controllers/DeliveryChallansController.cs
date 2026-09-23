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
/// and moves its order's delivered and reserved quantities; the invoice that
/// follows bills what was delivered. Skipping it is normal — an invoice raised
/// directly still issues its own stock.
///
/// <b>Posting writes nothing to the ledger from here.</b> The stock issue reaches
/// it through Inventory's costing worker, like every other issue. See
/// <see cref="DeliveryChallanService.PostAsync"/> for why the challan's own
/// GDNI post was withdrawn.
///
/// <b>Every refusal is an outcome, never an exception.</b> A challan outside
/// the caller's branch is <c>NotFound</c>, the same as an id that is nobody's:
/// row-level security hides other branches' rows from this service, so there is
/// nothing to tell the two apart, and a 403 would confirm the id exists in
/// someone else's books (decided 23 September 2026, TK-02). A rule about the
/// document comes back with the service's own sentence.
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
        return view is null ? NotFound() : Ok(view);
    }

    [HttpPost]
    public async Task<IActionResult> Save(
        [FromBody] SaveDeliveryChallanRequest request, CancellationToken ct)
    {
        DeliveryChallanResult result = await _service.SaveAsync(null, request, ct);

        return result.Outcome == DeliveryChallanOutcome.Ok
            ? CreatedAtAction(nameof(Get), new { id = result.DeliveryChallanId }, result)
            : Respond(result);
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(
        long id, [FromBody] SaveDeliveryChallanRequest request, CancellationToken ct)
    {
        DeliveryChallanResult result = await _service.SaveAsync(id, request, ct);
        return result.Outcome == DeliveryChallanOutcome.Ok ? Ok(result) : Respond(result);
    }

    /// <summary>Dispatches: issues the stock and moves the order's quantities.</summary>
    [HttpPost("{id:long}/post")]
    [PermissionAction("approve")]
    public async Task<IActionResult> Post(long id, CancellationToken ct)
    {
        DeliveryChallanResult result = await _service.PostAsync(id, ct);
        return result.Outcome == DeliveryChallanOutcome.Ok ? Ok(result) : Respond(result);
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
        DeliveryChallanResult result = await _service.VoidAsync(id, request.Reason, ct);
        return result.Outcome == DeliveryChallanOutcome.Ok ? Ok(result) : Respond(result);
    }

    private IActionResult Respond(DeliveryChallanResult result) =>
        result.Outcome switch
        {
            DeliveryChallanOutcome.NotFound => NotFound(),
            DeliveryChallanOutcome.LifecycleRefused or DeliveryChallanOutcome.OverDelivered
                or DeliveryChallanOutcome.StockRefused => Conflict(Message(result)),
            DeliveryChallanOutcome.RatesUnavailable => StatusCode(
                StatusCodes.Status503ServiceUnavailable, Message(result)),
            _ => UnprocessableEntity(Message(result)),
        };

    private static MessageResponse Message(DeliveryChallanResult result) =>
        new() { Message = result.Detail ?? "This delivery challan was refused." };
}
