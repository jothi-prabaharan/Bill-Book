using Microsoft.EntityFrameworkCore;
using Printing.Entity.TableEntities;
using Printing.Repository;
using Printing.Repository.SeedData;

namespace Printing.Api.Services;

/// <summary>
/// Writes a branch's starting print templates into <c>prt</c>.
///
/// <b>Re-runnable, and meant to be.</b> It adds only the document types the
/// branch is missing, so running it against a branch created months ago
/// backfills whatever has been added to the catalogue since — and is how a
/// branch that existed before Printing took templates over from Master gets
/// its set (TK-81: re-seeded rather than copied).
/// </summary>
public sealed class PrintTemplateSeeder
{
    private readonly PrintingDbContext _db;

    public PrintTemplateSeeder(PrintingDbContext db) => _db = db;

    public async Task<int> SeedForOrganizationAsync(Guid orgId, CancellationToken ct)
    {
        List<string> present = await _db.PrintTemplates
            .IgnoreQueryFilters()
            .Where(t => t.OrgId == orgId)
            .Select(t => t.DocumentTypeCode)
            .ToListAsync(ct);

        IReadOnlyList<PrintTemplate> missing = PrintTemplateSeed.Build(orgId, present);
        if (missing.Count == 0)
        {
            return 0;
        }

        _db.PrintTemplates.AddRange(missing);
        await _db.SaveChangesAsync(ct);
        return missing.Count;
    }
}
