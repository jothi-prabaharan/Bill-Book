using System.ComponentModel.DataAnnotations;

namespace Reporting.Entity.Models;

/// <summary>A custom report as the API carries it (TK-113).</summary>
public class CustomReportModel
{
    public long CustomReportId { get; set; }

    [Required(ErrorMessage = "Dataset is required.")]
    [MaxLength(80, ErrorMessage = "Dataset cannot exceed 80 characters.")]
    public string DatasetKey { get; set; } = null!;

    [Required(ErrorMessage = "Title is required.")]
    [MaxLength(120, ErrorMessage = "Title cannot exceed 120 characters.")]
    public string Title { get; set; } = null!;

    [MaxLength(500, ErrorMessage = "Description cannot exceed 500 characters.")]
    public string? Description { get; set; }

    /// <summary>True when the whole branch sees it. Setting it needs <c>reports.edit</c>.</summary>
    public bool IsShared { get; set; }

    /// <summary>Whether this caller may change or delete it. Set by the server.</summary>
    public bool MayEdit { get; set; }

    [Required(ErrorMessage = "A custom report needs a layout.")]
    public ReportQueryRequest Layout { get; set; } = new();

    /// <summary>
    /// Columns the saved layout named that the dataset no longer has, a
    /// deactivated custom field's most often. They are taken out of
    /// <see cref="Layout"/> and listed here so the page can say so. Set by the server.
    /// </summary>
    public List<string> DroppedColumns { get; set; } = [];
}
