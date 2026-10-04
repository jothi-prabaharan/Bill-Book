namespace Customer.Entity.Enums;

/// <summary>Where a service plan stands (TK-140).</summary>
public enum ServicePlanStatus
{
    Active = 1,
    Paused = 2,
    Ended = 3,
}

/// <summary>How often a service falls due (TK-140).</summary>
public enum ServicePlanInterval
{
    Daily = 1,
    Weekly = 2,
    Monthly = 3,
    Yearly = 4,
}
