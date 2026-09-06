using Shared.Kernel.Printing;
using Xunit;

namespace Shared.Kernel.Tests;

/// <summary>
/// The generated layouts have to satisfy the two rules that everything else
/// downstream assumes, and it is cheaper to assert them over all twelve
/// document types than to discover one of them is wrong on a customer's page.
/// </summary>
public class DefaultLayoutTests
{
    public static TheoryData<string> DocumentTypes()
    {
        var data = new TheoryData<string>();
        foreach (DocumentTypeProfile profile in DocumentTypeCatalog.All)
        {
            data.Add(profile.Code);
        }

        return data;
    }

    [Fact]
    public void The_catalogue_holds_the_twelve_printable_document_types()
    {
        Assert.Equal(12, DocumentTypeCatalog.All.Count);
        Assert.Equal(
            DocumentTypeCatalog.All.Select(p => p.Code).Distinct().Count(),
            DocumentTypeCatalog.All.Count);
    }

    [Theory]
    [MemberData(nameof(DocumentTypes))]
    public void Every_chip_in_a_generated_layout_resolves_for_its_document_type(string code)
    {
        PrintContent content = DefaultLayoutGenerator.Build(DocumentTypeCatalog.Find(code)!);

        // A generated template that shipped with an unresolvable tag would print
        // a blank space on a real document and refuse its own activation.
        Assert.Empty(MergeTags.Unknown(content, code));
    }

    [Theory]
    [MemberData(nameof(DocumentTypes))]
    public void A_generated_layout_passes_the_sanitiser_unchanged(string code)
    {
        PrintContent generated = DefaultLayoutGenerator.Build(DocumentTypeCatalog.Find(code)!);
        PrintContent sanitized = DefaultLayoutGenerator.Build(DocumentTypeCatalog.Find(code)!);

        IReadOnlyList<SegmentViolation> violations = new SegmentSanitizer().SanitizeContent(sanitized);

        // Nothing removed, and nothing reformatted either: the generator writes
        // CSS in the canonical spacing AngleSharp re-serialises to. If the two
        // ever drift, every seeded template in the product is quietly altered
        // the first time it is saved.
        Assert.Empty(violations);

        foreach (PrintSegment segment in PrintSegments.InOrder)
        {
            Assert.Equal(
                PrintSegments.Html(generated, segment),
                PrintSegments.Html(sanitized, segment));
        }
    }

    [Theory]
    [MemberData(nameof(DocumentTypes))]
    public void Every_document_type_generates_all_five_segments(string code)
    {
        PrintContent content = DefaultLayoutGenerator.Build(DocumentTypeCatalog.Find(code)!);

        foreach (PrintSegment segment in PrintSegments.InOrder)
        {
            Assert.False(
                string.IsNullOrWhiteSpace(PrintSegments.Html(content, segment)),
                $"{code} generated an empty {PrintSegments.Code(segment)} segment.");
        }
    }

    [Fact]
    public void Kind_is_declared_rather_than_read_off_the_dot_in_the_tag()
    {
        // Both tags carry a dot. Only one of them repeats.
        Assert.Equal(PlaceholderKind.List, PlaceholderCatalog.Find("INV", "Item.ItemName")!.Kind);
        Assert.Equal(PlaceholderKind.Single, PlaceholderCatalog.Find("INV", "Organization.Name")!.Kind);

        Assert.Equal("Item", MergeTags.ListGroup("Item.ItemName"));
        Assert.Null(MergeTags.ListGroup("Organization.Name"));
    }

    [Fact]
    public void A_list_chips_repeat_marker_is_not_part_of_its_tag()
    {
        string chip = DefaultLayoutGenerator.Chip("Item.Rate", PlaceholderKind.List);

        Assert.Contains("↻", chip, StringComparison.Ordinal);
        Assert.Equal(["Item.Rate"], MergeTags.Extract(chip));
    }

    [Fact]
    public void A_document_type_with_no_party_offers_no_party_tags()
    {
        // A journal has no counterparty, so a Party chip on one would resolve to
        // nothing at print time.
        Assert.DoesNotContain(PlaceholderCatalog.For("JRN"), p => p.Group == "Party");
        Assert.Contains(PlaceholderCatalog.For("INV"), p => p.Group == "Party");
    }

    [Fact]
    public void Placeholder_groups_come_back_in_panel_order_with_lists_flagged()
    {
        IReadOnlyList<PlaceholderGroup> groups = PlaceholderCatalog.Grouped("INV");

        Assert.Equal("Organization", groups[0].Group);
        Assert.False(groups[0].IsList);
        Assert.True(groups.Single(g => g.Group == "Item").IsList);
        Assert.True(groups.Single(g => g.Group == "Tax").IsList);
    }

    [Fact]
    public void A_goods_receipt_carries_no_tax_block_because_the_bill_determines_it()
    {
        Assert.DoesNotContain(PlaceholderCatalog.For("GRN"), p => p.Group == "Tax");
        Assert.Contains(PlaceholderCatalog.For("BIL"), p => p.Group == "Tax");
    }
}
