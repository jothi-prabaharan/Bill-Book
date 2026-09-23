using Inventory.Api.Services;
using Inventory.Entity.Enums;
using Inventory.Entity.TableEntities;
using Inventory.Repository;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Shared.Kernel.Errors;
using Shared.Kernel.Persistence;
using Shared.Kernel.Tenancy;

namespace CostingEngine.Worker.Consumers;

/// <summary>
/// Settles what stock cost, after the quantity has already moved.
///
/// <b>The queue is the movements table.</b> There is no message broker in the
/// path, and that is deliberate rather than a shortcut — it is what makes the
/// two hard guarantees free:
///
/// <list type="bullet">
/// <item><b>Order.</b> FIFO gives the wrong answer if movements are costed out
/// of sequence. Work is read <c>ORDER BY ItemId, MovementDate, StockMovementId</c>
/// and processed one at a time per item, so the order is a property of the read
/// rather than something a broker has to promise.</item>
/// <item><b>Exactly once.</b> A movement is claimed by a guarded status change
/// from Pending to InProgress. Two workers racing means one of them updates no
/// rows and moves on. There is no redelivery to dedupe, because there is no
/// delivery — and the unique index on (issue, layer) would refuse a second
/// allocation even if there were.</item>
/// </list>
///
/// Nothing is lost to a restart either: a movement that was InProgress when the
/// process died is reclaimed after a timeout, because its state is a row rather
/// than a message that was in flight.
///
/// If a broker is added later it should <i>wake</i> this loop, not replace it.
/// The database stays the source of truth.
/// </summary>
public sealed class CostingWorker : BackgroundService
{
    private readonly ITenantEnumerator _tenants;
    private readonly IServiceScopeFactory _scopes;
    private readonly IConfiguration _config;
    private readonly ILogger<CostingWorker> _log;

    public CostingWorker(
        ITenantEnumerator tenants,
        IServiceScopeFactory scopes,
        IConfiguration config,
        ILogger<CostingWorker> log)
    {
        _tenants = tenants;
        _scopes = scopes;
        _config = config;
        _log = log;
    }

    private TimeSpan PollInterval => TimeSpan.FromSeconds(
        int.TryParse(_config["Costing:PollSeconds"], out int seconds) ? seconds : 5);

    /// <summary>How long a claim may go stale before another worker may take it back.</summary>
    private TimeSpan ClaimTimeout => TimeSpan.FromMinutes(
        int.TryParse(_config["Costing:ClaimTimeoutMinutes"], out int minutes) ? minutes : 10);

    /// <summary>
    /// Attempts before a movement is parked as Failed. It stops rather than
    /// retrying forever, because a movement that cannot be costed needs a person
    /// and an infinite retry hides that.
    /// </summary>
    private int MaxAttempts =>
        int.TryParse(_config["Costing:MaxAttempts"], out int attempts) ? attempts : 5;

    /// <summary>Movements taken per organization per tick.</summary>
    private int BatchSize =>
        int.TryParse(_config["Costing:BatchSize"], out int size) ? size : 100;

    /// <summary>
    /// Attempts before a posting is parked. Separate from the costing bound
    /// because the two fail for unrelated reasons — costing fails on this
    /// branch's own data, posting fails on another service being reachable.
    /// </summary>
    private int LedgerMaxAttempts =>
        int.TryParse(_config["Ledger:MaxAttempts"], out int attempts) ? attempts : 5;

    private int LedgerBatchSize =>
        int.TryParse(_config["Ledger:BatchSize"], out int size) ? size : 100;

