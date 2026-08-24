namespace Shared.Kernel.Customer;

public enum LeadStatus
{
    New,
    Contacted,
    Qualified,
    Converted,
    Lost
}

public enum LeadSource
{
    Website,
    Referral,
    WalkIn,
    Other
}

public enum TicketStatus
{
    Open,
    InProgress,
    Resolved,
    Closed
}

public enum TicketPriority
{
    Low,
    Medium,
    High,
    Urgent
}

public enum TicketAuthorType
{
    Contact,
    User
}
