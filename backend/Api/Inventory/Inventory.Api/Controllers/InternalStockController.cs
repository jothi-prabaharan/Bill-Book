using Inventory.Api.Services;
using Inventory.Entity.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Internal;
using Shared.Kernel.Tenancy;

namespace Inventory.Api.Controllers;

/// <summary>
/// Where Accounting brings stock across at go-live.
///
/// <b>Why Inventory records it rather than Accounting posting it.</b> Opening
/// stock is a quantity and a unit cost, and that cost is what seeds the weighted
/// average every cost of sale is computed from until the next receipt. Only
/// Inventory can seed it, and once it has, it posts
/// <c>Dr Inventory / Cr Opening Balance Equity</c> itself — the same path a
/// receipt takes. Accounting posting a value of its own would put a second figure
/// against the Inventory account, free to disagree with the stock the items are
/// actually carrying.
///
/// <b>Guarded by the internal key, with the tenant in the body</b>, like every
/// other internal door. The alternative is forwarding the user's token, which
/// would mean the person finalizing a migration needs inventory permissions to do
/// an accounting act.
///
/// <b>Safe to call twice.</b> A movement is keyed on its source document and
/// line, so a retried finalize re-presents the same lines and Inventory reports
/// each as already recorded rather than doubling the stock.
/// </summary>
[ApiController]
[AllowAnonymous]
[InternalOnly]
[Route("internal/stock")]
public sealed class InternalStockController : ControllerBase
{
    private readonly TenantContext _tenant;
    private readonly IServiceProvider _services;
    private readonly ILogger<InternalStockController> _log;

    public InternalStockController(
        TenantContext tenant, IServiceProvider services, ILogger<InternalStockController> log)
    {
        _tenant = tenant;
        _services = services;
        _log = log;
    }

    [HttpPost("opening")]
    public async Task<IActionResult> Opening(
        [FromBody] RecordOpeningStockRequest request, CancellationToken ct)
    {
        if (request.CustomerId == Guid.Empty || request.OrgId == Guid.Empty)
        {
            return BadRequest(new MessageResponse
            {
                Message = "A customer and an organization are required to record opening stock.",
            });
        }

        // Set before anything resolves a DbContext: the context is built from the
        // tenant, so resolving the service first would bind it to no tenant.
        _tenant.CustomerId = request.CustomerId;
        _tenant.OrgId = request.OrgId;

        var stock = _services.GetRequiredService<StockService>();

        var response = new RecordOpeningStockResponse();

        foreach (OpeningStockLine line in request.Lines)
        {
            RecordStockMovementResult result = await stock.RecordAsync(
                new RecordStockMovementRequest
                {
                    ItemId = line.ItemId,
                    MovementType = nameof(Entity.Enums.StockMovementType.Opening),
                    MovementDate = request.AsOfDate,
                    Quantity = line.Quantity,
                    UnitCost = line.UnitCost,
                    WarehouseId = line.WarehouseId,
                    SourceType = OpeningBalanceCode,
                    SourceId = request.OpeningBalanceId,
                    SourceLineId = line.LineNumber,
                },
                ct);

            // Already recorded is success, not a failure: it means an earlier
            // attempt at this same finalize got this far. Treating it as an error
            // would make a retry impossible for exactly the documents that most
            // need one.
            bool ok = result.Outcome is StockOutcome.Ok or StockOutcome.DuplicateSource;

            response.Lines.Add(new OpeningStockLineResult
            {
                LineNumber = line.LineNumber,
                ItemId = line.ItemId,
                Recorded = ok,
                AlreadyRecorded = result.Outcome == StockOutcome.DuplicateSource,
                Outcome = result.Outcome.ToString(),
                Value = ok ? line.Quantity * line.UnitCost : 0m,
            });
        }

        response.Value = response.Lines.Sum(l => l.Value);

        if (response.Lines.Exists(l => !l.Recorded))
        {
            _log.LogWarning(
                "Opening stock for {OpeningBalanceId} in {OrgId}: {Failed} of {Total} lines refused.",
                request.OpeningBalanceId,
                request.OrgId,
                response.Lines.Count(l => !l.Recorded),
                response.Lines.Count);

            // 409, not 400: the request is well-formed and some of it landed.
            // The caller has to see which lines, because a partly recorded
            // opening stock is exactly the state it must not treat as done.
            return Conflict(response);
        }

        return Ok(response);
    }

    /// <summary><c>mst.TransactionTypes</c> OPB.</summary>
    private const string OpeningBalanceCode = "OPB";

