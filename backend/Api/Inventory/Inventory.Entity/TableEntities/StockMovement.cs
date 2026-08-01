using System.ComponentModel.DataAnnotations;
using Inventory.Entity.Enums;
using Shared.Kernel.Tenancy;

namespace Inventory.Entity.TableEntities;

/// <summary>
/// One stock movement. The append-only history behind <see cref="ItemStock"/>,
/// and the reason an item's unit type and costing method freeze once anything
/// has moved.
///
/// Every row carries the quantity <b>twice</b>: as the user entered it, in the
/// unit they entered it in, and again converted into the item's inventory unit.
/// The conversion factor is snapshotted alongside. Without that, a receipt of
/// two 50 kg bags and an issue of 300 grams are two numbers nobody can add; with
/// it they are 100 kg in and 0.3 kg out of one figure — and re-deriving the
/// factor later from the unit master would silently restate history if a factor
/// were ever corrected.
///
/// Rows are never edited or deleted. A mistake is corrected by a movement in the
/// opposite direction, the same way a posted journal is reversed rather than
/// changed.
/// </summary>
public class StockMovement : OrgScopedEntity
{
    public long StockMovementId { get; set; }

    public long ItemId { get; set; }

    /// <summary>Where it happened. A location dimension — it never partitions the pool.</summary>
    public long? WarehouseId { get; set; }

    public StockMovementType MovementType { get; set; }

    public StockDirection Direction { get; set; }

    /// <summary>The date the stock actually moved, which is not always today.</summary>
    public DateOnly MovementDate { get; set; }

    // --- The quantity, twice.

    /// <summary>As typed, in <see cref="EnteredUomId"/>. Always positive; direction carries the sign.</summary>
    [Range(0.000001, 999999999999.999, ErrorMessage = "Entered quantity must be greater than zero.")]
    public decimal EnteredQuantity { get; set; }

    /// <summary>The unit the user worked in. Must belong to the item's unit type.</summary>
    public long EnteredUomId { get; set; }

    /// <summary>
    /// <c>EnteredQuantity × ConversionFactor</c>, in the item's inventory unit.
    /// This is the only column stock arithmetic ever touches.
    /// </summary>
    [Range(0.000001, 999999999999.999, ErrorMessage = "Quantity must be greater than zero.")]
    public decimal Quantity { get; set; }

    /// <summary>
    /// Snapshot of <c>enteredUom.ConversionToBase ÷ inventoryUom.ConversionToBase</c>
    /// at the moment of the movement. Stored, not recomputed, so a later
    /// correction to a unit factor cannot restate quantities already recorded.
    /// </summary>
    [Range(0.000001, 999999999999.0, ErrorMessage = "Conversion factor must be greater than zero.")]
    public decimal ConversionFactor { get; set; } = 1m;

    // --- Cost. Set on the way in; on the way out it is what the costing engine decided.

    /// <summary>Cost of one inventory unit for this movement. Null when nothing costed it yet.</summary>
    [Range(0, 999999999999.999999, ErrorMessage = "Unit cost cannot be negative.")]
    public decimal? UnitCost { get; set; }

    /// <summary><c>Quantity × UnitCost</c>, held so a valuation report does not re-multiply.</summary>
    [Range(0, 999999999999.99, ErrorMessage = "Total cost cannot be negative.")]
    public decimal? TotalCost { get; set; }

    /// <summary>The weighted average cost immediately after this movement — the audit trail for a WAC dispute.</summary>
    [Range(0, 999999999999.999999, ErrorMessage = "Resulting cost cannot be negative.")]
    public decimal? ResultingWeightedAverageCost { get; set; }

    // --- Provenance. Together these are the idempotency key: Service Bus is
    // at-least-once, and a redelivered event must not move stock twice.

    /// <summary>The document type that caused this, matching mst.TransactionTypes ("INV", "BIL").</summary>
    [MaxLength(3, ErrorMessage = "Source type must be 3 characters.")]
    public string? SourceType { get; set; }

    /// <summary>The source document header id.</summary>
    public long? SourceId { get; set; }

    /// <summary>The source document line. Zero when the movement is not line-level.</summary>
    public long SourceLineId { get; set; }

    [MaxLength(300, ErrorMessage = "Notes cannot exceed 300 characters.")]
    public string? Notes { get; set; }
}
