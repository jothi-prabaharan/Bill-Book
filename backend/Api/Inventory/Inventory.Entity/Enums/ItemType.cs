namespace Inventory.Entity.Enums;

/// <summary>Goods move through stock; services never do.</summary>
public enum ItemType
{
    Goods = 0,
    Service = 1,
}
