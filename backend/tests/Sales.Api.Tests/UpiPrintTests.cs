using Sales.Repository.Clients;
using Sales.Repository.Repositories;
using Shared.Kernel.Documents;
using Shared.Kernel.Printing;
using Xunit;

namespace Sales.Api.Tests;

/// <summary>
/// What an invoice's UPI payment QR asks for (TK-158): the whole total on a
/// draft, what is still outstanding once posted, and nothing when paid, voided
/// or when the ledger could not say.
/// </summary>
public sealed class UpiPrintTests
{
    [Theory]
    [InlineData(DocumentStatus.Draft, 118, null, 118)]
    [InlineData(DocumentStatus.ReadyToPost, 118, null, 118)]
    [InlineData(DocumentStatus.Posted, 118, 50.0, 50)]
    [InlineData(DocumentStatus.Posted, 118, 0.0, 0)]
    public void The_qr_asks_for_what_is_still_due(DocumentStatus status, double total, double? outstanding, double expected)
    {
        Settlement? settlement = outstanding is double o ? new Settlement((decimal)total, (decimal)total - (decimal)o, (decimal)o) : null;

        Assert.Equal((decimal)expected, InvoicePrintRepository.AmountDue(status, (decimal)total, settlement));
    }

    [Fact]
    public void A_posted_invoice_whose_settlement_could_not_be_read_asks_for_nothing() =>
        Assert.Null(InvoicePrintRepository.AmountDue(DocumentStatus.Posted, 118m, null));

    [Fact]
    public void A_voided_invoice_asks_for_nothing() =>
        Assert.Null(InvoicePrintRepository.AmountDue(DocumentStatus.Void, 118m, new Settlement(118m, 0m, 118m)));

    [Fact]
    public void The_payload_key_is_the_catalog_tag_offered_on_invoices_only()
    {
        Assert.Equal("Payment.UpiQr", InvoicePrintRepository.UpiQrTag);
        Assert.Equal(PlaceholderType.QrCode, PlaceholderCatalog.Find("INV", InvoicePrintRepository.UpiQrTag)?.Type);
        Assert.Null(PlaceholderCatalog.Find("QTE", InvoicePrintRepository.UpiQrTag));
    }
}
