using PdfSharp.Drawing;
using PdfSharp.Pdf;
using Sales.Entity.TableEntities;
using Shared.Kernel.Documents;

namespace Sales.Api.Services.Pdf;

public sealed class PdfSharpInvoiceRenderer : IInvoicePdfRenderer
{
    public byte[] Render(PdfInvoiceModel model)
    {
        var doc = new PdfDocument();
        var page = doc.AddPage();
        var gfx = XGraphics.FromPdfPage(page);

        // Simple default font
        var titleFont = new XFont("Arial", 20, XFontStyleEx.Bold);
        var headerFont = new XFont("Arial", 10, XFontStyleEx.Bold);
        var normalFont = new XFont("Arial", 10, XFontStyleEx.Regular);

        // Watermark if draft or void
        if (model.Invoice.Status == DocumentStatus.Draft)
        {
            var watermarkFont = new XFont("Arial", 60, XFontStyleEx.Bold);
            gfx.TranslateTransform(page.Width.Point / 2, page.Height.Point / 2);
            gfx.RotateTransform(-45);
            var format = new XStringFormat { Alignment = XStringAlignment.Center, LineAlignment = XLineAlignment.Center };
            gfx.DrawString("PROFORMA", watermarkFont, XBrushes.LightGray, new XPoint(0, 0), format);
            gfx.RotateTransform(45);
            gfx.TranslateTransform(-page.Width.Point / 2, -page.Height.Point / 2);
        }
        else if (model.Invoice.Status.ToString() == "Voided") // Workaround for enum
        {
            var watermarkFont = new XFont("Arial", 60, XFontStyleEx.Bold);
            gfx.TranslateTransform(page.Width.Point / 2, page.Height.Point / 2);
            gfx.RotateTransform(-45);
            var format = new XStringFormat { Alignment = XStringAlignment.Center, LineAlignment = XLineAlignment.Center };
            gfx.DrawString("VOID", watermarkFont, XBrushes.LightGray, new XPoint(0, 0), format);
            gfx.RotateTransform(45);
            gfx.TranslateTransform(-page.Width.Point / 2, -page.Height.Point / 2);
        }

        // Header
        gfx.DrawString("TAX INVOICE", titleFont, XBrushes.Black, new XRect(0, 40, page.Width.Point, 30), XStringFormats.Center);
        
        // Org Details
        gfx.DrawString($"Seller: {model.OrgName}", headerFont, XBrushes.Black, new XPoint(40, 80));
        gfx.DrawString($"GSTIN: {model.OrgGstin ?? "N/A"}", normalFont, XBrushes.Black, new XPoint(40, 95));
        if (!string.IsNullOrEmpty(model.OrgAddress))
            gfx.DrawString($"Address: {model.OrgAddress}", normalFont, XBrushes.Black, new XPoint(40, 110));

        // Customer Details
        gfx.DrawString($"Buyer: {model.CustomerName}", headerFont, XBrushes.Black, new XPoint(page.Width.Point / 2, 80));
        gfx.DrawString($"GSTIN: {model.Invoice.ContactGstin ?? "N/A"}", normalFont, XBrushes.Black, new XPoint(page.Width.Point / 2, 95));
        if (!string.IsNullOrEmpty(model.Invoice.BillingAddress))
            gfx.DrawString($"Address: {model.Invoice.BillingAddress}", normalFont, XBrushes.Black, new XPoint(page.Width.Point / 2, 110));

        // Document Info
        gfx.DrawString($"Invoice No: {model.Invoice.DocumentNo}", headerFont, XBrushes.Black, new XPoint(40, 140));
        gfx.DrawString($"Date: {model.Invoice.DocumentDate:dd-MMM-yyyy}", normalFont, XBrushes.Black, new XPoint(40, 155));
        if (model.Invoice.DueDate.HasValue)
            gfx.DrawString($"Due Date: {model.Invoice.DueDate.Value:dd-MMM-yyyy}", normalFont, XBrushes.Black, new XPoint(40, 170));

        // Table Header
        int y = 200;
        gfx.DrawRectangle(XPens.Black, new XRect(40, y, page.Width.Point - 80, 20));
        gfx.DrawString("Item", headerFont, XBrushes.Black, new XPoint(45, y + 14));
        gfx.DrawString("HSN", headerFont, XBrushes.Black, new XPoint(250, y + 14));
        gfx.DrawString("Qty", headerFont, XBrushes.Black, new XPoint(320, y + 14));
        gfx.DrawString("Rate", headerFont, XBrushes.Black, new XPoint(370, y + 14));
        gfx.DrawString("Taxable", headerFont, XBrushes.Black, new XPoint(430, y + 14));
        gfx.DrawString("Total", headerFont, XBrushes.Black, new XPoint(490, y + 14));
        y += 20;

        // Lines
        foreach (var line in model.Invoice.Lines)
        {
            string itemName = line.ItemId.HasValue && model.ItemNames.TryGetValue(line.ItemId.Value, out var name) ? name : (line.Description ?? "Unknown");
            gfx.DrawString(itemName, normalFont, XBrushes.Black, new XPoint(45, y + 14));
            gfx.DrawString(line.HsnSacCode ?? "", normalFont, XBrushes.Black, new XPoint(250, y + 14));
            gfx.DrawString(line.Quantity.ToString("0.##"), normalFont, XBrushes.Black, new XPoint(320, y + 14));
            gfx.DrawString(line.UnitPrice.ToString("0.00"), normalFont, XBrushes.Black, new XPoint(370, y + 14));
            gfx.DrawString(line.TaxableAmount.ToString("0.00"), normalFont, XBrushes.Black, new XPoint(430, y + 14));
            gfx.DrawString(line.LineTotal.ToString("0.00"), normalFont, XBrushes.Black, new XPoint(490, y + 14));
            y += 20;
        }

        // Totals
        y += 10;
        gfx.DrawString($"Subtotal: {model.Invoice.SubTotal:0.00}", normalFont, XBrushes.Black, new XPoint(430, y + 14));
        y += 15;
        if (model.Invoice.CgstAmount > 0)
        {
            gfx.DrawString($"CGST: {model.Invoice.CgstAmount:0.00}", normalFont, XBrushes.Black, new XPoint(430, y + 14));
            y += 15;
        }
        if (model.Invoice.SgstAmount > 0)
        {
            gfx.DrawString($"SGST: {model.Invoice.SgstAmount:0.00}", normalFont, XBrushes.Black, new XPoint(430, y + 14));
            y += 15;
        }
        if (model.Invoice.IgstAmount > 0)
        {
            gfx.DrawString($"IGST: {model.Invoice.IgstAmount:0.00}", normalFont, XBrushes.Black, new XPoint(430, y + 14));
            y += 15;
        }
        if (model.Invoice.RoundOffAmount != 0)
        {
            gfx.DrawString($"Round Off: {model.Invoice.RoundOffAmount:0.00}", normalFont, XBrushes.Black, new XPoint(430, y + 14));
            y += 15;
        }
        
        gfx.DrawString($"Total: {model.Invoice.TotalAmount:0.00}", headerFont, XBrushes.Black, new XPoint(430, y + 14));
        y += 25;

        // Amount in Words
        string words = ConvertAmountToWords(model.Invoice.TotalAmount);
        gfx.DrawString($"Amount in Words: {words}", headerFont, XBrushes.Black, new XPoint(40, y + 14));

        using var ms = new MemoryStream();
        doc.Save(ms, false);
        return ms.ToArray();
    }

    private string ConvertAmountToWords(decimal amount)
    {
        if (amount == 0) return "Zero Only";
        return "Rupees " + amount.ToString("0.00") + " Only"; // A placeholder for Indian amount-in-words logic
    }
}
