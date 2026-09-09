namespace Shared.Kernel.Errors;

/// <summary>
/// The body of every failed request, in every service.
///
/// Two halves with different audiences. <see cref="Code"/>, <see cref="Message"/>
/// and <see cref="ErrorReference"/> are for the caller and are safe to show a
/// user. <see cref="Diagnostics"/> is for whoever is fixing it and is populated
/// in Development only — see <c>GlobalExceptionHandler</c>.
/// </summary>
public sealed class ApiErrorResponse
{
    /// <summary>The machine-readable cause. Stable across environments; branch on this, never on the message.</summary>
    public string Code { get; set; } = nameof(ApiErrorCode.Unexpected);

    /// <summary>The sentence a user may read. Identical in every environment.</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>The HTTP status, repeated in the body so a client that only reads JSON still has it.</summary>
    public int Status { get; set; }

    /// <summary>
    /// The row in the service's <c>ErrorLogs</c> table holding what actually
    /// happened. This is the whole point of the split: the user is given a
    /// reference, support looks the reference up, and nobody has to paste a
    /// stack trace into a chat window to get help.
    ///
    /// Null when the error could not be recorded — see
    /// <c>IErrorLogStore</c> for the one case where that happens.
    /// </summary>
    public Guid? ErrorReference { get; set; }

    /// <summary>Correlates this response with the service's logs.</summary>
    public string? TraceId { get; set; }

    /// <summary>Whether retrying the identical request could succeed.</summary>
    public bool IsTransient { get; set; }

    /// <summary>
    /// The exact error, Development only. Null in every other environment —
    /// and null is what the property is, not an empty object, so a client
    /// cannot tell the difference between "no detail" and "detail withheld".
    /// </summary>
    public ApiErrorDiagnostics? Diagnostics { get; set; }
}

/// <summary>
/// The unabridged failure. Never serialized outside Development.
/// </summary>
public sealed class ApiErrorDiagnostics
{
    public string ExceptionType { get; set; } = string.Empty;

    /// <summary>The database's own words, verbatim.</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>Five-character SQLSTATE, when the failure came from Postgres.</summary>
    public string? SqlState { get; set; }

    public string? ConstraintName { get; set; }

    public string? TableName { get; set; }

    public string? ColumnName { get; set; }

    public string? SchemaName { get; set; }

    /// <summary>Postgres <c>DETAIL</c> — the offending key and its values.</summary>
    public string? Detail { get; set; }

    /// <summary>Postgres <c>HINT</c>.</summary>
    public string? Hint { get; set; }

    /// <summary>The plpgsql call stack, for a failure raised inside a trigger.</summary>
    public string? Where { get; set; }

    /// <summary>The C function that raised it, useful for telling two identical messages apart.</summary>
    public string? Routine { get; set; }

    /// <summary>The entity types EF was writing when <c>SaveChanges</c> failed.</summary>
    public IReadOnlyList<string>? Entries { get; set; }

    public string? StackTrace { get; set; }

    /// <summary>Each inner exception's type and message, outermost first.</summary>
    public IReadOnlyList<string>? InnerExceptions { get; set; }
}
