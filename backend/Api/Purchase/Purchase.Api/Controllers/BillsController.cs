using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Purchase.Api.Services;
using Purchase.Entity.Models;
using Shared.Kernel.Internal;

namespace Purchase.Api.Controllers;

/// <summary>
/// The vendor bill — <c>BIL</c>. What is owed, and what input tax credit is
/// claimed on.
/// </summary>
[ApiController]
[Authorize]
[RequireModulePermission("purchase")]
[Route("api/purchase/bills")]
public sealed class BillsController : ControllerBase
{
    private readonly BillService _bills;

    public BillsController(BillService bills) => _bills = bills;

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) => Ok(await _bills.ListAsync(ct));

    [HttpGet("{billId:long}")]
    public async Task<IActionResult> Get(long billId, CancellationToken ct)
    {
        BillView? bill = await _bills.GetAsync(billId, ct);
        return bill is null ? NotFound() : Ok(bill);
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] SaveBillRequest request, CancellationToken ct)
    {
        BillResult result = await _bills.CreateAsync(request, ct);

        return result.Outcome == BillOutcome.Ok
            ? CreatedAtAction(nameof(Get), new { billId = result.BillId }, result)
            : Respond(result);
    }

    [HttpPut("{billId:long}")]
    public async Task<IActionResult> Update(
        long billId, [FromBody] SaveBillRequest request, CancellationToken ct)
    {
        BillResult result = await _bills.UpdateAsync(billId, request, ct);
        return result.Outcome == BillOutcome.Ok ? NoContent() : Respond(result);
    }

    /// <summary>Clears the clearing account, or brings the goods in — and always owes the vendor.</summary>
    [HttpPost("{billId:long}/post")]
    [PermissionAction("approve")]
    public async Task<IActionResult> Post(long billId, CancellationToken ct)
    {
        BillResult result = await _bills.PostAsync(billId, ct);
        return result.Outcome == BillOutcome.Ok ? NoContent() : Respond(result);
    }

    [HttpPost("{billId:long}/void")]
    [PermissionAction("void")]
    public async Task<IActionResult> Void(
        long billId, [FromBody] VoidBillRequest request, CancellationToken ct)
    {
        BillResult result = await _bills.VoidAsync(billId, request, ct);
        return result.Outcome == BillOutcome.Ok ? NoContent() : Respond(result);
    }

    private IActionResult Respond(BillResult result) =>
        result.Outcome switch
        {
            BillOutcome.NotFound => NotFound(),

            BillOutcome.LifecycleRefused => BadRequest(new MessageResponse
            {
                Message = result.Detail ?? "Action refused by the document lifecycle.",
            }),

            BillOutcome.LineInvalid => BadRequest(new MessageResponse
            {
                Message = result.Detail ?? "One or more lines are invalid.",
            }),

            BillOutcome.PlaceOfSupplyRefused => BadRequest(new MessageResponse
            {
                Message = result.Detail ?? "Place of supply could not be determined.",
            }),

            BillOutcome.SourceInvalid => BadRequest(new MessageResponse
            {
                Message = result.Detail ?? "That order or receipt cannot be billed.",
            }),

            BillOutcome.DueDateMissing => BadRequest(new MessageResponse
            {
                Message = result.Detail ?? "A bill needs a due date.",
            }),

            // 409, not 400: the request is well-formed and the refusal is about
            // a document that already exists.
            BillOutcome.DuplicateVendorBillNo => Conflict(new MessageResponse
            {
                Message = result.Detail
                    ?? "This vendor has already billed that number this financial year.",
            }),

            // Not the caller's mistake and not transient — a decision has not
            // been made. 409 says the state of the world refuses it.
            BillOutcome.PriceVarianceUndecided => Conflict(new MessageResponse
            {
                Message = result.Detail
                    ?? "The bill prices the goods differently from the receipt, and purchase "
                        + "price variance is not implemented yet.",
            }),

            BillOutcome.StockRefused => Conflict(new MessageResponse
            {
                Message = result.Detail ?? "Inventory refused the goods.",
            }),

            BillOutcome.AlreadyCredited => Conflict(new MessageResponse
            {
                Message = result.Detail ?? "A debit note has been raised against this bill.",
            }),

            BillOutcome.RatesUnavailable => StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new MessageResponse
                {
                    Message = result.Detail
                        ?? "Tax rates or the base currency are temporarily unavailable.",
                }),

            BillOutcome.PostingRefused => StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new MessageResponse
                {
                    Message = result.Detail
                        ?? "The ledger refused the posting. The bill is still a draft — post "
                            + "it again.",
                }),

            _ => StatusCode(StatusCodes.Status500InternalServerError),
        };
}
