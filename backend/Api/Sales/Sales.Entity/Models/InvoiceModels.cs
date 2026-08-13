using System.ComponentModel.DataAnnotations;
using Shared.Kernel.Documents;

namespace Sales.Entity.Models;

public class InvoiceListItem
{
    public long InvoiceId { get; set; }
    public long? SalesOrderId { get; set; }
    public DateOnly DocumentDate { get; set; }
    public string DocumentNo { get; set; } = null!;
    public long ContactId { get; set; }
    public string ContactName { get; set; } = string.Empty;
    public DocumentStatus Status { get; set; }
    public DateOnly? DueDate { get; set; }
    public decimal TotalAmount { get; set; }
}

public class InvoiceView
{
    public long InvoiceId { get; set; }
    public long? QuoteId { get; set; }
    public long? SalesOrderId { get; set; }
    public long? DeliveryChallanId { get; set; }
    public long? PaymentTermId { get; set; }
    public DateOnly? DueDate { get; set; }

    public long? TillId { get; set; }
    public Guid? CashierUserId { get; set; }
    public string? PaymentMode { get; set; }
    public decimal? TenderedAmount { get; set; }
    public decimal? ChangeAmount { get; set; }

    public DateOnly DocumentDate { get; set; }
    public string DocumentNo { get; set; } = null!;
    public string? Notes { get; set; }
    public DocumentStatus Status { get; set; }
    public string CurrencyCode { get; set; } = null!;
    public decimal ExchangeRate { get; set; }
    public long ContactId { get; set; }
    public string ContactName { get; set; } = string.Empty;
    public string? BillingAddress { get; set; }
    public string? ShippingAddress { get; set; }

    public List<InvoiceLineView> Lines { get; set; } = [];
}

public class InvoiceLineView
{
    public long InvoiceLineId { get; set; }
    public long ItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal DiscountPercent { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal LineTotal { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal NetTotal { get; set; }
    
    public List<InvoiceLineTaxView> Taxes { get; set; } = [];
}

public class InvoiceLineTaxView
{
    public long SubAccountId { get; set; }
    public decimal Amount { get; set; }
}

public class SaveInvoiceRequest
{
    public long? QuoteId { get; set; }
    public long? SalesOrderId { get; set; }
    public long? DeliveryChallanId { get; set; }
    public long? PaymentTermId { get; set; }
    public DateOnly? DueDate { get; set; }

    public long? TillId { get; set; }
    public Guid? CashierUserId { get; set; }
    public string? PaymentMode { get; set; }
    public decimal? TenderedAmount { get; set; }
    public decimal? ChangeAmount { get; set; }

    public long ContactId { get; set; }
    public DateOnly DocumentDate { get; set; }
    
    [MaxLength(200)]
    public string? Notes { get; set; }

    [MaxLength(3)]
    public string CurrencyCode { get; set; } = "USD";
    public decimal ExchangeRate { get; set; } = 1;

    [MaxLength(100)]
    public string? BillingAddress { get; set; }
    
    [MaxLength(100)]
    public string? ShippingAddress { get; set; }

    public List<SaveInvoiceLineRequest> Lines { get; set; } = [];
}

public class SaveInvoiceLineRequest
{
    public long ItemId { get; set; }
    
    [Range(0.000001, double.MaxValue)]
    public decimal Quantity { get; set; }
    
    public decimal UnitPrice { get; set; }
    public decimal DiscountPercent { get; set; }
    
    public List<long> TaxGroupIds { get; set; } = [];
}
