using Printing.Entity.Models;
using Printing.Entity.TableEntities;
using Printing.Repository.Repositories;
using Printing.Repository.SeedData;
using Shared.Kernel.Printing;
using Xunit;

namespace Printing.Api.Tests;

/// <summary>
/// The UPI payment QR on a printed invoice (TK-158): drawn with its caption
/// when the payload carries a link, nothing at all when it does not, and on the
/// standard invoice template.
/// </summary>
public sealed class UpiQrPrintTests
{
    private const string Link = "upi://pay?pa=shop%40okhdfcbank&pn=Shop&am=118.00&cu=INR&tn=Invoice%20INV-1&tr=INV-1";

    private static PrintRenderResult Render(PrintContent content, string? link)
    {
        var payload = new PrintPayload();
        payload.Singles["Document.No"] = "INV-1";
        payload.Singles[PlaceholderCatalog.UpiQrTag] = link ?? string.Empty;
        return new PrintRendererRepository().Render(new PrintRenderRequest { Content = content, DocumentTypeCode = "INV", Payload = payload });
    }

    [Fact]
    public void An_unpaid_invoice_prints_the_qr_with_its_caption()
    {
        PrintRenderResult result = Render(new PrintContent { FooterHtml = "<div>{{Payment.UpiQr}}</div>" }, Link);

        Assert.Contains("class=\"pt-upi\"", result.Html);
        Assert.Contains("Scan to pay with any UPI app", result.Html);
        Assert.Contains("alt=\"UPI QR code to pay this invoice\"", result.Html);
    }

    [Fact]
    public void A_paid_invoice_prints_none_and_no_caption()
    {
        PrintRenderResult result = Render(new PrintContent { FooterHtml = "<div>{{Payment.UpiQr}}</div>" }, null);

        Assert.DoesNotContain("pt-upi", result.Html);
        Assert.DoesNotContain("Scan to pay", result.Html);
    }

    [Fact]
    public void The_standard_invoice_template_carries_it_and_no_other_type_does()
    {
        foreach (PrintTemplate template in PrintTemplateSeed.Build(Guid.NewGuid(), []))
        {
            string html = template.Content.FooterHtml;
            Assert.Equal(template.DocumentTypeCode == "INV", html.Contains("{{Payment.UpiQr}}"));
        }
    }

    [Fact]
    public void An_untouched_template_from_either_older_generation_is_brought_up_to_date()
    {
        foreach (int generation in new[] { 2, 3 })
        {
            var template = new PrintTemplate
            {
                DocumentTypeCode = "INV",
                Content = DefaultLayoutGenerator.Build(DocumentTypeCatalog.Find("INV")!, generation),
                SeedVersion = generation,
            };

            Assert.True(PrintTemplateSeederRepository.Upgrade(template));
            Assert.Contains("{{Payment.UpiQr}}", template.Content.FooterHtml);
            Assert.Equal(DefaultLayoutGenerator.SeedVersion, template.SeedVersion);
        }
    }

    [Fact]
    public void A_generation_one_template_is_never_matched()
    {
        var template = new PrintTemplate
        {
            DocumentTypeCode = "INV",
            Content = DefaultLayoutGenerator.Build(DocumentTypeCatalog.Find("INV")!, 2),
            SeedVersion = 1,
        };

        Assert.False(PrintTemplateSeederRepository.Upgrade(template));
    }
}
