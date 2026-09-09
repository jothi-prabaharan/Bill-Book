using System.Net;

namespace Shared.Kernel.Errors;

/// <summary>
/// One SQLSTATE, and what the product does about it.
/// </summary>
/// <param name="Code">The code a caller branches on.</param>
/// <param name="StatusCode">The HTTP status this becomes.</param>
/// <param name="Message">
/// The sentence a user is allowed to read. Deliberately free of table names,
/// constraint names, column names and figures — a unique-violation message
/// naming <c>IX_Contacts_OrgId_Gstin</c> tells an attacker the schema, and the
/// ledger balance trigger's own text quotes the branch's total debits and
/// credits, which belong to nobody outside the branch.
/// </param>
/// <param name="IsTransient">
/// Whether retrying the same request unchanged could succeed. Serialization
/// failures and deadlocks can; a duplicate key never will.
/// </param>
public sealed record SqlErrorRule(
    ApiErrorCode Code,
    int StatusCode,
    string Message,
    bool IsTransient = false);

/// <summary>
/// The audit of every SQLSTATE this product can provoke, and the answer it gives
/// for each.
///
/// Postgres SQLSTATEs are five characters: a two-character class and a
/// three-character subclass. Specific codes are matched first, then the class,
/// so an unlisted member of a known class still gets a sane answer rather than
/// a bare 500 — a new integrity constraint added in a migration years from now
/// lands on 23000 and is reported as a rejected value, not as a crash.
///
/// Reference: https://www.postgresql.org/docs/16/errcodes-appendix.html
/// </summary>
public static class SqlErrorCatalog
{
    /// <summary>What the caller is told when nothing matches.</summary>
    public static SqlErrorRule Unexpected { get; } = new(
        ApiErrorCode.Unexpected,
        (int)HttpStatusCode.InternalServerError,
        "Something went wrong while saving. The problem has been recorded and nothing was changed.");

    private static readonly Dictionary<string, SqlErrorRule> ByState = new(StringComparer.Ordinal)
    {
        // ---- Class 23 — integrity constraint violation -------------------
        ["23505"] = new(
            ApiErrorCode.DuplicateRecord,
            (int)HttpStatusCode.Conflict,
            "A record with these details already exists."),

        ["23503"] = new(
            ApiErrorCode.ReferencedRecordMissing,
            (int)HttpStatusCode.Conflict,
            "This refers to a record that does not exist, or is still in use elsewhere and cannot be removed."),

        ["23502"] = new(
            ApiErrorCode.RequiredValueMissing,
            (int)HttpStatusCode.BadRequest,
            "A required value was missing."),

        ["23514"] = new(
            ApiErrorCode.ValueRejected,
            (int)HttpStatusCode.BadRequest,
            "One of the values is not allowed here."),

        ["23P01"] = new(
            ApiErrorCode.ValueRejected,
            (int)HttpStatusCode.Conflict,
            "This overlaps a record that already exists."),

        // ---- Class 22 — data exception -----------------------------------
        ["22001"] = new(
            ApiErrorCode.ValueTooLong,
            (int)HttpStatusCode.BadRequest,
            "One of the values is longer than the field allows."),

        ["22003"] = new(
            ApiErrorCode.ValueOutOfRange,
            (int)HttpStatusCode.BadRequest,
            "A number is larger than the field allows."),

        ["22007"] = new(
            ApiErrorCode.InvalidFormat,
            (int)HttpStatusCode.BadRequest,
            "A date could not be read."),

        ["22008"] = new(
            ApiErrorCode.ValueOutOfRange,
            (int)HttpStatusCode.BadRequest,
            "A date is outside the range the field allows."),

        ["22012"] = new(
            ApiErrorCode.ValueRejected,
            (int)HttpStatusCode.BadRequest,
            "The calculation divided by zero."),

        ["22P02"] = new(
            ApiErrorCode.InvalidFormat,
            (int)HttpStatusCode.BadRequest,
            "One of the values is not in the expected format."),

        // ---- Class 40 / 55 — concurrency ---------------------------------
        ["40001"] = new(
            ApiErrorCode.ConcurrentUpdate,
            (int)HttpStatusCode.Conflict,
            "Someone else changed the same records at the same moment. Nothing was saved — please try again.",
            IsTransient: true),

        ["40P01"] = new(
            ApiErrorCode.ConcurrentUpdate,
            (int)HttpStatusCode.Conflict,
            "Two operations blocked each other. Nothing was saved — please try again.",
            IsTransient: true),

        ["55P03"] = new(
            ApiErrorCode.ConcurrentUpdate,
            (int)HttpStatusCode.Conflict,
            "These records are being changed by someone else. Please try again in a moment.",
            IsTransient: true),

        ["25P02"] = new(
            ApiErrorCode.TransactionAborted,
            (int)HttpStatusCode.InternalServerError,
            "An earlier step failed, so nothing in this operation was saved."),

        // ---- Class 42 / 3F — the code and the database disagree ----------
        // A caller cannot cause these and cannot fix them. They are a
        // deployment fault — a migration that did not run, or a hand-edited
        // database — so the message says nothing about tables or columns.
        ["42501"] = new(
            ApiErrorCode.PermissionDenied,
            (int)HttpStatusCode.Forbidden,
            "You do not have access to these records."),

        ["42P01"] = new(
            ApiErrorCode.SchemaMismatch,
            (int)HttpStatusCode.InternalServerError,
            "This feature is not available on this environment. The problem has been recorded."),

        ["42703"] = new(
            ApiErrorCode.SchemaMismatch,
            (int)HttpStatusCode.InternalServerError,
            "This feature is not available on this environment. The problem has been recorded."),

        ["42883"] = new(
            ApiErrorCode.SchemaMismatch,
            (int)HttpStatusCode.InternalServerError,
            "This feature is not available on this environment. The problem has been recorded."),

        ["3F000"] = new(
            ApiErrorCode.SchemaMismatch,
            (int)HttpStatusCode.InternalServerError,
            "This feature is not available on this environment. The problem has been recorded."),

        // ---- Class 53 — insufficient resources ---------------------------
        ["53100"] = new(
            ApiErrorCode.ServiceBusy,
            (int)HttpStatusCode.ServiceUnavailable,
            "The service is temporarily unable to save. Please try again shortly.",
            IsTransient: true),

        ["53200"] = new(
            ApiErrorCode.ServiceBusy,
            (int)HttpStatusCode.ServiceUnavailable,
            "The service is temporarily unable to save. Please try again shortly.",
            IsTransient: true),

        ["53300"] = new(
            ApiErrorCode.ServiceBusy,
            (int)HttpStatusCode.ServiceUnavailable,
            "The service is busy. Please try again shortly.",
            IsTransient: true),

        // ---- Class 57 — operator intervention ----------------------------
        ["57014"] = new(
            ApiErrorCode.RequestCancelled,
            (int)HttpStatusCode.GatewayTimeout,
            "The operation took too long and was stopped. Nothing was saved.",
            IsTransient: true),

        ["57P01"] = new(
            ApiErrorCode.ServiceUnavailable,
            (int)HttpStatusCode.ServiceUnavailable,
            "The service is restarting. Please try again shortly.",
            IsTransient: true),

        ["57P03"] = new(
            ApiErrorCode.ServiceUnavailable,
            (int)HttpStatusCode.ServiceUnavailable,
            "The service is starting up. Please try again shortly.",
            IsTransient: true),

        // ---- Raised by our own triggers ----------------------------------
        // The deferred ledger-balance trigger and the five allocation triggers
        // in acc all RAISE EXCEPTION without an ERRCODE, which Postgres reports
        // as P0001. Their own text quotes running totals for the whole branch,
        // so it is replaced rather than forwarded.
        ["P0001"] = new(
            ApiErrorCode.RuleViolation,
            (int)HttpStatusCode.Conflict,
            "The change was refused because it would leave the books inconsistent. Nothing was saved."),
    };

