using System.ComponentModel.DataAnnotations;

namespace Reporting.Entity.Models;

/// <summary>
/// What the grid asks for. One request shape for all forty-five reports —
/// <see cref="Parameters"/> is the only report-specific part, and the report's
/// metadata declares what belongs in it.
///
/// <b>This arrives by POST, and that is deliberate for a read.</b> A filter with
/// an <c>In</c> list of two hundred item ids does not fit a query string, and a
/// URL that long is refused by proxies before it reaches the gateway. The action
/// is idempotent and is permissioned as a read.
///
/// The limits below are not arbitrary politeness. Every one of them is a bound on
/// work the database would otherwise be asked to do on the say-so of whoever
/// wrote the request.
/// </summary>
public class ReportQueryRequest
{
    /// <summary>
    /// Report-specific parameters — date range, as-at date, warehouse, whether to
    /// include zero balances. Keys are declared by the report's metadata; a key
    /// the report does not declare is rejected rather than ignored, because a
    /// silently dropped date range produces a report for all time that looks
    /// exactly like a report for the period asked for.
    /// </summary>
    public Dictionary<string, string?> Parameters { get; set; } = [];

    /// <summary>
    /// Which columns, in display order. Empty means the report's defaults.
    /// </summary>
    [MaxLength(60, ErrorMessage = "A report can show at most 60 columns at once.")]
    public List<string> Columns { get; set; } = [];

    [MaxLength(50, ErrorMessage = "A report can have at most 50 filters at once.")]
    public List<ReportFilterModel> Filters { get; set; } = [];

    /// <summary>
    /// Sort keys in priority order. More than five is beyond what anyone can
    /// read, and each one costs the database an ordering.
    /// </summary>
    [MaxLength(5, ErrorMessage = "A report can be sorted by at most 5 columns.")]
    public List<ReportSortModel> Sorts { get; set; } = [];

    /// <summary>
    /// Grouping keys, outermost first. Three levels is the limit: past that the
    /// footers outnumber the rows.
    /// </summary>
    [MaxLength(3, ErrorMessage = "A report can be grouped by at most 3 columns.")]
    public List<string> GroupBy { get; set; } = [];

    /// <summary>
    /// When present, <see cref="GroupBy"/> and <see cref="Columns"/> are ignored —
    /// a pivot declares its own shape, and the response says what its columns
    /// turned out to be.
    /// </summary>
    public ReportPivotModel? Pivot { get; set; }

    public ReportPageModel Page { get; set; } = new();

    public ReportFreezeModel Freeze { get; set; } = new();
}
