using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Shared.Kernel.Errors;

/// <summary>
/// Records a background failure so somebody can act on it.
///
/// Workers are deliberately not wrapped in the request-wide transaction — there
/// is no request, and a costing run that claims a movement, costs it and marks
/// it Costed already owns that transaction itself. What a worker needs instead
/// is the other half: <b>nobody is watching</b>. An API failure produces a
/// response somebody reads and usually retries; a worker failure produces a log
/// line on a server, which is the same as producing nothing.
///
/// So every worker failure is written to the service's own ErrorLogs table with
/// <see cref="ErrorFollowUpStatus.Open"/>, and the set of open rows is the task
/// list. <see cref="ErrorLog.JobReference"/> names what it was working on, so
/// the follow-up has something to act on rather than a timestamp.
/// </summary>
public interface IWorkerErrorAuditor
{
    /// <summary>
    /// Writes the failure and returns its reference, or null when there was
    /// nowhere to write it — a worker tick that failed before it had picked an
    /// organization has no tenant, and the error log is tenant scoped.
    /// </summary>
    Task<Guid?> AuditAsync(
        string workerName,
        string? jobReference,
        Exception exception,
        CancellationToken ct);
}

/// <inheritdoc />
public sealed class WorkerErrorAuditor : IWorkerErrorAuditor
{
    private readonly IErrorLogStore _store;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<WorkerErrorAuditor> _logger;

    public WorkerErrorAuditor(
        IErrorLogStore store,
        IHostEnvironment environment,
        ILogger<WorkerErrorAuditor> logger)
    {
        _store = store;
        _environment = environment;
        _logger = logger;
    }

    public async Task<Guid?> AuditAsync(
        string workerName,
        string? jobReference,
        Exception exception,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(exception);

        TranslatedError translated = DbExceptionTranslator.Translate(exception);
        ApiErrorDiagnostics diagnostics = DbExceptionTranslator.Describe(exception);

        var entry = new ErrorLog
        {
            ErrorReference = Guid.NewGuid(),
            OccurredAt = DateTimeOffset.UtcNow,
            Source = ErrorSource.Worker,
            ServiceName = _environment.ApplicationName,
            Code = translated.Code,
            // A worker answers nobody, so there is no status to record. It is
            // also what tells the two apart when reading the table.
            HttpStatus = null,
            SqlState = diagnostics.SqlState,
            WorkerName = workerName,
            JobReference = jobReference,
            ExceptionType = diagnostics.ExceptionType,
            Message = diagnostics.Message,
            Detail = diagnostics.Detail,
            Hint = diagnostics.Hint,
            Where = diagnostics.Where,
            Routine = diagnostics.Routine,
            ConstraintName = diagnostics.ConstraintName,
            SchemaName = diagnostics.SchemaName,
            TableName = diagnostics.TableName,
            ColumnName = diagnostics.ColumnName,
            StackTrace = diagnostics.StackTrace,
            InnerExceptions = diagnostics.InnerExceptions is { Count: > 0 } inner
                ? string.Join(Environment.NewLine, inner)
                : null,
            // The task list. Every worker failure starts here, including the
            // transient ones — a transient failure that keeps recurring is
            // exactly what somebody should be looking at, and closing it is a
            // decision a person makes, not a default.
            FollowUpStatus = ErrorFollowUpStatus.Open,
        };

        Guid? reference = await _store.RecordAsync(entry, ct);

        _logger.LogError(
            exception,
            "{Worker} failed on {JobReference}: {Code} ({SqlState}). Error reference {Reference}.",
            workerName,
            jobReference ?? "the tick",
            translated.Code,
            diagnostics.SqlState ?? "none",
            reference is Guid id ? id.ToString() : "not recorded");

        return reference;
    }
}
