using System.ComponentModel.DataAnnotations;
using Printing.Entity.Enums;
using Shared.Kernel.Tenancy;

namespace Printing.Entity.TableEntities;

/// <summary>
/// A label's size, for one kind of label, in one branch (TK-159).
///
/// A sticker or label printer has no A4 or A5 paper: it prints on a roll of
/// labels of one size, so the page the browser prints must be exactly that
/// size or the driver scales and shifts it. This row is that size — width and
/// height in millimetres, how many labels sit side by side across the roll,
/// and the gaps — and the browser prints through the operating system's driver
/// at it. No printer language (ZPL, TSPL), no desktop app.
///
/// What is printed on the label is fixed per <see cref="LabelKind"/>; only the
/// size is the branch's to set.
/// </summary>
public class LabelTemplate : OrgScopedEntity
{
    public long LabelTemplateId { get; set; }

    public LabelKind Kind { get; set; }

    [Required(ErrorMessage = "Label name is required.")]
    [MaxLength(100, ErrorMessage = "Label name cannot exceed 100 characters.")]
    public string Name { get; set; } = null!;

    /// <summary>One label's width, in millimetres.</summary>
    [Range(10, 210, ErrorMessage = "Label width must be between 10 and 210 mm.")]
    public decimal WidthMm { get; set; }

    /// <summary>One label's height, in millimetres.</summary>
    [Range(8, 297, ErrorMessage = "Label height must be between 8 and 297 mm.")]
    public decimal HeightMm { get; set; }

    /// <summary>The space between one row of labels and the next, in millimetres.</summary>
    [Range(0, 20, ErrorMessage = "The gap between labels must be between 0 and 20 mm.")]
    public decimal GapMm { get; set; }

    /// <summary>How many labels sit side by side across the roll.</summary>
    [Range(1, 10, ErrorMessage = "Labels across must be between 1 and 10.")]
    public int Columns { get; set; } = 1;

    /// <summary>The space between labels side by side, in millimetres.</summary>
    [Range(0, 20, ErrorMessage = "The gap between labels side by side must be between 0 and 20 mm.")]
    public decimal ColumnGapMm { get; set; }

    /// <summary>The one used for its kind unless another is chosen. One per kind per branch.</summary>
    public bool IsDefault { get; set; }

    public bool IsActive { get; set; } = true;
}
