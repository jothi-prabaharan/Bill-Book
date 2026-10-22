using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using Shared.Kernel.Tenancy;

namespace Shared.Kernel.Licensing;

/// <summary>
/// Counts the documents a branch creates in a month and refuses the one that
/// would pass what its plan allows (D-69, TK-210).
///
/// <b>Where.</b> On <c>SaveChanges</c>, for every counted header being inserted:
/// Sales counts quotes, sales orders, invoices (POS included) and credit notes,
/// Purchase counts purchase orders, goods receipts, bills and debit notes. Every
/// document is inserted through <c>SaveChanges</c>, so this covers every path
/// that creates one: the form, a conversion from the document upstream, a till,
/// recurring billing and anything written later, without each of them having to
/// remember. A delivery challan is not counted, and neither is an edit.
///
/// <b>How.</b> One counter row a branch and month, taken with a guarded
/// <c>UPDATE</c> whose row count is the answer: <c>... WHERE count + n &lt;= limit</c>.
/// One row updated, the places are taken; none, the month is full or it has no
/// row yet. It runs inside the document's own transaction, which is the whole
/// point: two requests cannot both take the last place, because the second waits
/// on the first's row lock and then finds it taken, and a document that fails to
/// save rolls its place back with it. With no transaction open the interceptor
/// opens one for the save, so the place and the document stand or fall together.
///
/// <b>The month's first row</b> is made in a scope of its own, on its own
/// connection, and committed at once. Adding it to the document's save would let
/// two first documents of a month race for the unique index and fail one of them,
/// and inserting it from here through the same context would be a nested save.
/// A row made and then not needed is a counter at zero. Nothing is lost.
///
/// <b>A plan with no limit is not counted at all</b>, and neither is a branch
/// whose limits could not be read from Master: failing to reach Master must not
/// stop a branch trading, and Elite has nothing to count towards.
/// </summary>
public sealed class DocumentQuotaInterceptor : SaveChangesInterceptor
{
    private readonly PlanLimitKind _kind;
    private readonly IReadOnlyCollection<Type> _counted;
    private readonly IPlanLimitProvider _limits;
    private readonly ITenantContext _tenant;
    private readonly IServiceScopeFactory _scopes;
    private readonly TimeProvider _clock;
    private readonly ILogger<DocumentQuotaInterceptor> _log;

    // A transaction this interceptor opened for one save, to be finished with it.
    private readonly ConditionalWeakTable<DbContext, IDbContextTransaction> _opened = new();

