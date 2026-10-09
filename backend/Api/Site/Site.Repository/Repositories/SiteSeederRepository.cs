using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Numbering;

namespace Site.Repository.Repositories;

/// <summary>
/// Seeds Site services document series (SIT, SDP, SRT, SUS, SBL) for an organization (TK-169).
/// </summary>
public sealed class SiteSeederRepository
{
    private readonly SiteDbContext _db;

    public SiteSeederRepository(SiteDbContext db) => _db = db;

    public async Task<int> SeedNumberingSeriesAsync(Guid orgId, CancellationToken ct)
    {
        List<NumberingSeries> existing = await _db.NumberingSeries
            .IgnoreQueryFilters()
            .Where(s => s.OrgId == orgId)
            .ToListAsync(ct);

        HashSet<string> present = [.. existing
            .Where(s => s.SeriesSystemName is not null)
            .Select(s => s.SeriesSystemName!)];

        HashSet<string> taken = [.. existing.Select(s => s.SeriesName)];

        HashSet<string> defaulted = [.. existing
            .Where(s => s.IsDefault)
            .Select(s => s.SeriesCode)];

        List<NumberingSeries> missing = SeedData.NumberingSeriesSeed.Build(orgId)
            .Where(s => s.SeriesSystemName is not null
                && !present.Contains(s.SeriesSystemName)
                && !taken.Contains(s.SeriesName))
            .ToList();

        if (missing.Count == 0)
        {
            return 0;
        }

        foreach (NumberingSeries series in missing.Where(s => defaulted.Contains(s.SeriesCode)))
        {
            series.IsDefault = false;
        }

        _db.NumberingSeries.AddRange(missing);
        await _db.SaveChangesAsync(ct);
        return missing.Count;
    }
}
