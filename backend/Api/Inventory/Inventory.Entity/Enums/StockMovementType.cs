namespace Inventory.Entity.Enums;

/// <summary>
/// Why stock moved. The direction is stored alongside rather than inferred from
/// this, because an adjustment goes either way and a reader should not have to
/// know the rule to sum a column.
/// </summary>
public enum StockMovementType
{
    /// <summary>Migration and go-live. Sets the starting quantity and cost.</summary>
    Opening = 0,

    /// <summary>Goods in against a purchase. The only movement that changes weighted average cost.</summary>
    Receipt = 1,

    /// <summary>Goods out against a sale. Quantity moves; cost per unit does not.</summary>
    Issue = 2,

    /// <summary>A count correction, breakage, or a write-off. In or out.</summary>
    Adjustment = 3,

    /// <summary>Out of one warehouse. Paired with a TransferIn — the pool is unchanged.</summary>
    TransferOut = 4,

    /// <summary>Into another warehouse. Paired with a TransferOut.</summary>
    TransferIn = 5,

    /// <summary>A customer return coming back in.</summary>
    SalesReturn = 6,

    /// <summary>Stock going back to a vendor.</summary>
    PurchaseReturn = 7,
}
