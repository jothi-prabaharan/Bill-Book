using System.ComponentModel.DataAnnotations;
using Shared.Kernel.Documents;
using Shared.Kernel.Tenancy;

namespace Sales.Entity.TableEntities;

/// <summary>
/// The source for GSTR-1, the sales report, and the day book.
/// Written synchronously inside the post's transaction for Invoices, Credit Notes, and Delivery Challans.
/// Replaced by (TransactionTypeCode, SourceId) on re-post, deleted on void.
/// </summary>
public class SalesRegister : OrgScopedEntity
{
    [Key]
    public long SalesRegisterId { get; set; }

    /// <summary>Fixed per document type (e.g., INV, POS, CRN, DLC).</summary>
    [MaxLength(3)]
    public string TransactionTypeCode { get; set; } = null!;

    /// <summary>The ID of the source document.</summary>
    public long SourceId { get; set; }

    [MaxLength(30)]
    public string DocumentNo { get; set; } = null!;

    public DateOnly DocumentDate { get; set; }

    public long ContactId { get; set; }

    [MaxLength(15)]
    public string? ContactGstin { get; set; }

    public int PlaceOfSupplyStateId { get; set; }

    public bool IsInterState { get; set; }

    /// <summary>B2B, B2CL, B2CS, Export, SezWithPay, SezWithoutPay, Nil, Exempt, NonGst</summary>
    [MaxLength(15)]
    public string SupplyType { get; set; } = null!;

    public bool ReverseCharge { get; set; }

    [MaxLength(8)]
    public string? HsnSacCode { get; set; }

    /// <summary>The GST rate (e.g. 18.00)</summary>
    public decimal GstRate { get; set; }

    public decimal Quantity { get; set; }

    [MaxLength(3)]
    public string? UqcCode { get; set; }

    public decimal TaxableAmount { get; set; }

    public decimal CgstAmount { get; set; }
    
    public decimal SgstAmount { get; set; }

    public decimal IgstAmount { get; set; }
    
    public decimal CessAmount { get; set; }

    public decimal TotalAmount { get; set; }

    [MaxLength(3)]
    public string CurrencyCode { get; set; } = null!;

    public decimal ExchangeRate { get; set; }

    public decimal TaxableAmountBase { get; set; }

    // Used for Credit Notes against an original invoice
    public long? OriginalInvoiceId { get; set; }
    [MaxLength(30)]
    public string? OriginalInvoiceNo { get; set; }
    public DateOnly? OriginalInvoiceDate { get; set; }
}
