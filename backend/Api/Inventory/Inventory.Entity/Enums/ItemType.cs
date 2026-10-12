namespace Inventory.Entity.Enums;

/// <summary>
/// Inventory: Stocked items that move through stock.
/// Goods: Legacy synonym for Inventory.
/// Service: Services and labor that never move through stock.
/// NonStock: Non-inventory items (consumables, office supplies) that are not tracked in stock.
/// Group: Bundled or grouped items.
/// </summary>
public enum ItemType
{
    Inventory = 0,
    Goods = 0,
    Service = 1,
    NonStock = 2,
    Group = 3,
}

