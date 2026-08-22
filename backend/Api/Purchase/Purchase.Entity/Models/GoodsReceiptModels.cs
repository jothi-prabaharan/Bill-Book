using System.ComponentModel.DataAnnotations;
using Shared.Kernel.Documents;

namespace Purchase.Entity.Models;

/// <summary>
/// A goods receipt as the screen sends it.
///
/// <b>No totals and no tax amounts</b>, as everywhere else: the server computes
/// every figure from the lines at the rates in force on the document's date.
///
/// <b>Batch, expiry and serial are on the line, in this request.</b> They are
/// user input — the storekeeper reads them off the carton — so they belong in
/// the call that answers them rather than in a background job whose failure
/// surfaces after that person has gone home.
/// </summary>
public class SaveGoodsReceiptRequest
{
    public DateOnly DocumentDate { get; set; }

    [Range(1, long.MaxValue, ErrorMessage = "Choose the vendor.")]
    public long ContactId { get; set; }

    /// <summary>The order these goods came against, when there was one.</summary>
    public long? PurchaseOrderId { get; set; }

    /// <summary>The vendor's own delivery note number, off the paperwork in the storekeeper's hand.</summary>
    [MaxLength(50, ErrorMessage = "Vendor delivery note number cannot exceed 50 characters.")]
    public string? VendorDeliveryNoteNo { get; set; }

    public DateOnly? VendorDeliveryNoteDate { get; set; }

    /// <summary>Who took delivery. Defaults to the caller.</summary>
    public Guid? ReceivedBy { get; set; }

    [MaxLength(15, ErrorMessage = "GSTIN must be 15 characters.")]
    public string? ContactGstin { get; set; }

    [MaxLength(2, ErrorMessage = "Place of supply must be a 2-digit state code.")]
    public string? PlaceOfSupplyStateCode { get; set; }

    public string? BillingAddress { get; set; }

    public string? ShippingAddress { get; set; }

    [MaxLength(3, ErrorMessage = "Currency code must be a 3-letter code.")]
    public string? CurrencyCode { get; set; }

    [Range(typeof(decimal), "0.00000001", "79228162514264337593543950335",
        ErrorMessage = "Exchange rate must be greater than zero.")]
    public decimal? ExchangeRate { get; set; }

    public string? Notes { get; set; }

    public string? TermsAndConditions { get; set; }

    public List<SaveGoodsReceiptLineRequest> Lines { get; set; } = [];
}

/// <summary>One line of a goods receipt.</summary>
public class SaveGoodsReceiptLineRequest
{
    public long? ItemId { get; set; }

    /// <summary>The order line this fulfils, when the receipt came against an order.</summary>
    public long? PurchaseOrderDetailId { get; set; }

    [MaxLength(500, ErrorMessage = "Description cannot exceed 500 characters.")]
    public string? Description { get; set; }

    [MaxLength(8, ErrorMessage = "HSN/SAC code cannot exceed 8 characters.")]
    public string? HsnSacCode { get; set; }

    public long? WarehouseId { get; set; }

    /// <summary>What the vendor delivered. Accepted plus rejected must equal this.</summary>
    [Range(typeof(decimal), "0.000001", "79228162514264337593543950335",
        ErrorMessage = "Quantity must be greater than zero.")]
    public decimal Quantity { get; set; }

    /// <summary>
    /// What is taken into stock. <b>This, not <see cref="Quantity"/>, is what
    /// moves</b> — a rejected carton is a dispute, not inventory.
    /// </summary>
    [Range(typeof(decimal), "0", "79228162514264337593543950335",
        ErrorMessage = "Accepted quantity cannot be negative.")]
    public decimal AcceptedQuantity { get; set; }

    [Range(typeof(decimal), "0", "79228162514264337593543950335",
        ErrorMessage = "Rejected quantity cannot be negative.")]
    public decimal RejectedQuantity { get; set; }

