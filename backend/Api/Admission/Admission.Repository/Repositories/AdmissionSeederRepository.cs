using Admission.Repository;
using Admission.Repository.SeedData;
using Microsoft.EntityFrameworkCore;

namespace Admission.Repository.Repositories;

/// <summary>A branch's APL series (S2, TK-62). Re-runnable: added only when missing.</summary>
public sealed class AdmissionSeederRepository
{
    private readonly AdmissionDbContext _db;

    public AdmissionSeederRepository(AdmissionDbContext db) => _db = db;

    public async Task<Dictionary<string, int>> SeedForOrganizationAsync(Guid orgId, CancellationToken ct)
    {
        bool has = await _db.NumberingSeries.IgnoreQueryFilters()
            .AnyAsync(n => n.OrgId == orgId && n.SeriesCode == AdmissionSeed.ApplicationSeriesCode, ct);
        if (!has)
        {
            _db.NumberingSeries.Add(AdmissionSeed.ApplicationSeries(orgId));
            await _db.SaveChangesAsync(ct);
        }

        return new Dictionary<string, int> { ["numberingSeries"] = has ? 0 : 1 };
    }
}
