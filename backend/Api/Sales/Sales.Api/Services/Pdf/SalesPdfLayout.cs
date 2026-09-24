using PdfSharp.Drawing;
using PdfSharp.Pdf;
using Shared.Kernel.Documents;

namespace Sales.Api.Services.Pdf;

/// <summary>
/// What one archived sales document prints: the header and lines it was posted
/// with, plus the few things only its service knows — its title, what its number
/// is called, and the parties' names (TK-22).
/// </summary>
public sealed class SalesPdfModel
{
    public string Title { get; init; } = null!;

    /// <summary>"Invoice No", "Credit Note No", "Challan No".</summary>
    public string NumberLabel { get; init; } = null!;

    public DocumentHeaderBase Header { get; init; } = null!;

    public IReadOnlyList<DocumentLineBase> Lines { get; init; } = [];

    public DateOnly? DueDate { get; init; }

    /// <summary>A line under the number, such as the invoice a credit note is against.</summary>
    public string? Reference { get; init; }

    public string SellerName { get; init; } = null!;

    public string? SellerGstin { get; init; }

    public string? SellerAddress { get; init; }

    public string BuyerName { get; init; } = null!;

    public IReadOnlyDictionary<long, string> ItemNames { get; init; } = new Dictionary<long, string>();
}

/// <summary>
/// The fixed layout every archived sales document shares, drawn with PDFsharp.
///
/// <b>Not PDF/A.</b> PDFsharp 6.1.1, the pinned version, has no PDF/A API; see
/// TK-22 in <c>docs/TASKS.md</c>. <b>Not the print template either:</b> a
/// template is HTML, and PDFsharp draws rather than lays out HTML.
/// </summary>
public static class SalesPdfLayout
{
    /// <summary>
    /// The stamp across the page: PROFORMA for a document not yet posted, VOID
    /// for a voided one, nothing for a posted one — the same rule the print
    /// renderer applies.
    /// </summary>
    public static string? Watermark(DocumentStatus status) => status switch
    {
        DocumentStatus.Draft or DocumentStatus.ReadyToPost => "PROFORMA",
        DocumentStatus.Void => "VOID",
        _ => null,
    };

