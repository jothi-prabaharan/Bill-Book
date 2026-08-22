using System.ComponentModel.DataAnnotations;
using Shared.Kernel.Documents;

namespace Purchase.Entity.Models;

/// <summary>
/// A vendor bill as the screen sends it.
///
/// <b>The vendor's own number is required and ours is not sent.</b> Input tax
/// credit is claimed against <c>VendorBillNo</c>, and the database refuses a
/// second bill carrying the same number from the same vendor in the same
/// financial year — which is what stops a duplicate claim at entry rather than
/// at a demand notice.
/// </summary>
public class SaveBillRequest
{
    public DateOnly DocumentDate { get; set; }

    [Range(1, long.MaxValue, ErrorMessage = "Choose the vendor.")]
    public long ContactId { get; set; }

    /// <summary>The order this bills, when there was one.</summary>
    public long? PurchaseOrderId { get; set; }

    /// <summary>
    /// The receipt this bills. <b>Its presence changes what the bill posts</b>:
    /// against a receipt the goods already reached stock, so the bill clears the
    /// clearing account and moves nothing.
    /// </summary>
    public long? GoodsReceiptId { get; set; }

    [Required(ErrorMessage = "The vendor's bill number is required.")]
    [MaxLength(50, ErrorMessage = "Vendor bill number cannot exceed 50 characters.")]
    public string VendorBillNo { get; set; } = null!;

    /// <summary>The date the vendor dated their bill — the tax period the credit falls in.</summary>
    public DateOnly VendorBillDate { get; set; }

    /// <summary>From <c>acc.PaymentTerms</c>. Drives the due date.</summary>
    public long? PaymentTermId { get; set; }

    /// <summary>
    /// Set directly when there is no payment term, or to override what one
    /// implies. Required one way or the other — payables aging is only as good
    /// as this column.
    /// </summary>
    public DateOnly? DueDate { get; set; }

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

    /// <summary>
    /// Freight, insurance and duty to spread across the lines. Held, not yet
    /// apportioned — how it is spread is undecided, so this stays zero in
    /// practice until it is.
    /// </summary>
    [Range(typeof(decimal), "0", "79228162514264337593543950335",
        ErrorMessage = "Landed cost cannot be negative.")]
    public decimal LandedCostAmount { get; set; }

    public string? Notes { get; set; }

    public string? TermsAndConditions { get; set; }

    public List<SaveBillLineRequest> Lines { get; set; } = [];
}

public class SaveBillLineRequest
{
    public long? ItemId { get; set; }

    /// <summary>The receipt line this bills. Required on every line of a bill against a receipt.</summary>
    public long? GoodsReceiptDetailId { get; set; }

    public long? PurchaseOrderDetailId { get; set; }

    [MaxLength(500, ErrorMessage = "Description cannot exceed 500 characters.")]
    public string? Description { get; set; }

    [MaxLength(8, ErrorMessage = "HSN/SAC code cannot exceed 8 characters.")]
    public string? HsnSacCode { get; set; }

    public long? WarehouseId { get; set; }

    [Range(typeof(decimal), "0.000001", "79228162514264337593543950335",
        ErrorMessage = "Quantity must be greater than zero.")]
    public decimal Quantity { get; set; }

    public long? UomId { get; set; }

    [Range(typeof(decimal), "0.000001", "79228162514264337593543950335",
        ErrorMessage = "Conversion factor must be greater than zero.")]
    public decimal ConversionFactor { get; set; } = 1m;

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

    /// <summary>Stock, Expense or Capital. All three are used on a bill.</summary>
    public DocumentLineType LineType { get; set; } = DocumentLineType.Stock;

    /// <summary>Required on an expense line — the account it posts to.</summary>
    public long? AccountId { get; set; }

    /// <summary>Required on a capital line.</summary>
    public long? FixedAssetCategoryId { get; set; }

    public long? ItemBatchId { get; set; }

    [MaxLength(300, ErrorMessage = "Line notes cannot exceed 300 characters.")]
    public string? LineNotes { get; set; }
}

