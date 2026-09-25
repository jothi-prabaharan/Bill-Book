namespace Preventive.Entity.Enums;

/// <summary>How often a plan comes round; <c>Interval</c> multiplies it.</summary>
public enum Frequency
{
    Daily = 1,
    Weekly = 2,
    Monthly = 3,
    Quarterly = 4,
    HalfYearly = 5,
    Yearly = 6,
}

public enum OccurrenceStatus
{
    /// <summary>Generated; its work order is not raised yet.</summary>
    Scheduled = 1,

    /// <summary>Its work order is raised.</summary>
    Raised = 2,

    Done = 3,

    /// <summary>Not to be done this time; no work order is raised.</summary>
    Skipped = 4,
}
