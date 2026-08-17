using Reporting.Entity.Enums;

namespace Reporting.Entity.Models;

/// <summary>
/// A report as the catalog screen lists it.
///
/// <b>A report the caller may not run does not appear here at all</b>, rather than
/// appearing and refusing. Whether a report exists is itself information, and a
/// list of things you cannot have is a worse screen besides.
/// </summary>
public class ReportCatalogItemView
{
    public string ReportKey { get; set; } = null!;

    public string Title { get; set; } = null!;

    public ReportModule Module { get; set; }

    public string? Description { get; set; }

    public int SortOrder { get; set; }
}

/// <summary>
/// Everything needed to render a report's screen before running it: its
/// parameters, and every column it can show.
/// </summary>
public class ReportMetadataView
{
    public string ReportKey { get; set; } = null!;

    public string Title { get; set; } = null!;

    public ReportModule Module { get; set; }

    public string? Description { get; set; }

    public List<ReportParameterView> Parameters { get; set; } = [];

    public List<ReportColumnView> Columns { get; set; } = [];

    /// <summary>The columns selected before anybody chooses, in display order.</summary>
    public List<string> DefaultColumns { get; set; } = [];

    /// <summary>
    /// Set when the report defines a running balance or another window function.
    /// The grid disables sorting on everything else while such a column is shown,
    /// because the balance is only true of one ordering.
    /// </summary>
    public bool ForcesSortOrder { get; set; }
}

/// <summary>
/// One report parameter — a date range, an as-at date, a warehouse. Distinct from
/// a filter: a filter decides which rows survive, a parameter decides what the
/// report means.
/// </summary>
public class ReportParameterView
{
    public string Name { get; set; } = null!;

    public string Label { get; set; } = null!;

    public ColumnDataType DataType { get; set; }

    public bool IsRequired { get; set; }

    public string? DefaultValue { get; set; }
}
