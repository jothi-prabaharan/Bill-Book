using Payroll.Repository;
using Payroll.Repository.SeedData;
using Microsoft.EntityFrameworkCore;

namespace Payroll.Api.Services;

public sealed class PayrollSeeder
{
    private readonly PayrollDbContext _db;

    public PayrollSeeder(PayrollDbContext db) => _db = db;

    public async Task<Dictionary<string, int>> SeedForOrganizationAsync(Guid orgId, CancellationToken ct)
    {
        var seeded = new Dictionary<string, int>();

        seeded["payGroups"] = await AddWhenEmptyAsync(_db.PayGroups.IgnoreQueryFilters().AnyAsync(x => x.OrgId == orgId, ct), () => _db.PayGroups.Add(PayrollSeed.DefaultPayGroup(orgId)));
        seeded["components"] = await AddWhenEmptyAsync(_db.SalaryComponents.IgnoreQueryFilters().AnyAsync(x => x.OrgId == orgId, ct), () => _db.SalaryComponents.Add(PayrollSeed.BasicSalary(orgId)));

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
