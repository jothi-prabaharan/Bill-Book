using System.ComponentModel.DataAnnotations;
using Reporting.Entity.Enums;
using Shared.Kernel.Tenancy;

namespace Reporting.Entity.TableEntities;

/// <summary>
/// One row per report — the catalog the reports screen lists.
///
/// <b>The catalog is in the database rather than only in code</b> because a
/// branch turns reports off, reorders them, and renames them into its own
/// language, and none of those is a deploy. What a report <i>means</i> — which
/// tables it reads, how a column is computed — stays in its
/// <c>IReportSource</c>; this row is how it is presented and whether it appears.
///
/// Seeded as reference data, so <c>CreatedBy</c> is null on every seeded row.
/// </summary>
public class Report : OrgScopedEntity
{
    public long ReportId { get; set; }

    /// <summary>
    /// The stable identifier the API and the routes use — <c>account-movement</c>,
    /// <c>trial-balance</c>. Matches the <c>ReportKey</c> of the
    /// <c>IReportSource</c> that implements it, and a startup check refuses to
    /// begin if the two ever disagree.
    /// </summary>
    [Required(ErrorMessage = "Report key is required.")]
    [MaxLength(60, ErrorMessage = "Report key cannot exceed 60 characters.")]
    public string ReportKey { get; set; } = null!;

    [Required(ErrorMessage = "Report title is required.")]
    [MaxLength(120, ErrorMessage = "Report title cannot exceed 120 characters.")]
    public string Title { get; set; } = null!;

    public ReportModule Module { get; set; }

    [MaxLength(500, ErrorMessage = "Description cannot exceed 500 characters.")]
    public string? Description { get; set; }

    /// <summary>
    /// The module permission a caller needs on top of <c>reports.view</c> —
    /// <c>accounting.view</c>, <c>inventory.view</c>. A report the caller may not
    /// run is absent from the catalog rather than listed and refused, so the
    /// existence of a report is not itself information.
    /// </summary>
    [Required(ErrorMessage = "Required permission is required.")]
    [MaxLength(60, ErrorMessage = "Required permission cannot exceed 60 characters.")]
    public string RequiredPermission { get; set; } = null!;

    /// <summary>
    /// False hides the report from the catalog without deleting the row, which
    /// keeps any saved views pointing at it intact.
    /// </summary>
    public bool IsActive { get; set; } = true;

    public int SortOrder { get; set; }
}
