using System.ComponentModel.DataAnnotations;

namespace Reporting.Entity.TableEntities;

public class ReportMaster
{
    public long Id { get; set; }

    [Required(ErrorMessage = "ReportGroup is required.")]
    [MaxLength(255, ErrorMessage = "ReportGroup cannot exceed 255 characters.")]
    public string ReportGroup { get; set; } = null!;

    [MaxLength(255, ErrorMessage = "ReportSubGroup cannot exceed 255 characters.")]
    public string? ReportSubGroup { get; set; }

    [Required(ErrorMessage = "ReportName is required.")]
    [MaxLength(255, ErrorMessage = "ReportName cannot exceed 255 characters.")]
    public string ReportName { get; set; } = null!;

    public List<ReportColumn> Columns { get; set; } = [];
}
