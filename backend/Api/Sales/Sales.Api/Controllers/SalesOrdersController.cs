using System.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sales.Api.Services;
using Sales.Entity.Models;
using Sales.Entity.TableEntities;
using Sales.Entity.Enums;
using Sales.Repository;
using Shared.Kernel.Documents;
using Shared.Kernel.Internal;
using Shared.Kernel.Persistence;
using Shared.Kernel.Apps;

namespace Sales.Api.Controllers;

/// <summary>
/// Sales orders — <c>SOR</c>. The document that commits stock without selling it.
/// </summary>
[ApiController]
[Authorize]
[RequireModulePermission("sales")]
[Route("api/sales/sales-orders")]
[RequireApp(App.RetailErp)]
public sealed class SalesOrdersController : ControllerBase
{
    private const int DefaultPageSize = 50;

    private readonly SalesOrderService _SalesOrders;
    private readonly IInvoiceService _invoices;

    public SalesOrdersController(
        SalesOrderService SalesOrders,
        IInvoiceService invoices)
    {
        _SalesOrders = SalesOrders;
        _invoices = invoices;
    }

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

    [HttpPost("from-quote/{QuoteId:long}")]
    public async Task<IActionResult> CreateFromQuote(
        long QuoteId, [FromBody] CreateOrderFromQuoteRequest request, CancellationToken ct)
    {
        SalesOrderResult result = await _SalesOrders.CreateFromQuoteAsync(QuoteId, request, ct);

        return result.Outcome == SalesOrderOutcome.Ok
            ? CreatedAtAction(nameof(Get), new { SalesOrderId = result.SalesOrderId }, result)
            : Respond(result.Outcome, result.Detail);
    }

    [HttpPost("availability")]
    [PermissionAction("view")]
    public async Task<IActionResult> Availability(
        [FromBody] SalesOrderAvailabilityRequest request, CancellationToken ct)
    {
        List<SalesOrderAvailabilityLine> lines =
            await _SalesOrders.GetAvailabilityAsync(request.ItemIds, ct);

        return Ok(lines);
    }

    /// <summary>
    /// Bills some or all of what the order has left, on an invoice that is
    /// created and posted in one go. Empty <c>Lines</c> means everything still
    /// unbilled. The work is <see cref="IInvoiceService.FulfillSalesOrderAsync"/>'s;
    /// this action maps its answer and nothing else.
    /// </summary>
    [HttpPost("{SalesOrderId:long}/fulfill")]
    [PermissionAction("approve")]
    // Serializable, declared where the request-wide transaction filter can see
    // it: the posting checks what each line has left to bill and decides on
    // that — two concurrent fulfilments at Read Committed would each read the
    // same figure, each find room, and each write. The filter opens and
    // commits the transaction; this action never does.
    [Transactional(IsolationLevel.Serializable)]
    public async Task<IActionResult> Fulfill(
        long SalesOrderId,
        [FromBody] FulfillSalesOrderRequest request,
        CancellationToken ct)
    {
        (InvoiceResult result, FulfillSalesOrderResult? fulfilled) =
            await _invoices.FulfillSalesOrderAsync(SalesOrderId, request, ct);

        return result.Outcome == InvoiceOutcome.Ok && fulfilled is not null
            ? Ok(fulfilled)
            : InvoiceFailure(result);
    }

    [HttpPut("{SalesOrderId:long}")]
    public async Task<IActionResult> Update(long SalesOrderId, [FromBody] SaveSalesOrderRequest request, CancellationToken ct)
    {
        SalesOrderResult result = await _SalesOrders.UpdateAsync(SalesOrderId, request, ct);

        return result.Outcome == SalesOrderOutcome.Ok
            ? NoContent()
            : Respond(result.Outcome, result.Detail);
    }

    [HttpPost("{SalesOrderId:long}/confirm")]
    [PermissionAction("approve")]
    public async Task<IActionResult> Confirm(long SalesOrderId, CancellationToken ct)
    {
        SalesOrderResult result = await _SalesOrders.ConfirmAsync(SalesOrderId, ct);

        return result.Outcome == SalesOrderOutcome.Ok
            ? NoContent()
            : Respond(result.Outcome, result.Detail);
    }

    [HttpPost("{SalesOrderId:long}/short-close")]
    [PermissionAction("approve")]
    public async Task<IActionResult> ShortClose(
        long SalesOrderId, [FromBody] ShortCloseSalesOrderRequest request, CancellationToken ct)
    {
        SalesOrderResult result = await _SalesOrders.ShortCloseAsync(SalesOrderId, request, ct);

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

    private IActionResult InvoiceFailure(InvoiceResult result) =>
        result.Outcome switch
        {
            InvoiceOutcome.NotFound => NotFound(),
            InvoiceOutcome.AlreadyFulfilled => Conflict(new MessageResponse { Message = result.Detail ?? "There is nothing left to bill on this sales order." }),
            InvoiceOutcome.LineInvalid => BadRequest(new MessageResponse { Message = result.Detail ?? "One or more lines are invalid." }),
            InvoiceOutcome.InsufficientStock => Conflict(new MessageResponse { Message = result.Detail ?? "Insufficient stock to fulfill the Sales Order." }),
            InvoiceOutcome.CreditLimitExceeded => BadRequest(new MessageResponse { Message = result.Detail ?? "Credit limit exceeded or account on hold." }),
            InvoiceOutcome.DueDateMissing => BadRequest(new MessageResponse { Message = result.Detail ?? "An invoice requires a due date." }),
            InvoiceOutcome.PlaceOfSupplyRefused => BadRequest(new MessageResponse { Message = result.Detail ?? "Place of supply could not be determined." }),
            InvoiceOutcome.RatesUnavailable => StatusCode(StatusCodes.Status503ServiceUnavailable, new MessageResponse { Message = result.Detail ?? "Tax rates or base currency are temporarily unavailable." }),
            InvoiceOutcome.SourceInvalid => BadRequest(new MessageResponse { Message = result.Detail ?? "Source document is invalid." }),
            InvoiceOutcome.LifecycleRefused => BadRequest(new MessageResponse { Message = result.Detail ?? "Invoice lifecycle refused the operation." }),
            InvoiceOutcome.PostingRefused or InvoiceOutcome.StockRefused => BadRequest(new MessageResponse { Message = result.Detail ?? "Invoice posting failed." }),
            _ => StatusCode(StatusCodes.Status500InternalServerError, new MessageResponse { Message = result.Detail ?? "Invoice fulfillment failed." })
        };

    private IActionResult Respond(SalesOrderOutcome outcome, string? detail) =>
        outcome switch
        {
            SalesOrderOutcome.NotFound => NotFound(),
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
