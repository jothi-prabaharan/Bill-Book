using System.ComponentModel.DataAnnotations;
using Shared.Kernel.Tenancy;

namespace Accounting.Entity.TableEntities;

/// <summary>
/// Allocates one transaction (Source) against another (Target).
/// Example: Allocating a Payment (Source) to an Invoice (Target).
/// Also Credit Notes (Source) to Invoices (Target).
///
/// <b>Voided rather than deleted.</b> A reversed allocation keeps its row and
/// carries <see cref="IsVoided"/>, so what was claimed and then released stays
/// answerable — the same reasoning that makes a document row a void rather than
/// a delete. Every guard that asks what a target still owes must therefore
/// filter voided rows out, or a released claim goes on occupying the balance;
/// <c>AllocationService</c> is where that filter lives.
/// </summary>
public class TransactionRatio : OrgScopedEntity
{
    [Key]
    public long TransactionRatioId { get; set; }

    [MaxLength(3, ErrorMessage = "Source transaction type code must be a 3-letter code.")]
    public string SourceTransactionTypeCode { get; set; } = null!;

    public long SourceTransactionId { get; set; }

    [MaxLength(3, ErrorMessage = "Target transaction type code must be a 3-letter code.")]
    public string TargetTransactionTypeCode { get; set; } = null!;

    public long TargetTransactionId { get; set; }

    public decimal Amount { get; set; }

    /// <summary>
    /// The date the allocation is effective in the books, which the user chooses
    /// and may back-date into an open period. Distinct from
    /// <see cref="AllocatedAt"/>, which is when the row was written — a
    /// back-dated settlement needs both, and collapsing them would either lose
    /// the effective date or let a clock decide which period a claim lands in.
    /// </summary>
    public DateOnly AllocationDate { get; set; }

    /// <summary>Stamped when the allocation is applied. The audit timestamp, never the effective date.</summary>
    public DateTime AllocatedAt { get; set; }

    [MaxLength(300, ErrorMessage = "Notes cannot exceed 300 characters.")]
    public string? Notes { get; set; }

    /// <summary>
    /// Whether the claim has been released. A voided row is history: it is
    /// excluded from every balance guard, and the amount it held returns to the
    /// target's available balance.
    /// </summary>
    public bool IsVoided { get; set; }

    public DateTimeOffset? VoidedAt { get; set; }

    [MaxLength(300, ErrorMessage = "Void reason cannot exceed 300 characters.")]
    public string? VoidReason { get; set; }
}
