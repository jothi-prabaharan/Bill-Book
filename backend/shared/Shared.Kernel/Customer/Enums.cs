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

public enum TicketChannel
{
    Web = 1,
    Email = 2,
    Chat = 3,
    Call = 4,
    Phone = Call
}

public static class TicketChannelRules
{
    public static bool IsCallChannel(TicketChannel channel) => channel is TicketChannel.Call or TicketChannel.Phone;

    public static bool IsChannelAllowed(Shared.Kernel.Tenancy.PlanTier planTier, TicketChannel channel) =>
        !IsCallChannel(channel) || planTier is Shared.Kernel.Tenancy.PlanTier.Pro or Shared.Kernel.Tenancy.PlanTier.Elite;
}
