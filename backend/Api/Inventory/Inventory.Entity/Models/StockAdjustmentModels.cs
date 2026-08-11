using System.ComponentModel.DataAnnotations;
using Inventory.Entity.Enums;

namespace Inventory.Entity.Models;

/// <summary>One row of the adjustment list.</summary>
public class StockAdjustmentListItem
{
    public long StockAdjustmentId { get; set; }

    public string? AdjustmentNo { get; set; }

    public DateOnly AdjustmentDate { get; set; }

    public string Kind { get; set; } = null!;

    public string Reason { get; set; } = null!;

    public string Status { get; set; } = null!;

    public long? WarehouseId { get; set; }

    public string? WarehouseName { get; set; }

    public int LineCount { get; set; }

    /// <summary>
    /// What the sheet is worth: the cost of stock added less the cost of stock
    /// written off. Null while any line is still uncosted, because a partial
    /// figure reads like a complete one.
    /// </summary>
    public decimal? NetValue { get; set; }

    public string? Notes { get; set; }

    public long? ReversedByStockAdjustmentId { get; set; }

    public long? ReversesStockAdjustmentId { get; set; }

    public DateTimeOffset? PostedAt { get; set; }
}

/// <summary>The sheet and its lines.</summary>
public class StockAdjustmentDetail : StockAdjustmentListItem
{
    public List<StockAdjustmentLineDetail> Lines { get; set; } = [];
}

public class StockAdjustmentLineDetail
{
    public long StockAdjustmentLineId { get; set; }

    public int LineNumber { get; set; }

    public long ItemId { get; set; }

    public string ItemCode { get; set; } = null!;

    public string ItemName { get; set; } = null!;

    public long? UomId { get; set; }

    public string? UomCode { get; set; }

    /// <summary>The item's own stock unit, which is what the quantity converts into.</summary>
    public string InventoryUomCode { get; set; } = null!;

    public decimal? SystemQuantity { get; set; }

    public decimal? CountedQuantity { get; set; }

    public decimal Quantity { get; set; }

    public string Direction { get; set; } = null!;

    public decimal? UnitCost { get; set; }

    public string? BatchNumber { get; set; }

    public DateOnly? BatchExpiryDate { get; set; }

    public long? StockMovementId { get; set; }

    /// <summary>What the movement was costed at. Only once the engine has settled it.</summary>
    public decimal? MovementTotalCost { get; set; }

    public string? Notes { get; set; }
}

/// <summary>
/// A sheet as saved. Only a draft can be saved — a posted sheet has already
/// moved stock, and the movements behind it are append-only.
/// </summary>
public class SaveStockAdjustmentRequest
{
    public DateOnly? AdjustmentDate { get; set; }

    [Required(ErrorMessage = "Adjustment kind is required.")]
    public string Kind { get; set; } = null!;

    [Required(ErrorMessage = "A reason is required.")]
    public string Reason { get; set; } = null!;

    public long? WarehouseId { get; set; }

    [MaxLength(500, ErrorMessage = "Notes cannot exceed 500 characters.")]
    public string? Notes { get; set; }

    public List<SaveStockAdjustmentLineRequest> Lines { get; set; } = [];
}

public class SaveStockAdjustmentLineRequest
{
    [Range(1, long.MaxValue, ErrorMessage = "Choose an item.")]
    public long ItemId { get; set; }

    public long? UomId { get; set; }

    /// <summary>
    /// On a physical count, the only quantity keyed — the difference is worked
    /// out from what the system holds. Ignored on an adjustment.
    /// </summary>
    [Range(0, 999999999999.999, ErrorMessage = "Counted quantity cannot be negative.")]
    public decimal? CountedQuantity { get; set; }

    /// <summary>
    /// On an adjustment, how much to move. Ignored on a physical count, which
    /// derives it.
    /// </summary>
    [Range(0, 999999999999.999, ErrorMessage = "Quantity cannot be negative.")]
    public decimal? Quantity { get; set; }

    /// <summary>"In" or "Out". Only read on an adjustment.</summary>
    public string? Direction { get; set; }

    [Range(0, 999999999999.999999, ErrorMessage = "Unit cost cannot be negative.")]
    public decimal? UnitCost { get; set; }

    [MaxLength(50, ErrorMessage = "Batch number cannot exceed 50 characters.")]
    public string? BatchNumber { get; set; }

    public DateOnly? BatchExpiryDate { get; set; }

    [MaxLength(300, ErrorMessage = "Line notes cannot exceed 300 characters.")]
    public string? Notes { get; set; }
}

public class ReverseStockAdjustmentRequest
{
    [MaxLength(500, ErrorMessage = "Notes cannot exceed 500 characters.")]
    public string? Notes { get; set; }
}

public enum StockAdjustmentOutcome
{
    Ok = 0,
    NotFound = 1,

    /// <summary>Editing or deleting something already posted. Stock has moved.</summary>
    NotDraft = 2,

    /// <summary>Posting or reversing something that is not posted.</summary>
    NotPosted = 3,

    /// <summary>A sheet with no lines moves nothing and is not a document.</summary>
    NoLines = 4,

    /// <summary>The kind, reason or direction was not one of the known values.</summary>
    InvalidValue = 5,

    /// <summary>A count line whose counted quantity equals what is on hand moves nothing.</summary>
    NoDifference = 6,

    /// <summary>A line was refused by the stock rules — the detail says which.</summary>
    LineRefused = 7,

    /// <summary>Already reversed once. A second mirror would double the correction.</summary>
    AlreadyReversed = 8,

    /// <summary>The STA series is missing, so no number could be taken.</summary>
    SeriesMissing = 9,
}

public sealed record StockAdjustmentResult(
    StockAdjustmentOutcome Outcome,
    long? StockAdjustmentId = null,
    string? Detail = null);
