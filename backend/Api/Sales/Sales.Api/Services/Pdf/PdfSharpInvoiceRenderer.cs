namespace Sales.Api.Services.Pdf;

/// <summary>
/// The invoice's archived PDF: the shared sales layout, titled as a tax invoice.
/// </summary>
public sealed class PdfSharpInvoiceRenderer : IInvoicePdfRenderer
{
    public byte[] Render(PdfInvoiceModel model) => SalesPdfLayout.Render(new SalesPdfModel
    {
        Title = "TAX INVOICE",
        NumberLabel = "Invoice No",
        Header = model.Invoice,
        Lines = model.Invoice.Lines,
        DueDate = model.Invoice.DueDate,
        SellerName = model.OrgName,
        SellerGstin = model.OrgGstin,
        SellerAddress = model.OrgAddress,
        BuyerName = model.CustomerName,
        ItemNames = model.ItemNames,
    });
}
