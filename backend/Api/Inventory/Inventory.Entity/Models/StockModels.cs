using System.ComponentModel.DataAnnotations;

namespace Inventory.Entity.Models;

/// <summary>Where an item stands: one quantity, one cost, company-wide.</summary>
public class StockPosition
{
    public long ItemId { get; set; }

    public string ItemCode { get; set; } = null!;

    public string ItemName { get; set; } = null!;

    public decimal QuantityOnHand { get; set; }

    /// <summary>Promised to a confirmed order and not yet issued.</summary>
    public decimal QuantityReserved { get; set; }

    /// <summary>
    /// <c>QuantityOnHand - QuantityReserved</c> — what may still be sold.
    /// Computed rather than stored, so it cannot disagree with the two it
    /// comes from.
    /// </summary>
    public decimal QuantityAvailable { get; set; }

    public decimal WeightedAverageCost { get; set; }

    /// <summary><c>QuantityOnHand × WeightedAverageCost</c>, computed rather than stored.</summary>
    public decimal StockValue { get; set; }

    /// <summary>
    /// The item's unit type. Any unit of this type can be entered against it —
    /// that is exactly the set the movement form may offer.
    /// </summary>
    public long UomTypeId { get; set; }

    /// <summary>The unit everything above is in — the item's inventory unit.</summary>
    public long InventoryUomId { get; set; }

    public string InventoryUomCode { get; set; } = null!;

    /// <summary>What stock reports display in, which may differ from the inventory unit.</summary>
    public long ReportUomId { get; set; }

    public string ReportUomCode { get; set; } = null!;

    /// <summary>The same quantity expressed in the report unit, for display only.</summary>
    public decimal QuantityInReportUom { get; set; }

    public decimal? ReorderLevel { get; set; }

    /// <summary>True when on hand has fallen to or below the reorder level.</summary>
    public bool IsBelowReorderLevel { get; set; }

    public string CostingType { get; set; } = null!;

    /// <summary>Drives which fields a movement form has to ask for.</summary>
    public bool IsBatchTracked { get; set; }

    public bool IsExpiryTracked { get; set; }

    public bool IsSerialTracked { get; set; }

    /// <summary>True when the method draws down layers rather than a running average.</summary>
    public bool UsesCostLayers { get; set; }

    /// <summary>
    /// Value from the layers themselves — <c>Σ(remaining × layer cost)</c>. On a
    /// layered item this is the real figure; <see cref="StockValue"/> is the
    /// running average's version of the same thing and the two can differ.
    /// </summary>
    public decimal LayeredStockValue { get; set; }

    public DateTimeOffset? LastMovementAt { get; set; }
}

/// <summary>A cost restated because a receipt arrived dated before the sale.</summary>
public class RecostingAdjustmentItem
{
    public long RecostingAdjustmentId { get; set; }

    /// <summary>Groups every adjustment made by one run.</summary>
    public Guid RecostingBatchId { get; set; }

    public long ItemId { get; set; }

    public string ItemCode { get; set; } = null!;

    public string ItemName { get; set; } = null!;

    /// <summary>The sale whose cost changed.</summary>
    public long StockMovementId { get; set; }

    public DateOnly MovementDate { get; set; }

    /// <summary>The backdated receipt that caused it.</summary>
    public long TriggerStockMovementId { get; set; }

    public decimal PreviousCost { get; set; }

    public decimal NewCost { get; set; }

    /// <summary>Signed — a restatement genuinely runs both ways.</summary>
    public decimal Delta { get; set; }

    public DateTimeOffset RunAt { get; set; }
}

/// <summary>What one issue took, from which layer, at what cost.</summary>
public class CostAllocationItem
{
    public long CostLayerId { get; set; }

    public long StockMovementId { get; set; }

    /// <summary>The receipt that created the layer, so a margin query can name the purchase.</summary>
    public DateOnly ReceivedOn { get; set; }

    public DateOnly? ExpiresOn { get; set; }

    public string? BatchNumber { get; set; }

    public decimal Quantity { get; set; }

    public decimal UnitCost { get; set; }

    public decimal TotalCost { get; set; }
}

public class StockMovementListItem
{
    public long StockMovementId { get; set; }

    public long ItemId { get; set; }

    public string ItemCode { get; set; } = null!;

    public string ItemName { get; set; } = null!;

    public long? WarehouseId { get; set; }

    public string? WarehouseName { get; set; }

    public string MovementType { get; set; } = null!;

    public string Direction { get; set; } = null!;

    public DateOnly MovementDate { get; set; }

    public decimal EnteredQuantity { get; set; }

    public long EnteredUomId { get; set; }

    public string EnteredUomCode { get; set; } = null!;

    public decimal Quantity { get; set; }

    public decimal ConversionFactor { get; set; }

    public decimal? UnitCost { get; set; }

    public decimal? TotalCost { get; set; }

    public decimal? ResultingWeightedAverageCost { get; set; }

    public string? SourceType { get; set; }