    /// <summary>Required when anything was rejected.</summary>
    [MaxLength(300, ErrorMessage = "Rejection reason cannot exceed 300 characters.")]
    public string? RejectionReason { get; set; }

    public long? UomId { get; set; }

    [Range(typeof(decimal), "0.000001", "79228162514264337593543950335",
        ErrorMessage = "Conversion factor must be greater than zero.")]
    public decimal ConversionFactor { get; set; } = 1m;

    /// <summary>What the goods cost. This is what the cost layer is opened at.</summary>
    [Range(typeof(decimal), "0", "79228162514264337593543950335",
        ErrorMessage = "Unit price cannot be negative.")]
    public decimal UnitPrice { get; set; }

    public bool IsPriceInclusive { get; set; }

    [Range(typeof(decimal), "0", "100", ErrorMessage = "Discount percent runs from 0 to 100.")]
    public decimal? DiscountPercent { get; set; }

    [Range(typeof(decimal), "0", "79228162514264337593543950335",
        ErrorMessage = "Discount cannot be negative.")]
    public decimal DiscountAmount { get; set; }

    public TaxTreatment TaxTreatment { get; set; } = TaxTreatment.Taxable;

    public long? TaxGroupId { get; set; }

    public DocumentLineType LineType { get; set; } = DocumentLineType.Stock;

    public long? AccountId { get; set; }

    public long? FixedAssetCategoryId { get; set; }

    // ---- Batch, expiry and serial. Captured here because they are what the
    // person unpacking the carton can see, and nowhere else.

    /// <summary>An existing lot to receive into. Wins over <see cref="BatchNumber"/>.</summary>
    public long? ItemBatchId { get; set; }

    /// <summary>A lot to find or open. Required when the item is batch-tracked.</summary>
    [MaxLength(50, ErrorMessage = "Batch number cannot exceed 50 characters.")]
    public string? BatchNumber { get; set; }

    /// <summary>Required when the item is expiry-tracked and the lot is new.</summary>
    public DateOnly? BatchExpiryDate { get; set; }

    public DateOnly? BatchManufactureDate { get; set; }

    [MaxLength(300, ErrorMessage = "Line notes cannot exceed 300 characters.")]
    public string? LineNotes { get; set; }
}

/// <summary>Why a goods receipt was refused. Every value is something a user can act on.</summary>
public enum GoodsReceiptOutcome
{
    Ok = 0,
    NotFound = 1,

    /// <summary>The lifecycle refused the move. <c>Detail</c> carries its own words.</summary>
    LifecycleRefused = 2,

    /// <summary>A line is self-contradictory — quantities that do not add up, a rejection with no reason.</summary>
    LineInvalid = 3,

    /// <summary>Place of supply could not be resolved, or the GSTIN contradicts it.</summary>
    PlaceOfSupplyRefused = 5,

    /// <summary>Rates or the base currency could not be read. Transient — retry.</summary>
    RatesUnavailable = 6,

    /// <summary>The purchase order named does not exist, or belongs to another vendor.</summary>
    OrderInvalid = 7,

    /// <summary>Inventory refused the stock — an unknown item, a missing batch, a non-stocked item.</summary>
    StockRefused = 8,

    /// <summary>
    /// The stock moved but the ledger did not take the posting. The document is
    /// left un-posted so the whole thing can be retried.
    /// </summary>
    PostingRefused = 9,

    /// <summary>A bill has already been raised against this receipt, so it cannot be withdrawn.</summary>
    AlreadyBilled = 10,
}

public sealed record GoodsReceiptResult(
    GoodsReceiptOutcome Outcome, long GoodsReceiptId = 0, string? Detail = null);

/// <summary>A goods receipt on the list screen.</summary>
public class GoodsReceiptListItem
{
    public long GoodsReceiptId { get; set; }

    public string DocumentNo { get; set; } = null!;

    public DateOnly DocumentDate { get; set; }

    public long? PurchaseOrderId { get; set; }

    public string? PurchaseOrderNo { get; set; }

    public string? VendorDeliveryNoteNo { get; set; }

