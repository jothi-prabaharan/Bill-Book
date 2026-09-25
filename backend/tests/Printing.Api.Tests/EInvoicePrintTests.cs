using Printing.Api.Rendering;
using Printing.Entity.Models;
using Shared.Kernel.Printing;
using Xunit;

namespace Printing.Api.Tests;

/// <summary>
/// A registered e-invoice prints its IRN and QR code (TK-92): from the template's
/// own tags when it places them, and from the renderer's strip when it does not.
/// </summary>
public sealed class EInvoicePrintTests
{
    private const string Irn = "a3f1c2d4e5b6a7980c1d2e3f4a5b6c7d8e9f0a1b2c3d4e5f6a7b8c9d0e1f2a3b";

    private static readonly PrintRenderer Renderer = new();

    private static PrintPayload Payload(bool registered)
    {
        var payload = new PrintPayload();
        payload.Singles["Document.No"] = "INV/26-27/0042";
        if (registered)
        {
            payload.Singles["EInvoice.Irn"] = Irn;
            payload.Singles["EInvoice.AckNo"] = "112600000000001";
            payload.Singles["EInvoice.AckDate"] = new DateOnly(2026, 9, 20);
            payload.Singles["EInvoice.QrImage"] = "header.payload.signature";
        }

        return payload;
    }

    private static PrintRenderResult Render(PrintContent content, PrintPayload payload, string type = "INV") =>
        Renderer.Render(new PrintRenderRequest { Content = content, DocumentTypeCode = type, Payload = payload });

    [Fact]
    public void A_registered_invoice_on_a_template_without_the_tags_prints_the_strip()
    {
        PrintRenderResult result = Render(new PrintContent { HeaderHtml = "<div>{{Document.No}}</div>" }, Payload(true));

        Assert.Contains(Irn, result.Html);
        Assert.Contains("112600000000001", result.Html);
        Assert.Contains("<img class=\"pt-qr\" src=\"data:image/png;base64,", result.Html);
        Assert.Empty(result.UnknownTags);
    }

    [Fact]
    public void A_template_that_places_the_tags_is_left_as_written()
    {
        var content = new PrintContent { FooterHtml = "<p>IRN {{EInvoice.Irn}}</p><p>{{EInvoice.QrImage}}</p>" };

        PrintRenderResult result = Render(content, Payload(true));

        Assert.DoesNotContain("pt-einvoice", result.Html);
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(result.Html, Irn));
        Assert.Contains("class=\"pt-qr\"", result.Html);
    }

    [Fact]
    public void An_invoice_that_was_never_registered_prints_no_strip_and_no_code()
    {
        PrintRenderResult result = Render(new PrintContent { HeaderHtml = "<div>{{Document.No}} {{EInvoice.QrImage}}</div>" }, Payload(false));

        Assert.DoesNotContain("pt-einvoice", result.Html);
        Assert.DoesNotContain("<img", result.Html);
        Assert.Empty(result.UnknownTags);
    }

    [Fact]
    public void A_template_cannot_bring_in_its_own_data_uri_image()
    {
        var content = new PrintContent { HeaderHtml = "<img src=\"data:image/png;base64,AAAA\">" };

        PrintRenderResult result = Render(content, Payload(false));

        Assert.DoesNotContain("data:image", result.Html);
    }

    [Fact]
    public void The_qr_image_is_a_real_png()
    {
        string uri = QrImages.DataUri("header.payload.signature");
        byte[] png = Convert.FromBase64String(uri["data:image/png;base64,".Length..]);

        Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47 }, png[..4]);
    }

    [Theory]
    [InlineData("INV", true)]
    [InlineData("CRN", true)]
    [InlineData("QTE", false)]
    public void Only_invoices_and_credit_notes_offer_the_e_invoice_tags(string type, bool offered)
    {
        Assert.Equal(offered, PlaceholderCatalog.Find(type, "EInvoice.QrImage") is not null);
    }
}
