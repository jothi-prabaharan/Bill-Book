using System.ComponentModel.DataAnnotations;
using Reporting.Entity.Enums;

namespace Reporting.Entity.Models;

/// <summary>
/// What the Inventory Item Detail report is asked for (TK-244): From and To dates (required, D-67),
/// optionally an item, an item category, a warehouse, and a transaction source.
/// </summary>
public sealed class InventoryItemDetailReportRequest
{
    [Required(ErrorMessage = "From date is required.")]
    public DateOnly? FromDate { get; set; }

    [Required(ErrorMessage = "To date is required.")]
    public DateOnly? ToDate { get; set; }

    /// <summary>Only stock movements for this item. When provided, shows running balance for the item.</summary>
    public long? ItemId { get; set; }

    /// <summary>Only items in this category.</summary>
    public long? ItemCategoryId { get; set; }

    /// <summary>Only stock movements in this warehouse.</summary>
    public long? WarehouseId { get; set; }

    /// <summary>Only stock movements from this source (e.g. Receipt, Issue, Adjustment, Opening, etc.).</summary>
    [MaxLength(50, ErrorMessage = "Source cannot exceed 50 characters.")]
    public string? Source { get; set; }

    [Range(1, 100000, ErrorMessage = "Page number must be 1 or more.")]
    public int PageNumber { get; set; } = 1;

    [Range(1, 100, ErrorMessage = "Page size must be between 1 and 100.")]
    public int PageSize { get; set; } = 25;

    /// <summary>The branch, when named; it must be the token's.</summary>
    public Guid? OrgId { get; set; }
}

/// <summary>One stock movement of an item in the period.</summary>
public sealed class InventoryItemDetailMovementDto
{
    public long StockMovementId { get; set; }

    public DateOnly Date { get; set; }

    /// <summary>The source document number (e.g. INV-001, GRN-002, BIL-003, etc.).</summary>
    [MaxLength(100, ErrorMessage = "Document number cannot exceed 100 characters.")]
    public string? DocumentNo { get; set; }

    /// <summary>User-friendly source / movement description (e.g. Goods Receipt, Invoice, Stock Adjustment, Transfer In, etc.).</summary>
    [MaxLength(100, ErrorMessage = "Source description cannot exceed 100 characters.")]
    public string Source { get; set; } = string.Empty;

    /// <summary>Raw source type code (e.g. GRN, INV, BIL, STA, TRN, OB).</summary>
    [MaxLength(20, ErrorMessage = "Source type cannot exceed 20 characters.")]
    public string? SourceType { get; set; }

    public long? SourceId { get; set; }

    [MaxLength(50, ErrorMessage = "Contact code cannot exceed 50 characters.")]
    public string? ContactCode { get; set; }

    [MaxLength(200, ErrorMessage = "Contact name cannot exceed 200 characters.")]
    public string? ContactName { get; set; }

    [MaxLength(150, ErrorMessage = "Warehouse name cannot exceed 150 characters.")]
    public string? WarehouseName { get; set; }

    public StockDirection Direction { get; set; }

    /// <summary>Positive quantity for incoming stock movements (Direction == In).</summary>
    public decimal? QuantityIn { get; set; }

    /// <summary>Positive quantity for outgoing stock movements (Direction == Out).</summary>
    public decimal? QuantityOut { get; set; }

    /// <summary>Signed quantity: positive for in, negative for out.</summary>
    public decimal QuantitySigned { get; set; }

    /// <summary>Cost per inventory unit.</summary>
    public decimal? UnitCost { get; set; }

    /// <summary>Costed value of this movement (signed: positive for in, negative for out). Blank while costing is pending.</summary>
    public decimal? Value { get; set; }

    /// <summary>Sale price per unit for sales transactions (invoices, sales returns), net of tax.</summary>
    public decimal? UnitSalePrice { get; set; }

    /// <summary>Gross margin per unit on sales (UnitSalePrice - UnitCost).</summary>
    public decimal? Margin { get; set; }

    /// <summary>Margin as percentage of UnitSalePrice.</summary>
    public decimal? MarginPercent { get; set; }

    /// <summary>Running quantity on hand for this item up to this movement.</summary>
    public decimal RunningQuantity { get; set; }

    /// <summary>Running value for this item up to this movement.</summary>
    public decimal RunningValue { get; set; }

    /// <summary>True when this movement has not been costed yet.</summary>
    public bool CostPending { get; set; }

    [MaxLength(500, ErrorMessage = "Notes cannot exceed 500 characters.")]
    public string? Notes { get; set; }
}

/// <summary>An item's stock card: opening balance, all stock movements in the period, and closing balance.</summary>
public sealed class InventoryItemDetailGroupDto
{
    public long ItemId { get; set; }

    [MaxLength(50, ErrorMessage = "Item code cannot exceed 50 characters.")]
    public string ItemCode { get; set; } = string.Empty;

    [MaxLength(200, ErrorMessage = "Item name cannot exceed 200 characters.")]
    public string ItemName { get; set; } = string.Empty;

    [MaxLength(150, ErrorMessage = "Category name cannot exceed 150 characters.")]
    public string? CategoryName { get; set; }

    public CostingType CostingType { get; set; }

    [MaxLength(20, ErrorMessage = "Unit code cannot exceed 20 characters.")]
    public string? UnitCode { get; set; }

    /// <summary>Stock at the start of the From date: movements before From.</summary>
    public decimal OpeningQuantity { get; set; }

    public decimal OpeningValue { get; set; }

    /// <summary>Goods received and other additions in the period.</summary>
    public decimal InQuantity { get; set; }

    public decimal InValue { get; set; }

    /// <summary>Goods issued and other deductions in the period.</summary>
    public decimal OutQuantity { get; set; }

    public decimal OutValue { get; set; }

    /// <summary>OpeningQuantity + InQuantity - OutQuantity.</summary>
    public decimal ClosingQuantity { get; set; }

    /// <summary>OpeningValue + InValue - OutValue.</summary>
    public decimal ClosingValue { get; set; }

    /// <summary>A movement up to the To date has not been costed yet.</summary>
    public bool CostPending { get; set; }

    /// <summary>The chronological movements of this item in the period.</summary>
    public List<InventoryItemDetailMovementDto> Movements { get; set; } = [];
}

/// <summary>The values of every item the filters match added up.</summary>
public sealed class InventoryItemDetailTotalsDto
{
    public decimal OpeningValue { get; set; }

    public decimal InValue { get; set; }

    public decimal OutValue { get; set; }

    public decimal ClosingValue { get; set; }

    public int TotalMovements { get; set; }

    public bool CostPending { get; set; }
}

public sealed class InventoryItemDetailReportResultDto
{
    public DateOnly FromDate { get; set; }

    public DateOnly ToDate { get; set; }

    [MaxLength(3, ErrorMessage = "Currency code cannot exceed 3 characters.")]
    public string BaseCurrencyCode { get; set; } = string.Empty;

    public List<InventoryItemDetailGroupDto> Items { get; set; } = [];

    public int TotalItems { get; set; }

    public int TotalMovements { get; set; }

    public int PageNumber { get; set; }

    public int PageSize { get; set; }

    public InventoryItemDetailTotalsDto Totals { get; set; } = new();
}
