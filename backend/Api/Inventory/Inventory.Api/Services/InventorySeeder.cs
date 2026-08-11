using Inventory.Repository;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Numbering;

namespace Inventory.Api.Services;

/// <summary>
/// Inventory's own document numbering series for a newly created organization —
/// today, <c>STA</c>.
///
/// <b>Re-runnable, and meant to be.</b> Only the series the organization is
/// missing are added, so calling this against a branch created months ago
/// backfills whatever has been added to the seed list since. That is what makes
/// the stock adjustment usable on branches that existed before it did: nobody
/// has to migrate them, the next seed call gives them the series. The count
/// returned is what was actually written, which is zero on a branch already
/// complete.
/// </summary>
public sealed class InventorySeeder
{
    private readonly InventoryDbContext _db;

    public InventorySeeder(InventoryDbContext db) => _db = db;

    public async Task<int> SeedNumberingSeriesAsync(Guid orgId, CancellationToken ct)
    {
        // IgnoreQueryFilters because seeding runs for an organization the request
        // is not scoped to — the tenant comes from the body, not a token.
        List<NumberingSeries> existing = await _db.NumberingSeries
            .IgnoreQueryFilters()
            .Where(s => s.OrgId == orgId)
            .ToListAsync(ct);

        HashSet<string> present = [.. existing
            .Where(s => s.SeriesSystemName is not null)
            .Select(s => s.SeriesSystemName!)];

        // SeriesName is unique per organization, so a customer-created series
        // holding a seed row's name would fail the insert for all of them.
        HashSet<string> taken = [.. existing.Select(s => s.SeriesName)];

        // IsDefault is unique per SeriesCode rather than per organization, so it
        // is checked per code — Accounting's JRN default and this STA default
        // coexist.
        HashSet<string> defaulted = [.. existing
            .Where(s => s.IsDefault)
            .Select(s => s.SeriesCode)];

        List<NumberingSeries> missing = Repository.SeedData.NumberingSeriesSeed.Build(orgId)
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
