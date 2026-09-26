using System.ComponentModel.DataAnnotations;
using Sales.Entity.Enums;
using Shared.Kernel.Documents;
using Shared.Kernel.Tax;

namespace Sales.Entity.Models;

public class CreditNoteListItem
{
    public long CreditNoteId { get; set; }
    public long InvoiceId { get; set; }
    public DateOnly DocumentDate { get; set; }
    public string DocumentNo { get; set; } = null!;
    public long ContactId { get; set; }
    public string ContactName { get; set; } = null!;

    /// <summary>
    /// Draft / ReadyToPost / Posted / Void, by name — this API serializes enums as
    /// numbers, and the screen compares against the names, as on the invoice.
    /// </summary>
    public string Status { get; set; } = null!;
    public decimal TotalAmount { get; set; }
}

public class CreditNoteView
{
    public long CreditNoteId { get; set; }
    public long InvoiceId { get; set; }
    public DateOnly DocumentDate { get; set; }
    public string DocumentNo { get; set; } = null!;
    public long ContactId { get; set; }
    public string ContactName { get; set; } = null!;

    /// <summary>By name — see <see cref="CreditNoteListItem.Status"/>.</summary>
    public string Status { get; set; } = null!;
    public CreditNoteReason ReasonCode { get; set; }
    public string? ContactGstin { get; set; }
    public string? VoidReason { get; set; }

    public string CurrencyCode { get; set; } = null!;
    public decimal ExchangeRate { get; set; }

    public string? Notes { get; set; }
    public string? BillingAddress { get; set; }
    public string? ShippingAddress { get; set; }

    public int PlaceOfSupplyStateId { get; set; }
    public bool IsInterState { get; set; }

    public decimal SubTotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TaxableAmount { get; set; }
    public decimal CgstAmount { get; set; }
    public decimal SgstAmount { get; set; }
    public decimal IgstAmount { get; set; }
    public decimal CessAmount { get; set; }
    public decimal RoundOffAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal TotalAmountBase { get; set; }

    public List<CreditNoteLineView> Lines { get; set; } = [];
}

public class CreditNoteLineView
{
    public long CreditNoteDetailId { get; set; }
    public long InvoiceDetailId { get; set; }

