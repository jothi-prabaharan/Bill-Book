namespace Reporting.Entity.Enums;

/// <summary>
/// What a customer said to a quote on the portal, as Sales stores it (by name). Recorded, never acted
/// on: turning an accepted quote into an order is still a staff decision.
/// </summary>
public enum QuoteResponse
{
    None = 0,
    Accepted = 1,
    Rejected = 2,
}
