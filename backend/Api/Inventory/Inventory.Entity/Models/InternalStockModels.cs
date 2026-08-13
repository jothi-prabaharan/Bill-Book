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
