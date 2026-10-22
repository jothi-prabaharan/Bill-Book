namespace Reporting.Entity.Enums;

/// <summary>
/// Sales' <c>FulfilmentStatus</c>: how far a sales order has been delivered. Sales stores it by name, so these names must stay Sales';
/// the numbers are only this copy's (TK-213).
/// </summary>
public enum FulfilmentStatus
{
    Open = 0,
    PartlyDelivered = 1,
    Closed = 2,
    Cancelled = 3,
}
