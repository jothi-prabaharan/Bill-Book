using System.ComponentModel.DataAnnotations;
using Shared.Kernel.Documents;

namespace Purchase.Entity.Models;

/// <summary>
/// A purchase order as the screen sends it — header plus every line, in one
/// request.
///
/// <b>No totals and no tax amounts.</b> The server computes every figure from the
/// lines through <c>Shared.Kernel.Tax.GstCalculator</c>, at the rates in force on
/// the document's date. A caller free to send its own totals is a caller free to
/// save a document whose foot disagrees with its body, and a caller free to send
/// its own tax is one that can claim the wrong input credit.
///
/// <b>No document number either.</b> It is allocated on create from the `POR`
/// series, inside the same transaction as the insert.
///
/// <b>And no warehouse commitment.</b> A purchase order reserves nothing — the
/// goods are not there yet. That is the first of the five ways purchase is not a
/// mirror of sales, and it is why this request has no counterpart to the sales
/// order's reserved quantity.
/// </summary>
public class SavePurchaseOrderRequest
{
    public DateOnly DocumentDate { get; set; }

    [Range(1, long.MaxValue, ErrorMessage = "Choose the vendor.")]
    public long ContactId { get; set; }

    /// <summary>When the vendor is expected to deliver.</summary>
    public DateOnly? ExpectedDate { get; set; }

    [MaxLength(15, ErrorMessage = "GSTIN must be 15 characters.")]
    public string? ContactGstin { get; set; }

    /// <summary>
    /// The two-digit state code the supply is made in. Left null it falls back to
    /// the GSTIN's own state, which is right for the ordinary case.
    /// </summary>
    [MaxLength(2, ErrorMessage = "Place of supply must be a 2-digit state code.")]
    public string? PlaceOfSupplyStateCode { get; set; }

    public string? BillingAddress { get; set; }

    /// <summary>Where the goods should be delivered, which is not always the billing address.</summary>
    public string? ShippingAddress { get; set; }

    /// <summary>Null means the branch's own currency, which is the ordinary case.</summary>
    [MaxLength(3, ErrorMessage = "Currency code must be a 3-letter code.")]
    public string? CurrencyCode { get; set; }

    /// <summary>Snapshot at the document date. Never looked up live.</summary>
    [Range(typeof(decimal), "0.00000001", "79228162514264337593543950335",
        ErrorMessage = "Exchange rate must be greater than zero.")]
    public decimal? ExchangeRate { get; set; }

    public string? Notes { get; set; }

    public string? TermsAndConditions { get; set; }

    public List<SavePurchaseOrderLineRequest> Lines { get; set; } = [];
}

/// <summary>One line as the screen sends it. Money figures are derived, not sent.</summary>
public class SavePurchaseOrderLineRequest
{
    /// <summary>Null makes this a free-text line, which then needs an account.</summary>
    public long? ItemId { get; set; }

    [MaxLength(500, ErrorMessage = "Description cannot exceed 500 characters.")]
    public string? Description { get; set; }

    [MaxLength(8, ErrorMessage = "HSN/SAC code cannot exceed 8 characters.")]
    public string? HsnSacCode { get; set; }

    /// <summary>Where the goods are expected to land. A location dimension only.</summary>
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

    /// <summary>The vendor's price already contains its tax.</summary>
    public bool IsPriceInclusive { get; set; }

    [Range(typeof(decimal), "0", "100", ErrorMessage = "Discount percent runs from 0 to 100.")]
    public decimal? DiscountPercent { get; set; }

    [Range(typeof(decimal), "0", "79228162514264337593543950335",
        ErrorMessage = "Discount cannot be negative.")]
    public decimal DiscountAmount { get; set; }

    public TaxTreatment TaxTreatment { get; set; } = TaxTreatment.Taxable;

    /// <summary>
    /// The tax group, not the rate. The rate in force on the document date is
    /// resolved server-side — a caller that sent a rate could send yesterday's.
    /// </summary>
    public long? TaxGroupId { get; set; }

    /// <summary>
    /// Stock, Expense or Capital. Purchase is where all three are used — see
    /// <c>docs/modules/Purchase.md</c> §4.
    /// </summary>
    public DocumentLineType LineType { get; set; } = DocumentLineType.Stock;

    /// <summary>Required on a free-text or expense line.</summary>
    public long? AccountId { get; set; }

    /// <summary>Required on a capital line. The category owns the GL mapping, not the asset.</summary>
    public long? FixedAssetCategoryId { get; set; }

