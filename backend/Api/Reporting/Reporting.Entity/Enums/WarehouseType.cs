namespace Reporting.Entity.Enums;

/// <summary>
/// Inventory's <c>WarehouseType</c>. Inventory stores it by name, so these names must stay Inventory's;
/// the numbers are only this copy's (TK-213).
/// </summary>
public enum WarehouseType
{
    Warehouse = 0,
    Store = 1,
    Godown = 2,
    Transit = 3,
    Site = 4,
}
