namespace Reporting.Entity.Models;

/// <summary>
/// What stays put when the grid scrolls. Presentation, carried on the request so
/// that a saved view restores it and an export can honour it — a frozen header
/// becomes a frozen pane in the .xlsx.
/// </summary>
public class ReportFreezeModel
{
    public bool Header { get; set; } = true;

    /// <summary>
    /// How many leading columns stay put. Zero by default; Batch Tracking Detail
    /// wants two, which is why this is a count rather than a flag.
    /// </summary>
    public int Columns { get; set; }
}