    /// <summary>The project, taken from the invoice line it credits (TK-105).</summary>
    public long? ProjectId { get; set; }
    public long? ItemId { get; set; }
    public string? ItemLabel { get; set; }
    public string? HsnSacCode { get; set; }
    public string? Description { get; set; }
    public long? TaxGroupId { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal DiscountPercent { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal LineTotal { get; set; }
    public decimal TaxAmount { get; set; }

    public List<CreditNoteLineTaxView> Taxes { get; set; } = [];
}

public class CreditNoteLineTaxView
{
    public TaxComponent TaxComponent { get; set; }
    public long SubAccountId { get; set; }
    public decimal Amount { get; set; }
}

public class SaveCreditNoteRequest
{

    /// <summary>
    /// The print template this document should use. Null means the branch's
    /// default for this document type, which is also where a template that has
    /// since been deleted resolves to — so the choice can never make a document
    /// unprintable.
    /// </summary>
    public long? PrintTemplateId { get; set; }
    public long? CreditNoteId { get; set; }
    
    [Required]
    public long InvoiceId { get; set; }
    
    [Required]
    public DateOnly DocumentDate { get; set; }
    
    [Range(1, long.MaxValue, ErrorMessage = "Contact is required")]
    public long ContactId { get; set; }

    /// <summary>The contact's GSTIN, as of this document. See <see cref="PlaceOfSupplyStateCode"/>.</summary>
    [MaxLength(15, ErrorMessage = "GSTIN must be 15 characters.")]
    public string? ContactGstin { get; set; }

    /// <summary>
    /// The two-digit state code the supply is made in. Falls back to the state
    /// read off <see cref="ContactGstin"/> when left blank — see
    /// <c>Shared.Kernel.Tax.PlaceOfSupply</c>.
    /// </summary>
    public string? PlaceOfSupplyStateCode { get; set; }

    public CreditNoteReason ReasonCode { get; set; } = CreditNoteReason.SalesReturn;

    [MaxLength(3)]
    public string? CurrencyCode { get; set; }
    
    public decimal ExchangeRate { get; set; } = 1m;

    [MaxLength(500)]
    public string? Notes { get; set; }

    [MaxLength(100)]
    public string? BillingAddress { get; set; }

    [MaxLength(100)]
    public string? ShippingAddress { get; set; }

    public List<SaveCreditNoteLineRequest> Lines { get; set; } = [];
}

public class SaveCreditNoteLineRequest
{
    /// <summary>
    /// The invoice line this corrects. Required, and it must be a line of the
    /// invoice the note names — it is what a returned item's cost is taken back
    /// from, and what stops the same goods being returned twice.
    /// </summary>
    [Range(1, long.MaxValue, ErrorMessage = "Every line must name the invoice line it corrects.")]
    public long InvoiceDetailId { get; set; }

    /// <summary>The invoice line's item, or null for a free-text line. Checked against the invoice.</summary>
    public long? ItemId { get; set; }

    [Range(0.000001, double.MaxValue, ErrorMessage = "Quantity must be greater than zero.")]
    public decimal Quantity { get; set; }

    [Range(typeof(decimal), "0", "79228162514264337593543950335",
        ErrorMessage = "Unit price cannot be negative.")]
    public decimal UnitPrice { get; set; }

    [Range(typeof(decimal), "0", "100", ErrorMessage = "Discount must be between 0 and 100 percent.")]
    public decimal DiscountPercent { get; set; }

    /// <summary>The tax group, the way the invoice takes it. Read ahead of <see cref="TaxGroupIds"/>.</summary>
    public long? TaxGroupId { get; set; }

    public List<long> TaxGroupIds { get; set; } = [];
}

/// <summary>
/// Why a credit note is being withdrawn. Required: the
/// <c>chk_creditnotes_void_stamp</c> constraint ties <c>VoidedAt</c> to
/// <c>VoidReason</c>, and the void took no reason at all, so every credit note
/// void was refused by the database.
/// </summary>
public class VoidCreditNoteRequest
{
    [Required(ErrorMessage = "Say why this credit note is being voided.")]
    [MaxLength(300, ErrorMessage = "Reason cannot exceed 300 characters.")]
    public string Reason { get; set; } = null!;

    /// <summary>The IRP's reason code when the void cancels an IRN (TK-92).</summary>
    public Sales.Entity.Enums.EInvoiceCancelReason? CancelReason { get; set; }
}

/// <summary>Why a credit note was refused. Every value is something a user can act on.</summary>
public enum CreditNoteOutcome
{
    Ok = 0,
    NotFound = 1,

    /// <summary>The lifecycle refused the move. <c>Detail</c> carries its own words.</summary>
    LifecycleRefused = 2,

    /// <summary>A line is not a line of the invoice, or names another item.</summary>
    LineInvalid = 3,

    /// <summary>Place of supply could not be resolved, or the GSTIN contradicts it.</summary>
    PlaceOfSupplyRefused = 4,

    /// <summary>Branch settings, the base currency or a tax rate could not be read. Transient.</summary>
    RatesUnavailable = 5,

    /// <summary>The invoice is missing, not posted, or for another customer.</summary>
    SourceInvalid = 6,

    /// <summary>More would come back than the invoice line has left to return.</summary>
    OverReturned = 7,

    /// <summary>Accounting refused the claim against the invoice — it would exceed what is owed.</summary>
    AllocationRefused = 8,

    /// <summary>Inventory refused to take the goods back.</summary>
    StockRefused = 9,

    /// <summary>The ledger refused the posting or its withdrawal.</summary>
    PostingRefused = 10,

    /// <summary>The IRN stands in the way of a void (TK-92). <c>Detail</c> says why.</summary>
    EInvoiceRefused = 11,
}

public sealed record CreditNoteResult(
    CreditNoteOutcome Outcome, long CreditNoteId = 0, string? Detail = null, EInvoiceStateView? EInvoice = null);
