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
    /// The templates a branch is missing across all five paper sizes.
    /// A4 is marked as IsDefault = true, and all five are marked IsSample = true.
    /// </summary>
    public static IReadOnlyList<PrintTemplate> Build(
        Guid orgId, IEnumerable<(string Code, string PaperSize)> presentTemplates)
    {
        HashSet<(string, string)> present = new(
            presentTemplates.Select(t => (t.Code.Trim().ToUpperInvariant(), t.PaperSize.Trim())),
            EqualityComparer<(string, string)>.Default);

        var list = new List<PrintTemplate>();

        foreach (DocumentTypeProfile profile in DocumentTypeCatalog.All)
        {
            foreach (string paperSize in DefaultLayoutGenerator.AllPaperSizes)
            {
                if (present.Contains((profile.Code.ToUpperInvariant(), paperSize)))
                {
                    continue;
                }

                bool isA4 = paperSize == DefaultLayoutGenerator.PaperSizeA4;
                list.Add(new PrintTemplate
                {
                    OrgId = orgId,
                    DocumentTypeCode = profile.Code,
                    TemplateName = DefaultLayoutGenerator.SampleTemplateName(profile, paperSize),
                    PaperSizeName = paperSize,
                    IsSample = true,
                    IsDefault = isA4,
                    IsActive = true,
                    Settings = DefaultLayoutGenerator.BuildSettings(profile, paperSize),
                    Content = DefaultLayoutGenerator.Build(profile, paperSize),
                    TemplateVersion = 1,
                    SeedVersion = DefaultLayoutGenerator.SeedVersion,
                });
            }
        }

        return list;
    }

    public static IReadOnlyList<PrintTemplate> Build(Guid orgId, IEnumerable<string> presentCodes)
    {
        var existing = presentCodes.Select(code => (code, DefaultLayoutGenerator.PaperSizeA4));
        return Build(orgId, existing);
    }
}