    public long? SourceId { get; set; }

    public long? ReturnsStockMovementId { get; set; }

    /// <summary>Pending, InProgress, Costed, Skipped or Failed — costing is asynchronous.</summary>
    public string CostingStatus { get; set; } = null!;

    public DateTimeOffset? CostedAt { get; set; }

    public string? CostingError { get; set; }

    /// <summary>
    /// Pending, InProgress, Posted, NotApplicable or Failed. Posting follows
    /// costing, so a movement is behind on this until its cost has settled.
    /// </summary>
    public string LedgerStatus { get; set; } = null!;

    public DateTimeOffset? LedgerPostedAt { get; set; }

    /// <summary>
    /// Why the posting has not happened. On a <c>NotApplicable</c> movement this
    /// says why there was nothing to post rather than reporting a failure.
    /// </summary>
    public string? LedgerError { get; set; }

    public string? Notes { get; set; }

    public DateTimeOffset? RecordedAt { get; set; }
}

/// <summary>How far behind costing is, and whether anything has given up.</summary>
public class CostingQueueStatus
{
    public int Pending { get; set; }

    public int InProgress { get; set; }

    /// <summary>Movements that failed enough times to stop retrying. These need a person.</summary>
    public int Failed { get; set; }

    /// <summary>The oldest movement still waiting, so "behind" can be read as a duration.</summary>
    public DateTimeOffset? OldestPendingAt { get; set; }

    /// <summary>
    /// Movements costed but not yet posted to the general ledger. Some lag is
    /// normal — the posting runs just behind the costing.
    /// </summary>
    public int LedgerPending { get; set; }

    /// <summary>
    /// Movements that gave up posting. <b>While this is above zero, stock and
    /// the general ledger disagree</b>, which is worth saying out loud rather
    /// than leaving to be discovered in a reconciliation.
    /// </summary>
    public int LedgerFailed { get; set; }
}

/// <summary>
/// One movement to record. The direction is not in here — it comes from the
/// movement type, except for an adjustment, which says so explicitly.
/// </summary>
public class RecordStockMovementRequest
{
    [Range(1, long.MaxValue, ErrorMessage = "Item is required.")]
    public long ItemId { get; set; }

    [Required(ErrorMessage = "Movement type is required.")]
    public string MovementType { get; set; } = null!;

    /// <summary>Only read for an Adjustment, which can run either way. "In" or "Out".</summary>
    public string? Direction { get; set; }

    public DateOnly? MovementDate { get; set; }

    [Range(0.000001, 999999999999.999, ErrorMessage = "Quantity must be greater than zero.")]
    public decimal Quantity { get; set; }

    /// <summary>The unit the quantity is in. Defaults to the item's inventory unit.</summary>
    public long? UomId { get; set; }

    public long? WarehouseId { get; set; }

    /// <summary>Cost per <b>entered</b> unit. Required on anything that brings stock in.</summary>
    [Range(0, 999999999999.999999, ErrorMessage = "Unit cost cannot be negative.")]
    public decimal? UnitCost { get; set; }

    [MaxLength(3, ErrorMessage = "Source type must be 3 characters.")]
    public string? SourceType { get; set; }

    public long? SourceId { get; set; }

    public long SourceLineId { get; set; }

    // --- Batch and serial. Required by the item's own tracking flags, checked
    // in C# because the rule lives on the item rather than on this row.

    /// <summary>An existing lot to move against. Wins over <see cref="BatchNumber"/>.</summary>
    public long? ItemBatchId { get; set; }

    /// <summary>A lot to find or create on the way in. Required when the item is batch-tracked.</summary>
    [MaxLength(50, ErrorMessage = "Batch number cannot exceed 50 characters.")]
    public string? BatchNumber { get; set; }

    /// <summary>Required when the item is expiry-tracked and the lot is new.</summary>
    public DateOnly? BatchExpiryDate { get; set; }

    public DateOnly? BatchManufactureDate { get; set; }

    /// <summary>The MRP printed on this lot, which may differ from the item's.</summary>
    [Range(0, 999999999999.99, ErrorMessage = "MRP cannot be negative.")]
    public decimal? BatchMrp { get; set; }

    /// <summary>
    /// The individual pieces. On the way in they are created; on the way out
    /// they name exactly which pieces left. Count must equal the quantity when
    /// the item is serial-tracked.
    /// </summary>
    public List<string> SerialNumbers { get; set; } = [];

    /// <summary>Per-piece HUIDs, positionally matched to <see cref="SerialNumbers"/>.</summary>
    public List<string> HallmarkNumbers { get; set; } = [];

    /// <summary>
    /// On a return, the issue being returned. Stock then goes back onto the
    /// layers it left from, at the cost it left at.
    /// </summary>
    public long? ReturnsStockMovementId { get; set; }

    [MaxLength(300, ErrorMessage = "Notes cannot exceed 300 characters.")]
    public string? Notes { get; set; }
}

