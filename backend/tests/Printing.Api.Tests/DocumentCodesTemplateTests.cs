using Printing.Entity.Models;
using Printing.Entity.TableEntities;
using Printing.Repository.Repositories;
using Printing.Repository.SeedData;
using Shared.Kernel.Printing;
using Xunit;

namespace Printing.Api.Tests;

/// <summary>
/// Every printable type's seeded template carries its document codes (TK-157),
/// and a branch's untouched templates pick them up through the idempotent seed
/// while an edited one is left exactly as its owner wrote it.
/// </summary>
public sealed class DocumentCodesTemplateTests
{
    private static string AllHtml(PrintContent c) =>
        c.FixedHeaderHtml + c.HeaderHtml + c.DetailsHtml + c.FooterHtml + c.FixedFooterHtml;

    [Fact]
    public void Every_seeded_document_carries_a_barcode_and_a_qr_of_its_number_except_the_kitchen_ticket()
    {
        foreach (PrintTemplate template in PrintTemplateSeed.Build(Guid.NewGuid(), []))
        {
            string html = AllHtml(template.Content);
            bool kitchen = template.DocumentTypeCode == PlaceholderCatalog.KitchenTicketType;

            Assert.Equal(!kitchen, html.Contains("{{Document.BarcodeImage}}"));
            Assert.Equal(!kitchen, html.Contains("{{Document.QrImage}}"));
            Assert.Equal(DefaultLayoutGenerator.SeedVersion, template.SeedVersion);
        }
    }

    [Fact]
    public void A_printed_invoice_carries_a_barcode_and_a_qr_of_its_number()
    {
        DocumentTypeProfile invoice = DocumentTypeCatalog.Find("INV")!;
        var payload = new PrintPayload();
        payload.Singles["Document.No"] = "INV/2627/00042";

        PrintRenderResult result = new PrintRendererRepository().Render(new PrintRenderRequest
        {
            Content = DefaultLayoutGenerator.Build(invoice),
            DocumentTypeCode = "INV",
            Payload = payload,
        });

        Assert.Contains("pt-barcode-text\">INV/2627/00042</span>", result.Html);
        Assert.Contains("class=\"pt-qr\"", result.Html);
    }

    private static PrintTemplate Previous(string code) => new()
    {
        OrgId = Guid.NewGuid(),
        DocumentTypeCode = code,
        TemplateName = code,
        Content = DefaultLayoutGenerator.Build(DocumentTypeCatalog.Find(code)!, 2),
        SeedVersion = 2,
    };

    [Fact]
    public void An_untouched_previous_template_is_brought_up_to_date()
    {
        PrintTemplate template = Previous("INV");

        Assert.True(PrintTemplateSeederRepository.Upgrade(template));
        Assert.Contains("{{Document.BarcodeImage}}", AllHtml(template.Content));
        Assert.Equal(DefaultLayoutGenerator.SeedVersion, template.SeedVersion);
    }

    [Fact]
    public void An_edited_template_is_never_overwritten()
    {
        PrintTemplate template = Previous("INV");
        template.Content.FooterHtml += "<p>Bank: HDFC 0001</p>";

        Assert.False(PrintTemplateSeederRepository.Upgrade(template));
        Assert.Contains("HDFC", template.Content.FooterHtml);
        Assert.DoesNotContain("{{Document.BarcodeImage}}", AllHtml(template.Content));
        Assert.Equal(2, template.SeedVersion);
    }

    [Fact]
    public void A_current_template_is_left_alone()
    {
        var template = new PrintTemplate
        {
            DocumentTypeCode = "INV",
            Content = DefaultLayoutGenerator.Build(DocumentTypeCatalog.Find("INV")!),
            SeedVersion = DefaultLayoutGenerator.SeedVersion,
        };

        Assert.False(PrintTemplateSeederRepository.Upgrade(template));
    }

    [Fact]
    public void Generation_three_is_generation_two_plus_the_codes()
    {
        DocumentTypeProfile quote = DocumentTypeCatalog.Find("QTE")!;

        string with = AllHtml(DefaultLayoutGenerator.Build(quote, 3));
        string without = AllHtml(DefaultLayoutGenerator.Build(quote, 2));

        Assert.DoesNotContain("Document.BarcodeImage", without);
        Assert.DoesNotContain("Document.QrImage", without);
        Assert.Equal(
            without,
            with.Replace("<div>{{Document.BarcodeImage}}</div>", string.Empty)
                .Replace("<td align=\"left\">{{Document.QrImage}}</td>", "<td align=\"left\">&nbsp;</td>"));
    }
}
