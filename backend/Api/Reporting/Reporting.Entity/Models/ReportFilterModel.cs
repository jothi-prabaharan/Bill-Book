using System.ComponentModel.DataAnnotations;
using Reporting.Entity.Enums;

namespace Reporting.Entity.Models;

/// <summary>
/// One filter clause. Clauses combine with AND — there is no OR, and no
/// parenthesising, because every report screen this replaces had neither and
/// nobody asked.
///
/// <b><see cref="Column"/> is checked against the report's column map before it
/// reaches the database.</b> A key the report does not declare is a 400. That
/// check is the reason an arbitrary request from a browser is safe: no string
/// from the caller is ever used to build SQL, only to look up a member expression
/// the report itself declared.
/// </summary>
public class ReportFilterModel
{
    [Required(ErrorMessage = "A filter must name a column.")]
    [MaxLength(60, ErrorMessage = "Column key cannot exceed 60 characters.")]
    public string Column { get; set; } = null!;

    public FilterOperator Operator { get; set; }

    /// <summary>
    /// The comparison value, as text — parsed against the column's declared type
    /// rather than guessed at. Null for <see cref="FilterOperator.IsNull"/> and
    /// <see cref="FilterOperator.IsNotNull"/>.
    /// </summary>
    public string? Value { get; set; }

    /// <summary>The upper bound, for <see cref="FilterOperator.Between"/> only. Inclusive.</summary>
    public string? Value2 { get; set; }

    /// <summary>
    /// The candidates, for <see cref="FilterOperator.In"/> and
    /// <see cref="FilterOperator.NotIn"/>. Capped because each entry becomes a
    /// parameter, and a query with ten thousand of them is a query that will not
    /// plan.
    /// </summary>
    [MaxLength(500, ErrorMessage = "An 'in' filter can list at most 500 values.")]
    public List<string> Values { get; set; } = [];
}
