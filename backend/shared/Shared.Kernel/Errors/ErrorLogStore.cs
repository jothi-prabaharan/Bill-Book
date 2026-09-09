using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Shared.Kernel.Errors;

/// <summary>
/// The error log, written through the service's own DbContext.
///
/// Two things make this awkward and both are handled here rather than by every
/// caller:
///
/// 1. <b>The request's transaction is doomed.</b> Whatever failed, the
///    <c>TransactionFilter</c> is going to roll it back — so a row added to
///    that transaction would roll back with it, and the error log would be
///    empty precisely when something went wrong. Any transaction still open is
///    rolled back here first, and the log row is then written on its own.
///
/// 2. <b>The change tracker still holds the entities that failed.</b>
///    <c>SaveChanges</c> would retry them and fail again, this time from inside
///    the error handler. The tracker is cleared before the row is added; the
///    request is over, so there is nothing left to lose.
///
/// It swallows everything. A handler that throws replaces a precise 409 with an
/// opaque 500, which is strictly worse than not having the row.
/// </summary>
public sealed class ErrorLogStore<TContext> : IErrorLogStore
    where TContext : DbContext
{
    private readonly TContext _db;
    private readonly ILogger<ErrorLogStore<TContext>> _logger;

    public ErrorLogStore(TContext db, ILogger<ErrorLogStore<TContext>> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<Guid?> RecordAsync(ErrorLog entry, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(entry);

        try
        {
            // Its own try: a transaction the caller has already rolled back, or
            // one whose connection has gone, must not cost us the log row. The
            // point of this method is that the record survives whatever
            // happened, and an exception here would take it with it.
            try
            {
                if (_db.Database.CurrentTransaction is not null)
                {
                    await _db.Database.CurrentTransaction.RollbackAsync(ct);
                    await _db.Database.CurrentTransaction.DisposeAsync();
                }
            }
            catch (Exception unwind)
            {
                _logger.LogDebug(
                    unwind,
                    "The transaction was already finished when the error log was written.");
            }

            _db.ChangeTracker.Clear();

            Trim(entry);

            _db.Set<ErrorLog>().Add(entry);
            await _db.SaveChangesAsync(ct);

            return entry.ErrorReference;
        }
        catch (Exception failure)
        {
            // The usual cause is the one named on ErrorLog: no tenant context,
            // so the RLS policy has nothing to check the row against. Logged at
            // warning rather than error — the original failure is being logged
            // at error by the handler that called this, and two errors for one
            // event makes the log harder to read, not easier.
            _logger.LogWarning(
                failure,
                "Could not write error {ErrorLogId} to the error log. "
                + "The original failure is logged separately.",
                entry.ErrorReference);

            return null;
        }
    }

    /// <summary>
    /// Cuts every bounded column to its length. An error log that fails with
    /// 22001 while recording someone else's 22001 helps nobody, and the values
    /// most likely to overflow — a path, a constraint name — are exactly the
    /// ones a malformed request controls.
    /// </summary>
    private static void Trim(ErrorLog entry)
    {
        entry.ServiceName = Cut(entry.ServiceName, 50) ?? string.Empty;
        entry.SqlState = Cut(entry.SqlState, 5);
        entry.RequestMethod = Cut(entry.RequestMethod, 10);
        entry.RequestPath = Cut(entry.RequestPath, 500);
        entry.TraceId = Cut(entry.TraceId, 100);
        entry.WorkerName = Cut(entry.WorkerName, 100);
        entry.JobReference = Cut(entry.JobReference, 200);
        entry.ExceptionType = Cut(entry.ExceptionType, 300) ?? string.Empty;
        entry.Routine = Cut(entry.Routine, 100);
        entry.ConstraintName = Cut(entry.ConstraintName, 200);
        entry.SchemaName = Cut(entry.SchemaName, 100);
        entry.TableName = Cut(entry.TableName, 200);
        entry.ColumnName = Cut(entry.ColumnName, 200);
        entry.Message = Cut(entry.Message, 4000) ?? string.Empty;
        entry.Detail = Cut(entry.Detail, 4000);
        entry.Hint = Cut(entry.Hint, 2000);
        entry.Where = Cut(entry.Where, 4000);
        entry.StackTrace = Cut(entry.StackTrace, 8000);
        entry.InnerExceptions = Cut(entry.InnerExceptions, 4000);
        entry.FollowUpNote = Cut(entry.FollowUpNote, 1000);
    }

    private static string? Cut(string? value, int max) =>
        value is { Length: > 0 } && value.Length > max ? value[..max] : value;
}
