using System.ComponentModel.DataAnnotations;
using Shared.Kernel.Documents;

namespace Sales.Entity.Models;

/// <summary>
/// An Invoice as the screen sends it — header plus every line, in one request.
///
/// <b>No totals and no tax amounts.</b> The server computes every figure from the
/// lines through <c>Shared.Kernel.Tax.GstCalculator</c>, at the rates in force on
/// the document's date. A caller free to send its own totals is a caller free to
/// save a document whose foot disagrees with its body, and a caller free to send
/// its own tax is one that can file the wrong return.
///
/// <b>No document number either.</b> It is allocated on create from the `INV` (or `POS`)
/// series, inside the same transaction as the insert.
/// </summary>
public class SaveInvoiceRequest
{
    public DateOnly DocumentDate { get; set; }

    [Range(1, long.MaxValue, ErrorMessage = "Choose the customer.")]
    public long ContactId { get; set; }

    public long? QuoteId { get; set; }

    public long? SalesOrderId { get; set; }

    public long? DeliveryChallanId { get; set; }

    public long? PaymentTermId { get; set; }

    /// <summary>Derived from the payment term or specified directly. Required on an INV.</summary>
    public DateOnly? DueDate { get; set; }

    public long? TillId { get; set; }

    public Guid? CashierUserId { get; set; }

    [MaxLength(20, ErrorMessage = "Payment mode cannot exceed 20 characters.")]
    public string? PaymentMode { get; set; }

    public decimal? TenderedAmount { get; set; }

    public decimal? ChangeAmount { get; set; }

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

    public List<SaveInvoiceLineRequest> Lines { get; set; } = [];
}

/// <summary>
/// One page of invoices, and how many matched in all.
///
/// <b>The count is of the filtered set, not the page.</b> A list screen has to
/// say how many invoices match before it can draw a pager, and counting the rows
/// it was handed would say "50 of 50" on every page of a thousand.
/// </summary>
public class InvoiceListPage
{
    public int Total { get; set; }

    /// <summary>Echoed back already clamped, so the screen and the server agree on where it is.</summary>
    public int Skip { get; set; }

    public int Take { get; set; }

    public List<InvoiceListItem> Rows { get; set; } = [];
}

/// <summary>
/// Invoicing a confirmed sales order.
///
/// The lines are <b>not</b> sent: they are read from the order server-side and
/// recomputed at the rates in force on the invoice's own date. An invoice that
/// claimed to come from an order it does not match would leave the two documents
/// disagreeing for the rest of their lives — and this is the document a GST
/// return is filed from, so the disagreement would eventually be with the
/// department.
///
/// Same shape as <c>CreateOrderFromQuoteRequest</c> one step upstream, and for
/// the same reasons.
/// </summary>
public class CreateInvoiceFromOrderRequest
{
    /// <summary>Defaults to today when the screen does not say.</summary>
    public DateOnly? DocumentDate { get; set; }

    /// <summary>
    /// Required on an <c>INV</c>, here as everywhere else.
    ///
    /// It is not carried over from the order, because an order has no due date
    /// to carry — a delivery date is when goods are expected, not when money is.
    /// Send a <see cref="PaymentTermId"/> instead and the term derives it.
    /// </summary>
    public DateOnly? DueDate { get; set; }

    public long? PaymentTermId { get; set; }

    /// <summary>
    /// The two-digit state code the supply is made in.
    ///
    /// <b>Not recoverable from the order</b>, which stores the answer
    /// (<c>IsInterState</c>) and not the question. For a registered customer the
    /// GSTIN carried across settles it and this can be left null.
    /// </summary>
    [MaxLength(2, ErrorMessage = "Place of supply must be a 2-digit state code.")]
    public string? PlaceOfSupplyStateCode { get; set; }

    public string? Notes { get; set; }
}

/// <summary>One line as the screen sends it. Money figures are derived, not sent.</summary>
public class SaveInvoiceLineRequest
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
    /// resolved server-side.
    /// </summary>
    public long? TaxGroupId { get; set; }

    public List<long> TaxGroupIds { get; set; } = [];

    public DocumentLineType LineType { get; set; } = DocumentLineType.Stock;

    /// <summary>Required on a free-text or expense line.</summary>
    public long? AccountId { get; set; }

    public long? FixedAssetCategoryId { get; set; }

    public long? ItemBatchId { get; set; }

    [MaxLength(300, ErrorMessage = "Line notes cannot exceed 300 characters.")]
    public string? LineNotes { get; set; }

    public long? SalesOrderDetailId { get; set; }
}

/// <summary>Why an Invoice was refused. Every value is something a user can act on.</summary>
public enum InvoiceOutcome
{
    Ok = 0,
    NotFound = 1,

    /// <summary>The lifecycle refused the move. <c>Detail</c> carries its own words.</summary>
    LifecycleRefused = 2,

    /// <summary>A line is self-contradictory — no description on a free-text line, a negative price.</summary>
    LineInvalid = 3,

    /// <summary>The validity date is invalid or before the document itself.</summary>
    ValidityInvalid = 4,