    public static byte[] Render(SalesPdfModel model)
    {
        DocumentHeaderBase header = model.Header;

        var doc = new PdfDocument();
        doc.Info.Title = $"{model.Title} {header.DocumentNo}";
        doc.Info.Creator = "RetailErp";

        PdfPage page = doc.AddPage();
        XGraphics gfx = XGraphics.FromPdfPage(page);
        double width = page.Width.Point;

        var titleFont = new XFont("Arial", 20, XFontStyleEx.Bold);
        var headerFont = new XFont("Arial", 10, XFontStyleEx.Bold);
        var normalFont = new XFont("Arial", 10, XFontStyleEx.Regular);

        if (Watermark(header.Status) is string stamp)
        {
            var watermarkFont = new XFont("Arial", 60, XFontStyleEx.Bold);
            XGraphicsState state = gfx.Save();
            gfx.TranslateTransform(width / 2, page.Height.Point / 2);
            gfx.RotateTransform(-45);
            var format = new XStringFormat { Alignment = XStringAlignment.Center, LineAlignment = XLineAlignment.Center };
            gfx.DrawString(stamp, watermarkFont, XBrushes.LightGray, new XPoint(0, 0), format);
            gfx.Restore(state);
        }

        gfx.DrawString(model.Title, titleFont, XBrushes.Black, new XRect(0, 40, width, 30), XStringFormats.Center);

        gfx.DrawString($"Seller: {model.SellerName}", headerFont, XBrushes.Black, new XPoint(40, 80));
        gfx.DrawString($"GSTIN: {model.SellerGstin ?? "N/A"}", normalFont, XBrushes.Black, new XPoint(40, 95));
        if (!string.IsNullOrEmpty(model.SellerAddress))
        {
            gfx.DrawString($"Address: {model.SellerAddress}", normalFont, XBrushes.Black, new XPoint(40, 110));
        }

        gfx.DrawString($"Buyer: {model.BuyerName}", headerFont, XBrushes.Black, new XPoint(width / 2, 80));
        gfx.DrawString($"GSTIN: {header.ContactGstin ?? "N/A"}", normalFont, XBrushes.Black, new XPoint(width / 2, 95));
        if (!string.IsNullOrEmpty(header.BillingAddress))
        {
            gfx.DrawString($"Address: {header.BillingAddress}", normalFont, XBrushes.Black, new XPoint(width / 2, 110));
        }

        double y = 140;
        gfx.DrawString($"{model.NumberLabel}: {header.DocumentNo}", headerFont, XBrushes.Black, new XPoint(40, y));
        y += 15;
        gfx.DrawString($"Date: {header.DocumentDate:dd-MMM-yyyy}", normalFont, XBrushes.Black, new XPoint(40, y));
        if (model.DueDate.HasValue)
        {
            y += 15;
            gfx.DrawString($"Due Date: {model.DueDate.Value:dd-MMM-yyyy}", normalFont, XBrushes.Black, new XPoint(40, y));
        }
        if (!string.IsNullOrEmpty(model.Reference))
        {
            y += 15;
            gfx.DrawString(model.Reference, normalFont, XBrushes.Black, new XPoint(40, y));
        }

        y = Math.Max(200, y + 25);
        gfx.DrawRectangle(XPens.Black, new XRect(40, y, width - 80, 20));
        gfx.DrawString("Item", headerFont, XBrushes.Black, new XPoint(45, y + 14));
        gfx.DrawString("HSN", headerFont, XBrushes.Black, new XPoint(250, y + 14));
        gfx.DrawString("Qty", headerFont, XBrushes.Black, new XPoint(320, y + 14));
        gfx.DrawString("Rate", headerFont, XBrushes.Black, new XPoint(370, y + 14));
        gfx.DrawString("Taxable", headerFont, XBrushes.Black, new XPoint(430, y + 14));
        gfx.DrawString("Total", headerFont, XBrushes.Black, new XPoint(490, y + 14));
        y += 20;

        foreach (DocumentLineBase line in model.Lines.OrderBy(l => l.LineNumber))
        {
            string itemName = line.ItemId is long itemId && model.ItemNames.TryGetValue(itemId, out string? name)
                ? name
                : line.Description ?? "Unknown";

            gfx.DrawString(itemName, normalFont, XBrushes.Black, new XPoint(45, y + 14));
            gfx.DrawString(line.HsnSacCode ?? "", normalFont, XBrushes.Black, new XPoint(250, y + 14));
            gfx.DrawString(line.Quantity.ToString("0.##"), normalFont, XBrushes.Black, new XPoint(320, y + 14));
            gfx.DrawString(line.UnitPrice.ToString("0.00"), normalFont, XBrushes.Black, new XPoint(370, y + 14));
            gfx.DrawString(line.TaxableAmount.ToString("0.00"), normalFont, XBrushes.Black, new XPoint(430, y + 14));
            gfx.DrawString(line.LineTotal.ToString("0.00"), normalFont, XBrushes.Black, new XPoint(490, y + 14));
            y += 20;
        }

        y += 10;
        void Total(string label, decimal amount, XFont font)
        {
            gfx.DrawString($"{label}: {amount:0.00}", font, XBrushes.Black, new XPoint(430, y + 14));
            y += 15;
        }

        Total("Subtotal", header.SubTotal, normalFont);
        if (header.CgstAmount > 0) Total("CGST", header.CgstAmount, normalFont);
        if (header.SgstAmount > 0) Total("SGST", header.SgstAmount, normalFont);
        if (header.IgstAmount > 0) Total("IGST", header.IgstAmount, normalFont);
        if (header.CessAmount > 0) Total("Cess", header.CessAmount, normalFont);
        if (header.RoundOffAmount != 0) Total("Round Off", header.RoundOffAmount, normalFont);
        Total("Total", header.TotalAmount, headerFont);

        using var ms = new MemoryStream();
        doc.Save(ms, false);
        return ms.ToArray();
    }
}
