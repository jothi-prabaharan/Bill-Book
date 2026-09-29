using System.ComponentModel.DataAnnotations;
using Shared.Kernel.Documents;
using Shared.Kernel.Tenancy;

namespace Purchase.Entity.TableEntities;

/// <summary>
/// The purchase side's GST register (TK-115), the mirror of
/// <c>sal.SalesRegister</c>: one row per posted bill or debit note line,
/// written by the poster in the posting transaction and removed when the
/// document is voided. GSTR-3B's input tax credit and the GSTR-2B
/// reconciliation read it, never the documents.
///
/// Amounts are the document's, positive for a debit note too; the
/// <see cref="TransactionTypeCode"/> says which way it counts. The supplier's
/// own number and date are kept apart from this business's, because the
/// return and 2B know the invoice only by what the supplier printed on it.
/// </summary>
public class PurchaseRegister : OrgScopedEntity
{
    public long PurchaseRegisterId { get; set; }

    /// <summary><c>BIL</c> or <c>DBN</c>.</summary>
    [Required(ErrorMessage = "Transaction type is required.")]
    [MaxLength(3, ErrorMessage = "Transaction type cannot exceed 3 characters.")]
    public string TransactionTypeCode { get; set; } = null!;

    /// <summary>The bill or debit note.</summary>
    public long SourceId { get; set; }

    /// <summary>The bill or debit note line.</summary>
    public long SourceLineId { get; set; }

    [Required(ErrorMessage = "Document number is required.")]
    [MaxLength(30, ErrorMessage = "Document number cannot exceed 30 characters.")]
    public string DocumentNo { get; set; } = null!;

    public DateOnly DocumentDate { get; set; }

    /// <summary>The supplier's invoice number as they issued it. For a debit note, the bill's.</summary>
    [Required(ErrorMessage = "Supplier invoice number is required.")]
    [MaxLength(50, ErrorMessage = "Supplier invoice number cannot exceed 50 characters.")]
    public string SupplierInvoiceNo { get; set; } = null!;

    public DateOnly SupplierInvoiceDate { get; set; }

    public long ContactId { get; set; }

    [MaxLength(15, ErrorMessage = "GSTIN must be 15 characters.")]
    public string? ContactGstin { get; set; }

    public int PlaceOfSupplyStateId { get; set; }

    public bool IsInterState { get; set; }

    public bool ReverseCharge { get; set; }

    public ItcEligibility ItcEligibility { get; set; }

    public DocumentLineType LineType { get; set; }

    [MaxLength(8, ErrorMessage = "HSN/SAC code cannot exceed 8 characters.")]
    public string? HsnSacCode { get; set; }

    public decimal GstRate { get; set; }

    public decimal Quantity { get; set; }

    public decimal TaxableAmount { get; set; }

    public decimal CgstAmount { get; set; }

    public decimal SgstAmount { get; set; }

    public decimal IgstAmount { get; set; }

    public decimal CessAmount { get; set; }

    public decimal TotalAmount { get; set; }

    [Required(ErrorMessage = "Currency is required.")]
    [MaxLength(3, ErrorMessage = "Currency code must be 3 letters.")]
    public string CurrencyCode { get; set; } = null!;

    public decimal ExchangeRate { get; set; }

    public decimal TaxableAmountBase { get; set; }

    /// <summary>The line's GST in base currency, all components: what 3B adds up.</summary>
    public decimal TaxAmountBase { get; set; }

    /// <summary>For a debit note, the bill it reduces.</summary>
    public long? OriginalBillId { get; set; }

    [MaxLength(30, ErrorMessage = "Original bill number cannot exceed 30 characters.")]
    public string? OriginalBillNo { get; set; }
}
