using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sales.Entity.Models;
using Sales.Repository.Repositories;
using Shared.Kernel.Apps;
using Shared.Kernel.Internal;
using Shared.Kernel.Restaurant;

namespace Sales.Api.Controllers;

/// <summary>
/// Kitchen tickets and the kitchen display (TK-147). Answers only while
/// kitchen tickets are switched on; logic is in <see cref="KotRepository"/>.
/// </summary>
[ApiController]
[Authorize]
[RequireBusinessType(BusinessType.Restaurant)]
[RequireModulePermission("restaurant")]
[RequireRestaurantFeature(RestaurantFeature.Kitchen)]
[Route("api/restaurant")]
public sealed class KitchenTicketsController : ControllerBase
{
    private readonly KotRepository _kots;

    public KitchenTicketsController(KotRepository kots) => _kots = kots;

    /// <summary>Sends the order's new dishes: one ticket per station. Edit: it moves the order on.</summary>
    [HttpPost("orders/{orderId:long}/send")]
    public async Task<IActionResult> Send(long orderId, CancellationToken ct) =>
        Respond(await _kots.SendAsync(orderId, ct), ids => Ok(new { kotIds = ids }));

    [HttpGet("orders/{orderId:long}/kots")]
    public async Task<IActionResult> ForOrder(long orderId, CancellationToken ct) => Ok(await _kots.ForOrderAsync(orderId, ct));

    /// <summary>The kitchen display: tickets still being worked, oldest first.</summary>
    [HttpGet("kitchen")]
    public async Task<IActionResult> Display([FromQuery] MenuStation? station, CancellationToken ct) => Ok(await _kots.DisplayAsync(station, ct));

    /// <summary>Prints, or reprints, a ticket. A POST because each print is counted.</summary>
    [HttpPost("kots/{kotId:long}/print")]
    [PermissionAction("print")]
    public async Task<IActionResult> Print(long kotId, CancellationToken ct) =>
        await _kots.PrintAsync(kotId, ct) is { } printed ? Ok(printed) : NotFound();

    [HttpPut("kot-lines/{kotLineId:long}/status")]
    public async Task<IActionResult> SetStatus(long kotLineId, [FromBody] SetKitchenStatusRequest request, CancellationToken ct) =>
        Respond(await _kots.SetStatusAsync(kotLineId, request.Status, ct), _ => NoContent());

    /// <summary>
    /// Cancels a dish with a reason. Needs restaurant.void; once the kitchen has
    /// started it, restaurant.approve as well — checked in the repository so the
    /// refusal can say which.
    /// </summary>
    [HttpPost("kot-lines/{kotLineId:long}/cancel")]
    [PermissionAction("void")]
    public async Task<IActionResult> Cancel(long kotLineId, [FromBody] CancelKotLineRequest request, CancellationToken ct)
    {
        bool canApprove = User.FindAll("permission").Any(c => string.Equals(c.Value, "restaurant.approve", StringComparison.OrdinalIgnoreCase));
        return Respond(await _kots.CancelLineAsync(kotLineId, request.Reason, canApprove, ct), _ => NoContent());
    }

    private IActionResult Respond<T>(OrderResult<T> result, Func<T, IActionResult> onOk) => result.Outcome switch
    {
        OrderOutcome.Ok => onOk(result.Value!),
        OrderOutcome.NotFound => NotFound(),
        OrderOutcome.Unavailable => StatusCode(StatusCodes.Status503ServiceUnavailable, new MessageResponse { Message = result.Message! }),
        _ => BadRequest(new MessageResponse { Message = result.Message! }),
    };
}
