namespace Reporting.Entity.Enums;

/// <summary>
/// Inventory's <c>StockMovementType</c>: why stock moved. Inventory stores it by name, so these names must stay Inventory's;
/// the numbers are only this copy's (TK-213).
/// </summary>
public enum StockMovementType
{
    Opening = 0,
    Receipt = 1,
    Issue = 2,
    Adjustment = 3,
    TransferOut = 4,
    TransferIn = 5,
    SalesReturn = 6,
    PurchaseReturn = 7,
}
