namespace Accounting.Entity.Enums;

/// <summary>How a project is billed (design "Project accounting", decision 5).</summary>
public enum ProjectBillingMethod
{
    /// <summary>Invoiced by milestone.</summary>
    FixedFee = 1,

    /// <summary>Hours at a rate, plus re-billed expenses.</summary>
    TimeAndMaterials = 2,

    /// <summary>An internal job: cost is tracked, nothing is billed.</summary>
    NonBillable = 3,
}

/// <summary>Where a time-and-materials project's hourly rate comes from.</summary>
public enum ProjectRateBasis
{
    ProjectRate = 1,
    TaskRate = 2,
    UserRate = 3,
}

public enum ProjectStatus
{
    Active = 1,

    /// <summary>Paused. It still accepts postings; nobody has said the job is over.</summary>
    OnHold = 2,

    /// <summary>Finished. It accepts no new postings.</summary>
    Completed = 3,

    /// <summary>Abandoned. It accepts no new postings either.</summary>
    Cancelled = 4,
}

/// <summary>Where a logged hour stands against billing (TK-106).</summary>
public enum TimeBillingStatus
{
    Unbilled = 1,

    /// <summary>Claimed by a posted invoice; <c>InvoiceId</c> names it. A void releases it.</summary>
    Billed = 2,

    /// <summary>Will never be billed. Kept for job cost.</summary>
    WrittenOff = 3,
}
