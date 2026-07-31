namespace Inventory.Entity.Enums;

/// <summary>
/// Which vertical an item belongs to, and therefore which extension table and
/// which form tab it carries. Stored on the item rather than inferred from the
/// child row, because the form needs it before anything has been saved.
/// </summary>
public enum ItemProfile
{
    Standard = 0,
    Pharma = 1,
    Jewellery = 2,
}
