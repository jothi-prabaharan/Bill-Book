using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;

namespace Shared.Kernel.Persistence;

/// <summary>
/// A transaction that may or may not be this caller's to commit.
///
/// The problem it solves: a service method that opens its own transaction
/// unconditionally throws the moment anything above it has already opened one —
/// "the connection is already in a transaction". Before this existed,
/// <c>AllocationService</c>, <c>JournalService</c>, the three money-document
/// services and <c>StockAdjustmentService</c> all did exactly that, which is why
/// none of them could be called from inside another unit of work.
///
/// A scope either <b>owns</b> the transaction, in which case committing and
/// rolling back do what they say, or it has <b>joined</b> one, in which case
/// both are no-ops and the outermost owner decides the outcome for everybody.
/// Either way the calling code reads the same:
///
/// <code>
/// await using ITransactionScope tx = await _db.Database.BeginScopeAsync(ct);
/// ... work ...
/// await tx.CommitAsync(ct);
/// </code>
///
/// Disposing without committing rolls back, so an early <c>return</c> on a
/// refusal path cannot leave half the work behind.
/// </summary>
public interface ITransactionScope : IAsyncDisposable
{
    /// <summary>
    /// True when this scope opened the transaction and its commit is the real
    /// one. False when it joined a transaction somebody else owns.
    /// </summary>
    bool IsOwner { get; }

    Task CommitAsync(CancellationToken ct);

    Task RollbackAsync(CancellationToken ct);
}

/// <summary>
/// Opens a transaction, or joins the one already open.
/// </summary>
public static class TransactionScopeExtensions
{
    /// <summary>Begin or join at the provider's default isolation level (Read Committed on Postgres).</summary>
    public static Task<ITransactionScope> BeginScopeAsync(
        this DatabaseFacade database,
        CancellationToken ct) =>
        BeginScopeAsync(database, IsolationLevel.Unspecified, ct);

    /// <summary>
    /// Begin or join at <paramref name="isolationLevel"/>.
    ///
    /// When joining, the level asked for must be one the open transaction
    /// already provides. It cannot be raised — Postgres only accepts
    /// <c>SET TRANSACTION ISOLATION LEVEL</c> before the transaction's first
    /// statement, and by the time an inner service runs there is always one.
    /// Silently accepting the weaker level is the one thing not on offer:
    /// <c>AllocationService</c> asks for Serializable because two concurrent
    /// allocations against one invoice would otherwise both see it unallocated
    /// and both succeed. Downgrading that quietly turns a correct guard into a
    /// money bug that only appears under load.
    ///
    /// So this throws instead, and the fix is one attribute on the action:
    /// <c>[Transactional(IsolationLevel.Serializable)]</c>.
    /// </summary>
    public static async Task<ITransactionScope> BeginScopeAsync(
        this DatabaseFacade database,
        IsolationLevel isolationLevel,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(database);

        if (database.CurrentTransaction is IDbContextTransaction ambient)
        {
            GuardIsolation(ambient, isolationLevel);
            return new JoinedTransactionScope();
        }

        IDbContextTransaction transaction = isolationLevel == IsolationLevel.Unspecified
            ? await database.BeginTransactionAsync(ct)
            : await database.BeginTransactionAsync(isolationLevel, ct);

        return new OwnedTransactionScope(transaction);
    }

    private static void GuardIsolation(IDbContextTransaction ambient, IsolationLevel requested)
    {
        if (requested == IsolationLevel.Unspecified)
        {
            return;
        }

        IsolationLevel actual;

        try
        {
            actual = ambient.GetDbTransaction().IsolationLevel;
        }
        catch (InvalidOperationException)
        {
            // A provider that will not surface the level — the in-memory
            // provider in a test, most likely. Nothing to check against.
            return;
        }

        if (Rank(actual) >= Rank(requested))
        {
            return;
        }

        throw new InvalidOperationException(
            $"This work needs {requested} isolation, but it is running inside a "
            + $"{actual} transaction opened further out, and Postgres cannot raise the "
            + "level of a transaction that has already run a statement. Mark the "
            + $"controller action with [Transactional(IsolationLevel.{requested})] so the "
            + "outermost transaction is opened at the level this work requires.");
    }

    /// <summary>
    /// Strength order for the levels Postgres implements. Its Read Uncommitted
    /// behaves as Read Committed and its Repeatable Read is snapshot isolation,
    /// but the ordering below is what matters and it holds for both.
    /// </summary>
    private static int Rank(IsolationLevel level) => level switch
    {
        IsolationLevel.ReadUncommitted => 1,
        IsolationLevel.ReadCommitted => 2,
        IsolationLevel.RepeatableRead => 3,
        IsolationLevel.Snapshot => 3,
        IsolationLevel.Serializable => 4,
        _ => 0,
    };
}

/// <summary>The scope that opened the transaction and decides its outcome.</summary>
internal sealed class OwnedTransactionScope : ITransactionScope
{
    private readonly IDbContextTransaction _transaction;
    private bool _settled;

    public OwnedTransactionScope(IDbContextTransaction transaction) => _transaction = transaction;

    public bool IsOwner => true;

    public async Task CommitAsync(CancellationToken ct)
    {
        if (_settled)
        {
            return;
        }

        await _transaction.CommitAsync(ct);
        _settled = true;
    }

    public async Task RollbackAsync(CancellationToken ct)
    {
        if (_settled)
        {
            return;
        }

        await _transaction.RollbackAsync(ct);
        _settled = true;
    }

    /// <summary>
    /// Rolls back anything not committed. An early return on a refusal path is
    /// the common case and must not leave the work half-applied; EF's own
    /// dispose does roll back, but doing it here keeps the intent visible.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (!_settled)
        {
            try
            {
                await _transaction.RollbackAsync(CancellationToken.None);
            }
            catch (InvalidOperationException)
            {
                // Already finished by the provider — nothing to undo.
            }

            _settled = true;
        }

        await _transaction.DisposeAsync();
    }
}

/// <summary>
/// The scope for work running inside somebody else's transaction. Commit and
/// rollback are deliberately nothing: the owner decides, and an inner commit
/// would publish half a unit of work.
/// </summary>
internal sealed class JoinedTransactionScope : ITransactionScope
{
    public bool IsOwner => false;

    public Task CommitAsync(CancellationToken ct) => Task.CompletedTask;

    public Task RollbackAsync(CancellationToken ct) => Task.CompletedTask;

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
