using Printing.Entity.TableEntities;
using Shared.Kernel.Printing;

namespace Printing.Repository.SeedData;

/// <summary>
/// A branch's starting print templates: one default per printable document
/// type, generated from the platform catalogue.
///
/// <b>Generated, never copied from another branch.</b> Copying would carry one
/// customer's letterhead into another's books the first time somebody seeded
/// from the wrong source row.
///
/// A builder rather than a service, like every other service's seed data, so
/// the idempotent seeder in Printing.Api and Master's startup bootstrap build
/// the same rows from one place.
/// </summary>
public static class PrintTemplateSeed
{
    /// <summary>
    /// The templates a branch is missing. A document type the branch already has
    /// a template for — whatever state it is in — gets nothing: seeding must
    /// never overwrite an edit.
    /// </summary>
    public static IReadOnlyList<PrintTemplate> Build(Guid orgId, IEnumerable<string> presentCodes)
    {
        HashSet<string> present = new(
            presentCodes.Select(code => code.Trim()), StringComparer.OrdinalIgnoreCase);

        return [.. DocumentTypeCatalog.All
            .Where(profile => !present.Contains(profile.Code))
            .Select(profile => new PrintTemplate
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
            })];
    }
}
