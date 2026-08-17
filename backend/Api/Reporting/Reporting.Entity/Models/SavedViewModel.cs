using System.ComponentModel.DataAnnotations;

namespace Reporting.Entity.Models;

/// <summary>
/// A saved layout, as the API carries it.
///
/// The layout itself is a <see cref="ReportQueryRequest"/> minus the page number —
/// which is the point: saving a view and running a report use one shape, so a
/// saved view cannot drift out of step with what the grid can express.
/// </summary>
public class SavedViewModel
{
    public long ReportViewId { get; set; }

    [Required(ErrorMessage = "View name is required.")]
    [MaxLength(80, ErrorMessage = "View name cannot exceed 80 characters.")]
    public string ViewName { get; set; } = null!;

    /// <summary>
    /// True when this is a branch-wide view rather than one person's. Setting it
    /// needs <c>reporting.manage</c>; the server decides the owner from the token
    /// rather than taking one from the caller.
    /// </summary>
    public bool IsShared { get; set; }

    public bool IsDefault { get; set; }

    [Required(ErrorMessage = "A saved view needs a layout.")]
    public ReportQueryRequest Layout { get; set; } = new();
}
