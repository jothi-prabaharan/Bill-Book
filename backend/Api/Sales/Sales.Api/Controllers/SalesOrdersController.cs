using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sales.Api.Services;
using Sales.Entity.Models;
using Shared.Kernel.Internal;

namespace Sales.Api.Controllers;

/// <summary>
/// Sales orders — <c>SOR</c>. The document that commits stock without selling it.
///
/// <b>Nothing here reaches <c>acc.JournalLedger</c>.</b> An order is a promise
/// and a promise is not a supply, so the double entry belongs to the invoice
/// raised from it. What confirming one does instead is reserve: see
/// <see cref="SalesOrderService.ConfirmAsync"/>.
/// </summary>
[ApiController]
[Authorize]
[RequireModulePermission("sales")]
[Route("api/sales/sales-orders")]
public sealed class SalesOrdersController : ControllerBase
{
    /// <summary>What a page asks for when it does not say. Clamped again in the service.</summary>
    private const int DefaultPageSize = 50;

    private readonly SalesOrderService _SalesOrders;

    public SalesOrdersController(SalesOrderService SalesOrders) => _SalesOrders = SalesOrders;

    /// <summary>
    /// One page of sales orders, newest first.
    ///
    /// <c>skip</c> and <c>take</c> arrive off a query string and are clamped in
    /// the service rather than trusted — a negative skip is a hand-edited URL
    /// that either throws or silently serves page one while the pager claims
    /// otherwise.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAll(
        CancellationToken ct,
        [FromQuery] int skip = 0,
        [FromQuery] int take = DefaultPageSize,
        [FromQuery] string? status = null,
        [FromQuery] string? search = null)
    {
        SalesOrderListPage page = await _SalesOrders.ListAsync(skip, take, status, search, ct);
        return Ok(page);
    }

    [HttpGet("{SalesOrderId:long}")]
    public async Task<IActionResult> Get(long SalesOrderId, CancellationToken ct)
    {
        SalesOrderViewResult result = await _SalesOrders.GetAsync(SalesOrderId, ct);

        return result.Outcome == SalesOrderOutcome.Ok
            ? Ok(result.View)
            : Respond(result.Outcome, null);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SaveSalesOrderRequest request, CancellationToken ct)
    {
        SalesOrderResult result = await _SalesOrders.CreateAsync(request, ct);

        return result.Outcome == SalesOrderOutcome.Ok
            ? CreatedAtAction(nameof(Get), new { SalesOrderId = result.SalesOrderId }, result)
            : Respond(result.Outcome, result.Detail);
    }

    /// <summary>An accepted quote, turned into an order. The lines come from the quote.</summary>
    [HttpPost("from-quote/{QuoteId:long}")]
    public async Task<IActionResult> CreateFromQuote(
        long QuoteId, [FromBody] CreateOrderFromQuoteRequest request, CancellationToken ct)
    {
        SalesOrderResult result = await _SalesOrders.CreateFromQuoteAsync(QuoteId, request, ct);

        return result.Outcome == SalesOrderOutcome.Ok
            ? CreatedAtAction(nameof(Get), new { SalesOrderId = result.SalesOrderId }, result)
            : Respond(result.Outcome, result.Detail);
    }

    [HttpPut("{SalesOrderId:long}")]
    public async Task<IActionResult> Update(long SalesOrderId, [FromBody] SaveSalesOrderRequest request, CancellationToken ct)
    {
        SalesOrderResult result = await _SalesOrders.UpdateAsync(SalesOrderId, request, ct);

        return result.Outcome == SalesOrderOutcome.Ok
            ? NoContent()
            : Respond(result.Outcome, result.Detail);
    }

    /// <summary>
    /// Confirm the order and reserve its stock.
    ///
    /// The route says <c>confirm</c> because that is what it does to the order;
    /// the permission is still <c>sales.approve</c>, which is the authority a
    /// state change on a trading document takes throughout the product.
    /// </summary>
    [HttpPost("{SalesOrderId:long}/confirm")]
    [PermissionAction("approve")]
    public async Task<IActionResult> Confirm(long SalesOrderId, CancellationToken ct)
    {
        SalesOrderResult result = await _SalesOrders.ConfirmAsync(SalesOrderId, ct);

        return result.Outcome == SalesOrderOutcome.Ok
            ? NoContent()
            : Respond(result.Outcome, result.Detail);
    }

    [HttpPost("{SalesOrderId:long}/void")]
    [PermissionAction("void")]
    public async Task<IActionResult> Void(long SalesOrderId, [FromBody] VoidSalesOrderRequest request, CancellationToken ct)
    {
        SalesOrderResult result = await _SalesOrders.VoidAsync(SalesOrderId, request, ct);

        return result.Outcome == SalesOrderOutcome.Ok
            ? NoContent()
            : Respond(result.Outcome, result.Detail);
    }

    private IActionResult Respond(SalesOrderOutcome outcome, string? detail) =>
        outcome switch
        {
            SalesOrderOutcome.NotFound => NotFound(),

            // Never NotFound(): the row exists and the caller may not have it.
            // Those are different answers and the house rule is to say so.
            SalesOrderOutcome.Forbidden => Forbid(),

            SalesOrderOutcome.LifecycleRefused => BadRequest(new MessageResponse { Message = detail ?? "Action refused by document lifecycle." }),
            SalesOrderOutcome.LineInvalid => BadRequest(new MessageResponse { Message = detail ?? "One or more lines are invalid." }),
            SalesOrderOutcome.ValidityInvalid => BadRequest(new MessageResponse { Message = detail ?? "Validity date is invalid." }),
            SalesOrderOutcome.PlaceOfSupplyRefused => BadRequest(new MessageResponse { Message = detail ?? "Place of supply could not be determined." }),
            SalesOrderOutcome.RatesUnavailable => StatusCode(StatusCodes.Status503ServiceUnavailable, new MessageResponse { Message = detail ?? "Tax rates or base currency are temporarily unavailable." }),
            SalesOrderOutcome.AlreadyFulfilled => Conflict(new MessageResponse { Message = "This Sales Order has already been fulfilled." }),
            SalesOrderOutcome.InsufficientStock => Conflict(new MessageResponse { Message = detail ?? "Insufficient stock to reserve." }),
            SalesOrderOutcome.CreditLimitExceeded => BadRequest(new MessageResponse { Message = detail ?? "Credit limit exceeded or account on hold." }),
            SalesOrderOutcome.QuoteNotConvertible => Conflict(new MessageResponse { Message = detail ?? "This quote cannot be converted to a sales order." }),
            _ => StatusCode(StatusCodes.Status500InternalServerError)
        };
}
