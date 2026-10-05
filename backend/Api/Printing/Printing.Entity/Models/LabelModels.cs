using System.ComponentModel.DataAnnotations;
using Printing.Entity.Enums;

namespace Printing.Entity.Models;

/// <summary>A branch's label size for one kind (TK-159).</summary>
public class LabelTemplateView
{
    public long LabelTemplateId { get; set; }

    public string Kind { get; set; } = null!;

    public string Name { get; set; } = null!;

    public decimal WidthMm { get; set; }

    public decimal HeightMm { get; set; }

    public decimal GapMm { get; set; }

    public int Columns { get; set; }

    public decimal ColumnGapMm { get; set; }

    public bool IsDefault { get; set; }
}

/// <summary>Changes a label's size. The kind is fixed; the content follows it.</summary>
public class SaveLabelTemplateRequest
{
    [Required(ErrorMessage = "Label name is required.")]
    [MaxLength(100, ErrorMessage = "Label name cannot exceed 100 characters.")]
    public string Name { get; set; } = null!;

    [Range(10, 210, ErrorMessage = "Label width must be between 10 and 210 mm.")]
    public decimal WidthMm { get; set; }

    [Range(8, 297, ErrorMessage = "Label height must be between 8 and 297 mm.")]
    public decimal HeightMm { get; set; }

    [Range(0, 20, ErrorMessage = "The gap between labels must be between 0 and 20 mm.")]
    public decimal GapMm { get; set; }

    [Range(1, 10, ErrorMessage = "Labels across must be between 1 and 10.")]
    public int Columns { get; set; } = 1;

    [Range(0, 20, ErrorMessage = "The gap between labels side by side must be between 0 and 20 mm.")]
    public decimal ColumnGapMm { get; set; }
}

/// <summary>
/// What goes on one label, and how many of it. Printing never reads an item:
/// the page asking for labels sends what it already shows, as a document's own
/// service sends its payload (the boundary TK-80 was argued on).
/// </summary>
public class LabelData
{
    [MaxLength(50, ErrorMessage = "Item code cannot exceed 50 characters.")]
    public string? ItemCode { get; set; }

    [Required(ErrorMessage = "Every label needs a name.")]
    [MaxLength(200, ErrorMessage = "Label name cannot exceed 200 characters.")]
    public string Name { get; set; } = null!;

    /// <summary>The code the barcode carries; the item code when absent.</summary>
    [MaxLength(80, ErrorMessage = "Barcode cannot exceed 80 characters.")]
    public string? Barcode { get; set; }

    [Range(0, 999_999_999, ErrorMessage = "Price must be between 0 and 999,999,999.")]
    public decimal? Price { get; set; }

    [Range(0, 999_999_999, ErrorMessage = "MRP must be between 0 and 999,999,999.")]
    public decimal? Mrp { get; set; }

    [MaxLength(50, ErrorMessage = "Batch number cannot exceed 50 characters.")]
    public string? BatchNo { get; set; }

    public DateOnly? ExpiryDate { get; set; }

    public DateOnly? ManufactureDate { get; set; }

    [MaxLength(80, ErrorMessage = "Serial number cannot exceed 80 characters.")]
    public string? SerialNo { get; set; }

    [Range(0, 1_000_000, ErrorMessage = "Weight must be between 0 and 1,000,000 grams.")]
    public decimal? GrossWeightGrams { get; set; }

    [Range(0, 1_000_000, ErrorMessage = "Weight must be between 0 and 1,000,000 grams.")]
    public decimal? NetWeightGrams { get; set; }

    [MaxLength(30, ErrorMessage = "Purity cannot exceed 30 characters.")]
    public string? Purity { get; set; }

    [MaxLength(10, ErrorMessage = "HUID cannot exceed 10 characters.")]
    public string? Huid { get; set; }

    /// <summary>How many of this label to print.</summary>
    [Range(1, 1000, ErrorMessage = "Copies must be between 1 and 1,000.")]
    public int Copies { get; set; } = 1;
}

/// <summary>Labels to print (TK-159).</summary>
public class PrintLabelsRequest
{
    /// <summary>The kind of label, which decides its content.</summary>
    [EnumDataType(typeof(LabelKind), ErrorMessage = "Label kind must be PriceTag, ShelfLabel, BatchExpiry or JewelleryTag.")]
    public LabelKind Kind { get; set; } = LabelKind.PriceTag;

    /// <summary>A particular size; the branch's default for the kind when absent.</summary>
    public long? LabelTemplateId { get; set; }

    /// <summary>On A4 sticker sheets rather than a label roll.</summary>
    public bool Sheet { get; set; }

    [Required(ErrorMessage = "Choose something to print labels for.")]
    [MinLength(1, ErrorMessage = "Choose something to print labels for.")]
    [MaxLength(500, ErrorMessage = "At most 500 different labels can be printed at once.")]
    public List<LabelData> Labels { get; set; } = [];
}

/// <summary>The labels as one HTML page the browser prints at the label's size.</summary>
public class PrintLabelsResponse
{
    public string Html { get; set; } = null!;

    /// <summary>Labels on the page, copies included.</summary>
    public int Count { get; set; }
}
