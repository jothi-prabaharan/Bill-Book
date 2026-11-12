using Sales.Entity.TableEntities;
using Sales.Repository;
using Sales.Repository.Clients;
using Sales.Repository.Repositories;
using Shared.Kernel.Documents;
using Shared.Kernel.Tenancy;
using Xunit;

namespace Sales.Api.Tests;

/// <summary>
/// The name a downloaded sales PDF is saved under (TK-266). No database. The layout tests that stood here
/// drew a fixed PDFsharp page; there is none left to test, because a PDF is Printing's rendering of the
/// document's own template.
/// </summary>
public sealed class SalesDocumentPdfRulesTests
{
    [Theory]
    [InlineData("INV/26-27/0001", "INV-26-27-0001.pdf")]
    [InlineData("CRN-7", "CRN-7.pdf")]
    [InlineData(@"DLC\26\0004", "DLC-26-0004.pdf")]
    public void The_download_is_named_after_the_document_number(string documentNo, string expected)
    {
        Assert.Equal(expected, SalesDocumentPdfRepository.FileName(documentNo));
    }

    [Fact]
    public void A_file_name_never_holds_a_path_separator()
    {
        string name = SalesDocumentPdfRepository.FileName("A/B\\C:D");

        Assert.DoesNotContain('/', name);
        Assert.DoesNotContain('\\', name);
        Assert.EndsWith(".pdf", name);
    }
}

