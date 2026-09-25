using QRCoder;

namespace Printing.Api.Rendering;

/// <summary>
/// Draws a QR code as a PNG data URI (TK-92), for the e-invoice's signed QR
/// payload on a printed invoice.
///
/// The renderer builds this markup itself from text it was given, so it does
/// not pass through the template sanitiser, which refuses data URIs for a good
/// reason: a template's <c>data:</c> image is whatever its author wrote. This
/// one is a PNG this class encoded, and base64 has no characters that can leave
/// the attribute.
/// </summary>
public static class QrImages
{
    /// <summary>
    /// Error correction level M, which the IRP's own QR uses, at 4 pixels per
    /// module: large enough for a phone to read off a laser print at 32 mm.
    /// </summary>
    public static string DataUri(string text)
    {
        using var generator = new QRCodeGenerator();
        using QRCodeData data = generator.CreateQrCode(text, QRCodeGenerator.ECCLevel.M);
        byte[] png = new PngByteQRCode(data).GetGraphic(4);
        return "data:image/png;base64," + Convert.ToBase64String(png);
    }

    /// <summary>The element a QR placeholder becomes. Empty text draws nothing.</summary>
    public static string Img(string? text) =>
        string.IsNullOrWhiteSpace(text)
            ? string.Empty
            : $"<img class=\"pt-qr\" src=\"{DataUri(text)}\" alt=\"e-invoice QR code\">";
}

/// <summary>
/// Puts the IRN, the acknowledgement and the QR code on a registered e-invoice,
/// and the e-way bill on one that has it, when the template does not place them
/// itself (TK-92, TK-93). The QR is required on an
/// e-invoice, so a template written before e-invoicing, or one that simply
/// forgot, must not print a registered invoice without it. A template that
/// places any <c>EInvoice.</c> tag is left exactly as it was written.
/// </summary>
public static class EInvoiceStrip
{
    private const string IrnBlock =
        "<td><b>IRN:</b> {{EInvoice.Irn}}<br><b>Ack No:</b> {{EInvoice.AckNo}}"
        + "&nbsp;&nbsp;<b>Ack Date:</b> {{EInvoice.AckDate}}{EWB}</td>"
        + "<td class=\"pt-einvoice-qr\">{{EInvoice.QrImage}}</td>";

    private const string EwayBlock =
        "<b>E-way bill:</b> {{EInvoice.EwbNo}}&nbsp;&nbsp;<b>Date:</b> {{EInvoice.EwbDate}}"
        + "&nbsp;&nbsp;<b>Valid until:</b> {{EInvoice.EwbValidUntil}}";

    /// <summary>The strip for what the document has: an IRN, an e-way bill, or both (TK-92, TK-93).</summary>
    public static string Html(bool irn, bool eway) =>
        "<table class=\"pt-einvoice\"><tr>"
        + (irn
            ? IrnBlock.Replace("{EWB}", eway ? "<br>" + EwayBlock : string.Empty, StringComparison.Ordinal)
            : "<td>" + EwayBlock + "</td>")
        + "</tr></table>";

    /// <summary>The content to render: unchanged, or with the strip at the top of the header.</summary>
    public static Printing.Entity.Models.PrintContent Ensure(
        Printing.Entity.Models.PrintContent content, Shared.Kernel.Printing.PrintPayload payload)
    {
        bool registered = payload.Singles.TryGetValue("EInvoice.Irn", out object? irn)
            && irn is string { Length: > 0 };
        bool eway = payload.Singles.TryGetValue("EInvoice.EwbNo", out object? ewb)
            && ewb is string { Length: > 0 };
        if (!registered && !eway)
        {
            return content;
        }

        bool placed = PrintSegments.InOrder.Any(segment =>
            PrintSegments.Html(content, segment).Contains("EInvoice.", StringComparison.OrdinalIgnoreCase));
        if (placed)
        {
            return content;
        }

        return new Printing.Entity.Models.PrintContent
        {
            FixedHeaderHtml = content.FixedHeaderHtml,
            HeaderHtml = Html(registered, eway) + content.HeaderHtml,
            DetailsHtml = content.DetailsHtml,
            FooterHtml = content.FooterHtml,
            FixedFooterHtml = content.FixedFooterHtml,
        };
    }
}
