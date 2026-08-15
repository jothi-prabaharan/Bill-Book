using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Purchase.Api.Services;
using Purchase.Entity.Models;
using Shared.Kernel.Internal;

namespace Purchase.Api.Controllers;

/// <summary>
/// The purchase order — <c>POR</c>.
///
/// <b>Approve and confirm are both <c>purchase.approve</c>, and void is
/// <c>purchase.void</c>.</b> The permissions were seeded from the beginning and
/// nothing ever read one, which is the gap T0.4 exists to close: without
/// <see cref="PermissionActionAttribute"/> these would all derive
/// <c>purchase.edit</c> from their POST, and the clerk who keys an order would
/// be the person who can also commit the company to it and withdraw it again.
///
/// There is no <c>purchase.post</c> seeded, and confirming is deliberately filed
/// under approve rather than edit: issuing an order to a vendor is the
/// commitment, and that is the authority being exercised.
/// </summary>
[ApiController]
[Authorize]
[RequireModulePermission("purchase")]
[Route("api/purchase/purchase-orders")]
public sealed class PurchaseOrdersController : ControllerBase
{
    private readonly PurchaseOrderService _orders;

    public PurchaseOrdersController(PurchaseOrderService orders) => _orders = orders;

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) =>
        Ok(await _orders.ListAsync(ct));

    [HttpGet("{purchaseOrderId:long}")]
    public async Task<IActionResult> Get(long purchaseOrderId, CancellationToken ct)
    {
        PurchaseOrderView? order = await _orders.GetAsync(purchaseOrderId, ct);
        return order is null ? NotFound() : Ok(order);
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] SavePurchaseOrderRequest request, CancellationToken ct)
    {
        PurchaseOrderResult result = await _orders.CreateAsync(request, ct);

        return result.Outcome == PurchaseOrderOutcome.Ok
            ? CreatedAtAction(
                nameof(Get), new { purchaseOrderId = result.PurchaseOrderId }, result)
            : Respond(result);
    }

    [HttpPut("{purchaseOrderId:long}")]
    public async Task<IActionResult> Update(
        long purchaseOrderId,
        [FromBody] SavePurchaseOrderRequest request,
        CancellationToken ct)
    {
        PurchaseOrderResult result = await _orders.UpdateAsync(purchaseOrderId, request, ct);

        return result.Outcome == PurchaseOrderOutcome.Ok ? NoContent() : Respond(result);
    }

    /// <summary>Draft → ReadyToPost. The review step, for branches that want one.</summary>
    [HttpPost("{purchaseOrderId:long}/approve")]
    [PermissionAction("approve")]
    public async Task<IActionResult> Approve(long purchaseOrderId, CancellationToken ct)
    {
        PurchaseOrderResult result = await _orders.ApproveAsync(purchaseOrderId, ct);

        return result.Outcome == PurchaseOrderOutcome.Ok ? NoContent() : Respond(result);
    }

    /// <summary>Issued to the vendor. Posts nothing to the ledger and moves no stock.</summary>
    [HttpPost("{purchaseOrderId:long}/confirm")]
    [PermissionAction("approve")]
    public async Task<IActionResult> Confirm(long purchaseOrderId, CancellationToken ct)
    {
        PurchaseOrderResult result = await _orders.ConfirmAsync(purchaseOrderId, ct);

        return result.Outcome == PurchaseOrderOutcome.Ok ? NoContent() : Respond(result);
    }

    [HttpPost("{purchaseOrderId:long}/void")]
    [PermissionAction("void")]
    public async Task<IActionResult> Void(
        long purchaseOrderId,
        [FromBody] VoidPurchaseOrderRequest request,
        CancellationToken ct)
    {
        PurchaseOrderResult result = await _orders.VoidAsync(purchaseOrderId, request, ct);

        return result.Outcome == PurchaseOrderOutcome.Ok ? NoContent() : Respond(result);
    }

    private IActionResult Respond(PurchaseOrderResult result) =>
        result.Outcome switch
        {
            PurchaseOrderOutcome.NotFound => NotFound(),

            PurchaseOrderOutcome.LifecycleRefused => BadRequest(new MessageResponse
            {
                Message = result.Detail ?? "Action refused by the document lifecycle.",
            }),

            PurchaseOrderOutcome.LineInvalid => BadRequest(new MessageResponse
            {
                Message = result.Detail ?? "One or more lines are invalid.",
            }),

            PurchaseOrderOutcome.ExpectedDateInvalid => BadRequest(new MessageResponse
            {
                Message = result.Detail ?? "The expected delivery date is invalid.",
            }),

            PurchaseOrderOutcome.PlaceOfSupplyRefused => BadRequest(new MessageResponse
            {
                Message = result.Detail ?? "Place of supply could not be determined.",
            }),

            // Transient rather than wrong: the branch settings or the tax rates
            // could not be read, and the same request may well succeed next time.
            PurchaseOrderOutcome.RatesUnavailable => StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new MessageResponse
                {
                    Message = result.Detail
                        ?? "Tax rates or the base currency are temporarily unavailable.",
                }),

            PurchaseOrderOutcome.AlreadyReceived => Conflict(new MessageResponse
            {
                Message = result.Detail
                    ?? "Goods have already been received against this order.",
            }),

            _ => StatusCode(StatusCodes.Status500InternalServerError),
        };
}
