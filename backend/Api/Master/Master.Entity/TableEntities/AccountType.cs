using System.ComponentModel.DataAnnotations;
using Master.Entity.Enums;
using Shared.Kernel.Entities;

namespace Master.Entity.TableEntities;

/// <summary>
/// The five fixed account types — the only level above the chart of accounts
/// now that subtypes are gone. Global reference data in the master database;
/// acc.Accounts references AccountTypeId as an unenforced cross-database id.
/// </summary>
public class AccountType : AuditableEntity
{
    /// <summary>PK, not identity. Ids 1-5 are contractual.</summary>
    public int AccountTypeId { get; set; }

    /// <summary>Immutable, hidden — code and reports key on this, never the label.</summary>
    [Required(ErrorMessage = "System name is required.")]
    [MaxLength(20, ErrorMessage = "System name cannot exceed 20 characters.")]
    public string SystemName { get; set; } = null!;

    /// <summary>User-editable label.</summary>
    [Required(ErrorMessage = "Display name is required.")]
    [MaxLength(20, ErrorMessage = "Display name cannot exceed 20 characters.")]
    public string DisplayName { get; set; } = null!;

    public NormalBalance NormalBalance { get; set; }

    public ReportSection ReportSection { get; set; }

    /// <summary>Report ordering — these five always render in a fixed order.</summary>
    public short SortOrder { get; set; }

    public bool IsActive { get; set; } = true;
}
