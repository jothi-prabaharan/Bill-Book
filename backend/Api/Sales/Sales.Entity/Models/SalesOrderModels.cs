using System.ComponentModel.DataAnnotations;
using Shared.Kernel.Documents;

namespace Sales.Entity.Models;

/// <summary>
/// A SalesOrder as the screen sends it — header plus every line, in one request.
///
/// <b>No totals and no tax amounts.</b> The server computes every figure from the
/// lines through <c>Shared.Kernel.Tax.GstCalculator</c>, at the rates in force on
/// the document's date. A caller free to send its own totals is a caller free to
/// save a document whose foot disagrees with its body, and a caller free to send
/// its own tax is one that can file the wrong return.
///
/// <b>No document number either.</b> It is allocated on create from the `SOR`
/// series, inside the same transaction as the insert.
/// </summary>
public class SaveSalesOrderRequest
{
    public DateOnly DocumentDate { get; set; }

    [Range(1, long.MaxValue, ErrorMessage = "Choose the customer.")]
    public long ContactId { get; set; }

    public long? QuoteId { get; set; }

    /// <summary>When the customer is expecting the goods.</summary>
    public DateOnly? DeliveryDate { get; set; }

    [MaxLength(15, ErrorMessage = "GSTIN must be 15 characters.")]
    public string? ContactGstin { get; set; }

    /// <summary>
    /// The two-digit state code the supply is made in. Left null it falls back to
    /// the GSTIN's own state, which is right for the ordinary case.
    /// </summary>
    [MaxLength(2, ErrorMessage = "Place of supply must be a 2-digit state code.")]
    public string? PlaceOfSupplyStateCode { get; set; }

    public string? BillingAddress { get; set; }

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

    public List<SaveSalesOrderLineRequest> Lines { get; set; } = [];
}

/// <summary>One line as the screen sends it. Money figures are derived, not sent.</summary>
public class SaveSalesOrderLineRequest
{
    /// <summary>Null makes this a free-text line, which then needs an account.</summary>
    public long? ItemId { get; set; }

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

    /// <summary>An MRP: the price already contains its tax.</summary>
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

    public DocumentLineType LineType { get; set; } = DocumentLineType.Stock;

    /// <summary>Required on a free-text or expense line.</summary>
    public long? AccountId { get; set; }

    public long? FixedAssetCategoryId { get; set; }

    public long? ItemBatchId { get; set; }

    [MaxLength(300, ErrorMessage = "Line notes cannot exceed 300 characters.")]
    public string? LineNotes { get; set; }
}

/// <summary>Why a SalesOrder was refused. Every value is something a user can act on.</summary>
public enum SalesOrderOutcome
{
    Ok = 0,
    NotFound = 1,

    /// <summary>The lifecycle refused the move. <c>Detail</c> carries its own words.</summary>
    LifecycleRefused = 2,

    /// <summary>A line is self-contradictory — no description on a free-text line, a negative price.</summary>
    LineInvalid = 3,

    /// <summary>The SalesOrder has no validity date, or one before the SalesOrder itself.</summary>
    ValidityInvalid = 4,

    /// <summary>Place of supply could not be resolved, or the GSTIN contradicts it.</summary>
    PlaceOfSupplyRefused = 5,

    /// <summary>Rates or the base currency could not be read. Transient — retry.</summary>
    RatesUnavailable = 6,

    /// <summary>This SalesOrder has already been fully delivered or invoiced.</summary>
    AlreadyFulfilled = 7,

    /// <summary>Insufficient stock to reserve.</summary>
    InsufficientStock = 8,

    /// <summary>The order exceeds the customer's credit limit or maximum outstanding days.</summary>
    CreditLimitExceeded = 9,

    /// <summary>
    /// The row exists and belongs to another branch.
    ///
    /// <b>Distinct from <see cref="NotFound"/> on purpose</b>, because the two
    /// are different answers: one says the id names nothing, the other says the
    /// caller may not have it. The house rule is <c>Forbid()</c> on a cross-org
    /// reach and never <c>NotFound()</c>.
    ///
    /// Under the query filter and the RLS policy another branch's row is already
    /// invisible, so this is reached only when something has read past both —
    /// an <c>IgnoreQueryFilters</c> added later, or a connection whose role
    /// bypasses RLS. That is exactly when the right answer matters, and a guard
    /// that only fires when the guards above it have failed is the one worth
    /// having.
    /// </summary>
    Forbidden = 10,

