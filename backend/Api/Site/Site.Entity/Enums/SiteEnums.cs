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
    OnHold = 3,
    Completed = 4,
    Closed = 5,
}

public enum AssignmentStatus
{
    Active = 1,
    Ended = 2,
}

public enum MarginScope
{
    Assignment = 1,
    Site = 2,
    Trade = 3,
    Item = 4,
    Branch = 5,
}

public enum MarginAppliesTo
{
    All = 1,
    Labor = 2,
    Overtime = 3,
    Material = 4,
}

public enum MarginType
{
    Percent = 1,
    FlatPerUnit = 2,
}

public enum AttendanceStatus
{
    CheckedIn = 1,
    Submitted = 2,
    Approved = 3,
    Rejected = 4,
}

public enum ClockApprovalStatus
{
    Pending = 1,
    Approved = 2,
    Rejected = 3,
}

public enum SiteStockDocumentType
{
    Dispatch = 1,
    Return = 2,
    Usage = 3,
}

public enum SiteStockDocumentStatus
{
    Draft = 1,
    Submitted = 2,
    Approved = 3,
    Posted = 4,
    Void = 5,
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
