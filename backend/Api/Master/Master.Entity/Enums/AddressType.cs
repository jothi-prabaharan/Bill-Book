namespace Master.Entity.Enums;

/// <summary>
/// Billing or shipping. Not cosmetic: for goods the place of supply follows the
/// shipping address, and that is what decides CGST+SGST versus IGST.
/// </summary>
public enum AddressType
{
    Billing = 0,
    Shipping = 1,
}
