namespace Inventory.Entity.Enums;

/// <summary>Where one physical piece is in its life. Serials are never deleted.</summary>
public enum SerialStatus
{
    InStock = 0,

    Sold = 1,

    /// <summary>Came back from a customer and is saleable again.</summary>
    Returned = 2,

    /// <summary>Written off — broken, lost, or melted down.</summary>
    Scrapped = 3,
}
