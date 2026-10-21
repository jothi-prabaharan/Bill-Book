namespace Master.Entity.Enums;

/// <summary>Where a request to move to a higher plan stands (D-70, TK-209).</summary>
public enum UpgradeRequestStatus
{
    /// <summary>Waiting for a platform operator.</summary>
    Pending = 1,

    /// <summary>The operator moved the customer to the plan asked for.</summary>
    Done = 2,

    /// <summary>The operator closed it without changing the plan.</summary>
    Declined = 3,
}
