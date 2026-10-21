using System;
using Reporting.Entity.Enums;
using Shared.Kernel.Tenancy;

namespace Reporting.Repository.ReadModels;

/// <summary>
/// <c>acc.TransferMoney</c>, read-only (TK-202). A transfer is a movement on two
/// bank accounts at once, out of one and into the other, so the Reconciliation
/// Report looks for it on both: a statement line can be matched to it from
/// either side.
/// </summary>
[System.ComponentModel.DataAnnotations.Schema.Table("TransferMoney", Schema = "acc")]
public class TransferMoneyRead : OrgScopedEntity
{
    public long TransferMoneyId { get; set; }
    public string? TransactionNo { get; set; }
    public DateOnly TransactionDate { get; set; }
    public long FromBankAccountId { get; set; }
    public long ToBankAccountId { get; set; }
    public decimal Amount { get; set; }
    public string CurrencyCode { get; set; } = null!;
    public decimal ExchangeRate { get; set; }
    public string? ReferenceNo { get; set; }
    public string? Memo { get; set; }
    public MoneyDocumentStatus Status { get; set; }
}
