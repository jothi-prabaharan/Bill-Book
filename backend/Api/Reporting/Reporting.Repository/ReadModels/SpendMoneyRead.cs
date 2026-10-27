using System;
using Reporting.Entity.Enums;
using Shared.Kernel.Tenancy;

namespace Reporting.Repository.ReadModels;

[System.ComponentModel.DataAnnotations.Schema.Table("SpendMoney", Schema = "acc")]
public class SpendMoneyRead : OrgScopedEntity
{
    public long SpendMoneyId { get; set; }
    public string? TransactionNo { get; set; }
    public DateOnly TransactionDate { get; set; }
    public long BankAccountId { get; set; }
    public long ContactId { get; set; }
    public decimal Amount { get; set; }
    public string CurrencyCode { get; set; } = null!;
    public decimal ExchangeRate { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
    public string? ReferenceNo { get; set; }
    public DateOnly? ReferenceDate { get; set; }
    public string? Memo { get; set; }
    /// <summary>TDS withheld from the vendor on the advance lines, in the payment's currency; the lines settle this and the amount together (TK-238).</summary>
    public decimal TdsAmount { get; set; }

    /// <summary>A payment of deducted TDS to the government rather than to a vendor (TK-238).</summary>
    public bool IsTdsChallan { get; set; }
    public string? MappingTransactionTypeCode { get; set; }
    public long? MappingTransactionId { get; set; }
    public MoneyDocumentStatus Status { get; set; }
    public DateTimeOffset? PostedAt { get; set; }
    public Guid? PostedBy { get; set; }
    public DateTimeOffset? VoidedAt { get; set; }
    public Guid? VoidedBy { get; set; }
    public string? VoidReason { get; set; }
}

