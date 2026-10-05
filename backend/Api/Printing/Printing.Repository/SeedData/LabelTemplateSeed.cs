using Printing.Entity.Enums;
using Printing.Entity.TableEntities;

namespace Printing.Repository.SeedData;

/// <summary>
/// A branch's starting label sizes (TK-159): one default per kind, at the
/// sizes most label rolls sold in India come in. A branch changes them to its
/// own roll from the print dialog.
/// </summary>
public static class LabelTemplateSeed
{
    /// <summary>Kind, name, width, height, gap, labels across, gap across — in millimetres.</summary>
    public static readonly IReadOnlyList<(LabelKind Kind, string Name, decimal Width, decimal Height, decimal Gap, int Columns, decimal ColumnGap)> Defaults =
    [
        (LabelKind.PriceTag, "Price tag 50 × 25 mm", 50m, 25m, 2m, 1, 0m),
        (LabelKind.ShelfLabel, "Shelf label 70 × 30 mm", 70m, 30m, 3m, 1, 0m),
        (LabelKind.BatchExpiry, "Batch and expiry 50 × 25 mm", 50m, 25m, 2m, 1, 0m),
        (LabelKind.JewelleryTag, "Jewellery tag 60 × 15 mm", 60m, 15m, 3m, 1, 0m),
    ];

    /// <summary>The default rows for the kinds a branch has no default for. Seeding never overwrites a size the branch set.</summary>
    public static IReadOnlyList<LabelTemplate> Build(Guid orgId, IEnumerable<LabelKind> kindsWithDefault)
    {
        HashSet<LabelKind> present = [.. kindsWithDefault];
        return [.. Defaults
            .Where(d => !present.Contains(d.Kind))
            .Select(d => new LabelTemplate
            {
                OrgId = orgId,
                Kind = d.Kind,
                Name = d.Name,
                WidthMm = d.Width,
                HeightMm = d.Height,
                GapMm = d.Gap,
                Columns = d.Columns,
                ColumnGapMm = d.ColumnGap,
                IsDefault = true,
                IsActive = true,
            })];
    }

    /// <summary>The shipped size for a kind, used when a branch somehow has none.</summary>
    public static Repositories.LabelSize SizeFor(LabelKind kind)
    {
        var d = Defaults.First(x => x.Kind == kind);
        return new Repositories.LabelSize(d.Width, d.Height, d.Gap, d.Columns, d.ColumnGap);
    }
}
