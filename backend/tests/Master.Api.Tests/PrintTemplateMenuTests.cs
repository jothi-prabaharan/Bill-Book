using Master.Entity.TableEntities;
using Master.Repository.SeedData;
using Shared.Kernel.Printing;
using Xunit;

namespace Master.Api.Tests;

/// <summary>
/// The Print templates group of the Settings menu: one row per printable
/// document type, each opening the editor on its own type.
///
/// <b>Pure — no database.</b> The menu is <c>HasData</c>, so what the seed says is
/// what every database gets; asserting the seed is asserting the menu. Switched
/// on in TK-25, when the editor was built; before that the rows were seeded
/// inactive with no route, as every unbuilt screen is.
/// </summary>
public sealed class PrintTemplateMenuTests
{
    private const int PrintTemplatesGroupId = 117;

    private static IReadOnlyList<Menu> Items =>
        [.. MenuSeed.Build().Where(m => m.ParentId == PrintTemplatesGroupId)];

    [Fact]
    public void Every_print_template_row_is_active_and_routed()
    {
        Assert.NotEmpty(Items);

        Assert.All(Items, item =>
        {
            Assert.True(item.IsActive, $"{item.Code} is inactive.");
            Assert.StartsWith("/settings/print-templates/", item.RoutePath, StringComparison.Ordinal);
            Assert.Equal("settings", item.Module);
        });
    }

    [Fact]
    public void The_rows_name_exactly_the_printable_document_types()
    {
        // The route's last segment is the document type the editor opens on. A
        // row naming a type the catalogue does not print would open an editor
        // Printing refuses; a printable type with no row would be unreachable
        // from the menu.
        List<string> routed = [.. Items
            .Select(item => item.RoutePath!.Split('/').Last())
            .OrderBy(code => code, StringComparer.Ordinal)];

        List<string> printable = [.. DocumentTypeCatalog.All
            .Select(profile => profile.Code)
            .OrderBy(code => code, StringComparer.Ordinal)];

        Assert.Equal(printable, routed);
    }
}
