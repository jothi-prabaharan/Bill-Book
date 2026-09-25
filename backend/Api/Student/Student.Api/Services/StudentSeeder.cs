using Microsoft.EntityFrameworkCore;
using Student.Repository;
using Student.Repository.SeedData;

namespace Student.Api.Services;

/// <summary>
/// A branch's classes LKG–XII and its ADM series (S1, TK-61). Re-runnable:
/// each part is added only when the branch has none, so a retry adds nothing twice.
/// </summary>
public sealed class StudentSeeder
{
    private readonly StudentDbContext _db;

    public StudentSeeder(StudentDbContext db) => _db = db;

    public async Task<Dictionary<string, int>> SeedForOrganizationAsync(Guid orgId, CancellationToken ct)
    {
        var seeded = new Dictionary<string, int>();

        if (await _db.SchoolClasses.IgnoreQueryFilters().AnyAsync(c => c.OrgId == orgId, ct))
        {
            seeded["classes"] = 0;
        }
        else
        {
            var classes = StudentSeed.Classes(orgId);
            _db.SchoolClasses.AddRange(classes);
            seeded["classes"] = classes.Count;
        }

        bool hasSeries = await _db.NumberingSeries.IgnoreQueryFilters()
            .AnyAsync(n => n.OrgId == orgId && n.SeriesCode == StudentSeed.AdmissionSeriesCode, ct);
        if (!hasSeries)
        {
            _db.NumberingSeries.Add(StudentSeed.AdmissionSeries(orgId));
        }

        seeded["numberingSeries"] = hasSeries ? 0 : 1;

        await _db.SaveChangesAsync(ct);
        return seeded;
    }
}
