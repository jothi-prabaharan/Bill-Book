using System.ComponentModel.DataAnnotations;
using Master.Entity.Enums;
using Shared.Kernel.Entities;

namespace Master.Entity.TableEntities;

/// <summary>
/// What produced a ledger row. Since a payment and a refund share the same
/// transaction type (both are Spend/Receive Money), this is what tells them
/// apart — refund reports and reconciliation filter on it.
/// </summary>
public class LedgerSource : AuditableEntity
{
    /// <summary>PK, not identity — explicit ids for seeding.</summary>
    public int LedgerSourceId { get; set; }

    /// <summary>
    /// The canonical identity, and what reports filter on. 30 rather than 20:
    /// the scheme is entity + kind + action, and CUSTOMEROVERPAYMENTREFUND is 25
    /// characters. Abbreviating a key to fit the column is how a lookup table
    /// ends up full of codes nobody can read.
    /// </summary>
    [Required(ErrorMessage = "Code is required.")]
    [MaxLength(30, ErrorMessage = "Code cannot exceed 30 characters.")]
    public string Code { get; set; } = null!;

    [Required(ErrorMessage = "Name is required.")]
    [MaxLength(50, ErrorMessage = "Name cannot exceed 50 characters.")]
    public string Name { get; set; } = null!;

    public LedgerDirection Direction { get; set; } = LedgerDirection.Both;

    public bool IsActive { get; set; } = true;
}
