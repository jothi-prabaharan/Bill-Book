using System.ComponentModel.DataAnnotations;
using Shared.Kernel.Tenancy;

namespace Reporting.Entity.TableEntities;

/// <summary>
/// A saved layout: which columns, in what order, with which filters, sorting,
/// grouping, pivot and frozen columns.
///
/// Reports here carry eight to forty columns, so without this every user
/// re-picks them on every visit.
/// </summary>
public class ReportView : OrgScopedEntity
{
    public long ReportViewId { get; set; }

    public long ReportId { get; set; }

    [Required(ErrorMessage = "View name is required.")]
    [MaxLength(80, ErrorMessage = "View name cannot exceed 80 characters.")]
    public string ViewName { get; set; } = null!;

    /// <summary>
    /// Whose view it is. <b>Null means the whole branch sees it</b> — a standard
    /// layout somebody with <c>reporting.manage</c> published, rather than one
    /// person's working preference.
    ///
    /// No foreign key: users live in the master database and this table does not.
    /// </summary>
    public Guid? OwnerUserId { get; set; }

    /// <summary>
    /// Opened by default. One per user per report, and one branch-wide default
    /// per report, enforced by filtered unique indexes rather than in C#.
    /// </summary>
    public bool IsDefault { get; set; }

    /// <summary>
    /// The layout, as JSONB: selected columns and their order, filters, sorts,
    /// grouping, pivot specification, freeze settings, page size.
    ///
    /// <b>JSONB rather than four child tables</b> because the layout is read and
    /// written whole, is never queried into, and changes shape whenever the grid
    /// grows a capability — which would otherwise be a migration each time.
    /// </summary>
    [Required(ErrorMessage = "Layout is required.")]
    public string LayoutJson { get; set; } = null!;
}
