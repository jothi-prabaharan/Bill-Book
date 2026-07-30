namespace Accounting.Entity.Enums;

/// <summary>
/// The GST component a tax sub-account represents. Completes the sub-account key
/// so CGST, SGST and IGST can share one parent control account and one tax rate.
/// None for contact and item sub-accounts.
/// </summary>
public enum TaxComponent
{
    None = 0,
    Cgst = 1,
    Sgst = 2,
    Igst = 3,
}
