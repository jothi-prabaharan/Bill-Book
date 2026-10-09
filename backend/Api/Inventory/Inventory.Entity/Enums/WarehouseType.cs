namespace Inventory.Entity.Enums;

public enum WarehouseType
{
    Warehouse = 0,
    Store = 1,
    Godown = 2,
    /// <summary>Goods in movement between two locations.</summary>
    Transit = 3,
    /// <summary>Dedicated stock warehouse for a job site (TK-170).</summary>
    Site = 4,
}
