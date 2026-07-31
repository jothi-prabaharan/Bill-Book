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

    [Required(ErrorMessage = "Code is required.")]
    [MaxLength(20, ErrorMessage = "Code cannot exceed 20 characters.")]
    public string Code { get; set; } = null!;

    [Required(ErrorMessage = "Name is required.")]
    [MaxLength(50, ErrorMessage = "Name cannot exceed 50 characters.")]
    public string Name { get; set; } = null!;

    public LedgerDirection Direction { get; set; } = LedgerDirection.Both;

    public bool IsActive { get; set; } = true;
}
