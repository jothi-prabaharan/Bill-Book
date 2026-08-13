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