/// <summary>Why a bill was refused. Every value is something a user can act on.</summary>
public enum BillOutcome
{
    Ok = 0,
    NotFound = 1,
    LifecycleRefused = 2,
    LineInvalid = 3,
    PlaceOfSupplyRefused = 5,
    RatesUnavailable = 6,

    /// <summary>The order or receipt named does not exist, or belongs to another vendor.</summary>
    SourceInvalid = 7,

    /// <summary>Inventory refused the stock — only reachable on a bill with no receipt behind it.</summary>
    StockRefused = 8,

    /// <summary>The ledger did not take the posting. The bill stays a draft and can be retried.</summary>
    PostingRefused = 9,

    /// <summary>A debit note has been raised against this bill, so it cannot be withdrawn.</summary>
    AlreadyCredited = 10,

    /// <summary>This vendor has already billed that number in this financial year.</summary>
    DuplicateVendorBillNo = 11,

    /// <summary>No due date, and no payment term to derive one from.</summary>
    DueDateMissing = 12,

    /// <summary>
    /// The bill prices the goods differently from the receipt that took them in,
    /// and how that difference is treated is not decided yet.
    /// </summary>
    PriceVarianceUndecided = 13,
}

public sealed record BillResult(BillOutcome Outcome, long BillId = 0, string? Detail = null);

public class BillListItem
{
    public long BillId { get; set; }

    public string DocumentNo { get; set; } = null!;

    public DateOnly DocumentDate { get; set; }

    public string VendorBillNo { get; set; } = null!;

    public DateOnly VendorBillDate { get; set; }

    public DateOnly DueDate { get; set; }

    public long? PurchaseOrderId { get; set; }

    public long? GoodsReceiptId { get; set; }

    public string? GoodsReceiptNo { get; set; }

    public long ContactId { get; set; }

    public string? ContactName { get; set; }

    public string? ContactCode { get; set; }

    public string CurrencyCode { get; set; } = null!;

    public decimal TaxableAmount { get; set; }

    public decimal TotalAmount { get; set; }

    public string Status { get; set; } = null!;

    public bool IsInterState { get; set; }

    /// <summary>Days past the due date, for a posted and unsettled bill. Negative is not yet due.</summary>
    public int DaysOverdue { get; set; }
}

public class BillView : BillListItem
{
    public string? ContactGstin { get; set; }

    public int PlaceOfSupplyStateId { get; set; }

    public long? PaymentTermId { get; set; }

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

    public decimal LandedCostAmount { get; set; }

    public string? Notes { get; set; }

    public string? TermsAndConditions { get; set; }

    public DateTimeOffset? PostedAt { get; set; }

    public DateTimeOffset? VoidedAt { get; set; }

    public string? VoidReason { get; set; }

    public List<BillLineView> Lines { get; set; } = [];
}

public class BillLineView
{
    public long BillDetailId { get; set; }

    public int LineNumber { get; set; }

    public long? GoodsReceiptDetailId { get; set; }

    public long? PurchaseOrderDetailId { get; set; }

    public long? ItemId { get; set; }

    public string? ItemLabel { get; set; }

    public string? Description { get; set; }

    public string? HsnSacCode { get; set; }

    public long? WarehouseId { get; set; }

    public decimal Quantity { get; set; }

    public long? UomId { get; set; }

    public decimal ConversionFactor { get; set; }

    public decimal BaseQuantity { get; set; }

    public decimal ReturnedQuantity { get; set; }

    public decimal ApportionedLandedCost { get; set; }

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

    public List<BillLineTaxView> Taxes { get; set; } = [];
}

public class BillLineTaxView
{
    public long BillDetailTaxId { get; set; }

    public string TaxComponent { get; set; } = null!;

    public long SubAccountId { get; set; }

    public decimal Rate { get; set; }

    public decimal TaxableAmount { get; set; }

    public decimal Amount { get; set; }

    public decimal AmountBase { get; set; }
}

public class VoidBillRequest
{
    [Required(ErrorMessage = "Say why this bill is being voided.")]
    [MaxLength(300, ErrorMessage = "Reason cannot exceed 300 characters.")]
    public string Reason { get; set; } = null!;
}
