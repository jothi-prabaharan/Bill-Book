using System.ComponentModel.DataAnnotations;
using Reporting.Entity.Enums;

namespace Reporting.Entity.TableEntities;

/// <summary>
/// One column of one report, as the imported specification declares it —
/// <c>reports.json</c> read into a table.
///
/// <b>Not to be confused with <c>ReportDetail</c>, which is the catalog a branch
/// actually runs on.</b> A <c>ReportDetail</c> row belongs to a branch and may be
/// renamed, reordered or switched off by whoever owns those books. This row is
/// the specification behind it: global reference data, the same for everybody,
/// carrying no tenant column and never edited by a customer. The two are compared
/// when a report is built, which is the whole reason this table holds the same
/// presentation properties under the same names.
/// </summary>
public class ReportColumn
{
    public long Id { get; set; }

    public long ReportMasterId { get; set; }

    [Required(ErrorMessage = "ColumnName is required.")]
    [MaxLength(255, ErrorMessage = "ColumnName cannot exceed 255 characters.")]
    public string ColumnName { get; set; } = null!;

    [MaxLength(255, ErrorMessage = "DisplayName cannot exceed 255 characters.")]
    public string? DisplayName { get; set; }

    /// <summary>
    /// What the column holds, which decides the filter operators offered, the
    /// grid's formatting and the Excel writer's cell format.
    /// </summary>
    public ColumnDataType DataType { get; set; }

    public ColumnAlignment Alignment { get; set; } = ColumnAlignment.Left;

    /// <summary>Preferred width in pixels. Null lets the grid decide.</summary>
    public int? Width { get; set; }

    public int Order { get; set; }

    public bool IsFilterable { get; set; } = true;

    public bool IsSortable { get; set; } = true;

    /// <summary>
    /// Grouping keys are text only. The constraint is the query builder's rather
    /// than a preference — grouping concatenates in SQL, and a date or an enum
    /// would be rendered in a format nobody chose and which moves with the
    /// server's settings.
    /// </summary>
    public bool IsGroupable { get; set; }

    /// <summary>
    /// Whether the column can carry a pivot axis. A subset of
    /// <see cref="IsGroupable"/>: the column axis is data-dependent and capped at
    /// 200 distinct values, so only a low-cardinality dimension belongs on one.
    /// </summary>
    public bool IsPivotable { get; set; }

    public ReportMaster ReportMaster { get; set; } = null!;
}
