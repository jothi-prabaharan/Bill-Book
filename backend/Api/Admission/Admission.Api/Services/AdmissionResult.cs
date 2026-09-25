namespace Admission.Api.Services;

public enum AdmissionOutcome
{
    Ok = 1,
    NotFound = 2,
    Invalid = 3,

    /// <summary>The move between stages is not allowed, or the application is already closed.</summary>
    StageRule = 4,

    /// <summary>Sis or Master could not be asked.</summary>
    Unavailable = 5,
}

/// <param name="Detail">A sentence that never names a table, a column or a figure.</param>
public sealed record AdmissionResult(AdmissionOutcome Outcome, long? Id = null, string? Detail = null, object? Body = null)
{
    public static AdmissionResult Ok(long id, object? body = null) => new(AdmissionOutcome.Ok, id, null, body);

    public static AdmissionResult Fail(AdmissionOutcome outcome, string? detail = null) => new(outcome, null, detail);
}
