namespace Shared.Kernel.Numbering;

/// <summary>
/// Allocates the next code from a series.
///
/// Call this <b>inside the transaction that writes the record</b>, at save time
/// and not when the form opens. Both matter: allocating at form-open leaves a
/// gap every time a draft is abandoned, and allocating outside the transaction
/// leaves one every time the insert fails.
/// </summary>
public interface INumberGenerator
{
    /// <summary>
    /// Consumes one number and returns the composed code. A branch series is
    /// scoped to the caller's organization, which is the branch.
    /// </summary>
    /// <param name="onDate">
    /// The document's own date, not today — it decides the financial-year segment
    /// and whether the counter is due to restart.
    /// </param>
    Task<NumberAllocation> NextAsync(
        string seriesCode, DateOnly onDate, CancellationToken ct);

    /// <summary>
    /// The code the next allocation would produce, without consuming it. For
    /// previews only — two callers can preview the same value.
    /// </summary>
    Task<string?> PeekAsync(
        string seriesCode, DateOnly onDate, CancellationToken ct);
}

/// <summary>The allocated code and the series it came from.</summary>
public sealed record NumberAllocation(long NumberingSeriesId, long Number, string Code);
