namespace Shared.Kernel.Numbering;

/// <summary>Settings the number generator needs that are not on the series row.</summary>
public sealed class NumberingOptions
{
    /// <summary>
    /// The organization's financial year start month — 4 for India. Decides the
    /// financial-year segment and the yearly reset boundary.
    ///
    /// TODO: read from plt.Organizations.FinancialYearStartMonth per request.
    /// It lives in the master database, so it needs the same cached Platform
    /// lookup the tenant directory already does; until then this is configured
    /// per service and an organization on a different year would number wrongly.
    /// </summary>
    public int FinancialYearStartMonth { get; set; } = 4;

    /// <summary>
    /// How many times to retry when another request wins the counter. Contention
    /// is brief — a lost race retries against a value that has already moved.
    /// </summary>
    public int MaxAllocationAttempts { get; set; } = 8;
}
