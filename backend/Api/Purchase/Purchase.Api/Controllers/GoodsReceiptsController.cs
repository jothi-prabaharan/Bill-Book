using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Purchase.Api.Services;
using Purchase.Entity.Models;
using Shared.Kernel.Internal;

namespace Purchase.Api.Controllers;

/// <summary>
/// The goods receipt — <c>GRN</c>. Where stock arrives on the purchase side.
///
/// Posting is <c>purchase.approve</c> rather than the <c>purchase.edit</c> a
/// bare POST would derive: it moves stock and writes to the ledger, which is a
/// different authority from keying the delivery note.
/// </summary>
[ApiController]
[Authorize]
[RequireModulePermission("purchase")]
[Route("api/purchase/goods-receipts")]
public sealed class GoodsReceiptsController : ControllerBase
{
    private readonly GoodsReceiptService _receipts;

    public GoodsReceiptsController(GoodsReceiptService receipts) => _receipts = receipts;

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) =>
        Ok(await _receipts.ListAsync(ct));

    [HttpGet("{goodsReceiptId:long}")]
    public async Task<IActionResult> Get(long goodsReceiptId, CancellationToken ct)
    {
        GoodsReceiptView? receipt = await _receipts.GetAsync(goodsReceiptId, ct);
        return receipt is null ? NotFound() : Ok(receipt);
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] SaveGoodsReceiptRequest request, CancellationToken ct)
    {
        GoodsReceiptResult result = await _receipts.CreateAsync(request, ct);

        return result.Outcome == GoodsReceiptOutcome.Ok
            ? CreatedAtAction(nameof(Get), new { goodsReceiptId = result.GoodsReceiptId }, result)
            : Respond(result);
    }

    [HttpPut("{goodsReceiptId:long}")]
    public async Task<IActionResult> Update(
        long goodsReceiptId,
        [FromBody] SaveGoodsReceiptRequest request,
        CancellationToken ct)
    {
        GoodsReceiptResult result = await _receipts.UpdateAsync(goodsReceiptId, request, ct);

        return result.Outcome == GoodsReceiptOutcome.Ok ? NoContent() : Respond(result);
    }

    /// <summary>Stock in, ledger written, the order advanced. Safe to call again after a failure.</summary>
    [HttpPost("{goodsReceiptId:long}/post")]
    [PermissionAction("approve")]
    public async Task<IActionResult> Post(long goodsReceiptId, CancellationToken ct)
    {
        GoodsReceiptResult result = await _receipts.PostAsync(goodsReceiptId, ct);

        return result.Outcome == GoodsReceiptOutcome.Ok ? NoContent() : Respond(result);
    }

    [HttpPost("{goodsReceiptId:long}/void")]
    [PermissionAction("void")]
    public async Task<IActionResult> Void(
        long goodsReceiptId,
        [FromBody] VoidGoodsReceiptRequest request,
        CancellationToken ct)
    {
        GoodsReceiptResult result = await _receipts.VoidAsync(goodsReceiptId, request, ct);

        return result.Outcome == GoodsReceiptOutcome.Ok ? NoContent() : Respond(result);
    }

    private IActionResult Respond(GoodsReceiptResult result) =>
        result.Outcome switch
        {
            GoodsReceiptOutcome.NotFound => NotFound(),

            GoodsReceiptOutcome.LifecycleRefused => BadRequest(new MessageResponse
            {
                Message = result.Detail ?? "Action refused by the document lifecycle.",
            }),

            GoodsReceiptOutcome.LineInvalid => BadRequest(new MessageResponse
            {
                Message = result.Detail ?? "One or more lines are invalid.",
            }),

            GoodsReceiptOutcome.PlaceOfSupplyRefused => BadRequest(new MessageResponse
            {
                Message = result.Detail ?? "Place of supply could not be determined.",
            }),

            GoodsReceiptOutcome.OrderInvalid => BadRequest(new MessageResponse
            {
                Message = result.Detail ?? "That purchase order cannot be received against.",
            }),

            // Transient rather than wrong — the same request may well succeed
            // next time, and the receipt is still a draft that can be posted again.
            GoodsReceiptOutcome.RatesUnavailable => StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new MessageResponse
                {
                    Message = result.Detail
                        ?? "Tax rates or the base currency are temporarily unavailable.",
                }),

            GoodsReceiptOutcome.PostingRefused => StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new MessageResponse
                {
                    Message = result.Detail
                        ?? "The ledger refused the posting. The stock is recorded and the "
                            + "receipt is still a draft — post it again.",
                }),

            GoodsReceiptOutcome.StockRefused => Conflict(new MessageResponse
            {
                Message = result.Detail ?? "Inventory refused the receipt.",
            }),

            GoodsReceiptOutcome.AlreadyBilled => Conflict(new MessageResponse
            {
                Message = result.Detail ?? "A bill has already been raised against this receipt.",
            }),

            _ => StatusCode(StatusCodes.Status500InternalServerError),
        };
}
