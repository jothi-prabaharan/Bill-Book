using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Shared.Kernel.Numbering;

/// <summary>
/// Allocates numbers by compare-and-swap: a single UPDATE that moves the counter
/// only if it still holds the value we read. A lost race affects no rows, so we
/// re-read and try again.
///
/// Deliberately not read-max-then-increment — that is the pattern already noted
/// as a defect on CustomerCode, and under two concurrent invoices it hands out
/// the same number twice.
/// </summary>
public sealed class NumberGenerator : INumberGenerator
{
    private readonly DbContext _db;
    private readonly NumberingOptions _options;

    public NumberGenerator(DbContext db, IOptions<NumberingOptions> options)
    {
        _db = db;
        _options = options.Value;
    }

    public async Task<NumberAllocation> NextAsync(
        string seriesCode, long? branchId, DateOnly onDate, CancellationToken ct)
    {
        for (int attempt = 0; attempt < _options.MaxAllocationAttempts; attempt++)
        {
            NumberingSeries series = await ResolveAsync(seriesCode, branchId, ct)
                ?? throw new InvalidOperationException(
                    $"No active numbering series is configured for '{seriesCode}'. " +
                    "Add one under Settings › Numbering series.");

            bool resetDue = NumberFormat.IsResetDue(series, onDate, _options.FinancialYearStartMonth);
            long current = resetDue ? series.StartNumber : series.NextNumber;
            long expected = series.NextNumber;

            // Computed here rather than inside the SetProperty lambda, so the
            // update carries a plain value instead of an expression EF has to
            // pull apart.
            DateOnly lastResetOn = resetDue ? onDate : series.LastResetOn ?? onDate;

            // Guarded on NextNumber, so two requests reading the same value can
            // never both succeed. ExecuteUpdate runs one statement and joins the
            // caller's transaction, which is what keeps the series gapless when
            // the surrounding insert rolls back.
            int affected = await _db.Set<NumberingSeries>()
                .Where(s => s.NumberingSeriesId == series.NumberingSeriesId && s.NextNumber == expected)
                .ExecuteUpdateAsync(
                    set => set
                        .SetProperty(s => s.NextNumber, current + 1)
                        .SetProperty(s => s.LastResetOn, lastResetOn),
                    ct);

            if (affected == 1)
            {
                // The tracked copy is now stale — a later SaveChanges in the same
                // unit of work would otherwise write the old counter back.
                _db.Entry(series).State = EntityState.Detached;

                string code = NumberFormat.Compose(
                    series, current, onDate, _options.FinancialYearStartMonth);
                return new NumberAllocation(series.NumberingSeriesId, current, code);
            }

            _db.Entry(series).State = EntityState.Detached;
        }

        throw new InvalidOperationException(
            $"Could not allocate a number from series '{seriesCode}' after " +
            $"{_options.MaxAllocationAttempts} attempts. The series is under unusual contention.");
    }

    public async Task<string?> PeekAsync(
        string seriesCode, long? branchId, DateOnly onDate, CancellationToken ct)
    {
        NumberingSeries? series = await ResolveAsync(seriesCode, branchId, ct);
        if (series is null)
        {
            return null;
        }

        long next = NumberFormat.IsResetDue(series, onDate, _options.FinancialYearStartMonth)
            ? series.StartNumber
            : series.NextNumber;

        return NumberFormat.Compose(series, next, onDate, _options.FinancialYearStartMonth);
    }

    /// <summary>
    /// A series for this branch wins over the org-wide one; among equals the
    /// default wins. Ordering rather than filtering, so a branch with no series
    /// of its own still numbers from the org series instead of failing.
    /// </summary>
    private Task<NumberingSeries?> ResolveAsync(
        string seriesCode, long? branchId, CancellationToken ct) =>
        _db.Set<NumberingSeries>()
            .Where(s => s.SeriesCode == seriesCode
                && s.IsActive
                && (s.BranchId == null || s.BranchId == branchId))
            .OrderByDescending(s => s.BranchId != null)
            .ThenByDescending(s => s.IsDefault)
            .ThenBy(s => s.DisplayOrder)
            .ThenBy(s => s.NumberingSeriesId)
            .FirstOrDefaultAsync(ct);
}
