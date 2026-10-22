namespace Reporting.Entity.Enums;

/// <summary>
/// Purchase's <c>PurchaseFulfilmentStatus</c>: how far a purchase order has been received. Purchase stores it by name, so these names must stay Purchase's;
/// the numbers are only this copy's (TK-213).
/// </summary>
public enum PurchaseFulfilmentStatus
{
    Open = 0,
    PartlyReceived = 1,
    Closed = 2,
    Cancelled = 3,
}
