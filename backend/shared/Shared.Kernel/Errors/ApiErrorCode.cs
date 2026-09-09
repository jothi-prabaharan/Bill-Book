namespace Shared.Kernel.Errors;

/// <summary>
/// What went wrong, in terms a caller can branch on.
///
/// A code rather than a message, because the message is the one thing that
/// changes between environments — the exact database text in Development, a
/// curated sentence everywhere else. A client that switched on the message
/// would work on a developer's machine and nowhere else.
/// </summary>
public enum ApiErrorCode
{
    /// <summary>Nothing in the catalogue matched. Always a 500.</summary>
    Unexpected = 0,

    /// <summary>A unique index or primary key refused a duplicate.</summary>
    DuplicateRecord = 1,

    /// <summary>A foreign key named a row that does not exist.</summary>
    ReferencedRecordMissing = 2,

    /// <summary>A foreign key elsewhere still points at the row being removed.</summary>
    RecordInUse = 3,

    /// <summary>A NOT NULL column was left empty.</summary>
    RequiredValueMissing = 4,

    /// <summary>A CHECK or exclusion constraint refused the value.</summary>
    ValueRejected = 5,

    /// <summary>A value was longer than its column.</summary>
    ValueTooLong = 6,

    /// <summary>A number was outside what its column can hold.</summary>
    ValueOutOfRange = 7,

    /// <summary>A value could not be read as the type its column expects.</summary>
    InvalidFormat = 8,

    /// <summary>Two transactions collided — serialization failure, deadlock, or a lock that would not wait.</summary>
    ConcurrentUpdate = 9,

    /// <summary>The row changed under an optimistic-concurrency check (<c>xmin</c>).</summary>
    ConcurrencyConflict = 10,

    /// <summary>Postgres refused the operation. On this product that is nearly always an RLS policy.</summary>
    PermissionDenied = 11,

    /// <summary>The server is up but cannot take the work — connections, disk, or memory.</summary>
    ServiceBusy = 12,

    /// <summary>The database could not be reached at all.</summary>
    ServiceUnavailable = 13,

    /// <summary>The caller went away, or the statement timed out.</summary>
    RequestCancelled = 14,

    /// <summary>The database does not have the shape the code expects. A deployment fault, not a caller's.</summary>
    SchemaMismatch = 15,

    /// <summary>A trigger or stored procedure raised its own error — the ledger balance and allocation triggers land here.</summary>
    RuleViolation = 16,

    /// <summary>An earlier statement failed and the transaction is no longer usable.</summary>
    TransactionAborted = 17,
}
