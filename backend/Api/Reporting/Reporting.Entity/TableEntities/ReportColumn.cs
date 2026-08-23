using System.ComponentModel.DataAnnotations;

namespace Reporting.Entity.TableEntities;

public class ReportColumn
{
    public long Id { get; set; }

    public long ReportMasterId { get; set; }

    [Required(ErrorMessage = "ColumnName is required.")]
    [MaxLength(255, ErrorMessage = "ColumnName cannot exceed 255 characters.")]
    public string ColumnName { get; set; } = null!;

    [MaxLength(255, ErrorMessage = "DisplayName cannot exceed 255 characters.")]
    public string? DisplayName { get; set; }

    [MaxLength(50, ErrorMessage = "DataType cannot exceed 50 characters.")]
    public string? DataType { get; set; }

    public bool IsGroup { get; set; }

    public int Order { get; set; }

    public bool IsSort { get; set; }

    public bool IsFilter { get; set; }

    [MaxLength(50, ErrorMessage = "FilterType cannot exceed 50 characters.")]
    public string? FilterType { get; set; }

    public ReportMaster ReportMaster { get; set; } = null!;
}
