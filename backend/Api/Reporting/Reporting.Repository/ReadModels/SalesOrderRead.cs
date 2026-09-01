using System;
using Shared.Kernel.Tenancy;

namespace Reporting.Repository.ReadModels;

public class SalesOrderRead : OrgScopedEntity
{
    public long SalesOrderId { get; set; }
    public long? QuoteId { get; set; }
    public DateOnly? DeliveryDate { get; set; }
    public int FulfilmentStatus { get; set; } // enum mapped as int
    public string? ShortCloseReason { get; set; }

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
