using System.ComponentModel.DataAnnotations;
using Shared.Kernel.Tenancy;

namespace Accounting.Entity.TableEntities;

/// <summary>
/// Allocates one transaction (Source) against another (Target).
/// Example: Allocating a Payment (Source) to an Invoice (Target).
/// Also Credit Notes (Source) to Invoices (Target).
/// </summary>
public class TransactionRatio : OrgScopedEntity
{
    [Key]
    public long TransactionRatioId { get; set; }

    [MaxLength(3)]
    public string SourceTransactionTypeCode { get; set; } = null!;
    public long SourceTransactionId { get; set; }

    [MaxLength(3)]
    public string TargetTransactionTypeCode { get; set; } = null!;
    public long TargetTransactionId { get; set; }

    public decimal Amount { get; set; }
    
    // Stamped when the allocation is applied
    public DateTime AllocatedAt { get; set; }
}
