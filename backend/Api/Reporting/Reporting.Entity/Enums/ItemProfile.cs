namespace Reporting.Entity.Enums;

/// <summary>
/// Inventory's <c>ItemProfile</c>: which trade's details an item carries. Inventory stores it by name, so these names must stay Inventory's;
/// the numbers are only this copy's (TK-213).
/// </summary>
public enum ItemProfile
{
    Standard = 0,
    Pharma = 1,
    Jewellery = 2,
    Restaurant = 3,
}
