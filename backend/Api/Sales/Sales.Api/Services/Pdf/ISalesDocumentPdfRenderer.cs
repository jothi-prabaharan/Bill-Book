namespace Sales.Api.Services.Pdf;

/// <summary>Draws the archived PDF of a credit note or delivery challan (TK-22).</summary>
public interface ISalesDocumentPdfRenderer
{
    byte[] Render(SalesPdfModel model);
}

public sealed class PdfSharpSalesDocumentRenderer : ISalesDocumentPdfRenderer
{
    public byte[] Render(SalesPdfModel model) => SalesPdfLayout.Render(model);
}
