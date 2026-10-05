using System.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sales.Entity.Models;
using Sales.Repository.Clients;
using Sales.Repository.Repositories;
using Shared.Kernel.Apps;
using Shared.Kernel.Internal;
using Shared.Kernel.Persistence;

namespace Sales.Api.Controllers;

/// <summary>
/// Billing restaurant orders (TK-148) through the sales invoice. Logic is in
/// <see cref="RestaurantBillingRepository"/>; the invoice is made and posted
/// by the invoice's own repository.
/// </summary>
[ApiController]
[Authorize]
[RequireBusinessType(BusinessType.Restaurant)]
[RequireModulePermission("restaurant")]
[Route("api/restaurant/bills")]
public sealed class RestaurantBillsController : ControllerBase
{
    private readonly RestaurantBillingRepository _billing;

    public RestaurantBillsController(RestaurantBillingRepository billing) => _billing = billing;

    /// <summary>What the bill will come to. A POST because it reads a body; it writes nothing.</summary>
    [HttpPost("preview")]
    [PermissionAction("view")]
    public async Task<IActionResult> Preview([FromBody] BillRestaurantOrdersRequest request, CancellationToken ct) =>
        Respond(await _billing.PreviewAsync(request, ct), Ok);

    /// <summary>Bills and posts. Serializable, as every posting that allocates a number and moves stock is.</summary>
    [HttpPost]
    [PermissionAction("create")]
    [Transactional(IsolationLevel.Serializable)]
    public async Task<IActionResult> Bill([FromBody] BillRestaurantOrdersRequest request, CancellationToken ct) =>
        Respond(await _billing.BillAsync(request, ct), bill => StatusCode(StatusCodes.Status201Created, bill));

    /// <summary>The cash and bank accounts a payment or a tip can go into.</summary>
    [HttpGet("tender-accounts")]
    public async Task<IActionResult> TenderAccounts(CancellationToken ct) => Ok(await _billing.TenderAccountsAsync(ct));

    /// <summary>Prints the bill: its invoice, through the branch's invoice template.</summary>
    [HttpGet("{billId:long}/print")]
    [PermissionAction("print")]
    public async Task<IActionResult> Print(long billId, [FromServices] InvoicePrintRepository printing, CancellationToken ct)
    {
        if (await _billing.InvoiceOfAsync(billId, ct) is not long invoiceId)
        {
            return NotFound();
        }

        PrintedDocument? printed = await printing.PrintAsync(invoiceId, ct);
        return printed is null ? NotFound() : Ok(printed);
    }

    private IActionResult Respond<T>(OrderResult<T> result, Func<T, IActionResult> onOk) => result.Outcome switch
    {
        OrderOutcome.Ok => onOk(result.Value!),
        OrderOutcome.NotFound => NotFound(),
        OrderOutcome.Unavailable => StatusCode(StatusCodes.Status503ServiceUnavailable, new MessageResponse { Message = result.Message! }),
        _ => BadRequest(new MessageResponse { Message = result.Message! }),
    };
}
