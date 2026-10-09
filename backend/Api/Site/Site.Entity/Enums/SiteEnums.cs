namespace Site.Entity.Enums;

public enum LaborerType
{
    InternalEmployee = 1,
    Subcontractor = 2,
}

public enum SiteStatus
{
    Planning = 1,
    Active = 2,
    Paused = 3,
    Completed = 4,
    Cancelled = 5,
}

public enum ClockApprovalStatus
{
    Pending = 1,
    Approved = 2,
    Rejected = 3,
}

public enum DispatchStatus
{
    Draft = 1,
    Dispatched = 2,
    ReceivedAtSite = 3,
}

public enum ReturnStatus
{
    Draft = 1,
    Returned = 2,
}

public enum UsageApprovalStatus
{
    Pending = 1,
    Approved = 2,
    Rejected = 3,
}

public enum BillingLineType
{
    Labor = 1,
    Material = 2,
    Retainage = 3,
}
