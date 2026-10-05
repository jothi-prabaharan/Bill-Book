using System.Net;
using ZXing;
using ZXing.Common;

namespace Printing.Repository.Repositories;

/// <summary>
/// Draws a barcode as an SVG data URI (TK-156): Code128 for any printable
/// text — a document number such as <c>INV/2627/00042</c> — and EAN-13 for a
/// 13-digit retail code whose check digit is right. Vector, so it prints sharp
/// on a laser page and on a 203 dpi label alike.
///
/// Like <see cref="QrImagesRepository"/>, the markup is built here from text
/// the payload carried, never taken from the payload as markup, so it does not
/// pass through the template sanitiser and its rule against a template's own
/// <c>data:</c> images stands. Base64 has no character that can leave the
/// attribute, and the caption is HTML-encoded.
/// </summary>
public static class BarcodeImagesRepository
{
    /// <summary>The bar height in SVG units; the template's CSS sizes the image.</summary>
    private const int BarHeight = 60;

    /// <summary>The symbology for some text: EAN-13 when it is a valid EAN-13, Code128 otherwise.</summary>
    public static BarcodeFormat FormatFor(string text) =>
        IsEan13(text) ? BarcodeFormat.EAN_13 : BarcodeFormat.CODE_128;

    /// <summary>Thirteen digits whose last is the GS1 check digit of the first twelve.</summary>
    public static bool IsEan13(string? text)
    {
        if (text is not { Length: 13 } || !text.All(char.IsAsciiDigit))
        {
            return false;
        }

        int sum = 0;
        for (int i = 0; i < 12; i++)
        {
            sum += (text[i] - '0') * (i % 2 == 0 ? 1 : 3);
        }

        return (10 - sum % 10) % 10 == text[12] - '0';
    }

    /// <summary>
    /// Whether Code128 can carry the text: printable ASCII only. A document
    /// number always is; anything else draws nothing rather than a wrong code.
    /// </summary>
    public static bool CanEncode(string? text) =>
        !string.IsNullOrWhiteSpace(text) && (IsEan13(text) || text.All(c => c is >= ' ' and <= '~'));

    /// <summary>The SVG itself, bars only (the caption is HTML, beneath it).</summary>
    public static string Svg(string text)
    {
        var writer = new BarcodeWriterSvg
        {
            Format = FormatFor(text),
            Options = new EncodingOptions { Height = BarHeight, Width = 0, Margin = 0, PureBarcode = true },
        };

        return writer.Write(text).Content;
    }

    public static string DataUri(string text) =>
        "data:image/svg+xml;base64," + Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(Svg(text)));

    /// <summary>
    /// The element a barcode placeholder becomes: the bars, and the text they
    /// carry printed under them so a person can read it too. Text that cannot
    /// be encoded draws nothing.
    /// </summary>
    public static string Img(string? text)
    {
        if (!CanEncode(text))
        {
            return string.Empty;
        }

        string encoded = WebUtility.HtmlEncode(text!.Trim());
        return $"<span class=\"pt-barcode\"><img src=\"{DataUri(text.Trim())}\" alt=\"Barcode {encoded}\">"
            + $"<span class=\"pt-barcode-text\">{encoded}</span></span>";
    }
}
