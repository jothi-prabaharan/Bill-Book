using Sales.Repository.Clients;

namespace Sales.Repository.Repositories;

/// <summary>Draws the archived PDF of a credit note or delivery challan (TK-22).</summary>
public interface ISalesDocumentPdfRendererRepository
{
    byte[] Render(SalesPdfModel model);
}

public sealed class PdfSharpSalesDocumentRenderer : ISalesDocumentPdfRendererRepository
{
    public byte[] Render(SalesPdfModel model) => SalesPdfLayoutRepository.Render(model);
}