    /// <summary>The quote named cannot become an order — wrong status, or already converted.</summary>
    QuoteNotConvertible = 11,
}

public sealed record SalesOrderResult(SalesOrderOutcome Outcome, long SalesOrderId = 0, string? Detail = null);

/// <summary>
/// A read, and why it failed when it did.
///
/// A bare <c>SalesOrderView?</c> cannot say the difference between "no such
/// order" and "not yours", and the controller has to answer <c>NotFound()</c>
/// for one and <c>Forbid()</c> for the other.
/// </summary>
public sealed record SalesOrderViewResult(SalesOrderOutcome Outcome, SalesOrderView? View = null);

/// <summary>
/// One page of sales orders, and how many matched in all.
///
/// <b>The count is of the filtered set, not the page.</b> A list screen has to
/// say how many orders match before it can draw a pager, and counting the rows
/// it was handed would say "50 of 50" on every page of a thousand.
/// </summary>
public class SalesOrderListPage
{
    public int Total { get; set; }

    /// <summary>Echoed back already clamped, so the screen and the server agree on where it is.</summary>
    public int Skip { get; set; }

    public int Take { get; set; }

    public List<SalesOrderListItem> Rows { get; set; } = [];
}

/// <summary>
/// Turning an accepted quote into an order.
///
/// The lines are <b>not</b> sent: they are read from the quote server-side and
/// recomputed at the rates in force on the order's own date. A caller free to
/// send its own lines could raise an order that claims to come from a quote it
/// does not match, and the two documents would disagree for the rest of their
/// lives.
/// </summary>
public class CreateOrderFromQuoteRequest
{
    /// <summary>Defaults to today when the screen does not say.</summary>
    public DateOnly? DocumentDate { get; set; }

    /// <summary>When the customer is expecting the goods.</summary>
    public DateOnly? DeliveryDate { get; set; }

    /// <summary>
    /// The two-digit state code the supply is made in.
    ///
    /// <b>Not recoverable from the quote</b>, which stores the answer
    /// (<c>IsInterState</c>) and not the question. For a registered customer the
    /// GSTIN carried across settles it and this can be left null, which is the
    /// ordinary case; for an unregistered one the conversion needs it stated,
    /// exactly as raising the order directly would.
    /// </summary>
    [MaxLength(2, ErrorMessage = "Place of supply must be a 2-digit state code.")]
    public string? PlaceOfSupplyStateCode { get; set; }

    public string? Notes { get; set; }
}

/// <summary>A SalesOrder on the list screen. Contact name resolved in a batch, never stored.</summary>
public class SalesOrderListItem
{
    public long SalesOrderId { get; set; }

    public string DocumentNo { get; set; } = null!;

    public DateOnly DocumentDate { get; set; }

    public long? QuoteId { get; set; }

    public DateOnly? DeliveryDate { get; set; }

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

    /// <summary>The invoice this became, when it has. Null otherwise.</summary>
    public long? InvoicedDocumentId { get; set; }
}

/// <summary>A SalesOrder with its lines and their tax rows.</summary>
public class SalesOrderView : SalesOrderListItem
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

    public List<SalesOrderLineView> Lines { get; set; } = [];
}

public class SalesOrderLineView
{
    public long SalesOrderDetailId { get; set; }

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

    public decimal ReservedQuantity { get; set; }

    public decimal DeliveredQuantity { get; set; }

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

    public List<SalesOrderLineTaxView> Taxes { get; set; } = [];
}

public class SalesOrderLineTaxView
{
    public long SalesOrderDetailTaxId { get; set; }

    public string TaxComponent { get; set; } = null!;

    public long SubAccountId { get; set; }

    public decimal Rate { get; set; }

    public decimal TaxableAmount { get; set; }

    public decimal Amount { get; set; }

    public decimal AmountBase { get; set; }
}

/// <summary>Why a SalesOrder is being withdrawn. The reason is required, always.</summary>
public class VoidSalesOrderRequest
{
    [Required(ErrorMessage = "Say why this SalesOrder is being voided.")]
    [MaxLength(300, ErrorMessage = "Reason cannot exceed 300 characters.")]
    public string Reason { get; set; } = null!;
}
