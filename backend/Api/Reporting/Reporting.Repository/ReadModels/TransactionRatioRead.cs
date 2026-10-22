using System;
using Shared.Kernel.Tenancy;

namespace Reporting.Repository.ReadModels;

/// <summary>
/// <c>acc.TransactionRatios</c>, read-only (TK-213): one settlement between two
/// documents. Payments write them when posted (TK-206, TK-208) and Allocate
/// writes them document to document; the bill or credit is the source and the
/// invoice the target, as Accounting writes them. A voided row settles nothing,
/// and the allocation date is what an as-at report compares with.
/// </summary>
[System.ComponentModel.DataAnnotations.Schema.Table("TransactionRatios", Schema = "acc")]
public class TransactionRatioRead : OrgScopedEntity
{
    public long TransactionRatioId { get; set; }
    public string SourceTransactionTypeCode { get; set; } = null!;
    public long SourceTransactionId { get; set; }
    public string TargetTransactionTypeCode { get; set; } = null!;
    public long TargetTransactionId { get; set; }
    public decimal Amount { get; set; }
    public DateOnly AllocationDate { get; set; }
    public bool IsVoided { get; set; }
}
