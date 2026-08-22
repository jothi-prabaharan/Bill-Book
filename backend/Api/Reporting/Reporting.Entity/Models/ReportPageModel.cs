using System.ComponentModel.DataAnnotations;

namespace Reporting.Entity.Models;

/// <summary>
/// Which page, and how big.
///
/// <b>The count is the expensive half.</b> Totalling twelve thousand ledger rows
/// costs a second scan, so it is asked for on the first page of a query and
/// carried in the client's state until a filter changes it.
/// </summary>
public class ReportPageModel
{
    [Range(1, int.MaxValue, ErrorMessage = "Page number starts at 1.")]
    public int Number { get; set; } = 1;

    /// <summary>
    /// Rows per page. Two hundred is the ceiling: beyond it the response stops
    /// being a page and the browser stops being able to render it smoothly.
    /// </summary>
    [Range(1, 200, ErrorMessage = "Page size must be between 1 and 200 rows.")]
    public int Size { get; set; } = 50;

    /// <summary>
    /// Whether to count the whole result. False on subsequent pages of the same
    /// query, which takes three queries down to two.
    /// </summary>
    public bool IncludeCount { get; set; } = true;
}
