using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sales.Api.Services;
using Sales.Entity.Models;
using Shared.Kernel.Internal;

namespace Sales.Api.Controllers;

/// <summary>
/// Sales orders — <c>SOR</c>. A commitment document.
///
/// <b>Posts nothing to the general ledger, and reserves stock.</b> That pair is
/// what makes it different from a quote on one side and an invoice on the other:
/// a quote promises nothing, an invoice sells, and an order holds.
///
/// <b>There is no void.</b> A confirmed order is cancelled or closed short, and
/// either way it keeps its number — the two differ in what they say happened,
/// not in what they release.
/// </summary>
[ApiController]
[Authorize]
[RequireModulePermission("sales")]
[Route("api/sales-orders")]
public sealed class SalesOrdersController : ControllerBase
{
    private readonly SalesOrderService _orders;

    public SalesOrdersController(SalesOrderService orders) => _orders = orders;

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] DateOnly? from = null,
        [FromQuery] DateOnly? to = null,
        [FromQuery] string? status = null,
        CancellationToken ct = default) =>
        Ok(await _orders.ListAsync(page, pageSize, from, to, status, ct));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetById(long id, CancellationToken ct)
    {
        SalesOrderDetailModel? order = await _orders.GetAsync(id, ct);
        return order is null ? NotFound() : Ok(order);
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] SaveSalesOrderRequest dto, CancellationToken ct) =>
        Respond(await _orders.SaveAsync(null, dto, ct));

    /// <summary>Only a draft can be changed: a confirmed order is holding stock.</summary>
    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(
        long id, [FromBody] SaveSalesOrderRequest dto, CancellationToken ct) =>
        Respond(await _orders.SaveAsync(id, dto, ct));

    /// <summary>Takes the number and holds the stock.</summary>
    [HttpPost("{id:long}/confirm")]
    public async Task<IActionResult> ConfirmOrder(long id, CancellationToken ct) =>
        Respond(await _orders.ConfirmAsync(id, ct));

    /// <summary>The customer withdrew. Everything still held is released.</summary>
    [HttpPost("{id:long}/cancel")]
    public async Task<IActionResult> CancelOrder(
        long id, [FromBody] CloseSalesOrderRequest dto, CancellationToken ct) =>
        Respond(await _orders.CancelAsync(id, dto, ct));

    /// <summary>Nothing further is coming. What has not shipped is released.</summary>
    [HttpPost("{id:long}/short-close")]
    public async Task<IActionResult> ShortCloseOrder(
        long id, [FromBody] CloseSalesOrderRequest dto, CancellationToken ct) =>
        Respond(await _orders.ShortCloseAsync(id, dto, ct));

    private IActionResult Respond(SalesOrderResult result) => result.Outcome switch
    {
        SalesOrderOutcome.Ok => Ok(new { salesOrderId = result.SalesOrderId }),

        SalesOrderOutcome.NotFound => NotFound(),

        // The shortage is data, not prose: the screen lists the lines and what
        // each could actually draw on, which a message string cannot carry.
        SalesOrderOutcome.InsufficientStock => BadRequest(new
        {
            message = result.Detail,
            shortages = result.Shortages,
        }),

        // Transient. A 503 says "nothing changed, come back", which is a
        // different instruction to the caller than "you got this wrong".
        SalesOrderOutcome.InventoryUnreachable => StatusCode(
            StatusCodes.Status503ServiceUnavailable, new { message = result.Detail }),

        SalesOrderOutcome.SeriesMissing => StatusCode(
            StatusCodes.Status409Conflict, new { message = result.Detail }),

        _ => BadRequest(new { message = result.Detail ?? $"Refused: {result.Outcome}." }),
    };
}
