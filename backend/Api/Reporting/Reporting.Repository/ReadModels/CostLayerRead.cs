#pragma warning disable CS8618

using Shared.Kernel.Tenancy;

namespace Reporting.Repository.ReadModels;

/// <summary>
/// One receipt, at the cost it came in at, with however much of it is left.
///
/// This is what makes FIFO, LIFO, FEFO and specific identification mean
/// anything. Weighted average needs none of it — it blends every receipt into a
/// single number and forgets which was which — so an item on weighted average
/// creates layers but never consumes them, and they stand as the receipt history
/// rather than as an allocation pool.
///
/// <see cref="RemainingQuantity"/> is the only mutable column, and it moves the
/// same way stock does: a conditional UPDATE guarded on there being enough,
/// never a read followed by a write. Two sales cannot consume the same last unit
/// of a layer.
/// </summary>
public class CostLayerRead : OrgScopedEntity
{
    public long CostLayerId { get; set; }

    public long ItemId { get; set; }

    /// <summary>The receipt that created it. One layer per inbound movement.</summary>
    public long StockMovementId { get; set; }

    public long? ItemBatchId { get; set; }

    /// <summary>What FIFO orders by ascending, and LIFO descending.</summary>
    public DateOnly ReceivedOn { get; set; }

    /// <summary>
    /// Copied from the batch rather than joined to it, so FEFO can order on one
    /// index over this table alone. A layer with no expiry sorts last, which is
    /// correct: something that never expires should go out after something that
    /// does.
    /// </summary>
    public DateOnly? ExpiresOn { get; set; }

    /// <summary>In the item's inventory unit, like every other quantity.</summary>

    public decimal OriginalQuantity { get; set; }

    /// <summary>Falls to zero as the layer is consumed. Never below it, and never above the original.</summary>

    public decimal RemainingQuantity { get; set; }

    /// <summary>Cost of one inventory unit on this receipt. Fixed for the life of the layer.</summary>

    public decimal UnitCost { get; set; }
}