    public DocumentQuotaInterceptor(
        PlanLimitKind kind,
        IReadOnlyCollection<Type> counted,
        IPlanLimitProvider limits,
        ITenantContext tenant,
        IServiceScopeFactory scopes,
        TimeProvider clock,
        ILogger<DocumentQuotaInterceptor> log)
    {
        _kind = kind;
        _counted = counted;
        _limits = limits;
        _tenant = tenant;
        _scopes = scopes;
        _clock = clock;
        _log = log;
    }

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData, InterceptionResult<int> result)
    {
        // A synchronous save is waited on, not skipped: skipping would let a
        // document past the count. There is no synchronization context to
        // deadlock on in a service or a worker.
        TakeAsync(eventData.Context, CancellationToken.None).GetAwaiter().GetResult();
        return base.SavingChanges(eventData, result);
    }

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        await TakeAsync(eventData.Context, cancellationToken);
        return await base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        FinishAsync(eventData.Context, commit: true, CancellationToken.None).GetAwaiter().GetResult();
        return base.SavedChanges(eventData, result);
    }

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        await FinishAsync(eventData.Context, commit: true, cancellationToken);
        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    public override void SaveChangesFailed(DbContextErrorEventData eventData)
    {
        FinishAsync(eventData.Context, commit: false, CancellationToken.None).GetAwaiter().GetResult();
        base.SaveChangesFailed(eventData);
    }

    public override async Task SaveChangesFailedAsync(
        DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        await FinishAsync(eventData.Context, commit: false, cancellationToken);
        await base.SaveChangesFailedAsync(eventData, cancellationToken);
    }

    public override void SaveChangesCanceled(DbContextEventData eventData)
    {
        FinishAsync(eventData.Context, commit: false, CancellationToken.None).GetAwaiter().GetResult();
        base.SaveChangesCanceled(eventData);
    }

    public override async Task SaveChangesCanceledAsync(
        DbContextEventData eventData, CancellationToken cancellationToken = default)
    {
        await FinishAsync(eventData.Context, commit: false, cancellationToken);
        await base.SaveChangesCanceledAsync(eventData, cancellationToken);
    }

    private async Task TakeAsync(DbContext? db, CancellationToken ct)
    {
        if (db is null)
        {
            return;
        }

        int count = db.ChangeTracker.Entries()
            .Count(e => e.State == EntityState.Added && IsCounted(e));
        if (count == 0)
        {
            return;
        }

        // No branch: the insert is refused by row-level security in any case.
        if (_tenant.CustomerId is not Guid customerId || _tenant.OrgId is not Guid orgId)
        {
            return;
        }

        PlanLimits? plan = await _limits.GetAsync(ct);
        if (plan?.MonthlyFor(_kind) is not int cap)
        {
            return;
        }

        DateOnly period = QuotaPeriod.StartOf(_clock.GetUtcNow());

        if (db.Database.CurrentTransaction is null)
        {
            _opened.Add(db, await db.Database.BeginTransactionAsync(ct));
        }

        DbSet<MonthlyUsage> usage = db.Set<MonthlyUsage>();

        for (int attempt = 0; attempt < 2; attempt++)
        {
            int taken = await usage
                .Where(u => u.PeriodStart == period && u.DocumentCount + count <= cap)
                .ExecuteUpdateAsync(s => s.SetProperty(u => u.DocumentCount, u => u.DocumentCount + count), ct);
            if (taken == 1)
            {
                return;
            }

            int? used = await usage
                .Where(u => u.PeriodStart == period)
                .Select(u => (int?)u.DocumentCount)
                .FirstOrDefaultAsync(ct);
            if (used is int current)
            {
                throw new PlanLimitReachedException(new PlanLimitResponse
                {
                    Message = PlanLimitReachedException.MessageFor(plan.Plan, _kind, cap),
                    Kind = _kind,
                    Plan = plan.Plan,
                    Limit = cap,
                    Used = current,
                });
            }

            if (attempt == 0)
            {
                await CreateRowAsync(db, customerId, orgId, period, ct);
            }
        }

        // The row was made and this transaction still cannot see it: it began
        // on a snapshot older than the row. Bookkeeping never refuses a document.
        _log.LogWarning(
            "The {Kind} counter for {Period} could not be read after it was created; "
            + "{Count} document(s) for organization {OrgId} were not counted.",
            _kind, period, count, orgId);
    }

    private bool IsCounted(EntityEntry entry)
    {
        object entity = entry.Entity;
        return _counted.Any(type => type.IsInstanceOfType(entity));
    }

    /// <summary>
    /// Makes the month's row, empty, in a scope of its own so it commits at once
    /// and a second branch of the race for it simply finds it already there.
    /// </summary>
    private async Task CreateRowAsync(
        DbContext db, Guid customerId, Guid orgId, DateOnly period, CancellationToken ct)
    {
        await using AsyncServiceScope scope = _scopes.CreateAsyncScope();

        TenantContext tenant = scope.ServiceProvider.GetRequiredService<TenantContext>();
        tenant.CustomerId = customerId;
        tenant.OrgId = orgId;
        tenant.CustomerCode = _tenant.CustomerCode;
        tenant.DatabaseName = _tenant.DatabaseName;

        var sibling = (DbContext)scope.ServiceProvider.GetRequiredService(db.GetType());
        sibling.Set<MonthlyUsage>().Add(new MonthlyUsage
        {
            CustomerId = customerId,
            OrgId = orgId,
            PeriodStart = period,
            DocumentCount = 0,
        });

        try
        {
            await sibling.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            // Another request made the month's row first. That is all this wanted.
        }
    }

    private static bool IsUniqueViolation(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Ends the transaction this interceptor opened for the save, if it opened one.</summary>
    private async Task FinishAsync(DbContext? db, bool commit, CancellationToken ct)
    {
        if (db is null || !_opened.TryGetValue(db, out IDbContextTransaction? transaction))
        {
            return;
        }

        _opened.Remove(db);

        try
        {
            if (commit)
            {
                await transaction.CommitAsync(ct);
            }
            else
            {
                await transaction.RollbackAsync(ct);
            }
        }
        finally
        {
            await transaction.DisposeAsync();
        }
    }
}
