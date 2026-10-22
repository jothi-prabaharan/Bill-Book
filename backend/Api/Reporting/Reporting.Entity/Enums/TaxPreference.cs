namespace Reporting.Entity.Enums;

/// <summary>
/// Inventory's <c>TaxPreference</c>: how GST applies to an item. Inventory stores it by name, so these names must stay Inventory's;
/// the numbers are only this copy's (TK-213).
/// </summary>
public enum TaxPreference
{
    Taxable = 0,
    Exempt = 1,
    NilRated = 2,
    NonGst = 3,
    ZeroRated = 4,
}
