using System.ComponentModel.DataAnnotations;
using Shared.Kernel.Tenancy;

namespace Reporting.Repository.ReadModels;

/// <summary>
/// Mapped over sal."SalesRegisters" with ExcludeFromMigrations.
/// </summary>
public class SalesRegisterRead : OrgScopedEntity
{
    [Key]
    public long SalesRegisterId { get; set; }

    [MaxLength(3)]
    public string TransactionTypeCode { get; set; } = null!;

    public long SourceId { get; set; }

    [MaxLength(30)]
    public string DocumentNo { get; set; } = null!;

    public DateOnly DocumentDate { get; set; }

    public long ContactId { get; set; }

    [MaxLength(15)]
    public string? ContactGstin { get; set; }

    public int PlaceOfSupplyStateId { get; set; }

    public bool IsInterState { get; set; }

    [MaxLength(15)]
    public string SupplyType { get; set; } = null!;

    public bool ReverseCharge { get; set; }

    [MaxLength(8)]
    public string? HsnSacCode { get; set; }

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

    public long? OriginalInvoiceId { get; set; }
    [MaxLength(30)]
    public string? OriginalInvoiceNo { get; set; }
    public DateOnly? OriginalInvoiceDate { get; set; }
}
