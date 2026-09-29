using Accounting.Entity.Enums;
using Accounting.Entity.Models;
using Accounting.Entity.TableEntities;
using Accounting.Repository.SeedData;
using Microsoft.EntityFrameworkCore;

namespace Accounting.Repository.Repositories;

/// <summary>
/// The TDS section master (TK-117): seeded per branch, editable, effective-dated.
/// A revision closes the current row's period and adds a row from the new date,
/// so a document is always taxed by the row in force on its own date.
/// </summary>
public sealed class TdsSectionRepository
{
    private readonly AccountingDbContext _db;

    public TdsSectionRepository(AccountingDbContext db) => _db = db;

    public async Task<List<TdsSectionView>> ListAsync(CancellationToken ct)
    {
        List<string> used = await _db.TdsEntries
            .Where(e => e.SectionCode != null)
            .Select(e => e.SectionCode!)
            .Distinct()
            .ToListAsync(ct);
        HashSet<string> inUse = new(used, StringComparer.OrdinalIgnoreCase);

        List<TdsSection> rows = await _db.TdsSections.AsNoTracking()
            .OrderBy(s => s.SectionCode)
            .ThenByDescending(s => s.EffectiveFrom)
            .ToListAsync(ct);

        return [.. rows.Select(s => new TdsSectionView
        {
            TdsSectionId = s.TdsSectionId,
            SectionCode = s.SectionCode,
            NatureOfPayment = s.NatureOfPayment,
            EffectiveFrom = s.EffectiveFrom,
            EffectiveTo = s.EffectiveTo,
            RateIndividual = s.RateIndividual,
            RateOther = s.RateOther,
            RateNoPan = s.RateNoPan,
            SingleThreshold = s.SingleThreshold,
            AggregateThreshold = s.AggregateThreshold,
            ThresholdBasis = s.ThresholdBasis.ToString(),
            IsActive = s.IsActive,
            IsUsed = inUse.Contains(s.SectionCode),
        })];
    }

    /// <summary>Whether the branch has an active row with this code, in any period.</summary>
    public async Task<bool> ExistsAsync(string code, CancellationToken ct)
    {
        string wanted = code.Trim().ToUpperInvariant();
        return await _db.TdsSections.AnyAsync(s => s.IsActive && s.SectionCode.ToUpper() == wanted, ct);
    }

    /// <summary>The active row for this code in force on <paramref name="on"/>, or null.</summary>
    public async Task<TdsSection?> InForceAsync(string code, DateOnly on, CancellationToken ct)
    {
        string wanted = code.Trim().ToUpperInvariant();
        return await _db.TdsSections.AsNoTracking()
            .Where(s => s.IsActive
                && s.SectionCode.ToUpper() == wanted
                && s.EffectiveFrom <= on
                && (s.EffectiveTo == null || s.EffectiveTo >= on))
            .OrderByDescending(s => s.EffectiveFrom)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<SaveTdsSectionResult> SaveAsync(long? id, SaveTdsSectionRequest request, CancellationToken ct)
    {
        string code = request.SectionCode.Trim();
        string upper = code.ToUpperInvariant();

        if (request.EffectiveTo is DateOnly to && to < request.EffectiveFrom)
        {
            return new SaveTdsSectionResult(SaveTdsSectionOutcome.BadPeriod);
        }

        TdsSection? row = null;
        if (id is long existing)
        {
            row = await _db.TdsSections.FirstOrDefaultAsync(s => s.TdsSectionId == existing, ct);
            if (row is null)
            {
                return new SaveTdsSectionResult(SaveTdsSectionOutcome.NotFound);
            }

            if (!string.Equals(row.SectionCode, code, StringComparison.OrdinalIgnoreCase)
                && await _db.TdsEntries.AnyAsync(e => e.SectionCode == row.SectionCode, ct))
            {
                return new SaveTdsSectionResult(SaveTdsSectionOutcome.CodeInUse, existing);
            }
        }

        // Two rows of one code may not be in force on the same day, or a
        // document's rate would depend on which the query happened to pick.
        List<TdsSection> siblings = await _db.TdsSections.AsNoTracking()
            .Where(s => s.SectionCode.ToUpper() == upper && s.TdsSectionId != (id ?? 0))
            .ToListAsync(ct);

        if (siblings.Any(s => s.EffectiveFrom == request.EffectiveFrom))
        {
            return new SaveTdsSectionResult(SaveTdsSectionOutcome.Duplicate);
        }

        DateOnly end = request.EffectiveTo ?? DateOnly.MaxValue;
        if (siblings.Any(s => s.EffectiveFrom <= end && (s.EffectiveTo ?? DateOnly.MaxValue) >= request.EffectiveFrom))
        {
            return new SaveTdsSectionResult(SaveTdsSectionOutcome.BadPeriod);
        }

        if (row is null)
        {
            row = new TdsSection();
            _db.TdsSections.Add(row);
        }

        row.SectionCode = code;
        row.NatureOfPayment = request.NatureOfPayment.Trim();
        row.EffectiveFrom = request.EffectiveFrom;
        row.EffectiveTo = request.EffectiveTo;
        row.RateIndividual = request.RateIndividual;
        row.RateOther = request.RateOther;
        row.RateNoPan = request.RateNoPan;
        row.SingleThreshold = request.SingleThreshold;
        row.AggregateThreshold = request.AggregateThreshold;
        row.ThresholdBasis = request.ThresholdBasis;
        row.IsActive = request.IsActive;

        await _db.SaveChangesAsync(ct);
        return new SaveTdsSectionResult(SaveTdsSectionOutcome.Ok, row.TdsSectionId);
    }

    /// <summary>
    /// Adds the seeded sections a branch does not have yet, by code. Idempotent:
    /// a code the branch already holds, in any period, is left as it is.
    /// </summary>
    public async Task<int> SeedForOrganizationAsync(Guid orgId, CancellationToken ct)
    {
        List<string> codes = await _db.TdsSections
            .IgnoreQueryFilters()
            .Where(s => s.OrgId == orgId)
            .Select(s => s.SectionCode)
            .ToListAsync(ct);
        HashSet<string> present = new(codes, StringComparer.OrdinalIgnoreCase);

        List<TdsSection> missing = [.. TdsSectionSeed.Build(orgId).Where(s => !present.Contains(s.SectionCode))];
        if (missing.Count == 0)
        {
            return 0;
        }

        _db.TdsSections.AddRange(missing);
        await _db.SaveChangesAsync(ct);
        return missing.Count;
    }
}
