using Microsoft.Extensions.Logging;

namespace Shared.Kernel.Persistence;

/// <summary>
/// Work a request wants done once its transaction has committed, and only then
/// (TK-92).
///
/// The case it was made for is a call to a remote system that must not happen
/// inside the transaction: registering a posted invoice at the IRP can take
/// seconds, and holding the posting's row locks across it would lock the
/// ledger, while a timeout would roll back a posting the IRP may already have
/// registered. So the posting writes a Pending row in the transaction and asks
/// for the call here. If the transaction rolls back, the work is dropped with
/// it.
///
/// Work queued where no request transaction runs (a worker, a test calling a
/// service directly) never runs from here. Anything queued must therefore
/// also be picked up some other way, as the e-invoice retry worker picks up
/// every Pending row.
/// </summary>
public interface IAfterCommit
{
    void Enqueue(Func<CancellationToken, Task> work);
}

/// <summary>The request's queue. <see cref="TransactionFilter"/> runs it after commit and clears it on rollback.</summary>
public sealed class AfterCommitQueue : IAfterCommit
{
    private readonly List<Func<CancellationToken, Task>> _work = [];

    public int Count => _work.Count;

    public void Enqueue(Func<CancellationToken, Task> work)
    {
        ArgumentNullException.ThrowIfNull(work);
        _work.Add(work);
    }

    public void Clear() => _work.Clear();

    /// <summary>
    /// Runs each piece of work in order. A failure is logged and does not stop
    /// the rest, and never reaches the caller: the transaction has committed,
    /// so the request has succeeded whatever happens here.
    /// </summary>
    public async Task RunAsync(ILogger logger, CancellationToken ct)
    {
        List<Func<CancellationToken, Task>> work = [.. _work];
        _work.Clear();

        foreach (Func<CancellationToken, Task> item in work)
        {
            try
            {
                await item(ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
            {
                logger.LogWarning(ex, "Work queued to run after commit failed; its own retry picks it up.");
            }
        }
    }
}
