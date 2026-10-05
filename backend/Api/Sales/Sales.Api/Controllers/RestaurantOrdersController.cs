using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sales.Entity.Enums;
using Sales.Entity.Models;
using Sales.Repository.Repositories;
using Shared.Kernel.Apps;
using Shared.Kernel.Internal;
using Shared.Kernel.Restaurant;

namespace Sales.Api.Controllers;

/// <summary>Restaurant orders (TK-146). Logic is in <see cref="RestaurantOrderRepository"/>.</summary>
[ApiController]
[Authorize]
[RequireBusinessType(BusinessType.Restaurant)]
[RequireModulePermission("restaurant")]
[Route("api/restaurant/orders")]
public sealed class RestaurantOrdersController : ControllerBase
{
    private readonly RestaurantOrderRepository _orders;

    public RestaurantOrdersController(RestaurantOrderRepository orders) => _orders = orders;

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] RestaurantOrderStatus? status, [FromQuery] OrderType? orderType, [FromQuery] DateOnly? date, CancellationToken ct) =>
        Ok(await _orders.ListAsync(status, orderType, date, ct));

    [HttpGet("{orderId:long}")]
    public async Task<IActionResult> Get(long orderId, CancellationToken ct) =>
        await _orders.GetAsync(orderId, ct) is { } order ? Ok(order) : NotFound();

    [HttpPost]
    [PermissionAction("create")]
    public async Task<IActionResult> Open([FromBody] OpenRestaurantOrderRequest request, CancellationToken ct) =>
        Respond(await _orders.OpenAsync(request, ct), id => StatusCode(StatusCodes.Status201Created, new { restaurantOrderId = id }));

    /// <summary>Another round. Edit, not create: the order already exists.</summary>
    [HttpPost("{orderId:long}/lines")]
    public async Task<IActionResult> AddLines(long orderId, [FromBody] AddOrderLinesRequest request, CancellationToken ct) =>
        Respond(await _orders.AddLinesAsync(orderId, request.Lines, ct), _ => NoContent());

    [HttpPut("{orderId:long}/lines/{lineId:long}")]
    public async Task<IActionResult> UpdateLine(long orderId, long lineId, [FromBody] UpdateOrderLineRequest request, CancellationToken ct) =>
        Respond(await _orders.UpdateLineAsync(orderId, lineId, request, ct), _ => NoContent());

    /// <summary>Taking off a dish the kitchen has not seen. Edit: it changes the order, it does not delete one.</summary>
    [HttpDelete("{orderId:long}/lines/{lineId:long}")]
    [PermissionAction("edit")]
    public async Task<IActionResult> RemoveLine(long orderId, long lineId, CancellationToken ct) =>
        Respond(await _orders.RemoveLineAsync(orderId, lineId, ct), _ => NoContent());

    [HttpPut("{orderId:long}")]
    public async Task<IActionResult> UpdateDetails(long orderId, [FromBody] UpdateOrderDetailsRequest request, CancellationToken ct) =>
        Respond(await _orders.UpdateDetailsAsync(orderId, request, ct), _ => NoContent());

    [HttpPut("{orderId:long}/order-type")]
    public async Task<IActionResult> ChangeOrderType(long orderId, [FromBody] ChangeOrderTypeRequest request, CancellationToken ct) =>
        Respond(await _orders.ChangeOrderTypeAsync(orderId, request.OrderType, ct), _ => NoContent());

    [HttpPost("{orderId:long}/move")]
    public async Task<IActionResult> Move(long orderId, [FromBody] MoveOrderRequest request, CancellationToken ct) =>
        Respond(await _orders.MoveToTableAsync(orderId, request.RestaurantTableId, ct), _ => NoContent());

    [HttpPost("{orderId:long}/cancel")]
    [PermissionAction("void")]
    public async Task<IActionResult> Cancel(long orderId, [FromBody] CancelOrderRequest request, CancellationToken ct) =>
        Respond(await _orders.CancelAsync(orderId, request.Reason, ct), _ => NoContent());

    private IActionResult Respond<T>(OrderResult<T> result, Func<T, IActionResult> onOk) => result.Outcome switch
    {
        OrderOutcome.Ok => onOk(result.Value!),
        OrderOutcome.NotFound => NotFound(),
        OrderOutcome.Unavailable => StatusCode(StatusCodes.Status503ServiceUnavailable, new MessageResponse { Message = result.Message! }),
        _ => BadRequest(new MessageResponse { Message = result.Message! }),
    };
}
