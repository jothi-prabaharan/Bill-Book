using System.ComponentModel.DataAnnotations;

namespace Sales.Entity.Models;

/// <summary>One row of the sales order list.</summary>
public class SalesOrderListItem
{
    public long SalesOrderId { get; set; }

    public string DocumentNo { get; set; } = null!;

    public DateOnly DocumentDate { get; set; }

    public DateOnly? DeliveryDate { get; set; }

    public long ContactId { get; set; }

    public string? ContactName { get; set; }

    public string Status { get; set; } = null!;

    public string FulfilmentStatus { get; set; } = null!;

    public decimal TotalAmount { get; set; }

    public string CurrencyCode { get; set; } = null!;

    /// <summary>The quote it came from, when it came from one.</summary>
    public long? QuoteId { get; set; }

    public int LineCount { get; set; }

    /// <summary>How much stock this order is currently holding, across its lines.</summary>
    public decimal ReservedQuantity { get; set; }
}

public class SalesOrderDetailModel : SalesOrderListItem
{
    public string? ContactGstin { get; set; }

    public string? BillingAddress { get; set; }

    public string? ShippingAddress { get; set; }

    public int PlaceOfSupplyStateId { get; set; }

    public bool IsInterState { get; set; }

    public decimal ExchangeRate { get; set; }

    public decimal SubTotal { get; set; }

    public decimal DiscountAmount { get; set; }

    public decimal TaxableAmount { get; set; }

    public decimal CgstAmount { get; set; }

    public decimal SgstAmount { get; set; }

    public decimal IgstAmount { get; set; }

    public decimal CessAmount { get; set; }

    public decimal RoundOffAmount { get; set; }

    public string? Notes { get; set; }

    public string? TermsAndConditions { get; set; }

    public string? VoidReason { get; set; }

    public List<SalesOrderLineModel> Lines { get; set; } = [];
}

public class SalesOrderLineModel
{
    public long SalesOrderDetailId { get; set; }

    public int LineNumber { get; set; }

    public long? ItemId { get; set; }

    public string? ItemCode { get; set; }

    public string? ItemName { get; set; }

    public string? Description { get; set; }

    public string? HsnSacCode { get; set; }

    public long? WarehouseId { get; set; }

    public decimal Quantity { get; set; }

    public long? UomId { get; set; }

    public decimal UnitPrice { get; set; }

    public decimal DiscountAmount { get; set; }

    public decimal TaxableAmount { get; set; }

    public decimal TaxAmount { get; set; }

    public decimal LineTotal { get; set; }

    /// <summary>What this line is holding. Zero until the order is confirmed.</summary>
    public decimal ReservedQuantity { get; set; }

    public decimal DeliveredQuantity { get; set; }

    public string? LineNotes { get; set; }
}

/// <summary>
/// A sales order as created. Only a draft can be written — a confirmed order is
/// holding stock, and editing what it promised without adjusting what it holds
/// would put the two out of step.
/// </summary>
public class SaveSalesOrderRequest
{
    [Range(1, long.MaxValue, ErrorMessage = "Choose the customer this order is for.")]
    public long ContactId { get; set; }

    public DateOnly? DocumentDate { get; set; }

    public DateOnly? DeliveryDate { get; set; }

    /// <summary>Set when the order is being raised from a quote.</summary>
    public long? QuoteId { get; set; }

    [MaxLength(3, ErrorMessage = "Currency code must be a 3-letter code.")]
    public string? CurrencyCode { get; set; }

    [Range(typeof(decimal), "0.00000001", "79228162514264337593543950335",
        ErrorMessage = "Exchange rate must be greater than zero.")]
    public decimal? ExchangeRate { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "A place of supply is required to work out the tax.")]
    public int PlaceOfSupplyStateId { get; set; }

    [MaxLength(1000, ErrorMessage = "Notes cannot exceed 1000 characters.")]
    public string? Notes { get; set; }

    [MaxLength(2000, ErrorMessage = "Terms cannot exceed 2000 characters.")]
    public string? TermsAndConditions { get; set; }

    [MinLength(1, ErrorMessage = "An order needs at least one line.")]
    public List<SaveSalesOrderLineRequest> Lines { get; set; } = [];
}

public class SaveSalesOrderLineRequest
{
    public long? ItemId { get; set; }

    [MaxLength(500, ErrorMessage = "Description cannot exceed 500 characters.")]
    public string? Description { get; set; }

    public long? WarehouseId { get; set; }

    [Range(0.000001, 999999999999.999999, ErrorMessage = "Quantity must be greater than zero.")]
    public decimal Quantity { get; set; }

    public long? UomId { get; set; }

    [Range(typeof(decimal), "0", "79228162514264337593543950335",
        ErrorMessage = "Unit price cannot be negative.")]
    public decimal UnitPrice { get; set; }

    [Range(0, 100, ErrorMessage = "Discount percent must be between 0 and 100.")]
    public decimal? DiscountPercent { get; set; }

    public long? TaxMasterId { get; set; }

    [MaxLength(300, ErrorMessage = "Line notes cannot exceed 300 characters.")]
    public string? LineNotes { get; set; }
}

/// <summary>Why an order was cancelled or closed short. Required, and read later.</summary>
public class CloseSalesOrderRequest
{
    [Required(ErrorMessage = "A reason is required.")]
    [MaxLength(500, ErrorMessage = "Reason cannot exceed 500 characters.")]
    public string Reason { get; set; } = null!;
}

/// <summary>A page of orders, and how many there are in total.</summary>
public class SalesOrderPage
{
    public List<SalesOrderListItem> Items { get; set; } = [];

    public int Page { get; set; }

    public int PageSize { get; set; }

    public int TotalCount { get; set; }
}

/// <summary>
/// What a line could draw on, for the availability indicator beside the quantity
/// and the breakdown behind it.
/// </summary>
public class ItemAvailability
{
    public long ItemId { get; set; }

    public string ItemCode { get; set; } = null!;

    public string ItemName { get; set; } = null!;

    public decimal QuantityOnHand { get; set; }

    /// <summary>Held by confirmed orders, this one included once it is confirmed.</summary>
    public decimal QuantityReserved { get; set; }

    public decimal QuantityAvailable { get; set; }

    public string InventoryUomCode { get; set; } = null!;
}

public enum SalesOrderOutcome
{
    Ok = 0,
    NotFound = 1,

    /// <summary>Editing something already confirmed. It is holding stock.</summary>
    NotDraft = 2,

    /// <summary>Confirming, cancelling or short-closing something in the wrong state.</summary>
    NotConfirmed = 3,

    /// <summary>An order with no lines promises nothing.</summary>
    NoLines = 4,

    /// <summary>Inventory could not hold the stock. The shortages say which lines.</summary>
    InsufficientStock = 5,

    /// <summary>Inventory could not be reached. Transient — nothing changed.</summary>
    InventoryUnreachable = 6,

    /// <summary>The SOR series is missing, so no number could be taken.</summary>
    SeriesMissing = 7,

    InvalidValue = 8,
}

public sealed record SalesOrderResult(
    SalesOrderOutcome Outcome,
    long? SalesOrderId = null,
    string? Detail = null,
    IReadOnlyList<SalesOrderShortage>? Shortages = null);

/// <summary>A line the stock could not cover, shaped for the screen's message box.</summary>
public sealed record SalesOrderShortage(
    int LineNumber,
    long ItemId,
    string ItemCode,
    string ItemName,
    decimal Requested,
    decimal Available);
