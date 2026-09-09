using System.Net;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Shared.Kernel.Errors;

/// <summary>
/// What the caller is told, and what is written to the error log. Both come out
/// of one call so the two can never describe different failures.
/// </summary>
/// <param name="Rule">The catalogued answer for this SQLSTATE.</param>
/// <param name="Postgres">The Postgres exception, when there was one.</param>
public sealed record TranslatedError(SqlErrorRule Rule, PostgresException? Postgres)
{
    public ApiErrorCode Code => Rule.Code;

    public int StatusCode => Rule.StatusCode;

    /// <summary>The sentence a user may read.</summary>
    public string Message => Rule.Message;

    public bool IsTransient => Rule.IsTransient;
}

/// <summary>
/// Turns an exception into the product's answer for it.
///
/// Every database failure in this product arrives in one of four shapes, and
/// three of them hide the useful part one or two levels down:
///
///   DbUpdateException            -> inner PostgresException (SaveChanges)
///   PostgresException            -> raw (ExecuteUpdate, ExecuteDelete, raw SQL)
///   NpgsqlException              -> no SQLSTATE at all; the socket failed
///   DbUpdateConcurrencyException -> no Postgres involvement; xmin disagreed
///
/// Unwrapping is depth-first through <see cref="Exception.InnerException"/>
/// rather than one level, because a failure inside an interceptor or a
/// <c>Task.WhenAll</c> arrives wrapped again.
/// </summary>
public static class DbExceptionTranslator
{
    /// <summary>The answer for any exception, database-related or not.</summary>
    public static TranslatedError Translate(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        // xmin said the row moved under us. Postgres never saw an error — EF
        // compared the row count against what it expected — so there is no
        // SQLSTATE to look up.
        if (Find<DbUpdateConcurrencyException>(exception) is not null)
        {
            return new TranslatedError(
                new SqlErrorRule(
                    ApiErrorCode.ConcurrencyConflict,
                    (int)HttpStatusCode.Conflict,
                    "Someone else changed this record while you were working on it. "
                    + "Reload the page and try again — nothing was saved.",
                    IsTransient: false),
                null);
        }

        if (Find<PostgresException>(exception) is PostgresException postgres)
        {
            return new TranslatedError(SqlErrorCatalog.ForSqlState(postgres.SqlState), postgres);
        }

        // NpgsqlException without a PostgresException inside it means the
        // conversation with the server failed rather than the statement — a
        // refused socket, a dropped connection, an exhausted pool.
        if (Find<NpgsqlException>(exception) is not null)
        {
            return new TranslatedError(
                new SqlErrorRule(
                    ApiErrorCode.ServiceUnavailable,
                    (int)HttpStatusCode.ServiceUnavailable,
                    "The service is temporarily unavailable. Please try again shortly.",
                    IsTransient: true),
                null);
        }

        if (Find<TimeoutException>(exception) is not null)
        {
            return new TranslatedError(
                new SqlErrorRule(
                    ApiErrorCode.RequestCancelled,
                    (int)HttpStatusCode.GatewayTimeout,
                    "The operation took too long and was stopped. Nothing was saved.",
                    IsTransient: true),
                null);
        }

        return new TranslatedError(SqlErrorCatalog.Unexpected, null);
    }

    /// <summary>
    /// The full detail, for Development responses and for the error-log row.
    /// Built from the outermost exception and whichever Postgres exception is
    /// underneath it, so nothing is lost to the wrapping.
    /// </summary>
    public static ApiErrorDiagnostics Describe(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        PostgresException? postgres = Find<PostgresException>(exception);
        DbUpdateException? update = Find<DbUpdateException>(exception);

        return new ApiErrorDiagnostics
        {
            ExceptionType = exception.GetType().FullName ?? exception.GetType().Name,
            // The database's own words when there are any, not EF's wrapper.
            // A SaveChanges failure's outer message is always the same
            // "An error occurred while saving the entity changes", which says
            // nothing — the useful sentence is one level down, and a developer
            // reading a Development response should not have to go looking for
            // it in the chain. The wrapper is still there, in InnerExceptions.
            Message = postgres is not null ? postgres.MessageText : exception.Message,
            SqlState = postgres?.SqlState,
            ConstraintName = postgres?.ConstraintName,
            TableName = postgres?.TableName,
            ColumnName = postgres?.ColumnName,
            SchemaName = postgres?.SchemaName,
            Detail = postgres?.Detail,
            Hint = postgres?.Hint,
            Where = postgres?.Where,
            Routine = postgres?.Routine,
            Entries = update?.Entries.Count > 0
                ? update.Entries
                    .Select(e => $"{e.Entity.GetType().Name} ({e.State})")
                    .Distinct(StringComparer.Ordinal)
                    .ToList()
                : null,
            StackTrace = exception.StackTrace,
            InnerExceptions = Chain(exception).Skip(1).ToList() is { Count: > 0 } inner ? inner : null,
        };
    }

    /// <summary>The first exception of type <typeparamref name="T"/> in the chain, outermost first.</summary>
    private static T? Find<T>(Exception exception) where T : Exception
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is T match)
            {
                return match;
            }

            // AggregateException hides its causes in Flatten rather than in
            // InnerException alone when more than one task faulted.
            if (current is AggregateException aggregate)
            {
                foreach (Exception nested in aggregate.Flatten().InnerExceptions)
                {
                    if (Find<T>(nested) is T found)
                    {
                        return found;
                    }
                }
            }
        }

        return null;
    }

    private static IEnumerable<string> Chain(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            yield return $"{current.GetType().Name}: {current.Message}";
        }
    }
}