    /// <summary>
    /// Holds stock for a confirmed order.
    ///
    /// <b>All of it or none of it.</b> Reserving four lines and failing on the
    /// fifth would leave stock held by an order that was never confirmed, with
    /// nothing on any screen saying so — so the shortages are found first, and
    /// only if every line can be taken is anything taken.
    ///
    /// <b>Not idempotent, and cannot be.</b> A reservation is a quantity, not a
    /// row keyed on a document, so a repeated call reserves twice. The caller
    /// only ever calls this on the Draft-to-Confirmed transition, which its own
    /// guarded status change makes happen once.
    /// </summary>
    [HttpPost("reserve")]
    public async Task<IActionResult> Reserve(
        [FromBody] ReserveStockRequest request, CancellationToken ct)
    {
        if (Tenant(request.CustomerId, request.OrgId) is IActionResult bad)
        {
            return bad;
        }

        var stock = _services.GetRequiredService<StockService>();
        var db = _services.GetRequiredService<Repository.InventoryDbContext>();

        var response = new ReserveStockResponse { Reserved = true };

        // Every line's availability first, before a single one is taken.
        foreach (ReserveStockLine line in request.Lines)
        {
            var position = await db.Items
                .Where(i => i.ItemId == line.ItemId)
                .Select(i => new
                {
                    i.ItemCode,
                    i.ItemName,
                    Available = db.ItemStock
                        .Where(s => s.ItemId == i.ItemId)
                        .Select(s => s.QuantityOnHand - s.QuantityReserved)
                        .FirstOrDefault(),
                })
                .FirstOrDefaultAsync(ct);

            bool enough = position is not null && position.Available >= line.Quantity;

            response.Lines.Add(new ReserveStockLineResult
            {
                LineNumber = line.LineNumber,
                ItemId = line.ItemId,
                ItemCode = position?.ItemCode ?? string.Empty,
                ItemName = position?.ItemName ?? string.Empty,
                Requested = line.Quantity,
                Available = position?.Available ?? 0m,
                Ok = enough,
                Outcome = position is null
                    ? nameof(StockOutcome.ItemNotFound)
                    : enough ? nameof(StockOutcome.Ok) : nameof(StockOutcome.InsufficientStock),
            });

            response.Reserved &= enough;
        }

        if (!response.Reserved)
        {
            _log.LogInformation(
                "Reservation for {SourceType}-{SourceId} refused: {Short} of {Total} lines short.",
                request.SourceType,
                request.SourceId,
                response.Lines.Count(l => !l.Ok),
                response.Lines.Count);

            return BadRequest(response);
        }

        // Now take them. Each is still guarded, so a concurrent sale between the
        // check above and the take below loses the race rather than overdrawing —
        // and that line comes back short.
        foreach (ReserveStockLine line in request.Lines)
        {
            StockOutcome outcome = await stock.ReserveAsync(line.ItemId, line.Quantity, ct);

            if (outcome != StockOutcome.Ok)
            {
                // Give back whatever this call already took. Without it, losing
                // the race on line five would leave lines one to four held by an
                // order that did not confirm.
                foreach (ReserveStockLine taken in request.Lines)
                {
                    if (taken.LineNumber == line.LineNumber)
                    {
                        break;
                    }

                    await stock.ReleaseAsync(taken.ItemId, taken.Quantity, ct);
                }

                ReserveStockLineResult row =
                    response.Lines.First(l => l.LineNumber == line.LineNumber);
                row.Ok = false;
                row.Outcome = outcome.ToString();
                response.Reserved = false;

                return BadRequest(response);
            }
        }

        return Ok(response);
    }

    /// <summary>
    /// Gives stock back — a cancelled order, a short close, or the part of a line
    /// that shipped and so stopped being a promise.
    ///
    /// A release of more than is held is refused per line rather than clamped: it
    /// means the caller believes it holds something it does not, and quietly
    /// releasing the difference would free stock nobody reserved.
    /// </summary>
    [HttpPost("release")]
    public async Task<IActionResult> Release(
        [FromBody] ReserveStockRequest request, CancellationToken ct)
    {
        if (Tenant(request.CustomerId, request.OrgId) is IActionResult bad)
        {
            return bad;
        }

        var stock = _services.GetRequiredService<StockService>();
        var response = new ReserveStockResponse { Reserved = true };

        foreach (ReserveStockLine line in request.Lines)
        {
            StockOutcome outcome = await stock.ReleaseAsync(line.ItemId, line.Quantity, ct);
            bool ok = outcome == StockOutcome.Ok;

            response.Lines.Add(new ReserveStockLineResult
            {
                LineNumber = line.LineNumber,
                ItemId = line.ItemId,
                Requested = line.Quantity,
                Ok = ok,
                Outcome = outcome.ToString(),
            });

            response.Reserved &= ok;
        }

        if (!response.Reserved)
        {
            _log.LogWarning(
                "Release for {SourceType}-{SourceId}: {Failed} of {Total} lines held less than "
                    + "the caller believed.",
                request.SourceType,
                request.SourceId,
                response.Lines.Count(l => !l.Ok),
                response.Lines.Count);
        }

        // Reported, not refused. The document has moved on regardless, and a
        // release that found less than expected must not block a cancellation.
        return Ok(response);
    }

    /// <summary>Sets the tenant, or says why it could not.</summary>
    private IActionResult? Tenant(Guid customerId, Guid orgId)
    {
        if (customerId == Guid.Empty || orgId == Guid.Empty)
        {
            return BadRequest(new MessageResponse
            {
                Message = "A customer and an organization are required.",
            });
        }

        // Set before anything resolves a DbContext: the context is built from the
        // tenant, so resolving a service first would bind it to no tenant.
        _tenant.CustomerId = customerId;
        _tenant.OrgId = orgId;
        return null;
    }
}
