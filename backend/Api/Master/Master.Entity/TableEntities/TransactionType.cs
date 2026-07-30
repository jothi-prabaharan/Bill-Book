using System.ComponentModel.DataAnnotations;
using Shared.Kernel.Entities;

namespace Master.Entity.TableEntities;

/// <summary>
/// Every document type in the system, keyed by a three-letter code. Per-customer
/// tables store the code itself (no FK — cross-database), so a ledger row reads
/// without a join. A code is immutable once any data references it.
/// </summary>
public class TransactionType : AuditableEntity
{
    /// <summary>PK. Exactly three uppercase letters, e.g. INV.</summary>
    [Required(ErrorMessage = "Code is required.")]
    [StringLength(3, MinimumLength = 3, ErrorMessage = "Code must be exactly 3 characters.")]
    [RegularExpression("^[A-Z]{3}$", ErrorMessage = "Code must be three uppercase letters.")]
    public string Code { get; set; } = null!;

    [Required(ErrorMessage = "Name is required.")]
    [MaxLength(50, ErrorMessage = "Name cannot exceed 50 characters.")]
    public string Name { get; set; } = null!;

    /// <summary>
    /// False for documents that post nothing to the ledger (quotes, orders).
    /// The posting path checks this rather than hard-coding a list of codes.
    /// </summary>
    public bool IsLedgerPosting { get; set; }

    public bool IsActive { get; set; } = true;
}
