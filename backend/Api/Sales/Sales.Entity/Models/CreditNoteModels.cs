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
    public DocumentStatus Status { get; set; }
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
    public DocumentStatus Status { get; set; }
    public CreditNoteReason ReasonCode { get; set; }

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
    public long? ItemId { get; set; }
    public string? ItemLabel { get; set; }
    public string? Description { get; set; }
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
    public long? CreditNoteId { get; set; }
    
    [Required]
    public long InvoiceId { get; set; }
    
    [Required]
    public DateOnly DocumentDate { get; set; }
    
    [Range(1, long.MaxValue, ErrorMessage = "Contact is required")]
    public long ContactId { get; set; }

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
    public long InvoiceDetailId { get; set; }
    public long ItemId { get; set; }
    
    [Range(0.000001, double.MaxValue)]
    public decimal Quantity { get; set; }
    
    public decimal UnitPrice { get; set; }
    public decimal DiscountPercent { get; set; }
    
    public List<long> TaxGroupIds { get; set; } = [];
}
