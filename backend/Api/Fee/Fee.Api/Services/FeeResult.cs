namespace Fee.Api.Services;

public enum FeeOutcome
{
    Ok = 1,
    NotFound = 2,
    Invalid = 3,
    Duplicate = 4,

    /// <summary>The document is not in a state that allows it: posted, void, or already paid against.</summary>
    StateRule = 5,

    /// <summary>Student, Master or Accounting could not be reached, or the base currency could not be read.</summary>
    Unavailable = 6,

    /// <summary>Accounting refused the posting. The detail goes to the log, not to the caller.</summary>
    LedgerRefused = 7,
}

/// <param name="Detail">A sentence for the caller that never names a table, a column or a figure.</param>
public sealed record FeeResult(FeeOutcome Outcome, long? Id = null, string? Detail = null, object? Body = null)
{
    public static FeeResult Ok(long id, object? body = null) => new(FeeOutcome.Ok, id, null, body);

    public static FeeResult Fail(FeeOutcome outcome, string? detail = null) => new(outcome, null, detail);
}
