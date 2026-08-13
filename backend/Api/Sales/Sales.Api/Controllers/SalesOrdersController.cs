using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sales.Api.Services;
using Sales.Entity.Models;
using Shared.Kernel.Internal;

namespace Sales.Api.Controllers;

[ApiController]
[Authorize]
[RequireModulePermission("sales")]
[Route("api/sales/sales-orders")]
public sealed class SalesOrdersController : ControllerBase
{
    private readonly SalesOrderService _SalesOrders;

    public SalesOrdersController(SalesOrderService SalesOrders) => _SalesOrders = SalesOrders;

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        List<SalesOrderListItem> SalesOrders = await _SalesOrders.ListAsync(ct);
        return Ok(SalesOrders);
    }

    [HttpGet("{SalesOrderId:long}")]
    public async Task<IActionResult> Get(long SalesOrderId, CancellationToken ct)
    {
        SalesOrderView? SalesOrder = await _SalesOrders.GetAsync(SalesOrderId, ct);
        return SalesOrder is null ? NotFound() : Ok(SalesOrder);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SaveSalesOrderRequest request, CancellationToken ct)
    {
        SalesOrderResult result = await _SalesOrders.CreateAsync(request, ct);

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

    [HttpPost("{SalesOrderId:long}/approve")]
    [PermissionAction("approve")]
    public async Task<IActionResult> Approve(long SalesOrderId, CancellationToken ct)
    {
        SalesOrderResult result = await _SalesOrders.PostAsync(SalesOrderId, ct);

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
            SalesOrderOutcome.LifecycleRefused => BadRequest(new MessageResponse { Message = detail ?? "Action refused by document lifecycle." }),
            SalesOrderOutcome.LineInvalid => BadRequest(new MessageResponse { Message = detail ?? "One or more lines are invalid." }),
            SalesOrderOutcome.ValidityInvalid => BadRequest(new MessageResponse { Message = detail ?? "Validity date is invalid." }),
            SalesOrderOutcome.PlaceOfSupplyRefused => BadRequest(new MessageResponse { Message = detail ?? "Place of supply could not be determined." }),
            SalesOrderOutcome.RatesUnavailable => StatusCode(StatusCodes.Status503ServiceUnavailable, new MessageResponse { Message = detail ?? "Tax rates or base currency are temporarily unavailable." }),
            SalesOrderOutcome.AlreadyFulfilled => Conflict(new MessageResponse { Message = "This Sales Order has already been fulfilled." }),
            SalesOrderOutcome.InsufficientStock => Conflict(new MessageResponse { Message = detail ?? "Insufficient stock to reserve." }),
            _ => StatusCode(StatusCodes.Status500InternalServerError)
        };
}