/// <summary>
/// An invoice's PDF, laid out through the invoice's own template (TK-266): what Sales sends Printing, which
/// template it asks for, and who is named as the seller. Written by an AI and not run (section 0.5 of
/// docs/Task_Details.md).
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class InvoicePdfTests
{
    private static readonly DateOnly Dated = new(2026, 11, 2);

    private readonly PostgresFixture _postgres;

    public InvoicePdfTests(PostgresFixture postgres) => _postgres = postgres;

    private sealed record Arrangement(SalesDbContext Db, InvoicePrintRepository Printing, RecordingPrintingClient Recorder);

    private Arrangement Arrange(OrgIdentity? seller = null, bool resolvesBranch = true, Guid? customerId = null)
    {
        SalesDbContext db = _postgres.CreateContext(customerId ?? Guid.NewGuid(), Guid.NewGuid());
        var recorder = new RecordingPrintingClient();
        var names = new StubNameLookup();

        return new Arrangement(
            db,
            new InvoicePrintRepository(db, new StubOrgIdentity(seller) { Resolves = resolvesBranch }, names, names, recorder),
            recorder);
    }

    private static Invoice NewInvoice(DocumentStatus status, long? printTemplateId = null)
    {
        bool posted = status is DocumentStatus.Posted or DocumentStatus.Void;
        bool voided = status == DocumentStatus.Void;

        return new Invoice
        {
            TransactionTypeCode = "INV",
            DocumentNo = $"INV/{Guid.NewGuid():N}"[..20],
            DocumentDate = Dated,
            DueDate = Dated.AddDays(30),
            ContactId = 4,
            ContactGstin = "29AABCU9603R1ZX",
            BillingAddress = "1 MG Road",
            CurrencyCode = "INR",
            ExchangeRate = 1m,
            SubTotal = 200m,
            TaxableAmount = 200m,
            CgstAmount = 18m,
            SgstAmount = 18m,
            TotalAmount = 236m,
            TotalAmountBase = 236m,
            Status = status,
            PrintTemplateId = printTemplateId,
            PostedAt = posted ? DateTimeOffset.UtcNow : null,
            PostedBy = posted ? Guid.NewGuid() : null,
            VoidedAt = voided ? DateTimeOffset.UtcNow : null,
            VoidedBy = voided ? Guid.NewGuid() : null,
            VoidReason = voided ? "Wrong customer" : null,
            Lines =
            [
                new InvoiceDetail
                {
                    LineNumber = 1,
                    ItemId = 7,
                    Description = "Widget",
                    Quantity = 1m,
                    BaseQuantity = 1m,
                    ConversionFactor = 1m,
                    UnitPrice = 200m,
                    GrossAmount = 200m,
                    TaxableAmount = 200m,
                    TaxAmount = 36m,
                    LineTotal = 236m,
                },
            ],
        };
    }

    private static async Task<Invoice> SaveAsync(Arrangement a, Invoice invoice)
    {
        a.Db.Invoices.Add(invoice);
        await a.Db.SaveChangesAsync();
        a.Db.ChangeTracker.Clear();
        return invoice;
    }

    [SkippableFact]
    public async Task The_pdf_follows_the_template_the_invoice_names_and_the_file_is_named_after_the_invoice()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        Arrangement a = Arrange();
        await using (a.Db)
        {
            Invoice invoice = await SaveAsync(a, NewInvoice(DocumentStatus.Posted, printTemplateId: 77));

            PrintedPdf? pdf = await a.Printing.PrintPdfAsync(invoice.InvoiceId, default);

            Assert.NotNull(pdf);
            Assert.Equal(invoice.DocumentNo, pdf!.DocumentNo);
            Assert.StartsWith("%PDF", System.Text.Encoding.ASCII.GetString(pdf.Content));

            PrintCall call = Assert.Single(a.Recorder.Pdfs);
            Assert.Equal("INV", call.DocumentTypeCode);
            Assert.Equal(77, call.PrintTemplateId);
            Assert.Null(call.Watermark);

            // Asking for the file does not also lay out a page.
            Assert.Empty(a.Recorder.Pages);
        }
    }

    [SkippableFact]
    public async Task An_invoice_that_names_no_template_asks_for_none_so_printing_falls_back_to_the_branch_default()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        Arrangement a = Arrange();
        await using (a.Db)
        {
            Invoice invoice = await SaveAsync(a, NewInvoice(DocumentStatus.Posted));

            await a.Printing.PrintPdfAsync(invoice.InvoiceId, default);

            Assert.Null(Assert.Single(a.Recorder.Pdfs).PrintTemplateId);
        }
    }

    [SkippableFact]
    public async Task A_draft_invoices_pdf_is_stamped_proforma_and_a_voided_ones_void()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        Arrangement a = Arrange();
        await using (a.Db)
        {
            Invoice draft = await SaveAsync(a, NewInvoice(DocumentStatus.Draft));
            Invoice voided = await SaveAsync(a, NewInvoice(DocumentStatus.Void));

            await a.Printing.PrintPdfAsync(draft.InvoiceId, default);
            await a.Printing.PrintPdfAsync(voided.InvoiceId, default);

            Assert.Equal(["PROFORMA", "VOID"], a.Recorder.Pdfs.Select(c => c.Watermark));
        }
    }

    /// <summary>
    /// A document's PDF and the page that shows it come from one payload, one template and one stamp, so the
    /// file a customer is sent cannot differ from what the user looked at.
    /// </summary>
    [SkippableFact]
    public async Task The_pdf_and_the_page_are_laid_out_from_the_same_payload_template_and_stamp()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        Arrangement a = Arrange();
        await using (a.Db)
        {
            Invoice invoice = await SaveAsync(a, NewInvoice(DocumentStatus.Draft, printTemplateId: 77));

            await a.Printing.PrintAsync(invoice.InvoiceId, default);
            await a.Printing.PrintPdfAsync(invoice.InvoiceId, default);

            PrintCall page = Assert.Single(a.Recorder.Pages);
            PrintCall file = Assert.Single(a.Recorder.Pdfs);

            Assert.Equal(page.DocumentTypeCode, file.DocumentTypeCode);
            Assert.Equal(page.PrintTemplateId, file.PrintTemplateId);
            Assert.Equal(page.Watermark, file.Watermark);
            Assert.Equal(page.Payload.Singles, file.Payload.Singles);
            Assert.Equal(page.Payload.Rows("Item").Count, file.Payload.Rows("Item").Count);
        }
    }

    /// <summary>
    /// The seller block on a printed invoice is the branch's own, read from Master. It was once two
    /// hard-coded lines — a name and a GSTIN belonging to nobody — printed on every customer's tax invoices.
    /// A tax invoice names the supplier whose registration the buyer claims an input credit against, so a
    /// placeholder there is not a cosmetic fault.
    /// </summary>
    [SkippableFact]
    public async Task Each_branchs_pdf_names_its_own_seller_and_gstin()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        Arrangement first = Arrange(new OrgIdentity("Kumar Traders", "33BBBBB1111B1Z9", "9 Mount Road", null, "Chennai", "33", "600002"));
        Arrangement second = Arrange(new OrgIdentity("Sharma Stores", "29CCCCC2222C1Z8", null, null, "Bengaluru", "29", null));
        await using (first.Db)
        await using (second.Db)
        {
            foreach (Arrangement a in new[] { first, second })
            {
                Invoice invoice = await SaveAsync(a, NewInvoice(DocumentStatus.Posted));
                await a.Printing.PrintPdfAsync(invoice.InvoiceId, default);
            }

            var firstSeller = Assert.Single(first.Recorder.Pdfs).Payload.Singles;
            var secondSeller = Assert.Single(second.Recorder.Pdfs).Payload.Singles;

            Assert.Equal("Kumar Traders", firstSeller["Organization.Name"]);
            Assert.Equal("33BBBBB1111B1Z9", firstSeller["Organization.Gstin"]);
            Assert.Equal("Sharma Stores", secondSeller["Organization.Name"]);
            Assert.Equal("29CCCCC2222C1Z8", secondSeller["Organization.Gstin"]);
        }
    }

    /// <summary>
    /// A branch Master cannot resolve stops the PDF instead of printing a placeholder. An invoice with the
    /// wrong seller on it is worse than no invoice, because it looks filed.
    /// </summary>
    [SkippableFact]
    public async Task A_branch_that_cannot_be_identified_stops_the_pdf_rather_than_printing_a_placeholder()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        Arrangement a = Arrange(resolvesBranch: false);
        await using (a.Db)
        {
            Invoice invoice = await SaveAsync(a, NewInvoice(DocumentStatus.Posted));

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => a.Printing.PrintPdfAsync(invoice.InvoiceId, default));
            Assert.Empty(a.Recorder.Pdfs);
        }
    }

    [SkippableFact]
    public async Task Another_branchs_invoice_has_no_pdf_and_nothing_is_rendered_for_it()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        Guid customerId = Guid.NewGuid();
        Arrangement mine = Arrange(customerId: customerId);
        Arrangement theirs = Arrange(customerId: customerId);
        await using (mine.Db)
        await using (theirs.Db)
        {
            Invoice invoice = await SaveAsync(mine, NewInvoice(DocumentStatus.Posted));

            Assert.NotNull(await mine.Printing.PrintPdfAsync(invoice.InvoiceId, default));
            Assert.Null(await theirs.Printing.PrintPdfAsync(invoice.InvoiceId, default));
            Assert.Empty(theirs.Recorder.Pdfs);
        }
    }
}
