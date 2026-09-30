using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Purchase.Repository.Clients;
using Purchase.Repository.Repositories;
using Purchase.Entity.Models;
using Shared.Kernel.Internal;
using Shared.Kernel.Apps;

namespace Purchase.Api.Controllers;

/// <summary>
/// The vendor bill â€” <c>BIL</c>. What is owed, and what input tax credit is
/// claimed on.
/// </summary>
[ApiController]
[Authorize]
[RequireModulePermission("purchase")]
[Route("api/purchase/bills")]
[RequireBusinessType(BusinessType.General, BusinessType.Pharma, BusinessType.Jewellery)]
public sealed class BillsController : ControllerBase
{
    private readonly BillRepository _bills;

    public BillsController(BillRepository bills) => _bills = bills;

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
        [FromBody] SaveBillRequest request, [FromServices] BillBudgetCheckRepository budget, CancellationToken ct)
    {
        BillResult result = await _bills.CreateAsync(request, ct);
        if (result.Outcome != BillOutcome.Ok)
        {
            return Respond(result);
        }

        // The save stands either way; passing the budget is a warning the form shows (TK-109).
        List<string> warnings = await budget.WarningsAsync(result.BillId, ct);
        return CreatedAtAction(nameof(Get), new { billId = result.BillId },
            new { result.Outcome, result.BillId, result.Detail, warnings });
    }

    [HttpPut("{billId:long}")]
    public async Task<IActionResult> Update(
        long billId, [FromBody] SaveBillRequest request, [FromServices] BillBudgetCheckRepository budget, CancellationToken ct)
    {
        BillResult result = await _bills.UpdateAsync(billId, request, ct);
        if (result.Outcome != BillOutcome.Ok)
        {
            return Respond(result);
        }

        // A detail on success says the edit returned it from approval (TK-100);
        // warnings say it passes the budget (TK-109).
        List<string> warnings = await budget.WarningsAsync(billId, ct);
        return result.Detail is null && warnings.Count == 0
            ? NoContent()
            : Ok(new { message = result.Detail, warnings });
    }

    /// <summary>Clears the clearing account, or brings the goods in â€” and always owes the vendor.</summary>
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
            BillOutcome.CustomFieldsRefused => UnprocessableEntity(new MessageResponse { Message = result.Detail ?? "A custom field value was refused." }),

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

            BillOutcome.MsmeDueDateExceeded45Days => BadRequest(new MessageResponse
            {
                Message = result.Detail ?? "Payment terms for an MSME vendor cannot exceed 45 days from acceptance.",
            }),

            // 409, not 400: the request is well-formed and the refusal is about
            // a document that already exists.
            BillOutcome.DuplicateVendorBillNo => Conflict(new MessageResponse
            {
                Message = result.Detail
                    ?? "This vendor has already billed that number this financial year.",
            }),

            // Not the caller's mistake and not transient â€” a decision has not
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

            BillOutcome.BilledToClient => Conflict(new MessageResponse
            {
                Message = result.Detail ?? "A line on this bill has been charged on to a client.",
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
                        ?? "The ledger refused the posting. The bill is still a draft â€” post "
                            + "it again.",
                }),

            _ => StatusCode(StatusCodes.Status500InternalServerError),
        };
}
