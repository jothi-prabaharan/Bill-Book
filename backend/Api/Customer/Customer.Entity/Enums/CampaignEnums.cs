namespace Customer.Entity.Enums;

public enum ConsentStatus
{
    Subscribed = 1,
    Unsubscribed = 2,
    Bounced = 3,
}

public enum ConsentSource
{
    UnsubscribeLink = 1,
    Staff = 2,
    Import = 3,
    Bounce = 4,
}

public enum CampaignChannel
{
    Email = 1,
}

public enum CampaignStatus
{
    Draft = 1,
    Scheduled = 2,
    Sending = 3,
    Sent = 4,
    Cancelled = 5,
}

public enum RecipientStatus
{
    Queued = 1,
    Sent = 2,
    Failed = 3,
    Suppressed = 4,
}
