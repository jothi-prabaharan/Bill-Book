using System.ComponentModel.DataAnnotations;
using Master.Entity.Enums;
using Shared.Kernel.Entities;

namespace Master.Entity.TableEntities;

/// <summary>
/// HSN (goods) and SAC (services) codes. Defined nationally, identical for every
/// customer, so they live in the master database and per-customer Items
/// reference them by unenforced id — a cross-database FK is impossible.
/// </summary>
public class HsnSacCode : AuditableEntity
{
    /// <summary>PK, not identity — explicit ids so seed rows are stable.</summary>
    public int HsnSacCodeId { get; set; }

    /// <summary>HSN is 2, 4, 6 or 8 digits; SAC is 6 and always begins 99.</summary>
    [Required(ErrorMessage = "Code is required.")]
    [MaxLength(8, ErrorMessage = "Code cannot exceed 8 characters.")]
    [RegularExpression(@"^\d{2,8}$", ErrorMessage = "Code must be 2 to 8 digits.")]
    public string Code { get; set; } = null!;

    public HsnSacCodeType CodeType { get; set; }

    [Required(ErrorMessage = "Description is required.")]
    [MaxLength(300, ErrorMessage = "Description cannot exceed 300 characters.")]
    public string Description { get; set; } = null!;

    /// <summary>First two digits — the chapter, used to group the picker.</summary>
    [MaxLength(2, ErrorMessage = "Chapter code must be 2 characters.")]
    public string? ChapterCode { get; set; }

    /// <summary>
    /// Suggested GST rate. A suggestion only: the same code can attract a
    /// different rate by condition, so the item's tax rate stays authoritative
    /// and this merely pre-selects it. Null where no single rate applies.
    /// </summary>
    public decimal? DefaultGstRate { get; set; }

    /// <summary>
    /// Number of digits — 2 for a chapter heading, 4/6/8 for real codes. A
    /// chapter is a grouping row and must not be put on an invoice line.
    /// </summary>
    public byte DigitLength { get; set; }

    public bool IsActive { get; set; } = true;
}
