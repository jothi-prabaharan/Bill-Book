using Inventory.Api.Services;
using Inventory.Entity.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Kernel.Internal;

namespace Inventory.Api.Controllers;

/// <summary>
/// Stock adjustment sheets — a write-off or a physical count, posted as one
/// document.
///
/// There is no void. A posted sheet has moved stock that physically moved, and
/// the movements behind it are append-only, so the correction is a mirror sheet:
/// <c>POST {id}/reverse</c>.
/// </summary>
[ApiController]
[Authorize]
[RequireModulePermission("inventory")]
[Route("api/stock-adjustments")]
public sealed class StockAdjustmentsController : ControllerBase
{
    private readonly StockAdjustmentService _adjustments;

    public StockAdjustmentsController(StockAdjustmentService adjustments) =>
        _adjustments = adjustments;

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? status, CancellationToken ct) =>
        Ok(await _adjustments.ListAsync(status, ct));

    [HttpGet("{stockAdjustmentId:long}")]
    public async Task<IActionResult> Get(long stockAdjustmentId, CancellationToken ct)
    {
        StockAdjustmentDetail? row = await _adjustments.GetAsync(stockAdjustmentId, ct);
        return row is null ? NotFound() : Ok(row);
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] SaveStockAdjustmentRequest request, CancellationToken ct) =>
        Respond(await _adjustments.SaveAsync(null, request, ct));

    [HttpPut("{stockAdjustmentId:long}")]
    public async Task<IActionResult> Update(
        long stockAdjustmentId,
        [FromBody] SaveStockAdjustmentRequest request,
        CancellationToken ct) =>
        Respond(await _adjustments.SaveAsync(stockAdjustmentId, request, ct));

    [HttpDelete("{stockAdjustmentId:long}")]
    public async Task<IActionResult> Delete(long stockAdjustmentId, CancellationToken ct) =>
        Respond(await _adjustments.DeleteAsync(stockAdjustmentId, ct));

    [HttpPost("{stockAdjustmentId:long}/post")]
    public async Task<IActionResult> Post(long stockAdjustmentId, CancellationToken ct) =>
        Respond(await _adjustments.PostAsync(stockAdjustmentId, ct));

    [HttpPost("{stockAdjustmentId:long}/reverse")]
    public async Task<IActionResult> Reverse(
        long stockAdjustmentId,
        [FromBody] ReverseStockAdjustmentRequest request,
        CancellationToken ct) =>
        Respond(await _adjustments.ReverseAsync(stockAdjustmentId, request, ct));

    private IActionResult Respond(StockAdjustmentResult result) => result.Outcome switch
    {
        StockAdjustmentOutcome.Ok => Ok(new { stockAdjustmentId = result.StockAdjustmentId }),

        StockAdjustmentOutcome.NotFound => NotFound(),

        // The chart of accounts equivalent: something the branch is missing
        // rather than something the caller got wrong.
        StockAdjustmentOutcome.SeriesMissing => StatusCode(
            StatusCodes.Status409Conflict,
            new MessageResponse { Message = result.Detail ?? "The STA series is missing." }),

        _ => BadRequest(new MessageResponse
        {
            Message = result.Detail ?? $"The sheet was refused: {result.Outcome}.",
        }),
    };
}
