using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shared.Kernel.Interfaces;
using Shared.Kernel.Tenancy;

namespace Shared.Kernel.Errors;

/// <summary>
/// The last thing that runs when a request fails, in every service.
///
/// It does three things, in this order, and the order matters:
///
/// 1. <b>Translates.</b> The exception becomes a catalogued
///    <see cref="SqlErrorRule"/> — a status, a code, and a sentence a user may
///    read. Nothing here inspects the exception message to decide.
/// 2. <b>Records.</b> The unabridged failure goes to the service's ErrorLogs
///    table and to <c>ILogger</c>. The row's id becomes the reference on the
///    response.
/// 3. <b>Answers.</b> In Development the body carries the exact error, SQLSTATE,
///    constraint, DETAIL, the plpgsql WHERE and the stack. In every other
///    environment it carries the curated sentence, the code and the reference,
///    and nothing else — no schema, no figures, no stack.
///
/// The environment check is <see cref="IHostEnvironment.IsDevelopment"/> alone.
/// There is deliberately no configuration key that turns detail on in
/// Production: a setting like that is one deployment mistake away from
/// publishing the schema, and the error reference already gets support to the
/// same information without publishing anything.
/// </summary>
public sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly IHostEnvironment _environment;
    private readonly ILogger<GlobalExceptionHandler> _logger;
    private readonly IServiceProvider _services;

    public GlobalExceptionHandler(
        IHostEnvironment environment,
        ILogger<GlobalExceptionHandler> logger,
        IServiceProvider services)
    {
        _environment = environment;
        _logger = logger;
        _services = services;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        // The caller hung up. There is nobody to answer and nothing worth
        // recording — this is not a fault of the service.
        if (httpContext.RequestAborted.IsCancellationRequested
            && exception is OperationCanceledException)
        {
            return true;
        }

        TranslatedError translated = DbExceptionTranslator.Translate(exception);
        ApiErrorDiagnostics diagnostics = DbExceptionTranslator.Describe(exception);

        string traceId = httpContext.TraceIdentifier;

        _logger.Log(
            translated.StatusCode >= 500 ? LogLevel.Error : LogLevel.Warning,
            exception,
            "{Method} {Path} failed: {Code} ({SqlState}) -> {Status}. Trace {TraceId}.",
            httpContext.Request.Method,
            httpContext.Request.Path.Value,
            translated.Code,
            diagnostics.SqlState ?? "none",
            translated.StatusCode,
            traceId);

        Guid? reference = await RecordAsync(httpContext, translated, diagnostics, traceId, cancellationToken);

        // Nothing has been written to the body yet unless an action started
        // streaming, in which case the status is already on the wire and there
        // is no way to correct it. Say so in the log rather than throwing here.
        if (httpContext.Response.HasStarted)
        {
            _logger.LogWarning(
                "The response for trace {TraceId} had already started; "
                + "the error body could not be written.",
                traceId);

            return true;
        }

        var body = new ApiErrorResponse
        {
            Code = translated.Code.ToString(),
            Message = translated.Message,
            Status = translated.StatusCode,
            ErrorReference = reference,
            TraceId = traceId,
            IsTransient = translated.IsTransient,
            Diagnostics = _environment.IsDevelopment() ? diagnostics : null,
        };

        httpContext.Response.StatusCode = translated.StatusCode;
        await httpContext.Response.WriteAsJsonAsync(body, cancellationToken);

        return true;
    }

    /// <summary>
    /// Writes the row, when this service has somewhere to write it. The Gateway
    /// has no database, so the store is resolved optionally rather than
    /// required — a missing registration there is the correct configuration,
    /// not an oversight.
    /// </summary>
    private async Task<Guid?> RecordAsync(
        HttpContext httpContext,
        TranslatedError translated,
        ApiErrorDiagnostics diagnostics,
        string traceId,
        CancellationToken ct)
    {
        if (_services.GetService(typeof(IErrorLogStore)) is not IErrorLogStore store)
        {
            return null;
        }

        var tenant = _services.GetService(typeof(ITenantContext)) as ITenantContext;
        var user = _services.GetService(typeof(ICurrentUser)) as ICurrentUser;

        var entry = new ErrorLog
        {
            ErrorReference = Guid.NewGuid(),
            OccurredAt = DateTimeOffset.UtcNow,
            Source = ErrorSource.Api,
            ServiceName = _environment.ApplicationName,
            Code = translated.Code,
            HttpStatus = translated.StatusCode,
            SqlState = diagnostics.SqlState,
            RequestMethod = httpContext.Request.Method,
            // Path only. The query string holds filter values, and those are
            // the customer's data, not diagnostics.
            RequestPath = httpContext.Request.Path.Value,
            TraceId = traceId,
            UserId = user?.UserId,
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
            // A user saw this one and can retry it. Only the ones nobody saw —
            // the workers — start on the task list.
            FollowUpStatus = translated.StatusCode >= 500
                ? ErrorFollowUpStatus.Open
                : ErrorFollowUpStatus.NoActionNeeded,
        };

        if (tenant?.CustomerId is Guid customerId && tenant.OrgId is Guid orgId)
        {
            entry.CustomerId = customerId;
            entry.OrgId = orgId;
        }

        return await store.RecordAsync(entry, ct);
    }
}
