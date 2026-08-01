namespace Shared.Kernel.Numbering;

/// <summary>Settings the number generator needs that are not on the series row.</summary>
public sealed class NumberingOptions
{
    /// <summary>
    /// Fallback financial year start month — 4 for India.
    ///
    /// The real value is the branch's own, read through
    /// <see cref="IFinancialYearProvider"/>. This is what is used when there is
    /// no branch to ask about (seeding, design time) or when Platform cannot be
    /// reached — refusing to allocate a code because a settings lookup failed
    /// would be the worse outcome.
    /// </summary>
    public int FinancialYearStartMonth { get; set; } = 4;

    /// <summary>
    /// How many times to retry when another request wins the counter. Contention
    /// is brief — a lost race retries against a value that has already moved.
    /// </summary>
    public int MaxAllocationAttempts { get; set; } = 8;
}