    private static readonly Dictionary<string, SqlErrorRule> ByClass = new(StringComparer.Ordinal)
    {
        // Connection exceptions. The database could not be reached, or dropped
        // the connection mid-statement.
        ["08"] = new(
            ApiErrorCode.ServiceUnavailable,
            (int)HttpStatusCode.ServiceUnavailable,
            "The service is temporarily unavailable. Please try again shortly.",
            IsTransient: true),

        ["22"] = new(
            ApiErrorCode.InvalidFormat,
            (int)HttpStatusCode.BadRequest,
            "One of the values could not be saved as given."),

        ["23"] = new(
            ApiErrorCode.ValueRejected,
            (int)HttpStatusCode.Conflict,
            "The change was refused because it conflicts with existing records."),

        ["40"] = new(
            ApiErrorCode.ConcurrentUpdate,
            (int)HttpStatusCode.Conflict,
            "The operation could not complete because of other activity. Please try again.",
            IsTransient: true),

        ["42"] = new(
            ApiErrorCode.SchemaMismatch,
            (int)HttpStatusCode.InternalServerError,
            "This feature is not available on this environment. The problem has been recorded."),

        ["53"] = new(
            ApiErrorCode.ServiceBusy,
            (int)HttpStatusCode.ServiceUnavailable,
            "The service is temporarily unable to save. Please try again shortly.",
            IsTransient: true),

        ["57"] = new(
            ApiErrorCode.ServiceUnavailable,
            (int)HttpStatusCode.ServiceUnavailable,
            "The service is temporarily unavailable. Please try again shortly.",
            IsTransient: true),

        ["58"] = new(
            ApiErrorCode.ServiceUnavailable,
            (int)HttpStatusCode.ServiceUnavailable,
            "The service is temporarily unavailable. Please try again shortly.",
            IsTransient: true),
    };

    /// <summary>
    /// The rule for a SQLSTATE: the exact code if it is catalogued, then its
    /// class, then <see cref="Unexpected"/>.
    /// </summary>
    public static SqlErrorRule ForSqlState(string? sqlState)
    {
        if (string.IsNullOrWhiteSpace(sqlState))
        {
            return Unexpected;
        }

        if (ByState.TryGetValue(sqlState, out SqlErrorRule? exact))
        {
            return exact;
        }

        return sqlState.Length >= 2 && ByClass.TryGetValue(sqlState[..2], out SqlErrorRule? byClass)
            ? byClass
            : Unexpected;
    }

    /// <summary>Every SQLSTATE named explicitly. Read by the tests, so the audit cannot quietly shrink.</summary>
    public static IReadOnlyCollection<string> CataloguedStates => ByState.Keys;

    /// <summary>Every SQLSTATE class with a fallback rule.</summary>
    public static IReadOnlyCollection<string> CataloguedClasses => ByClass.Keys;
}
