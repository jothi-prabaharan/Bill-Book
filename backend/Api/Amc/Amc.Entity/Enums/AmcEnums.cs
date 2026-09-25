namespace Amc.Entity.Enums;

public enum BillingFrequency
{
    Upfront = 1,
    Quarterly = 2,
    HalfYearly = 3,
    Annual = 4,
}

/// <summary>Whether the vendor's parts are covered, or only their labour.</summary>
public enum AmcCoverage
{
    Comprehensive = 1,
    NonComprehensive = 2,
}

public enum ContractStatus
{
    /// <summary>Being set up; covers nothing yet and takes no visits.</summary>
    Draft = 1,

    Active = 2,

    /// <summary>Past its end date. Stored so the list can filter on it, and set when the end date passes.</summary>
    Expired = 3,

    /// <summary>Ended early, with a reason.</summary>
    Terminated = 4,
}

public enum VisitKind
{
    Scheduled = 1,
    Breakdown = 2,
}
