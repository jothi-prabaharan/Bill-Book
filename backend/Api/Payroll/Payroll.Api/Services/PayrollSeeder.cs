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
        seeded["pf"] = await AddWhenEmptyAsync(_db.PfSettings.IgnoreQueryFilters().AnyAsync(x => x.OrgId == orgId, ct), () => _db.PfSettings.Add(PayrollSeed.DefaultPf(orgId)));
        seeded["esi"] = await AddWhenEmptyAsync(_db.EsiSettings.IgnoreQueryFilters().AnyAsync(x => x.OrgId == orgId, ct), () => _db.EsiSettings.Add(PayrollSeed.DefaultEsi(orgId)));
        seeded["gratuity"] = await AddWhenEmptyAsync(_db.GratuitySettings.IgnoreQueryFilters().AnyAsync(x => x.OrgId == orgId, ct), () => _db.GratuitySettings.Add(PayrollSeed.DefaultGratuity(orgId)));
        seeded["bonus"] = await AddWhenEmptyAsync(_db.BonusSettings.IgnoreQueryFilters().AnyAsync(x => x.OrgId == orgId, ct), () => _db.BonusSettings.Add(PayrollSeed.DefaultBonus(orgId)));

        seeded["ptSlabs"] = await AddWhenEmptyAsync(_db.ProfessionalTaxSlabs.IgnoreQueryFilters().AnyAsync(x => x.OrgId == orgId, ct), () => _db.ProfessionalTaxSlabs.AddRange(PayrollSeed.DefaultPtSlabs(orgId)));
        seeded["lwf"] = await AddWhenEmptyAsync(_db.LwfSettings.IgnoreQueryFilters().AnyAsync(x => x.OrgId == orgId, ct), () => _db.LwfSettings.AddRange(PayrollSeed.DefaultLwf(orgId)));

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
