namespace Reporting.Entity.Enums;

/// <summary>
/// Inventory's <c>ItemType</c>. <c>Inventory</c> and <c>Goods</c> are one value under two names, as in Inventory; either parses. Inventory stores it by name, so these names must stay Inventory's;
/// the numbers are only this copy's (TK-213).
/// </summary>
public enum ItemType
{
    Inventory = 0,
    Goods = 0,
    Service = 1,
    NonStock = 2,
    Group = 3,
}
