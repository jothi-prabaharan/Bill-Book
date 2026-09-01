using System;
using Shared.Kernel.Tenancy;

namespace Reporting.Repository.ReadModels;

public class GoodsReceiptRead : OrgScopedEntity
{
    public long GoodsReceiptId { get; set; }
    public long? PurchaseOrderId { get; set; }
    public string? VendorDeliveryNoteNo { get; set; }
    public DateOnly? VendorDeliveryNoteDate { get; set; }
    public Guid? ReceivedBy { get; set; }

    public string TransactionTypeCode { get; set; } = null!;
    public string DocumentNo { get; set; } = null!;
    public DateOnly DocumentDate { get; set; }
    public long ContactId { get; set; }
    public string? ContactGstin { get; set; }
    public string? BillingAddress { get; set; }
    public string? ShippingAddress { get; set; }
    public int PlaceOfSupplyStateId { get; set; }
    public bool IsInterState { get; set; }
    public string CurrencyCode { get; set; } = null!;
    public decimal ExchangeRate { get; set; }
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
    public Shared.Kernel.Documents.DocumentStatus Status { get; set; }
    public DateTimeOffset? PostedAt { get; set; }
    public Guid? PostedBy { get; set; }
    public DateTimeOffset? VoidedAt { get; set; }
    public Guid? VoidedBy { get; set; }
    public string? VoidReason { get; set; }
    public string? Notes { get; set; }
    public string? TermsAndConditions { get; set; }
}
