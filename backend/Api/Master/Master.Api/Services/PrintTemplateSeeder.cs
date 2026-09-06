using Master.Entity.TableEntities;
using Master.Repository;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Printing;

namespace Master.Api.Services;

/// <summary>
/// Writes a branch's starting print templates: one default per printable
/// document type, generated from the platform catalogue.
///
/// <b>Generated, never copied from another branch.</b> Copying would carry one
/// customer's letterhead into another's books the first time somebody seeded
/// from the wrong source row.
///
/// <b>Re-runnable, and meant to be</b>, exactly like the contact person role
/// seed beside it: it adds only the document types the branch is missing, so
/// running it against a branch created months ago backfills whatever has been
/// added to the catalogue since.
/// </summary>
public sealed class PrintTemplateSeeder
{
    private readonly ContactsDbContext _db;

    public PrintTemplateSeeder(ContactsDbContext db) => _db = db;

    public async Task<int> SeedForOrganizationAsync(Guid orgId, CancellationToken ct)
    {
        List<string> existing = await _db.PrintTemplates
            .IgnoreQueryFilters()
            .Where(t => t.OrgId == orgId)
            .Select(t => t.DocumentTypeCode)
            .ToListAsync(ct);

        HashSet<string> present = new(existing.Select(code => code.Trim()), StringComparer.OrdinalIgnoreCase);

        // A branch that already has a template for a document type keeps it,
        // whatever state it is in. Seeding must never overwrite an edit.
        List<DocumentTypeProfile> missing = [.. DocumentTypeCatalog.All.Where(p => !present.Contains(p.Code))];

        if (missing.Count == 0)
        {
            return 0;
        }

        foreach (DocumentTypeProfile profile in missing)
        {
            _db.PrintTemplates.Add(new PrintTemplate
            {
                OrgId = orgId,
                DocumentTypeCode = profile.Code,
                TemplateName = profile.Name,
                IsDefault = true,
                IsActive = true,
                Settings = DefaultLayoutGenerator.BuildSettings(),
                Content = DefaultLayoutGenerator.Build(profile),
                TemplateVersion = 1,
                SeedVersion = DefaultLayoutGenerator.SeedVersion,
            });
        }

        await _db.SaveChangesAsync(ct);
        return missing.Count;
    }
}
