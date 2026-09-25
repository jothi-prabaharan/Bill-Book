using Fee.Repository;
using Fee.Repository.SeedData;
using Microsoft.EntityFrameworkCore;

namespace Fee.Api.Services;

/// <summary>A branch's starting fee heads and its FDM and FRC series (S4, TK-64). Each part only when missing.</summary>
public sealed class FeeSeeder
{
    private readonly FeeDbContext _db;

    public FeeSeeder(FeeDbContext db) => _db = db;

    public async Task<Dictionary<string, int>> SeedForOrganizationAsync(Guid orgId, CancellationToken ct)
    {
        var seeded = new Dictionary<string, int>();

        if (await _db.FeeHeads.IgnoreQueryFilters().AnyAsync(h => h.OrgId == orgId, ct))
        {
            seeded["feeHeads"] = 0;
        }
        else
        {
            var heads = FeeSeed.Heads(orgId);
            _db.FeeHeads.AddRange(heads);
            seeded["feeHeads"] = heads.Count;
        }

        int series = 0;
        foreach ((string code, Func<Guid, Shared.Kernel.Numbering.NumberingSeries> make) in new (string, Func<Guid, Shared.Kernel.Numbering.NumberingSeries>)[]
        {
            (FeeSeed.DemandSeriesCode, FeeSeed.DemandSeries),
            (FeeSeed.ReceiptSeriesCode, FeeSeed.ReceiptSeries),
        })
        {
            if (!await _db.NumberingSeries.IgnoreQueryFilters().AnyAsync(n => n.OrgId == orgId && n.SeriesCode == code, ct))
            {
                _db.NumberingSeries.Add(make(orgId));
                series++;
            }
        }

        seeded["numberingSeries"] = series;
        await _db.SaveChangesAsync(ct);
        return seeded;
    }
}
