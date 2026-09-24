using Hrm.Repository;
using Hrm.Repository.SeedData;
using Microsoft.EntityFrameworkCore;

namespace Hrm.Api.Services;

/// <summary>
/// A branch's starting organisation and its <c>EMP</c> series (TK-48).
/// Re-runnable: each part is added only when the branch has none, so a retry
/// from <c>apps/admin</c> or a second app's trial adds nothing twice.
/// </summary>
public sealed class HrmSeeder
{
    private readonly HrmDbContext _db;

    public HrmSeeder(HrmDbContext db) => _db = db;

    public async Task<Dictionary<string, int>> SeedForOrganizationAsync(Guid orgId, CancellationToken ct)
    {
        var seeded = new Dictionary<string, int>();

        seeded["departments"] = await AddWhenEmptyAsync(_db.Departments.IgnoreQueryFilters().AnyAsync(x => x.OrgId == orgId, ct), () => _db.Departments.Add(HrmSeed.Department(orgId)));
        seeded["designations"] = await AddWhenEmptyAsync(_db.Designations.IgnoreQueryFilters().AnyAsync(x => x.OrgId == orgId, ct), () => _db.Designations.Add(HrmSeed.Designation(orgId)));
        seeded["grades"] = await AddWhenEmptyAsync(_db.Grades.IgnoreQueryFilters().AnyAsync(x => x.OrgId == orgId, ct), () => _db.Grades.Add(HrmSeed.Grade(orgId)));
        seeded["workLocations"] = await AddWhenEmptyAsync(_db.WorkLocations.IgnoreQueryFilters().AnyAsync(x => x.OrgId == orgId, ct), () => _db.WorkLocations.Add(HrmSeed.WorkLocation(orgId)));
        seeded["numberingSeries"] = await AddWhenEmptyAsync(
            _db.NumberingSeries.IgnoreQueryFilters().AnyAsync(n => n.OrgId == orgId && n.SeriesCode == HrmSeed.EmployeeSeriesCode, ct),
            () => _db.NumberingSeries.Add(HrmSeed.EmployeeSeries(orgId)));

        await _db.SaveChangesAsync(ct);
        return seeded;
    }

    private static async Task<int> AddWhenEmptyAsync(Task<bool> exists, Action add)
    {
        if (await exists)
        {
            return 0;
        }

        add();
        return 1;
    }
}
