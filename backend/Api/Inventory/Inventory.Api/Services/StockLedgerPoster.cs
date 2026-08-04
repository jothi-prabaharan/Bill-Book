using Inventory.Entity.Enums;
using Inventory.Entity.TableEntities;
using Inventory.Repository;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Tenancy;

namespace Inventory.Api.Services;

/// <summary>
/// Drains the ledger queue: takes movements whose cost has settled and asks
/// Accounting to post them.
///
/// <b>The queue is the movements table</b>, exactly as it is for costing, and
/// for the same two reasons. Exactly-once comes from a guarded
/// <c>Pending → InProgress</c> claim whose row count is the answer, so two
/// workers racing means one of them updates nothing and moves on. And nothing is
/// lost to a restart, because the state is a row rather than a message that was
/// in flight.
///
/// Unlike costing, <b>order does not matter here</b>. Each posting is two legs
/// that balance on their own and replace only their own rows, so posting a
/// Tuesday sale before a Monday one produces exactly the same ledger.
/// </summary>
public sealed class StockLedgerPoster
{
    private readonly InventoryDbContext _db;
    private readonly IAccountingLedger _ledger;
    private readonly ITenantContext _tenant;
    private readonly TimeProvider _clock;
    private readonly ILogger<StockLedgerPoster> _log;

    public StockLedgerPoster(
        InventoryDbContext db,
        IAccountingLedger ledger,
        ITenantContext tenant,
        TimeProvider clock,
        ILogger<StockLedgerPoster> log)
    {
        _db = db;
        _ledger = ledger;
        _tenant = tenant;
        _clock = clock;
        _log = log;
    }

    /// <summary>The sub-account dimension for an item, matching Accounting's own enum value.</summary>
    private const int ItemReference = 2;

    /// <summary>How many movements were posted. Anything else is left for the next pass.</summary>
    public async Task<int> PostPendingAsync(
        int batchSize, int maxAttempts, TimeSpan claimTimeout, CancellationToken ct)
    {
        if (_tenant.CustomerId is not Guid customerId || _tenant.OrgId is not Guid orgId)
        {
            return 0;
        }

        await ReclaimStaleAsync(claimTimeout, ct);

        // Only movements whose cost is settled. A movement still being costed
        // has nothing to post, and one whose costing failed is owed a posting it
        // cannot be given yet — it stays queued rather than being parked twice
        // for the same underlying problem.
        List<StockMovement> pending = await _db.StockMovements
            .Where(m => m.LedgerStatus == LedgerStatus.Pending
                && m.LedgerAttempts < maxAttempts
                && (m.CostingStatus == CostingStatus.Costed
                    || m.CostingStatus == CostingStatus.Skipped))
            .OrderBy(m => m.MovementDate)
            .ThenBy(m => m.StockMovementId)
            .Take(batchSize)
            .ToListAsync(ct);

        int posted = 0;

        foreach (StockMovement movement in pending)
        {
            if (await PostOneAsync(customerId, orgId, movement, maxAttempts, ct))
            {
                posted++;
            }
        }

        return posted;
    }

    /// <summary>
    /// Takes back claims from a worker that died holding them, exactly as the
    /// costing queue does.
    ///
    /// Without this a process killed mid-post leaves the movement InProgress
    /// for ever: the queue only reads Pending, so nothing would look at it
    /// again, and stock and the general ledger would disagree about that
    /// movement permanently with nothing on any screen saying why. Bounded by a
    /// timeout rather than a heartbeat, because the state is a row and the row
    /// is all a second worker has to go on.
    /// </summary>
    private async Task ReclaimStaleAsync(TimeSpan claimTimeout, CancellationToken ct)
    {
        DateTimeOffset cutoff = _clock.GetUtcNow() - claimTimeout;

        await _db.StockMovements
            .Where(m => m.LedgerStatus == LedgerStatus.InProgress
                && m.ModifiedAt != null
                && m.ModifiedAt < cutoff)
            .ExecuteUpdateAsync(
                m => m.SetProperty(x => x.LedgerStatus, LedgerStatus.Pending), ct);
    }

