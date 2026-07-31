namespace Inventory.Entity.Enums;

/// <summary>
/// How the item must be kept. ColdChain is 2–8 °C; breaking it makes the stock
/// unsaleable rather than merely inconvenient, which is why it is on the master
/// and not a free-text note.
/// </summary>
public enum StorageCondition
{
    Ambient = 0,
    CoolDry = 1,
    ColdChain = 2,
    Frozen = 3,
}
