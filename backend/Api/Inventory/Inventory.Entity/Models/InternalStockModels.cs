using System.ComponentModel.DataAnnotations;

namespace Inventory.Entity.Models;

public sealed record ReserveStockRequest
{
    [Required]
    public Guid OrgId { get; init; }

    [Required]
    public Guid CustomerId { get; init; }

    [Required]
    public List<ReserveStockLine> Lines { get; init; } = [];
}

public sealed record ReserveStockLine
{
    [Required]
    public long ItemId { get; init; }

    [Range(0.000001, double.MaxValue, ErrorMessage = "Quantity must be greater than zero.")]
    public decimal Quantity { get; init; }
}

public sealed record ReserveStockResponse
{
    public bool Success { get; set; }
    public List<ReserveStockLineResult> Lines { get; set; } = [];
}

public sealed record ReserveStockLineResult
{
    public long ItemId { get; init; }
    public decimal RequestedQuantity { get; init; }
    public bool Success { get; init; }
    public string Outcome { get; init; } = string.Empty;
}

public sealed record ReleaseStockRequest
{
    [Required]
    public Guid OrgId { get; init; }

    [Required]
    public Guid CustomerId { get; init; }

    [Required]
    public List<ReleaseStockLine> Lines { get; init; } = [];
}

public sealed record ReleaseStockLine
{
    [Required]
    public long ItemId { get; init; }

    [Range(0.000001, double.MaxValue, ErrorMessage = "Quantity must be greater than zero.")]
    public decimal Quantity { get; init; }
}

public sealed record ReleaseStockResponse
{
    public bool Success { get; set; }
    public List<ReleaseStockLineResult> Lines { get; set; } = [];
}

public sealed record ReleaseStockLineResult
{
    public long ItemId { get; init; }
    public decimal RequestedQuantity { get; init; }
    public bool Success { get; init; }
    public string Outcome { get; init; } = string.Empty;
}

public sealed record IssueStockRequest
{
    [Required]
    public Guid OrgId { get; init; }

    [Required]
    public Guid CustomerId { get; init; }

    public DateOnly MovementDate { get; init; }

    [Required]
    public string SourceType { get; init; } = null!;

    public long SourceId { get; init; }

    [Required]
    public List<IssueStockLine> Lines { get; init; } = [];
}

public sealed record IssueStockLine
{
    public long SourceLineId { get; init; }

    [Required]
    public long ItemId { get; init; }

    [Range(0.000001, double.MaxValue, ErrorMessage = "Quantity must be greater than zero.")]
    public decimal Quantity { get; init; }

    public long? WarehouseId { get; init; }

    /// <summary>
    /// If true, the quantity is released from reservation before being issued.
    /// Used when invoicing against a confirmed sales order.
    /// </summary>
    public bool ReleaseReservation { get; init; }
}

public sealed record IssueStockResponse
{
    public bool Success { get; set; }
    
    /// <summary>The total value (COGS) of the issued items.</summary>
    public decimal TotalValue { get; set; }
    
    public List<IssueStockLineResult> Lines { get; set; } = [];
}

public sealed record IssueStockLineResult
{
    public long SourceLineId { get; init; }
    public long ItemId { get; init; }
    public decimal RequestedQuantity { get; init; }
    public bool Success { get; init; }
    public string Outcome { get; init; } = string.Empty;
    public decimal UnitCost { get; init; }
    public decimal LineValue { get; init; }
}

/// <summary>
/// Goods arriving against a purchase document.
///
/// <b>Purchase posts the ledger for these, not Inventory.</b> A movement that
/// carries a source type is refused a posting by <c>StockLedgerMapping</c> on
/// purpose: the other leg of a receipt is Goods Received Not Invoiced or
/// Accounts Payable, and only Purchase knows which vendor and how much of the
/// figure is reclaimable tax. Inventory's job here is the quantity and the cost
/// layer.
///
/// <b>Safe to call twice.</b> A movement is keyed on its source document and
/// line, so a retried post re-presents the same lines and each comes back
/// already recorded rather than doubling the stock.
/// </summary>
public sealed record ReceiveStockRequest
{
    [Required]
    public Guid OrgId { get; init; }

    [Required]
    public Guid CustomerId { get; init; }

    public DateOnly MovementDate { get; init; }

