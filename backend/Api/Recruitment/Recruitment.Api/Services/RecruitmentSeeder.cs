using Microsoft.EntityFrameworkCore;
using Recruitment.Repository;
using Shared.Kernel.Numbering;

namespace Recruitment.Api.Services;

public sealed class RecruitmentSeeder
{
    private readonly RecruitmentDbContext _db;

    public RecruitmentSeeder(RecruitmentDbContext db) => _db = db;

    public async Task<Dictionary<string, int>> SeedAsync(Guid customerId, Guid orgId, CancellationToken ct)
    {
        var seeded = new Dictionary<string, int>();

        seeded["numberingSeries"] = await AddWhenEmptyAsync(
            _db.NumberingSeries.IgnoreQueryFilters().AnyAsync(n => n.OrgId == orgId && n.SeriesCode == "REQ", ct),
            () => _db.NumberingSeries.Add(new NumberingSeries
            {
                CustomerId = customerId,
                OrgId = orgId,
                SeriesCode = "REQ",
                SeriesName = "Job Requisitions",
                SeriesSystemName = "JOB_REQUISITION",
                SeriesFor = SeriesFor.Document,
                Prefix = "REQ",
                Separator = "-",
                IncludeFinancialYear = true,
                FinancialYearFormat = FinancialYearFormat.Compact,
                NumberLength = 5,
                NextNumber = 1,
                IsActive = true,
            }));

        await _db.SaveChangesAsync(ct);
        return seeded;
    }

    private static async Task<int> AddWhenEmptyAsync(Task<bool> exists, Action add)
    {
        if (await exists) return 0;
        add();
        return 1;
    }
}
