using Inventory.Api.Services;
using Inventory.Entity.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
}
