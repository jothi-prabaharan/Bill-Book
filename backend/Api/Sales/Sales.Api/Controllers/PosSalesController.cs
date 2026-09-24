using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sales.Api.Services;
using Sales.Entity.Models;
using Shared.Kernel.Internal;

namespace Sales.Api.Controllers;

/// <summary>
/// The till's one call (T7.1, TK-39): a sale made, paid and posted at once.
///
/// It needs <c>sales.approve</c>, the permission that posts an invoice, because
/// that is what it does. The Sales role holds it.
/// </summary>
[ApiController]
[Authorize]
[RequireModulePermission("sales")]
[Route("api/sales/pos/sales")]
public sealed class PosSalesController : ControllerBase
{
    private readonly PosSaleService _sales;

    public PosSalesController(PosSaleService sales) => _sales = sales;

    /// <summary>
    /// 200 with the number, total and change; 409 when an item has run out,
    /// naming it; 422 when the tenders do not pay the total. Any refusal rolls
    /// the whole sale back.
    /// </summary>
    [HttpPost]
    [PermissionAction("approve")]
    public async Task<IActionResult> Sell([FromBody] PosSaleRequest request, CancellationToken ct)
    {
        PosSaleResult result = await _sales.SellAsync(request, ct);

        return result.Outcome switch
        {
            PosSaleOutcome.Ok => Ok(result),
            PosSaleOutcome.TenderShort or PosSaleOutcome.TenderOverpaid =>
                UnprocessableEntity(new MessageResponse { Message = result.Detail ?? "The tenders do not pay the total." }),
            _ => result.Refusal switch
            {
                InvoiceOutcome.InsufficientStock => Conflict(new MessageResponse
                {
                    Message = result.Detail ?? "An item on this sale has run out.",
                }),
                InvoiceOutcome.NotFound => NotFound(),
                InvoiceOutcome.RatesUnavailable => StatusCode(
                    StatusCodes.Status503ServiceUnavailable,
                    new MessageResponse { Message = result.Detail ?? "Tax rates are temporarily unavailable." }),
                _ => BadRequest(new MessageResponse { Message = result.Detail ?? "The sale was refused." }),
            },
        };
    }
}
