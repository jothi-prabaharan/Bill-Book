using System.Data;
using Microsoft.EntityFrameworkCore;

namespace Shared.Kernel.Persistence;

/// <summary>
/// One DbContext's transaction, for the request-wide filter to drive.
///
/// A service registers one of these per DbContext it owns. Master registers two
/// — <c>AdminDbContext</c> and <c>ContactsDbContext</c> are different physical
/// databases, so they are two transactions that both have to be settled, not
/// one distributed one. Nothing here attempts two-phase commit: if the second
/// commit fails after the first succeeded, the first stands. That is a real
/// limit and it is the reason a single operation should not write to both.
/// </summary>
public interface IUnitOfWork
{
    /// <summary>The context's name, for log messages that have to say which one failed.</summary>
    string Name { get; }

    /// <summary>Opens a transaction unless one is already open on this context.</summary>
    Task BeginAsync(IsolationLevel isolationLevel, CancellationToken ct);

    Task CommitAsync(CancellationToken ct);

    Task RollbackAsync(CancellationToken ct);

    Task DisposeAsync();
}

/// <summary>
/// <see cref="IUnitOfWork"/> over a concrete DbContext. Registered scoped, so
/// it sees the same context instance the controllers and services do — which is
/// the whole point: the transaction it opens is the one their
/// <c>SaveChanges</c>, <c>ExecuteUpdate</c> and <c>ExecuteDelete</c> calls join.
/// </summary>
public sealed class DbContextUnitOfWork<TContext> : IUnitOfWork
    where TContext : DbContext
{
    private readonly TContext _db;
    private ITransactionScope? _scope;

    public DbContextUnitOfWork(TContext db) => _db = db;

    public string Name => typeof(TContext).Name;

    public async Task BeginAsync(IsolationLevel isolationLevel, CancellationToken ct)
    {
        if (_scope is not null)
        {
            return;
        }

        _scope = await _db.Database.BeginScopeAsync(isolationLevel, ct);
    }

    public async Task CommitAsync(CancellationToken ct)
    {
        if (_scope is not null)
        {
            await _scope.CommitAsync(ct);
        }
    }

    public async Task RollbackAsync(CancellationToken ct)
    {
        if (_scope is not null)
        {
            await _scope.RollbackAsync(ct);
        }
    }

    public async Task DisposeAsync()
    {
        if (_scope is not null)
        {
            await _scope.DisposeAsync();
            _scope = null;
        }
    }
}
