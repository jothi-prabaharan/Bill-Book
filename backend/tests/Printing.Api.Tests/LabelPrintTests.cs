using System.Text.RegularExpressions;
using Printing.Entity.Enums;
using Printing.Entity.Models;
using Printing.Repository.Repositories;
using Printing.Repository.SeedData;
using Xunit;

namespace Printing.Api.Tests;

/// <summary>
/// Labels at an exact size (TK-159): the page is the label's own size on a
/// roll and an A4 grid on a sticker sheet, every copy prints, every label
/// carries its barcode, and what a label shows follows its kind.
/// </summary>
public sealed class LabelPrintTests
{
    private static readonly LabelSize PriceTag = new(50m, 25m, 2m, 1, 0m);

    private static LabelData Item(int copies = 1) => new()
    {
        ItemCode = "SOAP01",
        Name = "Neem soap",
        Price = 45m,
        Mrp = 50m,
        Copies = copies,
    };

    private static int Labels(string html) => Regex.Matches(html, "class=\"lbl\"").Count;

    [Fact]
    public void Selecting_items_and_a_quantity_prints_that_many_labels_at_the_exact_size()
    {
        string html = LabelRendererRepository.Render(LabelKind.PriceTag, PriceTag, [Item(3), Item(2)], sheet: false);

        Assert.Equal(5, Labels(html));
        Assert.Contains("@page { size: 50mm 25mm; margin: 0; }", html);
        Assert.Equal(5, LabelRendererRepository.CountOf([Item(3), Item(2)]));
    }

    [Fact]
    public void A_two_across_roll_prints_a_row_per_page_as_wide_as_the_row()
    {
        var twoUp = new LabelSize(38m, 25m, 2m, 2, 3m);

        string html = LabelRendererRepository.Render(LabelKind.PriceTag, twoUp, [Item(3)], sheet: false);

        Assert.Contains("@page { size: 79mm 25mm; margin: 0; }", html);
        Assert.Equal(2, Regex.Matches(html, "class=\"row\"").Count);
    }

    [Fact]
    public void A_sticker_sheet_is_an_a4_grid_of_as_many_across_as_fit()
    {
        string html = LabelRendererRepository.Render(LabelKind.PriceTag, PriceTag, [Item(10)], sheet: true);

        Assert.Contains("@page { size: A4;", html);
        Assert.Equal(3, LabelRendererRepository.SheetColumns(PriceTag)); // 194 mm across holds three 50 mm labels
        Assert.Contains("repeat(3, 50mm)", html);
        Assert.Equal(10, Labels(html));
    }

    [Fact]
    public void Every_label_carries_its_barcode_and_its_text_is_encoded()
    {
        LabelData label = Item();
        label.Name = "Soap <b>&</b> co";

        string cell = LabelRendererRepository.Cell(LabelKind.PriceTag, PriceTag, label);

        Assert.Contains("class=\"pt-barcode\"", cell);
        Assert.Contains("SOAP01", cell);
        Assert.Contains("Soap &lt;b&gt;&amp;&lt;/b&gt; co", cell);
        Assert.DoesNotContain("<b>", cell);
    }

    [Fact]
    public void A_price_tag_shows_the_price_and_an_mrp_that_differs()
    {
        string cell = LabelRendererRepository.Cell(LabelKind.PriceTag, PriceTag, Item());

        Assert.Contains("₹45.00", cell);
        Assert.Contains("MRP ₹50.00", cell);
    }

    [Fact]
    public void A_batch_label_shows_the_batch_and_the_expiry()
    {
        LabelData label = Item();
        label.BatchNo = "B12";
        label.ExpiryDate = new DateOnly(2027, 3, 31);

        string cell = LabelRendererRepository.Cell(LabelKind.BatchExpiry, PriceTag, label);

        Assert.Contains("Batch B12", cell);
        Assert.Contains("Exp 03/2027", cell);
    }

    [Fact]
    public void A_jewellery_tag_shows_the_weights_the_purity_and_the_huid()
    {
        var label = new LabelData
        {
            ItemCode = "RING22",
            Name = "Ring",
            GrossWeightGrams = 10.25m,
            NetWeightGrams = 9.8m,
            Purity = "22K",
            Huid = "AB12CD",
            SerialNo = "S1",
            Barcode = "S1",
        };

        string cell = LabelRendererRepository.Cell(LabelKind.JewelleryTag, new LabelSize(60m, 15m, 3m, 1, 0m), label);

        Assert.Contains("RING22", cell);
        Assert.Contains("G.Wt 10.250 g", cell);
        Assert.Contains("N.Wt 9.800 g", cell);
        Assert.Contains("22K", cell);
        Assert.Contains("HUID AB12CD", cell);
        Assert.Contains("S/N S1", cell);
    }

    [Fact]
    public void Each_kind_has_one_seeded_default_size_and_seeding_never_adds_a_second()
    {
        Guid orgId = Guid.NewGuid();

        var first = LabelTemplateSeed.Build(orgId, []);
        Assert.Equal(Enum.GetValues<LabelKind>().Order(), first.Select(t => t.Kind).Order());
        Assert.All(first, t => Assert.True(t.IsDefault));

        Assert.Empty(LabelTemplateSeed.Build(orgId, first.Select(t => t.Kind)));
        Assert.Equal(new LabelSize(50m, 25m, 2m, 1, 0m), LabelTemplateSeed.SizeFor(LabelKind.PriceTag));
    }
}
