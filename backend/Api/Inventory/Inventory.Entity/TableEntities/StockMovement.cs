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

    /// <summary>
    /// The lot this movement is against. Resolved in the request, because a bad
    /// batch number should fail the save rather than surface later as a costing
    /// error nobody is watching.
    /// </summary>
    public long? ItemBatchId { get; set; }

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

    /// <summary>
    /// Cost of one inventory unit for this movement, to 12 decimals. Null when
    /// nothing costed it yet. On a weighted-average stock-out it is the average
    /// in force when the stock went out, restated by the worker when a
    /// date-ordered recalculation of the item says otherwise.
    /// </summary>
    [Range(0, 999999999999.999999, ErrorMessage = "Unit cost cannot be negative.")]
    public decimal? UnitCost { get; set; }

    /// <summary>
    /// <c>Quantity × UnitCost</c> to 2 decimals — the amount posted — held so a
    /// valuation report does not re-multiply. On a weighted-average stock-out it
    /// can sit 0.01 away from that product: the recalculation keeps the item's
    /// total cost of sales exact to the cent, and the line where the rounding
    /// drift first appears absorbs it.
    /// </summary>
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

    // --- Costing state. Costing is asynchronous, so a movement carries where
    // it has got to rather than leaving a reader to guess from a null cost.

    public CostingStatus CostingStatus { get; set; } = CostingStatus.Pending;

    public DateTimeOffset? CostedAt { get; set; }

    /// <summary>Attempts so far. A movement that keeps failing stops rather than retrying forever.</summary>
    public int CostingAttempts { get; set; }

    /// <summary>Why the last attempt failed, kept so the failure can be diagnosed without logs.</summary>
    [MaxLength(500, ErrorMessage = "Costing error cannot exceed 500 characters.")]
    public string? CostingError { get; set; }

    // --- Ledger state. The same shape as costing, one step behind it: a
    // movement cannot be posted until what it cost has been settled.

    public LedgerStatus LedgerStatus { get; set; } = LedgerStatus.Pending;

    public DateTimeOffset? LedgerPostedAt { get; set; }

    /// <summary>Attempts so far. Bounded for the same reason costing's is.</summary>
    public int LedgerAttempts { get; set; }

    /// <summary>
    /// Why the last posting failed. Worth surfacing rather than logging: while
    /// it is set, stock and the general ledger disagree about this movement.
    /// </summary>
    [MaxLength(500, ErrorMessage = "Ledger error cannot exceed 500 characters.")]
    public string? LedgerError { get; set; }

    /// <summary>
    /// The movement owes the ledger nothing, by the document's say-so: goods
    /// leaving on a job-work, approval, branch-transfer or sample challan are
    /// still the branch's own (TK-90). Created
    /// <see cref="LedgerStatus.NotApplicable"/>, and never put back in the ledger
    /// queue by recosting — which a zero-cost movement, also NotApplicable, must
    /// be once its cost is known. The flag is what tells the two apart.
    /// </summary>
    public bool LedgerExempt { get; set; }

    /// <summary>
    /// The issue this movement returns, when it is a return. Without it a
    /// return has no way to find the layers the stock originally left on, and
    /// falls back to the running average — which is what makes buy-sell-return
    /// fail to land stock value back where it started.
    /// </summary>
    public long? ReturnsStockMovementId { get; set; }

    [MaxLength(300, ErrorMessage = "Notes cannot exceed 300 characters.")]
    public string? Notes { get; set; }
}
