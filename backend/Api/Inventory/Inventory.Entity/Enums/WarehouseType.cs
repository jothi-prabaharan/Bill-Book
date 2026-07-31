namespace Inventory.Entity.Enums;

public enum WarehouseType
{
    Warehouse = 0,
    Store = 1,
    Godown = 2,
    /// <summary>Goods in movement between two locations.</summary>
    Transit = 3,
}
