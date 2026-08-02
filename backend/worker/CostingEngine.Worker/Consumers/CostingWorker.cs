using Inventory.Api.Services;
using Inventory.Entity.Enums;
using Inventory.Entity.TableEntities;
using Inventory.Repository;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
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

        foreach (StockMovement movement in pending)
        {
            await CostOneAsync(db, costing, movement, ct);
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

            int posted = await poster.PostPendingAsync(LedgerBatchSize, LedgerMaxAttempts, ct);

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
            _log.LogError(
                ex,
                "Posting stock movements failed for organization {OrgId}",
                organization.OrgId);
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
        InventoryDbContext db, CostingService costing, StockMovement movement, CancellationToken ct)
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

            _log.LogError(
                ex, "Costing movement {MovementId} failed", movement.StockMovementId);

            await ParkAsync(db, movement, ex.Message, ct);
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
