using Inventory.Api.Services;
using Inventory.Entity.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Inventory.Api.Controllers;

/// <summary>
/// Stock positions and movements.
///
/// One pool per item across every branch and warehouse, so there is no
/// per-location balance to ask for. A warehouse filters the movement history;
/// it never splits the quantity.
/// </summary>
[ApiController]
[Authorize]
[Route("api/stock")]
public sealed class StockController : ControllerBase
{
    private readonly StockService _stock;

    public StockController(StockService stock) => _stock = stock;

    /// <summary>Every stocked item, including ones that have never moved.</summary>
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? search,
        [FromQuery] bool belowReorderOnly,
        CancellationToken ct) =>
        Ok(await _stock.ListAsync(search, belowReorderOnly, ct));

    [HttpGet("{itemId:long}")]
    public async Task<IActionResult> Get(long itemId, CancellationToken ct)
    {
        StockPosition? position = await _stock.GetAsync(itemId, ct);
        return position is null ? NotFound() : Ok(position);
    }

    /// <summary>The stock ledger — what moved, when, in which unit, and why.</summary>
    [HttpGet("movements")]
    public async Task<IActionResult> Movements(
        [FromQuery] long? itemId,
        [FromQuery] long? warehouseId,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken ct) =>
        Ok(await _stock.ListMovementsAsync(itemId, warehouseId, from, to, ct));

    /// <summary>
    /// Records one movement and moves the pool with it, in a single
    /// transaction. Returns the resulting position so the caller never has to
    /// re-read it.
    /// </summary>
    [HttpPost("movements")]
    public async Task<IActionResult> Record(
        [FromBody] RecordStockMovementRequest request, CancellationToken ct)
    {
        RecordStockMovementResult result = await _stock.RecordAsync(request, ct);
        return Respond(result.Outcome, () => Ok(result));
    }

    /// <summary>
    /// Warehouse to warehouse. Two movements, no net change — the pool is
    /// shared, so this records where stock sits and nothing more.
    /// </summary>
    [HttpPost("transfers")]
    public async Task<IActionResult> Transfer(
        [FromBody] TransferStockRequest request, CancellationToken ct)
    {
        RecordStockMovementResult result = await _stock.TransferAsync(request, ct);
        return Respond(result.Outcome, () => Ok(result));
    }

    private IActionResult Respond(StockOutcome outcome, Func<IActionResult> onOk) =>
        outcome switch
        {
            StockOutcome.Ok => onOk(),
            StockOutcome.ItemNotFound => NotFound(),
            StockOutcome.UnknownWarehouse => NotFound(new MessageResponse
            {
                Message = "That warehouse no longer exists.",
            }),
            StockOutcome.NotStocked => BadRequest(new MessageResponse
            {
                Message = "This item does not track inventory, so it has no stock to move.",
            }),
            StockOutcome.UnknownUnit => BadRequest(new MessageResponse
            {
                Message = "That unit no longer exists.",
            }),
            StockOutcome.UnitTypeMismatch => BadRequest(new MessageResponse
            {
                Message = "That unit belongs to a different unit type from the item's, so the "
                    + "quantity cannot be converted into the item's inventory unit.",
            }),
            StockOutcome.InsufficientStock => Conflict(new MessageResponse
            {
                Message = "There is not enough stock. Nothing was changed.",
            }),
            StockOutcome.UnitCostRequired => BadRequest(new MessageResponse
            {
                Message = "Enter what the stock cost. Receiving without a cost would drag the "
                    + "weighted average down and understate the cost of every later sale.",
            }),
            StockOutcome.DuplicateSource => Conflict(new MessageResponse
            {
                Message = "That document line has already moved stock.",
            }),
            StockOutcome.SameWarehouse => BadRequest(new MessageResponse
            {
                Message = "Choose two different warehouses.",
            }),
            StockOutcome.InvalidValue => BadRequest(new MessageResponse
            {
                Message = "One of the selected options is not a recognised value.",
            }),
            _ => StatusCode(StatusCodes.Status500InternalServerError),
        };
}
