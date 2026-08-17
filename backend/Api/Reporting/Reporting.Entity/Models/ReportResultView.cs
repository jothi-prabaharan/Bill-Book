namespace Reporting.Entity.Models;

/// <summary>
/// One page of a report, and everything needed to render it.
///
/// <b><see cref="Columns"/> is echoed rather than assumed</b>, because a pivot's
/// columns are only known once the query has run. The grid renders what this says
/// and never what it asked for.
///
/// <b><see cref="GrandTotal"/> spans the whole result, not the page.</b> A page
/// total is a number nobody wants and several people would misread.
/// </summary>
public class ReportResultView
{
    public string ReportKey { get; set; } = null!;

    public string Title { get; set; } = null!;

    /// <summary>
    /// When the query ran. On a printed or exported copy this is the difference
    /// between a figure somebody can date and a figure they cannot.
    /// </summary>
    public DateTimeOffset GeneratedAt { get; set; }

    /// <summary>The branch's base currency, for formatting and for %CurCode% headers.</summary>
    public ReportCurrencyView Currency { get; set; } = new();

    public List<ReportColumnView> Columns { get; set; } = [];

    /// <summary>
    /// The rows, each a map of column key to value. Loosely typed on purpose: a
    /// pivot has no fixed shape, and forty-five reports would otherwise need
    /// forty-five response types for no gain the client can use.
    /// </summary>
    public List<Dictionary<string, object?>> Rows { get; set; } = [];

    /// <summary>
    /// Subtotals per group, computed over the whole result rather than the page —
    /// which is what makes a subtotal right when its group spans a page boundary.
    /// </summary>
    public List<ReportGroupFooterView> GroupFooters { get; set; } = [];

    public ReportGroupFooterView? GrandTotal { get; set; }

    public ReportPageView Page { get; set; } = new();

    /// <summary>
    /// Set when a limit stopped the result short — a pivot column axis over its
    /// ceiling, or an export over the row cap. <see cref="TruncationReason"/> says
    /// which, in words meant for the person who asked.
    /// </summary>
    public bool Truncated { get; set; }

    public string? TruncationReason { get; set; }
}

/// <summary>The base currency a report's money columns are expressed in.</summary>
public class ReportCurrencyView
{
    public string Code { get; set; } = "INR";

    public int Decimals { get; set; } = 2;
}

/// <summary>
/// One group's footer. <see cref="Path"/> is the group key from the outermost
/// level inwards, so ["Asset", "Cash"] is the Cash group inside Assets.
/// </summary>
public class ReportGroupFooterView
{
    public List<string?> Path { get; set; } = [];

    public long RowCount { get; set; }

    /// <summary>Column key to aggregated value, for the columns that declare an aggregate.</summary>
    public Dictionary<string, decimal?> Aggregates { get; set; } = [];
}

/// <summary>
/// Where this page sits. <see cref="TotalRows"/> and <see cref="TotalPages"/> are
/// null when the request declined the count.
/// </summary>
public class ReportPageView
{
    public int Number { get; set; }

    public int Size { get; set; }

    public long? TotalRows { get; set; }

    public int? TotalPages { get; set; }
}
