using System.ComponentModel.DataAnnotations;
using Shared.Kernel.Entities;

namespace Master.Entity.TableEntities;

/// <summary>Which leg of a document a ledger row represents (item, tax, control, COGS, FX, rounding).</summary>
public class LedgerType : AuditableEntity
{
    /// <summary>PK, not identity — explicit ids for seeding.</summary>
    public int LedgerTypeId { get; set; }

    [Required(ErrorMessage = "Code is required.")]
    [MaxLength(20, ErrorMessage = "Code cannot exceed 20 characters.")]
    public string Code { get; set; } = null!;

    [Required(ErrorMessage = "Name is required.")]
    [MaxLength(50, ErrorMessage = "Name cannot exceed 50 characters.")]
    public string Name { get; set; } = null!;

    public bool IsActive { get; set; } = true;
}
