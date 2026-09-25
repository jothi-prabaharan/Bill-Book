using Microsoft.EntityFrameworkCore;
using WorkOrder.Repository;
using WorkOrder.Repository.SeedData;

namespace WorkOrder.Api.Services;

/// <summary>A branch's WRK series (S6, TK-66). Added only when missing.</summary>
public sealed class WorkOrderSeeder
{
    private readonly WorkOrderDbContext _db;

    public WorkOrderSeeder(WorkOrderDbContext db) => _db = db;

    public async Task<Dictionary<string, int>> SeedForOrganizationAsync(Guid orgId, CancellationToken ct)
    {
        bool has = await _db.NumberingSeries.IgnoreQueryFilters()
            .AnyAsync(n => n.OrgId == orgId && n.SeriesCode == WorkOrderSeed.SeriesCode, ct);
        if (!has)
        {
            _db.NumberingSeries.Add(WorkOrderSeed.Series(orgId));
            await _db.SaveChangesAsync(ct);
        }

        return new Dictionary<string, int> { ["numberingSeries"] = has ? 0 : 1 };
    }
}
