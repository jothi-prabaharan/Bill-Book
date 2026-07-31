namespace Shared.Kernel.Numbering;

/// <summary>
/// How often a series restarts at its start number. Yearly means the
/// <em>financial</em> year, not the calendar year — an Indian invoice series
/// restarts on 1 April, so a calendar reset would break the sequence mid-year.
/// </summary>
public enum NumberResetFrequency
{
    Never = 0,
    Yearly = 1,
    Monthly = 2,
    Daily = 3,
}