/// <summary>
/// Moving stock between warehouses. Two movements, no net change: the pool is
/// shared, so a transfer is a location fact and nothing else.
/// </summary>
public class TransferStockRequest
{
    [Range(1, long.MaxValue, ErrorMessage = "Item is required.")]
    public long ItemId { get; set; }

    [Range(1, long.MaxValue, ErrorMessage = "Source warehouse is required.")]
    public long FromWarehouseId { get; set; }

    [Range(1, long.MaxValue, ErrorMessage = "Destination warehouse is required.")]
    public long ToWarehouseId { get; set; }

    [Range(0.000001, 999999999999.999, ErrorMessage = "Quantity must be greater than zero.")]
    public decimal Quantity { get; set; }

    public long? UomId { get; set; }

    public DateOnly? MovementDate { get; set; }

    [MaxLength(300, ErrorMessage = "Notes cannot exceed 300 characters.")]
    public string? Notes { get; set; }
}

public enum StockOutcome
{
    Ok = 0,
    ItemNotFound = 1,
    /// <summary>The item does not track inventory — a service has no stock to move.</summary>
    NotStocked = 2,
    UnknownUnit = 3,
    /// <summary>The chosen unit does not belong to the item's unit type, so it cannot convert.</summary>
    UnitTypeMismatch = 4,
    UnknownWarehouse = 5,
    /// <summary>Not enough on hand. The decrement affected no rows and nothing changed.</summary>
    InsufficientStock = 6,
    /// <summary>Bringing stock in without saying what it cost would corrupt weighted average cost.</summary>
    UnitCostRequired = 7,
    /// <summary>A movement for this source document and line already exists.</summary>
    DuplicateSource = 8,
    SameWarehouse = 9,
    InvalidValue = 10,
    /// <summary>The item is batch-tracked and the movement named no batch.</summary>
    BatchRequired = 11,
    /// <summary>The item is expiry-tracked and the new lot has no expiry date.</summary>
    ExpiryRequired = 12,
    BatchNotFound = 13,
    /// <summary>Serial numbers given do not match the quantity, one per unit.</summary>
    SerialCountMismatch = 14,
    /// <summary>A serial named on the way in already exists, or one named on the way out does not.</summary>
    SerialConflict = 15,
    /// <summary>Layered costing could not find enough remaining layers to cover the issue.</summary>
    InsufficientCostLayers = 16,
    /// <summary>The movement being returned does not exist, or is not an issue of this item.</summary>
    ReturnedMovementNotFound = 17,
    /// <summary>Returning more than went out on that issue.</summary>
    ReturnExceedsIssue = 18,

    /// <summary>The quantity is zero or negative — nothing to reserve or release.</summary>
    InvalidQuantity = 19,

    /// <summary>Releasing more than is reserved. Nothing changed.</summary>
    NotReserved = 20,
}

public sealed record RecordStockMovementResult(
    StockOutcome Outcome, long? StockMovementId, StockPosition? Position);

/// <summary>
/// Stock a confirmed order is holding, or giving back.
///
/// <b>Whole-document, not line at a time.</b> A sales order either reserves
/// everything it promised or reserves nothing: reserving four lines and failing
/// on the fifth would leave stock held by an order that was never confirmed, and
/// nothing on any screen saying so. The caller sends the lot and gets the lot
/// back, and a shortage on any line means none of it was taken.
/// </summary>
public class ReserveStockRequest
{
    /// <summary>Which database. In the body because the caller holds no user token.</summary>
    public Guid CustomerId { get; set; }

    public Guid OrgId { get; set; }

    /// <summary>
    /// What the reservation is for — the document type and id, carried so a
    /// refusal can name the order rather than only the item.
    /// </summary>
    [MaxLength(3, ErrorMessage = "Source type must be a 3-letter code.")]
    public string? SourceType { get; set; }

    public long? SourceId { get; set; }

    public List<ReserveStockLine> Lines { get; set; } = [];
}

public class ReserveStockLine
{
    public int LineNumber { get; set; }

    [Range(1, long.MaxValue, ErrorMessage = "An item is required.")]
    public long ItemId { get; set; }

    /// <summary>In the item's own stock unit — the caller converts before sending.</summary>
    [Range(0.000001, 999999999999.999, ErrorMessage = "Quantity must be greater than zero.")]
    public decimal Quantity { get; set; }
}

/// <summary>What was taken, or why it could not be.</summary>
public class ReserveStockResponse
{
    public bool Reserved { get; set; }

    public List<ReserveStockLineResult> Lines { get; set; } = [];
}

public class ReserveStockLineResult
{
    public int LineNumber { get; set; }

    public long ItemId { get; set; }

    public string ItemCode { get; set; } = null!;

    public string ItemName { get; set; } = null!;

    public decimal Requested { get; set; }

    /// <summary>On hand less what is already reserved — what this line could draw on.</summary>
    public decimal Available { get; set; }

    public bool Ok { get; set; }

    public string Outcome { get; set; } = null!;
}