    /// <summary>Recorded on every error row, so one schema's log can name which loop wrote it.</summary>
    private const string WorkerName = "CostingEngine";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _log.LogInformation("Costing engine started, polling every {Interval}", PollInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                IReadOnlyList<ActiveOrganization> organizations =
                    await _tenants.ListAsync(stoppingToken);

                foreach (ActiveOrganization organization in organizations)
                {
                    await ProcessOrganizationAsync(organization, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // One bad tick must not take the loop down; the work is still in
                // the table and the next tick picks it up.
                //
                // Logged and not recorded, deliberately. A tick fails before any
                // organization has been picked — listing them is the first thing
                // it does — so there is no tenant, and ErrorLogs is tenant
                // scoped. Anything that fails after an organization is chosen is
                // recorded; see ProcessOrganizationAsync.
                _log.LogError(ex, "Costing tick failed");
            }

            try
            {
                await Task.Delay(PollInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _log.LogInformation("Costing engine stopped");
    }

    private async Task ProcessOrganizationAsync(
        ActiveOrganization organization, CancellationToken ct)
    {
        using IServiceScope scope = _scopes.CreateScope();

        // No request to take the tenant from, so it is set on the scope by hand.
        // Everything below — the connection the context opens and the RLS
        // setting on it — follows from this.
        var tenant = (TenantContext)scope.ServiceProvider.GetRequiredService<ITenantContext>();
        tenant.CustomerId = organization.CustomerId;
        tenant.OrgId = organization.OrgId;

        InventoryDbContext db = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        CostingService costing = scope.ServiceProvider.GetRequiredService<CostingService>();
        var auditor = scope.ServiceProvider.GetRequiredService<IWorkerErrorAuditor>();

        await ReclaimStaleAsync(db, ct);

        // Order is the whole point. Within an item, movements have to be costed
        // in the order they happened, or FIFO consumes the wrong layer.
        List<StockMovement> pending = await db.StockMovements
            .Where(m => m.CostingStatus == CostingStatus.Pending
                && m.CostingAttempts < MaxAttempts)
            .OrderBy(m => m.ItemId)
            .ThenBy(m => m.MovementDate)
            .ThenBy(m => m.StockMovementId)
            .Take(BatchSize)
            .ToListAsync(ct);

        // Asked for at most once per organization per tick, and only when a
        // weighted-average item has work — most ticks for most branches have none.
        PeriodLockLookup? periodLock = null;

        // GroupBy keeps the read's order, both of the groups and within each.
        foreach (IGrouping<long, StockMovement> group in pending.GroupBy(m => m.ItemId))
        {
            CostingType? costingType = await db.Items
                .AsNoTracking()
                .Where(i => i.ItemId == group.Key)
                .Select(i => (CostingType?)i.CostingType)
                .FirstOrDefaultAsync(ct);

            if (costingType == CostingType.WeightedAverage)
            {
                periodLock ??= await scope.ServiceProvider
                    .GetRequiredService<IAccountingPeriodLock>()
                    .LockedUptoAsync(organization.CustomerId, organization.OrgId, ct);

                if (!periodLock.Known)
                {
                    // Without the lock date there is no telling which stock-outs
                    // are in a closed period, and guessing "none" would restate
                    // them. The movements stay Pending, unclaimed and with no
                    // attempt counted, and the next tick asks again.
                    continue;
                }

                await CostWeightedAverageAsync(
                    scope, db, costing, auditor, group.Key, [.. group], periodLock.LockedUpto, ct);

                continue;
            }

            foreach (StockMovement movement in group)
            {
                await CostOneAsync(db, costing, auditor, movement, ct);
            }
        }

        // Posting runs after costing in the same tick, so a movement costed a
        // moment ago is posted a moment later rather than a whole poll interval
        // later. It is a separate pass rather than part of CostOneAsync because
        // it must not be able to roll back a settled cost: Accounting being
        // briefly unreachable is not a reason to un-cost a sale.
        await PostOrganizationAsync(scope, organization, ct);
    }

    /// <summary>
    /// Drains this organization's ledger queue. Failures here are contained:
    /// one organization that cannot post must not stop the others, because the
    /// usual cause is that organization's own chart of accounts.
    /// </summary>
    private async Task PostOrganizationAsync(
        IServiceScope scope, ActiveOrganization organization, CancellationToken ct)
    {
        try
        {
            var poster = scope.ServiceProvider.GetRequiredService<StockLedgerPoster>();

            int posted = await poster.PostPendingAsync(
                LedgerBatchSize, LedgerMaxAttempts, ClaimTimeout, ct);

            if (posted > 0)
            {
                _log.LogInformation(
                    "Posted {Count} stock movement(s) to the ledger for organization {OrgId}",
                    posted,
                    organization.OrgId);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // The tenant is set on this scope, so this one can be recorded.
            await scope.ServiceProvider
                .GetRequiredService<IWorkerErrorAuditor>()
                .AuditAsync(
                    WorkerName,
                    $"LedgerPosting OrgId={organization.OrgId}",
                    ex,
                    ct);
        }
    }

    /// <summary>
    /// Takes back claims from a worker that died holding them. Bounded by a
    /// timeout rather than a heartbeat: a movement stuck InProgress forever
    /// would silently never be costed.
    /// </summary>
    private async Task ReclaimStaleAsync(InventoryDbContext db, CancellationToken ct)
    {
        DateTimeOffset cutoff = DateTimeOffset.UtcNow - ClaimTimeout;

        await db.StockMovements
            .Where(m => m.CostingStatus == CostingStatus.InProgress
                && m.ModifiedAt != null
                && m.ModifiedAt < cutoff)
            .ExecuteUpdateAsync(
                m => m.SetProperty(x => x.CostingStatus, CostingStatus.Pending), ct);
    }

    private async Task CostOneAsync(
        InventoryDbContext db,
        CostingService costing,
        IWorkerErrorAuditor auditor,
        StockMovement movement,
        CancellationToken ct)
    {
        // The claim. Guarded on still being Pending, so two workers racing for
        // the same movement means one of them changes no rows and walks away.
        int claimed = await db.StockMovements
            .Where(m => m.StockMovementId == movement.StockMovementId
                && m.CostingStatus == CostingStatus.Pending)
            .ExecuteUpdateAsync(
                m => m
                    .SetProperty(x => x.CostingStatus, CostingStatus.InProgress)
                    .SetProperty(x => x.CostingAttempts, x => x.CostingAttempts + 1)
                    .SetProperty(x => x.ModifiedAt, DateTimeOffset.UtcNow),
                ct);

        if (claimed == 0)
        {
            return;
        }

        Item? item = await db.Items
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.ItemId == movement.ItemId, ct);

        if (item is null)
        {
            await ParkAsync(db, movement, "The item no longer exists.", ct);
            return;
        }

        await using IDbContextTransaction tx = await db.Database.BeginTransactionAsync(ct);

        try
        {
            Inventory.Entity.Models.StockOutcome outcome =
                await costing.CostMovementAsync(item, movement, ct);

            if (outcome != Inventory.Entity.Models.StockOutcome.Ok)
            {
                await tx.RollbackAsync(ct);
                await ParkAsync(db, movement, $"Costing returned {outcome}.", ct);
                return;
            }

            await db.StockMovements
                .Where(m => m.StockMovementId == movement.StockMovementId)
                .ExecuteUpdateAsync(
                    m => m
                        .SetProperty(x => x.CostingStatus, CostingStatus.Costed)
                        .SetProperty(x => x.CostedAt, DateTimeOffset.UtcNow)
                        .SetProperty(x => x.CostingError, (string?)null),
                    ct);

            await tx.CommitAsync(ct);
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync(ct);

            // Onto the task list, naming the movement so the follow-up has
            // something to act on. CostingError on the row says a movement
            // failed; this says why, in the database's own words, which the row
            // has no space for and which nobody should have to read a log to
            // find.
            await auditor.AuditAsync(
                WorkerName,
                $"StockMovementId={movement.StockMovementId}",
                ex,
                ct);

            await ParkAsync(db, movement, ex.Message, ct);
        }
    }

    /// <summary>
    /// Costs a weighted-average item's pending movements together, and then
    /// recalculates the item once.
    ///
    /// <b>Once per item, not once per movement.</b> The recalculation walks the
    /// item's whole history in date order and revalues every stock-out after
    /// the lock date, so a till that sold the same item a hundred times since
    /// the last tick needs one pass, not a hundred.
    ///
    /// <b>One transaction for the lot.</b> A stock-in's layer, the revalued
    /// stock-outs, the item's average and the Costed status commit together or
    /// not at all. Marking a movement Costed before its value is settled would
    /// let the ledger post a figure the recalculation is about to change.
    /// </summary>
    private async Task CostWeightedAverageAsync(
        IServiceScope scope,
        InventoryDbContext db,
        CostingService costing,
        IWorkerErrorAuditor auditor,
        long itemId,
        List<StockMovement> movements,
        DateOnly? lockDate,
        CancellationToken ct)
    {
        // The same guarded claim as one movement at a time: a movement another
        // worker already holds changes no rows and is left to that worker.
        var claimed = new List<StockMovement>(movements.Count);

        foreach (StockMovement movement in movements)
        {
            int taken = await db.StockMovements
                .Where(m => m.StockMovementId == movement.StockMovementId
                    && m.CostingStatus == CostingStatus.Pending)
                .ExecuteUpdateAsync(
                    m => m
                        .SetProperty(x => x.CostingStatus, CostingStatus.InProgress)
                        .SetProperty(x => x.CostingAttempts, x => x.CostingAttempts + 1)
                        .SetProperty(x => x.ModifiedAt, DateTimeOffset.UtcNow),
                    ct);

            if (taken > 0)
            {
                claimed.Add(movement);
            }
        }

        if (claimed.Count == 0)
        {
            return;
        }

        Item? item = await db.Items
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.ItemId == itemId, ct);

        if (item is null)
        {
            foreach (StockMovement movement in claimed)
            {
                await ParkAsync(db, movement, "The item no longer exists.", ct);
            }

            return;
        }

        var recosting = scope.ServiceProvider.GetRequiredService<WeightedAverageRecosting>();

        await using ITransactionScope tx = await db.Database.BeginScopeAsync(ct);

        try
        {
            // Layers first. A stock-in on weighted average still records its
            // receipt as a layer — history rather than an allocation pool — and
            // a stock-out has nothing to do here: its value is the
            // recalculation's to decide.
            foreach (StockMovement movement in claimed)
            {
                Inventory.Entity.Models.StockOutcome outcome =
                    await costing.CostMovementAsync(item, movement, ct);

                if (outcome != Inventory.Entity.Models.StockOutcome.Ok)
                {
                    await tx.RollbackAsync(ct);
                    db.ChangeTracker.Clear();

                    foreach (StockMovement parked in claimed)
                    {
                        await ParkAsync(
                            db,
                            parked,
                            $"Costing movement {movement.StockMovementId} returned {outcome}.",
                            ct);
                    }

                    return;
                }
            }

            WeightedAverageRecostResult result =
                await recosting.RecalculateAsync(item.ItemId, lockDate, ct);

            long[] ids = [.. claimed.Select(m => m.StockMovementId)];

            await db.StockMovements
                .Where(m => ids.Contains(m.StockMovementId))
                .ExecuteUpdateAsync(
                    m => m
                        .SetProperty(x => x.CostingStatus, CostingStatus.Costed)
                        .SetProperty(x => x.CostedAt, DateTimeOffset.UtcNow)
                        .SetProperty(x => x.CostingError, (string?)null),
                    ct);

            await tx.CommitAsync(ct);

            if (result.StockOutsRevalued > 0)
            {
                _log.LogInformation(
                    "Recalculated the weighted average of item {ItemId}: {Revalued} stock-out(s) "
                        + "revalued, {Requeued} requeued for posting, average now {Average}",
                    item.ItemId,
                    result.StockOutsRevalued,
                    result.StockOutsRequeuedForPosting,
                    result.AverageCost);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            await tx.RollbackAsync(ct);

            // The rollback undid the rows; the tracker still believes in them.
            db.ChangeTracker.Clear();

            await auditor.AuditAsync(
                WorkerName,
                $"WeightedAverage ItemId={item.ItemId}",
                ex,
                ct);

            foreach (StockMovement movement in claimed)
            {
                await ParkAsync(db, movement, ex.Message, ct);
            }
        }
    }

    /// <summary>
    /// Returns a movement to the queue, or gives up on it once it has failed
    /// enough times. The reason is written to the row: a failure nobody can see
    /// without reading logs is a failure nobody sees.
    /// </summary>
    private async Task ParkAsync(
        InventoryDbContext db, StockMovement movement, string reason, CancellationToken ct)
    {
        int attempts = await db.StockMovements
            .Where(m => m.StockMovementId == movement.StockMovementId)
            .Select(m => m.CostingAttempts)
            .FirstOrDefaultAsync(ct);

        CostingStatus next = attempts >= MaxAttempts
            ? CostingStatus.Failed
            : CostingStatus.Pending;

        string trimmed = reason.Length > 500 ? reason[..500] : reason;

        await db.StockMovements
            .Where(m => m.StockMovementId == movement.StockMovementId)
            .ExecuteUpdateAsync(
                m => m
                    .SetProperty(x => x.CostingStatus, next)
                    .SetProperty(x => x.CostingError, trimmed),
                ct);
    }
}
