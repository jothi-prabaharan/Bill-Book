using System.ComponentModel.DataAnnotations;
using Purchase.Entity.Enums;
using Shared.Kernel.Documents;

namespace Purchase.Entity.Models;

/// <summary>
/// A debit note as the screen sends it — the way back out of a purchase.
///
/// <b>The bill is required, and so is the bill line on every row.</b> GST wants a
/// debit note to name the document it corrects, and stock going back needs the
/// bill line to find the cost layer it arrived on. A return valued at today's
/// weighted average rather than at what those units actually cost would move
/// value into or out of the branch that never existed.
/// </summary>
public class SaveDebitNoteRequest
{
    public DateOnly DocumentDate { get; set; }

    [Range(1, long.MaxValue, ErrorMessage = "Choose the vendor.")]
    public long ContactId { get; set; }

    /// <summary>The bill being corrected. Required.</summary>
    [Range(1, long.MaxValue, ErrorMessage = "Choose the bill this corrects.")]
    public long BillId { get; set; }

    /// <summary>
    /// Why. <b>Only <see cref="DebitNoteReason.PurchaseReturn"/> moves stock</b> —
    /// the rest are money-only corrections to a bill whose goods stayed.
    /// </summary>
    public DebitNoteReason ReasonCode { get; set; } = DebitNoteReason.PurchaseReturn;

    [MaxLength(15, ErrorMessage = "GSTIN must be 15 characters.")]
    public string? ContactGstin { get; set; }

    [MaxLength(2, ErrorMessage = "Place of supply must be a 2-digit state code.")]
    public string? PlaceOfSupplyStateCode { get; set; }

    [MaxLength(3, ErrorMessage = "Currency code must be a 3-letter code.")]
    public string? CurrencyCode { get; set; }

    [Range(typeof(decimal), "0.00000001", "79228162514264337593543950335",
        ErrorMessage = "Exchange rate must be greater than zero.")]
    public decimal? ExchangeRate { get; set; }

    public string? Notes { get; set; }

    public List<SaveDebitNoteLineRequest> Lines { get; set; } = [];
}

public class SaveDebitNoteLineRequest
{
    /// <summary>The bill line being reversed. <b>Required</b> — see the request summary.</summary>
    [Range(1, long.MaxValue, ErrorMessage = "Each line must name the bill line it reverses.")]
    public long BillDetailId { get; set; }

    public long? ItemId { get; set; }

    [MaxLength(500, ErrorMessage = "Description cannot exceed 500 characters.")]
    public string? Description { get; set; }

    [MaxLength(8, ErrorMessage = "HSN/SAC code cannot exceed 8 characters.")]
    public string? HsnSacCode { get; set; }

    public long? WarehouseId { get; set; }

    /// <summary>How much is going back, or being credited. Never more than the bill line carried.</summary>
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

    public DocumentLineType LineType { get; set; } = DocumentLineType.Stock;

    public long? AccountId { get; set; }

    public long? FixedAssetCategoryId { get; set; }

    public long? ItemBatchId { get; set; }

    [MaxLength(300, ErrorMessage = "Line notes cannot exceed 300 characters.")]
    public string? LineNotes { get; set; }
}

public enum DebitNoteOutcome
{
    Ok = 0,
    NotFound = 1,
    LifecycleRefused = 2,
    LineInvalid = 3,
    PlaceOfSupplyRefused = 5,
    RatesUnavailable = 6,

    /// <summary>The bill does not exist, belongs to another vendor, or was never posted.</summary>
    BillInvalid = 7,

    /// <summary>Inventory refused the return — not enough on hand, or an unknown lot.</summary>
    StockRefused = 8,

    /// <summary>The ledger did not take the posting. The note stays a draft and can be retried.</summary>
    PostingRefused = 9,

    /// <summary>More is being returned than the bill line has left to return.</summary>
    OverReturned = 10,
}

public sealed record DebitNoteResult(
    DebitNoteOutcome Outcome, long DebitNoteId = 0, string? Detail = null);

public class DebitNoteListItem
{
    public long DebitNoteId { get; set; }

    public string DocumentNo { get; set; } = null!;

    public DateOnly DocumentDate { get; set; }

    public long BillId { get; set; }

    public string? BillNo { get; set; }

    public string? VendorBillNo { get; set; }

    public string ReasonCode { get; set; } = null!;

    public long ContactId { get; set; }

    public string? ContactName { get; set; }

    public string? ContactCode { get; set; }

    public string CurrencyCode { get; set; } = null!;

    public decimal TaxableAmount { get; set; }

    public decimal TotalAmount { get; set; }

    public string Status { get; set; } = null!;

    public bool IsInterState { get; set; }

    /// <summary>Whether this note sent goods back, or only money.</summary>
    public bool MovesStock { get; set; }
}

public class DebitNoteView : DebitNoteListItem
{
    public string? ContactGstin { get; set; }

    public int PlaceOfSupplyStateId { get; set; }

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

    public DateTimeOffset? PostedAt { get; set; }

    public DateTimeOffset? VoidedAt { get; set; }

    public string? VoidReason { get; set; }

    public List<DebitNoteLineView> Lines { get; set; } = [];
}

public class DebitNoteLineView
{
    public long DebitNoteDetailId { get; set; }

    public int LineNumber { get; set; }

    public long BillDetailId { get; set; }

    public long? ItemId { get; set; }

    public string? ItemLabel { get; set; }

    public string? Description { get; set; }

    public string? HsnSacCode { get; set; }

    public long? WarehouseId { get; set; }

    public decimal Quantity { get; set; }

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

    public List<DebitNoteLineTaxView> Taxes { get; set; } = [];
}

public class DebitNoteLineTaxView
{
    public long DebitNoteDetailTaxId { get; set; }

    public string TaxComponent { get; set; } = null!;

    public long SubAccountId { get; set; }

    public decimal Rate { get; set; }

    public decimal TaxableAmount { get; set; }

    public decimal Amount { get; set; }

    public decimal AmountBase { get; set; }
}

public class VoidDebitNoteRequest
{
    [Required(ErrorMessage = "Say why this debit note is being voided.")]
    [MaxLength(300, ErrorMessage = "Reason cannot exceed 300 characters.")]
    public string Reason { get; set; } = null!;
}
