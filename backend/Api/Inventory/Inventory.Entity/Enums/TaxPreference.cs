namespace Inventory.Entity.Enums;

/// <summary>
/// How the item is treated for GST. Not the same as its rate — an exempt item
/// and a 0% item both charge nothing but are reported in different places.
/// </summary>
public enum TaxPreference
{
    Taxable = 0,
    Exempt = 1,
    NilRated = 2,
    NonGst = 3,
    /// <summary>Exports and SEZ supplies.</summary>
    ZeroRated = 4,
}
