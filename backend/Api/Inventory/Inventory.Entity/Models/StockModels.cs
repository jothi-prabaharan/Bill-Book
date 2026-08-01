using System.ComponentModel.DataAnnotations;

namespace Inventory.Entity.Models;

/// <summary>Where an item stands: one quantity, one cost, company-wide.</summary>
public class StockPosition
{
    public long ItemId { get; set; }

    public string ItemCode { get; set; } = null!;

    public string ItemName { get; set; } = null!;

    public decimal QuantityOnHand { get; set; }

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

    public DateTimeOffset? LastMovementAt { get; set; }
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

    public string? Notes { get; set; }

    public DateTimeOffset? RecordedAt { get; set; }
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
}

public sealed record RecordStockMovementResult(
    StockOutcome Outcome, long? StockMovementId, StockPosition? Position);
