using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sales.Entity.Models;
using Sales.Repository.Repositories;
using Shared.Kernel.Apps;
using Shared.Kernel.Internal;

namespace Sales.Api.Controllers;

/// <summary>
/// Table bookings and the waiting list (TK-165). Answers only while tables are
/// switched on in Settings › Restaurant; logic is in
/// <see cref="TableReservationRepository"/> and <see cref="WaitingListRepository"/>.
/// Hosts do this all evening, so changing, seating and clearing need edit.
/// </summary>
[ApiController]
[Authorize]
[RequireBusinessType(BusinessType.Restaurant)]
[RequireModulePermission("restaurant")]
[RequireRestaurantFeature(RestaurantFeature.Tables)]
[Route("api/restaurant")]
public sealed class TableReservationsController : ControllerBase
{
    private readonly TableReservationRepository _reservations;
    private readonly WaitingListRepository _waiting;

    public TableReservationsController(TableReservationRepository reservations, WaitingListRepository waiting)
    {
        _reservations = reservations;
        _waiting = waiting;
    }

    // ---- Bookings ----

    /// <summary>One day's bookings (today when no date is given); <c>liveOnly</c> leaves out seated, cancelled and no-shows.</summary>
    [HttpGet("reservations")]
    public async Task<IActionResult> List([FromQuery] DateOnly? date, [FromQuery] bool liveOnly, CancellationToken ct) =>
        Ok(await _reservations.ListAsync(date, liveOnly, ct));

    [HttpPost("reservations")]
    [PermissionAction("create")]
    public async Task<IActionResult> Book([FromBody] SaveReservationRequest request, CancellationToken ct) =>
        Respond(await _reservations.SaveAsync(null, request, ct), id => StatusCode(StatusCodes.Status201Created, new { tableReservationId = id }));

    [HttpPut("reservations/{reservationId:long}")]
    public async Task<IActionResult> Change(long reservationId, [FromBody] SaveReservationRequest request, CancellationToken ct) =>
        Respond(await _reservations.SaveAsync(reservationId, request, ct), _ => NoContent());

    [HttpPost("reservations/{reservationId:long}/cancel")]
    public async Task<IActionResult> Cancel(long reservationId, CancellationToken ct) =>
        Respond(await _reservations.CancelAsync(reservationId, ct), _ => NoContent());

    [HttpPost("reservations/{reservationId:long}/no-show")]
    public async Task<IActionResult> NoShow(long reservationId, CancellationToken ct) =>
        Respond(await _reservations.NoShowAsync(reservationId, ct), _ => NoContent());

    [HttpPost("reservations/{reservationId:long}/seat")]
    public async Task<IActionResult> SeatBooking(long reservationId, [FromBody] SeatPartyRequest request, CancellationToken ct) =>
        Respond(await _reservations.SeatAsync(reservationId, request, ct), seated => Ok(seated));

    // ---- Waiting list ----

    [HttpGet("waiting-list")]
    public async Task<IActionResult> Waiting([FromQuery] bool includeClosed, CancellationToken ct) =>
        Ok(await _waiting.ListAsync(includeClosed, ct));

    [HttpPost("waiting-list")]
    [PermissionAction("create")]
    public async Task<IActionResult> AddWaiting([FromBody] AddWaitingPartyRequest request, CancellationToken ct) =>
        Respond(await _waiting.AddAsync(request, ct), id => StatusCode(StatusCodes.Status201Created, new { waitingListEntryId = id }));

    /// <summary>The party a free table should take now; 204 when nobody waiting fits it.</summary>
    [HttpGet("waiting-list/next")]
    public async Task<IActionResult> Next([FromQuery] long tableId, CancellationToken ct) =>
        Respond(await _waiting.NextForTableAsync(tableId, ct), party => party is null ? NoContent() : Ok(party));

    [HttpPost("waiting-list/{entryId:long}/seat")]
    public async Task<IActionResult> SeatWaiting(long entryId, [FromBody] SeatPartyRequest request, CancellationToken ct) =>
        Respond(await _waiting.SeatAsync(entryId, request, ct), seated => Ok(seated));

    [HttpPost("waiting-list/{entryId:long}/leave")]
    public async Task<IActionResult> Leave(long entryId, CancellationToken ct) =>
        Respond(await _waiting.LeaveAsync(entryId, ct), _ => NoContent());

    private IActionResult Respond<T>(OrderResult<T> result, Func<T, IActionResult> onOk) => result.Outcome switch
    {
        OrderOutcome.Ok => onOk(result.Value!),
        OrderOutcome.NotFound => NotFound(),
        OrderOutcome.Unavailable => StatusCode(StatusCodes.Status503ServiceUnavailable, new MessageResponse { Message = result.Message! }),
        _ => BadRequest(new MessageResponse { Message = result.Message! }),
    };
}
