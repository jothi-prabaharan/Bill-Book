using Reporting.Entity.Enums;

namespace Reporting.Entity.Models;

/// <summary>
/// A column as the grid needs to know it: what to call it, how to draw it, and
/// what may be done to it.
///
/// This is the merge of two authorities. What the column <i>means</i> comes from
/// the report's <c>IReportSource</c>; how it <i>appears</i> comes from its
/// <c>rpt.ReportDetails</c> row. Where they disagree about which columns exist at
/// all, the service refuses to start rather than quietly serving a report with a
/// column nobody can choose.
/// </summary>
public class ReportColumnView
{
    public string Key { get; set; } = null!;

    /// <summary>
    /// Already substituted: a seeded header of "Debit(%CurCode%)" arrives here as
    /// "Debit(INR)". The client never sees the placeholder.
    /// </summary>
    public string Header { get; set; } = null!;

    public ColumnDataType DataType { get; set; }

    public ColumnAlignment Alignment { get; set; }

    public int? Width { get; set; }

    public bool IsFilterable { get; set; }

    public bool IsSortable { get; set; }

    public bool IsGroupable { get; set; }

    public bool IsPivotable { get; set; }

    public AggregateFunction Aggregate { get; set; }

    /// <summary>Leads the card at ~360px, where a row cannot be a row.</summary>
    public bool IsPrimary { get; set; }
}
