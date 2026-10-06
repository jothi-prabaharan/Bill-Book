using System.Text;
using Printing.Entity.Models;
using Printing.Repository.Repositories;
using Shared.Kernel.Printing;
using ZXing;
using Xunit;

namespace Printing.Api.Tests;

/// <summary>
/// Barcodes and QR codes on printed documents (TK-156): Code128 for any
/// printable text, EAN-13 for a valid retail code, and the document's own
/// number as both, from the two Document placeholders.
/// </summary>
public sealed class BarcodePrintTests
{
    private static readonly PrintRendererRepository Renderer = new();

    private static PrintRenderResult Render(string html, PrintPayload payload, string type = "INV") =>
        Renderer.Render(new PrintRenderRequest { Content = new PrintContent { HeaderHtml = html }, DocumentTypeCode = type, Payload = payload });

    [Theory]
    [InlineData("8901234567890", true)] // a valid GS1 check digit
    [InlineData("8901234567892", false)] // the wrong check digit
    [InlineData("890123456789", false)] // twelve digits
    [InlineData("89012345678AB", false)]
    [InlineData(null, false)]
    public void An_ean13_needs_thirteen_digits_and_the_right_check_digit(string? text, bool valid) =>
        Assert.Equal(valid, BarcodeImagesRepository.IsEan13(text));

    [Fact]
    public void A_retail_code_is_ean13_and_anything_else_code128()
    {
        Assert.Equal(BarcodeFormat.EAN_13, BarcodeImagesRepository.FormatFor("8901234567890"));
        Assert.Equal(BarcodeFormat.CODE_128, BarcodeImagesRepository.FormatFor("INV/2627/00042"));
    }

    [Fact]
    public void A_code128_draws_as_svg_bars()
    {
        string svg = BarcodeImagesRepository.Svg("INV/2627/00042");

        Assert.Contains("<svg", svg);
        Assert.Contains("rect", svg);
    }

    [Fact]
    public void The_image_is_an_svg_data_uri_with_its_text_encoded_beneath()
    {
        string img = BarcodeImagesRepository.Img("A<B>&C");

        Assert.Contains("src=\"data:image/svg+xml;base64,", img);
        Assert.Contains("<span class=\"pt-barcode-text\">A&lt;B&gt;&amp;C</span>", img);
        Assert.DoesNotContain("<B>", img);

        string base64 = img.Split("base64,")[1].Split('"')[0];
        Assert.Contains("<svg", Encoding.UTF8.GetString(Convert.FromBase64String(base64)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("  ")]
    [InlineData("தமிழ்")] // Code128 carries printable ASCII only
    public void Text_that_cannot_be_encoded_draws_nothing(string? text) =>
        Assert.Equal(string.Empty, BarcodeImagesRepository.Img(text));

    [Fact]
    public void A_template_barcode_tag_renders_a_code128_of_the_text_it_is_given()
    {
        var payload = new PrintPayload();
        payload.Singles["Document.No"] = "INV/2627/00042";
        payload.Singles[PlaceholderCatalog.DocumentBarcodeTag] = "INV/2627/00042";

        PrintRenderResult result = Render("<div>{{Document.BarcodeImage}}</div>", payload);

        Assert.Contains("class=\"pt-barcode\"", result.Html);
        Assert.Contains("data:image/svg+xml;base64,", result.Html);
        Assert.Contains("INV/2627/00042", result.Html);
        Assert.Empty(result.UnknownTags);
    }

    [Fact]
    public void The_document_codes_fall_back_to_the_document_number()
    {
        var payload = new PrintPayload();
        payload.Singles["Document.No"] = "QTE/2627/00007";

        PrintRenderResult result = Render("<div>{{Document.BarcodeImage}} {{Document.QrImage}}</div>", payload, "QTE");

        Assert.Contains("pt-barcode-text\">QTE/2627/00007</span>", result.Html);
        Assert.Contains("<img class=\"pt-qr\" src=\"data:image/png;base64,", result.Html);
        Assert.Contains("alt=\"QR code of the document number\"", result.Html);
    }

    [Fact]
    public void Every_printable_type_offers_the_two_document_codes()
    {
        foreach (string type in DocumentTypeCatalog.All.Select(t => t.Code))
        {
            Assert.Equal(PlaceholderType.Barcode, PlaceholderCatalog.Find(type, PlaceholderCatalog.DocumentBarcodeTag)?.Type);
            Assert.Equal(PlaceholderType.QrCode, PlaceholderCatalog.Find(type, PlaceholderCatalog.DocumentQrTag)?.Type);
        }
    }

    [Fact]
    public void A_template_cannot_bring_its_own_data_uri_image()
    {
        var payload = new PrintPayload();
        payload.Singles["Document.No"] = "INV/2627/00042";

        PrintRenderResult result = Render("<img src=\"data:image/svg+xml;base64,PHN2Zz48L3N2Zz4=\"><div>{{Document.BarcodeImage}}</div>", payload);

        // Only the renderer's own barcode image survives.
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(result.Html, "data:image/svg\\+xml"));
    }
}