    /// <summary>The document type these came in on — <c>GRN</c>, or <c>BIL</c> when no receipt preceded it.</summary>
    [Required]
    [MaxLength(3, ErrorMessage = "Source type must be a 3-letter code.")]
    public string SourceType { get; init; } = null!;

    public long SourceId { get; init; }

    [Required]
    public List<ReceiveStockLine> Lines { get; init; } = [];
}

public sealed record ReceiveStockLine
{
    public long SourceLineId { get; init; }

    [Required]
    public long ItemId { get; init; }

    /// <summary>
    /// What is being taken into stock. On a goods receipt this is the
    /// <b>accepted</b> quantity, never what the vendor delivered — a rejected
    /// carton is a dispute, not inventory.
    /// </summary>
    [Range(0.000001, double.MaxValue, ErrorMessage = "Quantity must be greater than zero.")]
    public decimal Quantity { get; init; }

    /// <summary>
    /// Cost per entered unit. Required: bringing stock in without saying what it
    /// cost would corrupt the weighted average every later cost of sale reads.
    /// </summary>
    [Range(0, double.MaxValue, ErrorMessage = "Unit cost cannot be negative.")]
    public decimal UnitCost { get; init; }

    public long? WarehouseId { get; init; }

    /// <summary>The unit the quantity was keyed in. Defaults to the item's own.</summary>
    public long? UomId { get; init; }

    // Batch, expiry and serial are captured here, in the request, rather than
    // settled afterwards: they are user input, and a background failure over
    // them would surface long after the person who knew the answer had gone.

    public long? ItemBatchId { get; init; }

    [MaxLength(50, ErrorMessage = "Batch number cannot exceed 50 characters.")]
    public string? BatchNumber { get; init; }

    public DateOnly? BatchExpiryDate { get; init; }

    public DateOnly? BatchManufactureDate { get; init; }
}

public sealed record ReceiveStockResponse
{
    public bool Success { get; set; }

    /// <summary>What the received lines came to. What Purchase debits Inventory by.</summary>
    public decimal TotalValue { get; set; }

    public List<ReceiveStockLineResult> Lines { get; set; } = [];
}

public sealed record ReceiveStockLineResult
{
    public long SourceLineId { get; init; }
    public long ItemId { get; init; }
    public decimal Quantity { get; init; }
    public bool Success { get; init; }

    /// <summary>An earlier attempt at this same post already recorded it. Still success.</summary>
    public bool AlreadyRecorded { get; init; }

    public string Outcome { get; init; } = string.Empty;
    public decimal UnitCost { get; init; }
    public decimal LineValue { get; init; }
}

/// <summary>Goods going back to a vendor. The mirror of <see cref="ReceiveStockRequest"/>.</summary>
public sealed record ReturnStockRequest
{
    [Required]
    public Guid OrgId { get; init; }

    [Required]
    public Guid CustomerId { get; init; }

    public DateOnly MovementDate { get; init; }

    /// <summary>The document sending them back — <c>DBN</c>.</summary>
    [Required]
    [MaxLength(3, ErrorMessage = "Source type must be a 3-letter code.")]
    public string SourceType { get; init; } = null!;

    public long SourceId { get; init; }

    [Required]
    public List<ReturnStockLine> Lines { get; init; } = [];
}

public sealed record ReturnStockLine
{
    public long SourceLineId { get; init; }

    [Required]
    public long ItemId { get; init; }

    [Range(0.000001, double.MaxValue, ErrorMessage = "Quantity must be greater than zero.")]
    public decimal Quantity { get; init; }

    public long? WarehouseId { get; init; }

    public long? UomId { get; init; }

    /// <summary>The lot going back, when the item is batch-tracked.</summary>
    public long? ItemBatchId { get; init; }
}

public sealed record ReturnStockResponse
{
    public bool Success { get; set; }

    /// <summary>What the returned units were carrying. What Purchase credits Inventory by.</summary>
    public decimal TotalValue { get; set; }

    public List<ReturnStockLineResult> Lines { get; set; } = [];
}

public sealed record ReturnStockLineResult
{
    public long SourceLineId { get; init; }
    public long ItemId { get; init; }
    public decimal Quantity { get; init; }
    public bool Success { get; init; }
    public bool AlreadyRecorded { get; init; }
    public string Outcome { get; init; } = string.Empty;
    public decimal UnitCost { get; init; }
    public decimal LineValue { get; init; }
}