    public long ContactId { get; set; }

    public string? ContactName { get; set; }

    public string? ContactCode { get; set; }

    public string CurrencyCode { get; set; } = null!;

    public decimal TaxableAmount { get; set; }

    public decimal TotalAmount { get; set; }

    public string Status { get; set; } = null!;

    public bool IsInterState { get; set; }

    /// <summary>How much was refused across the whole receipt. Zero on a clean delivery.</summary>
    public decimal RejectedQuantity { get; set; }
}

/// <summary>A goods receipt with its lines and their tax rows.</summary>
public class GoodsReceiptView : GoodsReceiptListItem
{
    public DateOnly? VendorDeliveryNoteDate { get; set; }

    public Guid? ReceivedBy { get; set; }

    public string? ContactGstin { get; set; }

    public int PlaceOfSupplyStateId { get; set; }

    public string? BillingAddress { get; set; }

    public string? ShippingAddress { get; set; }

    public decimal ExchangeRate { get; set; }

    public decimal SubTotal { get; set; }

    public decimal DiscountAmount { get; set; }

    public decimal CgstAmount { get; set; }

    public decimal SgstAmount { get; set; }

    public decimal IgstAmount { get; set; }

    public decimal CessAmount { get; set; }

    public decimal RoundOffAmount { get; set; }

    public decimal TotalAmountBase { get; set; }

    public string? Notes { get; set; }

    public string? TermsAndConditions { get; set; }

    public DateTimeOffset? PostedAt { get; set; }

    public DateTimeOffset? VoidedAt { get; set; }

    public string? VoidReason { get; set; }

    public List<GoodsReceiptLineView> Lines { get; set; } = [];
}

public class GoodsReceiptLineView
{
    public long GoodsReceiptDetailId { get; set; }

    public int LineNumber { get; set; }

    public long? PurchaseOrderDetailId { get; set; }

    public long? ItemId { get; set; }

    public string? ItemLabel { get; set; }

    public string? Description { get; set; }

    public string? HsnSacCode { get; set; }

    public long? WarehouseId { get; set; }

    public decimal Quantity { get; set; }

    public decimal AcceptedQuantity { get; set; }

    public decimal RejectedQuantity { get; set; }

    public string? RejectionReason { get; set; }

    public long? UomId { get; set; }

    public decimal ConversionFactor { get; set; }

    public decimal BaseQuantity { get; set; }

    public decimal UnitPrice { get; set; }

    public bool IsPriceInclusive { get; set; }

    public decimal? DiscountPercent { get; set; }

    public decimal DiscountAmount { get; set; }

    public decimal GrossAmount { get; set; }

    public decimal TaxableAmount { get; set; }

    public string TaxTreatment { get; set; } = null!;

    public long? TaxMasterId { get; set; }

    public long? TaxGroupId { get; set; }

    public decimal TaxAmount { get; set; }

    public string LineType { get; set; } = null!;

    public long? AccountId { get; set; }

    public long? FixedAssetCategoryId { get; set; }

    public decimal LineTotal { get; set; }

    public long? ItemBatchId { get; set; }

    public string? LineNotes { get; set; }

    public List<GoodsReceiptLineTaxView> Taxes { get; set; } = [];
}

public class GoodsReceiptLineTaxView
{
    public long GoodsReceiptDetailTaxId { get; set; }

    public string TaxComponent { get; set; } = null!;

    public long SubAccountId { get; set; }

    public decimal Rate { get; set; }

    public decimal TaxableAmount { get; set; }

    public decimal Amount { get; set; }

    public decimal AmountBase { get; set; }
}

/// <summary>Why a goods receipt is being withdrawn. The reason is required, always.</summary>
public class VoidGoodsReceiptRequest
{
    [Required(ErrorMessage = "Say why this goods receipt is being voided.")]
    [MaxLength(300, ErrorMessage = "Reason cannot exceed 300 characters.")]
    public string Reason { get; set; } = null!;
}
