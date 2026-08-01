using Inventory.Api.Services;
using Inventory.Entity.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Inventory.Api.Controllers;

/// <summary>
/// Stock positions and movements.
///
/// One pool per item across every warehouse in the branch, so there is no
/// per-location balance to ask for. A warehouse filters the movement history;
/// it never splits the quantity. Another branch is another organization, and
/// the query filter keeps its stock out of here entirely.
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
    /// Which layers an issue consumed, and at what cost. Empty for a weighted
    /// average item, which keeps one running cost rather than layers.
    /// </summary>
    [HttpGet("movements/{stockMovementId:long}/allocations")]
    public async Task<IActionResult> Allocations(long stockMovementId, CancellationToken ct) =>
        Ok(await _stock.AllocationsAsync(stockMovementId, ct));

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
            StockOutcome.BatchRequired => BadRequest(new MessageResponse
            {
                Message = "This item is batch-tracked. Enter the batch number the stock came in on.",
            }),
            StockOutcome.ExpiryRequired => BadRequest(new MessageResponse
            {
                Message = "This item is expiry-tracked, so a new batch needs its expiry date. "
                    + "Without one nothing can decide which lot goes out first.",
            }),
            StockOutcome.BatchNotFound => NotFound(new MessageResponse
            {
                Message = "That batch no longer exists.",
            }),
            StockOutcome.SerialCountMismatch => BadRequest(new MessageResponse
            {
                Message = "Enter one serial number for each unit — the count has to match the "
                    + "quantity exactly.",
            }),
            StockOutcome.SerialConflict => Conflict(new MessageResponse
            {
                Message = "One of those serial numbers is already in stock, or is not in stock "
                    + "to be sold. Check the list against what is on the shelf.",
            }),
            StockOutcome.InsufficientCostLayers => StatusCode(
                StatusCodes.Status500InternalServerError,
                new MessageResponse
                {
                    Message = "The cost layers for this item do not cover the quantity, although "
                        + "the stock does. Nothing was changed. This needs investigating before "
                        + "the item is sold again.",
                }),
            StockOutcome.InvalidValue => BadRequest(new MessageResponse
            {
                Message = "One of the selected options is not a recognised value.",
            }),
            _ => StatusCode(StatusCodes.Status500InternalServerError),
        };
}
