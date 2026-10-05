using Microsoft.EntityFrameworkCore;
using Printing.Entity.Models;
using Printing.Entity.TableEntities;
using Printing.Repository;
using Printing.Repository.SeedData;
using Shared.Kernel.Printing;

namespace Printing.Repository.Repositories;

/// <summary>
/// Writes a branch's starting print templates into <c>prt</c>.
///
/// <b>Re-runnable, and meant to be.</b> It adds only the document types the
/// branch is missing, so running it against a branch created months ago
/// backfills whatever has been added to the catalogue since — and is how a
/// branch that existed before Printing took templates over from Master gets
/// its set (TK-81: re-seeded rather than copied).
///
/// <b>It also brings untouched templates up to date</b> (TK-157): a template
/// still exactly as the previous generation of the layout wrote it is
/// replaced with the current one. A template anybody has changed, even by a
/// character, is never overwritten; the editor offers its owner the reset.
/// </summary>
public sealed class PrintTemplateSeederRepository
{
    private readonly PrintingDbContext _db;

    public PrintTemplateSeederRepository(PrintingDbContext db) => _db = db;

    /// <summary>Rows written: templates added plus templates brought up to date.</summary>
    public async Task<int> SeedForOrganizationAsync(Guid orgId, CancellationToken ct)
    {
        List<PrintTemplate> existing = await _db.PrintTemplates
            .IgnoreQueryFilters()
            .Where(t => t.OrgId == orgId)
            .ToListAsync(ct);

        IReadOnlyList<PrintTemplate> missing = PrintTemplateSeed.Build(orgId, existing.Select(t => t.DocumentTypeCode));
        _db.PrintTemplates.AddRange(missing);

        int upgraded = 0;
        foreach (PrintTemplate template in existing)
        {
            if (Upgrade(template))
            {
                upgraded++;
            }
        }

        if (missing.Count + upgraded > 0)
        {
            await _db.SaveChangesAsync(ct);
        }

        return missing.Count + upgraded;
    }

    /// <summary>
    /// Replaces an older generation's template with the current layout when,
    /// and only when, it is still exactly what its own generation wrote. Pure
    /// apart from the row it is given.
    /// </summary>
    public static bool Upgrade(PrintTemplate template)
    {
        if (template.SeedVersion < DefaultLayoutGenerator.OldestReproducibleSeedVersion
            || template.SeedVersion >= DefaultLayoutGenerator.SeedVersion
            || DocumentTypeCatalog.Find(template.DocumentTypeCode) is not DocumentTypeProfile profile
            || !SameContent(template.Content, DefaultLayoutGenerator.Build(profile, template.SeedVersion)))
        {
            return false;
        }

        template.Content = DefaultLayoutGenerator.Build(profile);
        template.SeedVersion = DefaultLayoutGenerator.SeedVersion;
        return true;
    }

    private static bool SameContent(PrintContent stored, PrintContent generated) =>
        stored.FixedHeaderHtml == generated.FixedHeaderHtml
        && stored.HeaderHtml == generated.HeaderHtml
        && stored.DetailsHtml == generated.DetailsHtml
        && stored.FooterHtml == generated.FooterHtml
        && stored.FixedFooterHtml == generated.FixedFooterHtml;
}
