using System.ComponentModel.DataAnnotations;
using Shared.Kernel.Tenancy;

namespace Shared.Kernel.Errors;

/// <summary>
/// What actually went wrong, kept where a user cannot reach it.
///
/// One table per service schema — <c>acc.ErrorLogs</c>, <c>sal.ErrorLogs</c> and
/// so on — mapped from this one class the way <c>NumberingSeries</c> is. Each
/// service owns and migrates its own; nothing reads across a service boundary,
/// so hard rule 8 holds.
///
/// It is an <see cref="OrgScopedEntity"/> like every other table in a tenant
/// schema: CustomerId and OrgId, the global query filter, and an RLS policy that
/// is ENABLEd and FORCEd. That has one consequence worth stating plainly rather
/// than discovering: <b>an error raised before the tenant is known cannot be
/// written here at all</b> — a failed sign-in, a request with no token, a
/// startup fault. Those go to <c>ILogger</c> and nowhere else. It was decided
/// this way deliberately, over an unscoped operations table, so that one
/// customer's failures can never be read while acting as another.
///
/// No endpoint returns rows from this table. The caller is given
/// <see cref="ErrorReference"/> and nothing else.
/// </summary>
public class ErrorLog : OrgScopedEntity
{
    /// <summary>Surrogate key. <c>long</c>, per the ID rule — Guid keys are for User, Customer and Organization only.</summary>
    public long ErrorLogId { get; set; }

    /// <summary>
    /// The reference quoted to the user, and the only part of this row that
    /// ever leaves the server.
    ///
    /// A Guid beside the key rather than as the key: it is generated before the
    /// insert is attempted, so a response can carry it even when writing this
    /// row is itself what failed, and it is safe to show — a sequential id
    /// would tell every customer how many errors every other customer has had.
    /// </summary>
    public Guid ErrorReference { get; set; }

    public DateTimeOffset OccurredAt { get; set; }

    public ErrorSource Source { get; set; }

    /// <summary>The service that raised it — "Sales", "Accounting". Present because one schema is written by an API and by its worker.</summary>
    [Required(ErrorMessage = "The service name is required.")]
    [MaxLength(50, ErrorMessage = "The service name cannot exceed 50 characters.")]
    public string ServiceName { get; set; } = string.Empty;

    /// <summary>The catalogued cause, matching what the caller was told.</summary>
    public ApiErrorCode Code { get; set; }

    /// <summary>The HTTP status the caller received. Null for a worker, which answers nobody.</summary>
    public int? HttpStatus { get; set; }

    [MaxLength(5, ErrorMessage = "A SQLSTATE cannot exceed 5 characters.")]
    public string? SqlState { get; set; }

    // ---- Where it came from ------------------------------------------------

    [MaxLength(10, ErrorMessage = "The request method cannot exceed 10 characters.")]
    public string? RequestMethod { get; set; }

    /// <summary>Path only. The query string is never stored — it carries filter values, and those are the customer's data.</summary>
    [MaxLength(500, ErrorMessage = "The request path cannot exceed 500 characters.")]
    public string? RequestPath { get; set; }

    [MaxLength(100, ErrorMessage = "The trace identifier cannot exceed 100 characters.")]
    public string? TraceId { get; set; }

    /// <summary>The signed-in user, when there was one. Resolved to a name from mst.Users in C#, never joined — Users are in another database.</summary>
    public Guid? UserId { get; set; }

    /// <summary>The worker loop, for <see cref="ErrorSource.Worker"/>. Null otherwise.</summary>
    [MaxLength(100, ErrorMessage = "The worker name cannot exceed 100 characters.")]
    public string? WorkerName { get; set; }

    /// <summary>What the worker was processing — a StockMovementId, a job id — so the follow-up has something to act on.</summary>
    [MaxLength(200, ErrorMessage = "The job reference cannot exceed 200 characters.")]
    public string? JobReference { get; set; }

    // ---- The failure itself ------------------------------------------------
    //
    // Every string is bounded, per the standards. The bounds are generous
    // rather than tight because these are diagnostics, and ErrorLogStore.Trim
    // cuts to them before the insert — an error log that fails with 22001 while
    // recording somebody else's 22001 helps nobody.

    [Required(ErrorMessage = "The exception type is required.")]
    [MaxLength(300, ErrorMessage = "The exception type cannot exceed 300 characters.")]
    public string ExceptionType { get; set; } = string.Empty;

    /// <summary>The database's own words, verbatim. Never sent to a caller outside Development.</summary>
    [Required(ErrorMessage = "The error message is required.")]
    [MaxLength(4000, ErrorMessage = "The error message cannot exceed 4000 characters.")]
    public string Message { get; set; } = string.Empty;

    /// <summary>Postgres DETAIL — the offending key and its values.</summary>
    [MaxLength(4000, ErrorMessage = "The detail cannot exceed 4000 characters.")]
    public string? Detail { get; set; }

    [MaxLength(2000, ErrorMessage = "The hint cannot exceed 2000 characters.")]
    public string? Hint { get; set; }

    /// <summary>The plpgsql call stack, for a failure raised inside a trigger.</summary>
    [MaxLength(4000, ErrorMessage = "The context cannot exceed 4000 characters.")]
    public string? Where { get; set; }

    [MaxLength(100, ErrorMessage = "The routine name cannot exceed 100 characters.")]
    public string? Routine { get; set; }

    [MaxLength(200, ErrorMessage = "The constraint name cannot exceed 200 characters.")]
    public string? ConstraintName { get; set; }

    [MaxLength(100, ErrorMessage = "The schema name cannot exceed 100 characters.")]
    public string? SchemaName { get; set; }

    [MaxLength(200, ErrorMessage = "The table name cannot exceed 200 characters.")]
    public string? TableName { get; set; }

    [MaxLength(200, ErrorMessage = "The column name cannot exceed 200 characters.")]
    public string? ColumnName { get; set; }

    [MaxLength(8000, ErrorMessage = "The stack trace cannot exceed 8000 characters.")]
    public string? StackTrace { get; set; }

    /// <summary>Each inner exception's type and message, newline separated, outermost first.</summary>
    [MaxLength(4000, ErrorMessage = "The inner exception list cannot exceed 4000 characters.")]
    public string? InnerExceptions { get; set; }

    // ---- The task list -----------------------------------------------------

    public ErrorFollowUpStatus FollowUpStatus { get; set; }

    [MaxLength(1000, ErrorMessage = "The follow-up note cannot exceed 1000 characters.")]
    public string? FollowUpNote { get; set; }

    public DateTimeOffset? ResolvedAt { get; set; }

    public Guid? ResolvedBy { get; set; }
}
