using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sales.Entity.Enums;
using Sales.Entity.Models;
using Sales.Repository.Repositories;
using Shared.Kernel.Apps;
using Shared.Kernel.Internal;

namespace Sales.Api.Controllers;

/// <summary>
/// Orders from Zomato and Swiggy (TK-167). Logic is in
/// <see cref="AggregatorOrderRepository"/>. A person at the till or an API
/// client (D-07) with the restaurant "create" permission can post an order.
/// </summary>
[ApiController]
[Authorize]
[RequireBusinessType(BusinessType.Restaurant)]
[RequireModulePermission("restaurant")]
[Route("api/restaurant/aggregator-orders")]
public sealed class AggregatorOrdersController : ControllerBase
{
    private readonly AggregatorOrderRepository _aggregators;

    public AggregatorOrdersController(AggregatorOrderRepository aggregators) => _aggregators = aggregators;

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] OrderAggregator? aggregator, [FromQuery] DateOnly? date, CancellationToken ct) =>
        Ok(await _aggregators.ListAsync(aggregator, date, ct));

    /// <summary>201 for a new order, 200 when this aggregator order id had already been received.</summary>
    [HttpPost]
    [PermissionAction("create")]
    public async Task<IActionResult> Receive([FromBody] ReceiveAggregatorOrderRequest request, CancellationToken ct)
    {
        OrderResult<AggregatorOrderReceipt> result = await _aggregators.ReceiveAsync(request, ct);
        return result.Outcome switch
        {
            OrderOutcome.Ok => result.Value!.AlreadyReceived
                ? Ok(result.Value)
                : StatusCode(StatusCodes.Status201Created, result.Value),
            OrderOutcome.NotFound => NotFound(),
            OrderOutcome.Unavailable => StatusCode(StatusCodes.Status503ServiceUnavailable, new MessageResponse { Message = result.Message! }),
            _ => BadRequest(new MessageResponse { Message = result.Message! }),
        };
    }
}
