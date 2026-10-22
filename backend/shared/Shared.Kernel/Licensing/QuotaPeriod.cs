namespace Shared.Kernel.Licensing;

/// <summary>
/// Which month a document counts in (D-69). Pure.
///
/// The IST calendar month of its creation, because the customers are Indian
/// businesses whose month turns at their midnight, not at Greenwich's. India has
/// no daylight saving, so a fixed offset is exact and needs no time-zone database
/// on the host.
/// </summary>
public static class QuotaPeriod
{
    private static readonly TimeSpan IndiaOffset = TimeSpan.FromMinutes(330);

    /// <summary>The first day of the IST month that <paramref name="moment"/> falls in.</summary>
    public static DateOnly StartOf(DateTimeOffset moment)
    {
        DateTimeOffset india = moment.ToOffset(IndiaOffset);
        return new DateOnly(india.Year, india.Month, 1);
    }
}
