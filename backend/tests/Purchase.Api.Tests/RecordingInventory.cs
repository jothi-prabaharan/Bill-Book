using Purchase.Api.Services;

namespace Purchase.Api.Tests;

/// <summary>
/// Inventory, recorded rather than called.
///
/// <b>It behaves the way the real endpoint does in the ways the tests depend
/// on</b>: a movement is keyed on (source type, source id, line), a second
/// presentation of the same key comes back already recorded and worth nothing
/// new, and a refusal names the line. Everything else — units, batches,
/// warehouses — is Inventory's own and has its own suite.
///
/// The alternative is standing up the Inventory service over HTTP, which would
/// make these tests about the network.
/// </summary>
public sealed class RecordingInventory : IInventoryClient
{
    private readonly HashSet<(string Source, long Id, long Line)> _recorded = [];

    /// <summary>Every request that arrived, in order.</summary>
    public List<ReceiveStockRequest> Calls { get; } = [];

    /// <summary>Item ids Inventory should refuse, and the outcome it should give.</summary>
    public Dictionary<long, string> Refuse { get; } = [];

    /// <summary>Every return that arrived, in order.</summary>
    public List<ReturnStockRequest> Returns { get; } = [];

    /// <summary>
    /// What a returned unit is carrying, which is what the layers say rather
    /// than what the bill charged. Purchase credits stock by this.
    /// </summary>
    public decimal ReturnUnitCost { get; set; } = 100m;

    public Task<ReceiveStockResponse> ReceiveAsync(
        ReceiveStockRequest request, CancellationToken ct)
    {
        Calls.Add(request);

        ReceiveStockResponse response = new() { Success = true };

        foreach (ReceiveStockLine line in request.Lines)
        {
            if (Refuse.TryGetValue(line.ItemId, out string? outcome))
            {
                response.Success = false;
                response.Lines.Add(new ReceiveStockLineResult
                {
                    SourceLineId = line.SourceLineId,
                    ItemId = line.ItemId,
                    Quantity = line.Quantity,
                    Success = false,
                    Outcome = outcome,
                });

                continue;
            }

            bool fresh = _recorded.Add(
                (request.SourceType, request.SourceId, line.SourceLineId));

            response.Lines.Add(new ReceiveStockLineResult
            {
                SourceLineId = line.SourceLineId,
                ItemId = line.ItemId,
                Quantity = line.Quantity,
                Success = true,
                AlreadyRecorded = !fresh,
                Outcome = fresh ? "Ok" : "DuplicateSource",
                UnitCost = line.UnitCost,
                LineValue = fresh ? line.Quantity * line.UnitCost : 0m,
            });
        }

        response.TotalValue = response.Lines.Sum(l => l.LineValue);
        return Task.FromResult(response);
    }

    public Task<ReturnStockResponse> ReturnAsync(
        ReturnStockRequest request, CancellationToken ct)
    {
        Returns.Add(request);

        ReturnStockResponse response = new() { Success = true };

        foreach (ReturnStockLine line in request.Lines)
        {
            if (Refuse.TryGetValue(line.ItemId, out string? outcome))
            {
                response.Success = false;
                response.Lines.Add(new ReturnStockLineResult
                {
                    SourceLineId = line.SourceLineId,
                    ItemId = line.ItemId,
                    Quantity = line.Quantity,
                    Success = false,
                    Outcome = outcome,
                });

                continue;
            }

            bool fresh = _recorded.Add(
                (request.SourceType, request.SourceId, line.SourceLineId));

            response.Lines.Add(new ReturnStockLineResult
            {
                SourceLineId = line.SourceLineId,
                ItemId = line.ItemId,
                Quantity = line.Quantity,
                Success = true,
                AlreadyRecorded = !fresh,
                Outcome = fresh ? "Ok" : "DuplicateSource",
                UnitCost = ReturnUnitCost,
                LineValue = fresh ? line.Quantity * ReturnUnitCost : 0m,
            });
        }

        response.TotalValue = response.Lines.Sum(l => l.LineValue);
        return Task.FromResult(response);
    }
}

/// <summary>
/// The ledger, recorded rather than posted.
///
/// Accounting's own suite proves that a balanced set of legs posts and an
/// unbalanced one does not. What these tests need to know is which legs Purchase
/// <i>sends</i> — which accounts, which way round, and for how much.
/// </summary>
public sealed class RecordingLedger : ILedgerClient
{
    public List<PostLedgerRequest> Posts { get; } = [];

    /// <summary>Set to make the ledger refuse, so the retry path can be exercised.</summary>
    public string? RefuseWith { get; set; }

    public Task<PostLedgerOutcomeResult> PostAsync(
        PostLedgerRequest request, CancellationToken ct)
    {
        Posts.Add(request);

        return Task.FromResult(
            RefuseWith is null
                ? new PostLedgerOutcomeResult(true, null)
                : new PostLedgerOutcomeResult(false, RefuseWith));
    }

    /// <summary>What one account was debited across the last posting.</summary>
    public decimal DebitOf(string accountSystemName) =>
        Posts.Count == 0
            ? 0m
            : Posts[^1].Legs
                .Where(l => l.AccountSystemName == accountSystemName)
                .Sum(l => l.DebitAmount);

    public decimal CreditOf(string accountSystemName) =>
        Posts.Count == 0
            ? 0m
            : Posts[^1].Legs
                .Where(l => l.AccountSystemName == accountSystemName)
                .Sum(l => l.CreditAmount);
}
