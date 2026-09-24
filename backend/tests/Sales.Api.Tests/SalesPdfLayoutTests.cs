using Sales.Api.Services.Pdf;
using Sales.Entity.TableEntities;
using Shared.Kernel.Documents;
using Xunit;

namespace Sales.Api.Tests;

/// <summary>
/// The shared layout every archived sales PDF is drawn with, and the names it
/// is filed and downloaded under (TK-22). No database.
/// </summary>
public sealed class SalesPdfLayoutTests
{
    [Theory]
    [InlineData(DocumentStatus.Draft, "PROFORMA")]
    [InlineData(DocumentStatus.ReadyToPost, "PROFORMA")]
    [InlineData(DocumentStatus.Posted, null)]
    [InlineData(DocumentStatus.Void, "VOID")]
    public void The_stamp_follows_the_status(DocumentStatus status, string? expected)
    {
        // The old renderer compared against "Voided", which the enum never says,
        // so a voided document was never stamped.
        Assert.Equal(expected, SalesPdfLayout.Watermark(status));
    }

    [Fact]
    public void A_document_renders_to_a_pdf()
    {
        var invoice = new Invoice
        {
            DocumentNo = "INV/26-27/0001",
            DocumentDate = new DateOnly(2026, 9, 24),
            Status = DocumentStatus.Posted,
            SubTotal = 100m,
            CgstAmount = 9m,
            SgstAmount = 9m,
            TotalAmount = 118m,
            Lines = [new InvoiceDetail { LineNumber = 1, Description = "Soap", Quantity = 1m, UnitPrice = 100m, TaxableAmount = 100m, LineTotal = 100m }],
        };

        byte[] pdf = new PdfSharpInvoiceRenderer().Render(new PdfInvoiceModel
        {
            Invoice = invoice,
            OrgName = "Kumar Traders",
            CustomerName = "Ravi",
        });

        Assert.Equal("%PDF"u8.ToArray(), pdf[..4]);
    }

    [Theory]
    [InlineData("INV/26-27/0001", "INV-26-27-0001.pdf")]
    [InlineData("CRN-7", "CRN-7.pdf")]
    public void The_download_is_named_after_the_document_number(string documentNo, string expected)
    {
        Assert.Equal(expected, SalesDocumentArchive.FileName(documentNo));
    }

    [Theory]
    [InlineData(ArchivedSalesDocument.Invoice, "invoices")]
    [InlineData(ArchivedSalesDocument.CreditNote, "credit-notes")]
    [InlineData(ArchivedSalesDocument.DeliveryChallan, "delivery-challans")]
    public void Each_document_type_has_its_own_folder(ArchivedSalesDocument kind, string folder)
    {
        Assert.Equal(folder, SalesDocumentArchive.Folder(kind));
    }
}
