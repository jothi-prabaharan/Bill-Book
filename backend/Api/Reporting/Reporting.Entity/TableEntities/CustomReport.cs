using System.ComponentModel.DataAnnotations;
using Shared.Kernel.Tenancy;

namespace Reporting.Entity.TableEntities;

/// <summary>
/// A report somebody built from a dataset (TK-113): which dataset it reads,
/// and the columns, filters, grouping and sorting they chose.
///
/// <b>It runs as its dataset, under the dataset's permission.</b> Saving a
/// purchase-lines report and sharing it does not show purchases to someone
/// without <c>purchase.view</c>; the catalog leaves it out for them, as it
/// leaves out the dataset.
/// </summary>
public class CustomReport : OrgScopedEntity
{
    public long CustomReportId { get; set; }

    /// <summary>The dataset's report key, <c>dataset-sales-lines</c> say.</summary>
    [Required(ErrorMessage = "Dataset is required.")]
    [MaxLength(80, ErrorMessage = "Dataset cannot exceed 80 characters.")]
    public string DatasetKey { get; set; } = null!;

    [Required(ErrorMessage = "Title is required.")]
    [MaxLength(120, ErrorMessage = "Title cannot exceed 120 characters.")]
    public string Title { get; set; } = null!;

    [MaxLength(500, ErrorMessage = "Description cannot exceed 500 characters.")]
    public string? Description { get; set; }

    /// <summary>
    /// The layout as JSONB, the same <c>ReportQueryRequest</c> shape a saved
    /// view holds, so a custom report can say nothing the grid cannot.
    /// </summary>
    [Required(ErrorMessage = "Definition is required.")]
    public string DefinitionJson { get; set; } = null!;

    /// <summary>
    /// Whose report it is. <b>Null means the whole branch sees it</b>, which
    /// needs <c>reports.edit</c> to set. No foreign key: users are in the master database.
    /// </summary>
    public Guid? OwnerUserId { get; set; }

    /// <summary>Deleting a custom report deactivates it.</summary>
    public bool IsActive { get; set; } = true;
}
