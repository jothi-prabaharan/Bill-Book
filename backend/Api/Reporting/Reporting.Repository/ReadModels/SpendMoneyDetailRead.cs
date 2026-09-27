using System;
using Shared.Kernel.Tenancy;

namespace Reporting.Repository.ReadModels;

[System.ComponentModel.DataAnnotations.Schema.Table("SpendMoneyDetail", Schema = "acc")]
public class SpendMoneyDetailRead : OrgScopedEntity
{
    public long SpendMoneyDetailId { get; set; }
    public long SpendMoneyId { get; set; }
    public int LineNumber { get; set; }
    public int LedgerSourceId { get; set; }
    public string? MappingTransactionTypeCode { get; set; }
    public long? MappingTransactionId { get; set; }
    public decimal Amount { get; set; }
    public decimal AmountBase { get; set; }
    public string? LineMemo { get; set; }
    public long? ProjectId { get; set; }

    /// <summary>Charged on to the project's client (TK-106), with this markup.</summary>
    public bool IsBillable { get; set; }
    public decimal? MarkupPercent { get; set; }
    public long? BilledInvoiceId { get; set; }
}

