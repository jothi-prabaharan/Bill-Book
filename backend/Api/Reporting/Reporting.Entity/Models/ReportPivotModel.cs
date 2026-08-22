using System.ComponentModel.DataAnnotations;
using Reporting.Entity.Enums;

namespace Reporting.Entity.Models;

/// <summary>
/// A pivot: what goes down the side, what goes across the top, and what fills the
/// cells.
///
/// <b>The column axis is data-dependent</b>, which is what makes a pivot different
/// from grouping. Pivoting sales by month produces as many columns as there are
/// months with data, so the response declares its columns and the grid renders
/// whatever it is told rather than what it expected.
/// </summary>
public class ReportPivotModel
{
    /// <summary>Down the side. Each becomes a row group.</summary>
    [MaxLength(3, ErrorMessage = "A pivot can have at most 3 row fields.")]
    public List<string> Rows { get; set; } = [];

    /// <summary>
    /// Across the top. Two is already a lot — the columns multiply.
    /// </summary>
    [MaxLength(2, ErrorMessage = "A pivot can have at most 2 column fields.")]
    public List<string> Columns { get; set; } = [];

    [MinLength(1, ErrorMessage = "A pivot needs at least one value field.")]
    [MaxLength(5, ErrorMessage = "A pivot can have at most 5 value fields.")]
    public List<ReportPivotValueModel> Values { get; set; } = [];
}

/// <summary>What a pivot cell computes, and from which column.</summary>
public class ReportPivotValueModel
{
    [Required(ErrorMessage = "A pivot value must name a column.")]
    [MaxLength(60, ErrorMessage = "Column key cannot exceed 60 characters.")]
    public string Column { get; set; } = null!;

    public AggregateFunction Aggregate { get; set; } = AggregateFunction.Sum;
}