    private async Task<bool> PostOneAsync(
        Guid customerId, Guid orgId, StockMovement movement, int maxAttempts, CancellationToken ct)
    {
        StockPosting? posting = StockLedgerMapping.For(movement);

        // No posting to make. A settled answer, not a failure — a transfer moves
        // no value, and a receipt against a purchase document is posted by that
        // document.
        if (posting is null)
        {
            await SettleAsync(movement, LedgerStatus.NotApplicable, null, ct);
            return false;
        }

        // Nothing to post either, but for a different reason: a movement that
        // cost nothing has no amount, and a leg of zero is neither a debit nor a
        // credit. Marked rather than retried, or it would sit in the queue for
        // ever waiting for a number that is never coming.
        if (movement.TotalCost is not decimal amount || amount <= 0)
        {
            await SettleAsync(
                movement,
                LedgerStatus.NotApplicable,
                "The movement carries no cost, so there is nothing to post.",
                ct);
            return false;
        }

        // The claim. Guarded on still being Pending, so two workers racing for
        // the same movement means one of them changes no rows and walks away.
        int claimed = await _db.StockMovements
            .Where(m => m.StockMovementId == movement.StockMovementId
                && m.LedgerStatus == LedgerStatus.Pending)
            .ExecuteUpdateAsync(
                m => m
                    .SetProperty(x => x.LedgerStatus, LedgerStatus.InProgress)
                    .SetProperty(x => x.LedgerAttempts, x => x.LedgerAttempts + 1)
                    // Stamped by hand because ExecuteUpdate never reaches the
                    // audit interceptor — it is one SQL statement with no
                    // change tracker behind it. This one is load-bearing rather
                    // than bookkeeping: it is what ReclaimStaleAsync measures a
                    // dead worker's claim against.
                    .SetProperty(x => x.ModifiedAt, _clock.GetUtcNow()),
                ct);

        if (claimed == 0)
        {
            return false;
        }

        string description = $"{movement.MovementType} {movement.Direction} "
            + $"{movement.Quantity} (movement {movement.StockMovementId})";

        // Both legs carry the item sub-account, so cost of goods sold and the
        // inventory asset can both be read per item without the chart of
        // accounts growing a line for every product.
        var request = new LedgerPosting(
            customerId,
            orgId,
            posting.TransactionTypeCode,
            posting.TransactionId,
            movement.MovementDate,
            movement.StockMovementId,
            [
                Leg(posting, movement.ItemId, amount, isDebit: true, description),
                Leg(posting, movement.ItemId, amount, isDebit: false, description),
            ]);

        LedgerPostOutcome outcome = await _ledger.PostAsync(request, ct);

        switch (outcome)
        {
            case LedgerPostOutcome.Posted:
                await SettleAsync(movement, LedgerStatus.Posted, null, ct);
                return true;

            case LedgerPostOutcome.Retry:
                // Back to the queue, and the attempt already counted. The bound
                // applies to transient failures too: something unreachable for
                // long enough is not transient, and a person should hear about
                // it rather than a log line repeating for a week.
                await ReturnOrParkAsync(
                    movement, maxAttempts, "Accounting could not be reached.", ct);
                return false;

            default:
                await ReturnOrParkAsync(
                    movement,
                    maxAttempts,
                    "Accounting refused the posting. The chart of accounts is probably "
                        + "missing an account this movement needs.",
                    ct);
                return false;
        }
    }

    private static LedgerPostingLeg Leg(
        StockPosting posting, long itemId, decimal amount, bool isDebit, string description)
    {
        string account = isDebit
            ? posting.DebitAccountSystemName
            : posting.CreditAccountSystemName;

        return new LedgerPostingLeg(
            // Both legs of a stock posting are the document's cost-of-sales leg,
            // filed against the document's own line. Together those two are the
            // key this posting replaces, so a recosted movement corrects its own
            // rows and leaves the rest of the document alone.
            StockLedgerMapping.CogsLedgerType,
            posting.LedgerSourceId,
            posting.TransactionDetailId,
            account,
            // Only the stock accounts have an item beneath them. Opening Balance
            // Equity has no sub-dimension, and asking for one that was never
            // provisioned would have Accounting refuse the whole posting.
            account == StockLedgerMapping.OpeningBalanceEquity ? null : ItemReference,
            account == StockLedgerMapping.OpeningBalanceEquity ? null : itemId,
            isDebit ? amount : 0m,
            isDebit ? 0m : amount,
            description);
    }

    private async Task SettleAsync(
        StockMovement movement, LedgerStatus status, string? note, CancellationToken ct) =>
        await _db.StockMovements
            .Where(m => m.StockMovementId == movement.StockMovementId)
            .ExecuteUpdateAsync(
                m => m
                    .SetProperty(x => x.LedgerStatus, status)
                    .SetProperty(
                        x => x.LedgerPostedAt,
                        status == LedgerStatus.Posted ? _clock.GetUtcNow() : null)
                    .SetProperty(x => x.LedgerError, note),
                ct);

    /// <summary>
    /// Returns a movement to the queue, or gives up on it once it has failed
    /// enough times. The reason goes on the row: while it is set, stock and the
    /// general ledger disagree about this movement, and that is not something to
    /// leave discoverable only in logs.
    /// </summary>
    private async Task ReturnOrParkAsync(
        StockMovement movement, int maxAttempts, string reason, CancellationToken ct)
    {
        int attempts = await _db.StockMovements
            .Where(m => m.StockMovementId == movement.StockMovementId)
            .Select(m => m.LedgerAttempts)
            .FirstOrDefaultAsync(ct);

        LedgerStatus next = attempts >= maxAttempts ? LedgerStatus.Failed : LedgerStatus.Pending;

        if (next == LedgerStatus.Failed)
        {
            _log.LogError(
                "Movement {MovementId} could not be posted after {Attempts} attempts: {Reason}",
                movement.StockMovementId,
                attempts,
                reason);
        }

        await _db.StockMovements
            .Where(m => m.StockMovementId == movement.StockMovementId)
            .ExecuteUpdateAsync(
                m => m
                    .SetProperty(x => x.LedgerStatus, next)
                    .SetProperty(x => x.LedgerError, reason),
                ct);
    }
}
