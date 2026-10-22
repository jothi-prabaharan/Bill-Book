namespace Reporting.Entity.Enums;

/// <summary>
/// Inventory's <c>SerialStatus</c>: where a serial number stands. Inventory stores it by name, so these names must stay Inventory's;
/// the numbers are only this copy's (TK-213).
/// </summary>
public enum SerialStatus
{
    InStock = 0,
    Sold = 1,
    Returned = 2,
    Scrapped = 3,
}
