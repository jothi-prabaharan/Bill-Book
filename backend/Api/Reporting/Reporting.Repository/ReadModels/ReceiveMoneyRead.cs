using System;
using Shared.Kernel.Tenancy;

namespace Reporting.Repository.ReadModels;

public class ReceiveMoneyRead : OrgScopedEntity
{
    public long ReceiveMoneyId { get; set; }
    public string? TransactionNo { get; set; }
    public DateOnly TransactionDate { get; set; }
    public long BankAccountId { get; set; }
    public long ContactId { get; set; }
    public decimal Amount { get; set; }
    public string CurrencyCode { get; set; } = null!;
    public decimal ExchangeRate { get; set; }
    public int PaymentMethod { get; set; }
    public string? ReferenceNo { get; set; }
    public DateOnly? ReferenceDate { get; set; }
    public string? Memo { get; set; }
    public string? MappingTransactionTypeCode { get; set; }
    public long? MappingTransactionId { get; set; }
    public int Status { get; set; }
    public DateTimeOffset? PostedAt { get; set; }
    public Guid? PostedBy { get; set; }
    public DateTimeOffset? VoidedAt { get; set; }
    public Guid? VoidedBy { get; set; }
    public string? VoidReason { get; set; }
}
