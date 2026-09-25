namespace WorkOrder.Entity.Enums;

// The wrk schema's fixed sets (S6, TK-66), stored by name.

public enum WorkOrderSource
{
    Complaint = 1,
    Preventive = 2,
    Amc = 3,
    Inspection = 4,
}

public enum WorkOrderPriority
{
    Low = 1,
    Medium = 2,
    High = 3,
    Critical = 4,
}

/// <summary>
/// Open → Assigned → InProgress ⇄ OnHold → Completed → Closed, and Cancelled
/// from Open or Assigned. Only an Open work order's header can be edited.
/// </summary>
public enum WorkOrderStatus
{
    Open = 1,
    Assigned = 2,
    InProgress = 3,
    OnHold = 4,
    Completed = 5,
    Closed = 6,
    Cancelled = 7,
}

/// <summary>A move a work order is asked to make.</summary>
public enum WorkOrderAction
{
    Assign = 1,
    Start = 2,
    Hold = 3,
    Resume = 4,
    Complete = 5,
    Close = 6,
    Cancel = 7,
}
