namespace Student.Api.Services;

/// <summary>How a sis write ended. The controller turns each into a status and one curated sentence.</summary>
public enum StudentOutcome
{
    Ok = 1,
    NotFound = 2,
    Duplicate = 3,
    Invalid = 4,

    /// <summary>A rule about the school year: closed, overlapping, or the section is another year's.</summary>
    YearRule = 5,

    /// <summary>The section is full.</summary>
    SectionFull = 6,

    /// <summary>A guardian that is not a guardian contact of this branch.</summary>
    GuardianRule = 7,

    /// <summary>The exam is not open for marks, or the move between states is not allowed.</summary>
    ExamState = 8,

    /// <summary>Master could not be asked.</summary>
    Unavailable = 9,
}

/// <param name="Detail">A sentence for the caller that never names a table, a column or a figure.</param>
public sealed record StudentResult(StudentOutcome Outcome, long? Id = null, string? Detail = null)
{
    public static StudentResult Ok(long id) => new(StudentOutcome.Ok, id);

    public static StudentResult Fail(StudentOutcome outcome, string? detail = null) => new(outcome, null, detail);
}