    public long? ItemBatchId { get; set; }

    [MaxLength(300, ErrorMessage = "Line notes cannot exceed 300 characters.")]
    public string? LineNotes { get; set; }
}

/// <summary>
/// Why a purchase order was refused. Every value is something a user can act on.
///
/// <b>There is no <c>InsufficientStock</c>.</b> The sales order has one because it
/// reserves; ordering from a vendor cannot fail for want of stock, which is the
/// entire reason the order is being raised.
/// </summary>
public enum PurchaseOrderOutcome
{
    Ok = 0,
    NotFound = 1,

    /// <summary>The lifecycle refused the move. <c>Detail</c> carries its own words.</summary>
    LifecycleRefused = 2,

    /// <summary>A line is self-contradictory — no description on a free-text line, a capital line with no category.</summary>
    LineInvalid = 3,

    /// <summary>The expected delivery date falls before the order itself.</summary>
    ExpectedDateInvalid = 4,

    /// <summary>Place of supply could not be resolved, or the GSTIN contradicts it.</summary>
    PlaceOfSupplyRefused = 5,

    /// <summary>Rates or the base currency could not be read. Transient — retry.</summary>
    RatesUnavailable = 6,

    /// <summary>Goods have already been received against this order, so it cannot be withdrawn.</summary>
    AlreadyReceived = 7,
}

public sealed record PurchaseOrderResult(
    PurchaseOrderOutcome Outcome, long PurchaseOrderId = 0, string? Detail = null);

/// <summary>A purchase order on the list screen. Vendor name resolved in a batch, never stored.</summary>
public class PurchaseOrderListItem
{
    public long PurchaseOrderId { get; set; }

    public string DocumentNo { get; set; } = null!;

    public DateOnly DocumentDate { get; set; }

    public DateOnly? ExpectedDate { get; set; }

    /// <summary>Open, PartlyReceived, Closed or Cancelled.</summary>
    public string FulfilmentStatus { get; set; } = null!;

    public long ContactId { get; set; }

    /// <summary>
    /// Read from Contacts in one call for the whole page, and null when it could
    /// not be read — the screen then shows the id rather than failing.
    /// </summary>
    public string? ContactName { get; set; }

    public string? ContactCode { get; set; }

    public string CurrencyCode { get; set; } = null!;

    public decimal TaxableAmount { get; set; }

    public decimal TotalAmount { get; set; }

    public string Status { get; set; } = null!;

    public bool IsInterState { get; set; }

    /// <summary>How many goods receipts have been raised against this order.</summary>
    public int ReceiptCount { get; set; }
}

/// <summary>A purchase order with its lines and their tax rows.</summary>
public class PurchaseOrderView : PurchaseOrderListItem
{
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

    public List<PurchaseOrderLineView> Lines { get; set; } = [];
}

public class PurchaseOrderLineView
{
    public long PurchaseOrderDetailId { get; set; }

    public int LineNumber { get; set; }

    public long? ItemId { get; set; }

    /// <summary>Resolved from Inventory in one call for the whole document.</summary>
    public string? ItemLabel { get; set; }

    public string? Description { get; set; }

    public string? HsnSacCode { get; set; }

    public long? WarehouseId { get; set; }

    public decimal Quantity { get; set; }

    public long? UomId { get; set; }

    public decimal ConversionFactor { get; set; }

    public decimal BaseQuantity { get; set; }

    /// <summary>How much has arrived, across every goods receipt against this line.</summary>
    public decimal ReceivedQuantity { get; set; }

    /// <summary>How much has been billed, across every bill against this line.</summary>
    public decimal BilledQuantity { get; set; }

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

    public List<PurchaseOrderLineTaxView> Taxes { get; set; } = [];
}

public class PurchaseOrderLineTaxView
{
    public long PurchaseOrderDetailTaxId { get; set; }

    public string TaxComponent { get; set; } = null!;

    public long SubAccountId { get; set; }

    public decimal Rate { get; set; }

    public decimal TaxableAmount { get; set; }

    public decimal Amount { get; set; }

    public decimal AmountBase { get; set; }
}

/// <summary>Why a purchase order is being withdrawn. The reason is required, always.</summary>
public class VoidPurchaseOrderRequest
{
    [Required(ErrorMessage = "Say why this purchase order is being voided.")]
    [MaxLength(300, ErrorMessage = "Reason cannot exceed 300 characters.")]
    public string Reason { get; set; } = null!;
}