    /// <summary>Place of supply could not be resolved, or the GSTIN contradicts it.</summary>
    PlaceOfSupplyRefused = 5,

    /// <summary>Rates or the base currency could not be read. Transient — retry.</summary>
    RatesUnavailable = 6,

    /// <summary>This order has already been fully invoiced.</summary>
    AlreadyFulfilled = 7,

    /// <summary>Insufficient stock to issue.</summary>
    InsufficientStock = 8,

    /// <summary>The invoice exceeds the customer's credit limit or maximum outstanding days.</summary>
    CreditLimitExceeded = 9,

    /// <summary>Invoice requires a due date.</summary>
    DueDateMissing = 10,

    /// <summary>Source document is invalid or not in expected state.</summary>
    SourceInvalid = 11,

    /// <summary>Ledger posting failed.</summary>
    PostingRefused = 12,

    /// <summary>Stock issue failed.</summary>
    StockRefused = 13,

    /// <summary>Downstream credit note prevents voiding.</summary>
    AlreadyCredited = 14,
}

public sealed record InvoiceResult(InvoiceOutcome Outcome, long InvoiceId = 0, string? Detail = null);

/// <summary>An Invoice on the list screen. Contact name resolved in a batch, never stored.</summary>
public class InvoiceListItem
{
    public long InvoiceId { get; set; }

    public string DocumentNo { get; set; } = null!;

    public DateOnly DocumentDate { get; set; }

    public DateOnly? DueDate { get; set; }

    public long? QuoteId { get; set; }

    public long? SalesOrderId { get; set; }

    public long? DeliveryChallanId { get; set; }

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

    public int DaysOverdue { get; set; }

    public string? PaymentMode { get; set; }

    /// <summary>
    /// What has been received against this invoice, from Accounting's ledger.
    ///
    /// <b>Null when the invoice has never posted</b>, which is not the same as
    /// zero: a draft is not an unpaid receivable, and folding it in as owing
    /// nothing would put it in the same bucket as one paid in full. Null also
    /// when the ledger could not be read — the list still loads, it just does
    /// not claim to know.
    /// </summary>
    public decimal? PaidAmount { get; set; }

    public decimal? OutstandingAmount { get; set; }

    /// <summary>
    /// Unpaid, PartPaid or Paid — derived, never stored.
    ///
    /// <b>Deliberately not a <c>DocumentStatus</c>.</b> Settlement is a second
    /// axis: an invoice is Posted <i>and</i> part-paid at the same time, and one
    /// enum cannot say both. It is also not the invoice's own fact to keep — the
    /// money arrives on a receipt, and a copy here would be a figure that drifts
    /// from the ledger the first time an allocation is undone.
    /// </summary>
    public string? SettlementStatus { get; set; }

}

public class InvoiceSummary : InvoiceListItem
{
}

/// <summary>An Invoice with its lines and their tax rows.</summary>
public class InvoiceView : InvoiceListItem
{
    public string? ContactGstin { get; set; }

    public int PlaceOfSupplyStateId { get; set; }

    public long? PaymentTermId { get; set; }

    public long? TillId { get; set; }

    public Guid? CashierUserId { get; set; }

    public decimal? TenderedAmount { get; set; }

    public decimal? ChangeAmount { get; set; }

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

    public List<InvoiceLineView> Lines { get; set; } = [];
}

public class InvoiceLineView
{
    public long InvoiceDetailId { get; set; }

    public int LineNumber { get; set; }

    public long? SalesOrderDetailId { get; set; }

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

    public decimal ReturnedQuantity { get; set; }

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

    public List<InvoiceLineTaxView> Taxes { get; set; } = [];
}

public class InvoiceLineTaxView
{
    public long InvoiceDetailTaxId { get; set; }

    public string TaxComponent { get; set; } = null!;

    public long SubAccountId { get; set; }

    public decimal Rate { get; set; }

    public decimal TaxableAmount { get; set; }

    public decimal Amount { get; set; }

    public decimal AmountBase { get; set; }
}

public class InvoiceTaxView : InvoiceLineTaxView
{
}

/// <summary>Why an Invoice is being withdrawn. The reason is required, always.</summary>
public class VoidInvoiceRequest
{
    [Required(ErrorMessage = "Say why this invoice is being voided.")]
    [MaxLength(300, ErrorMessage = "Reason cannot exceed 300 characters.")]
    public string Reason { get; set; } = null!;
}

/// <summary>GL breakdown preview before finalizing an invoice.</summary>
public class GlPreviewResult
{
    public List<GlEntryLegView> Legs { get; set; } = [];
    public decimal TotalDebit { get; set; }
    public decimal TotalCredit { get; set; }
    public bool IsBalanced { get; set; }
}

public class GlEntryLegView
{
    public int LedgerTypeId { get; set; }
    public string AccountName { get; set; } = null!;
    public string? SubAccountName { get; set; }
    public decimal DebitAmount { get; set; }
    public decimal CreditAmount { get; set; }
    public string? Description { get; set; }
}
