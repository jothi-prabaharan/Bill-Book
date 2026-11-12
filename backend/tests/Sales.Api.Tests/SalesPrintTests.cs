using Sales.Entity.Enums;
using Sales.Entity.TableEntities;
using Sales.Repository;
using Sales.Repository.Clients;
using Sales.Repository.Repositories;
using Shared.Kernel.Documents;
using Shared.Kernel.Printing;
using Xunit;

namespace Sales.Api.Tests;

/// <summary>
/// The rules a quote, sales order, delivery challan or credit note prints by (TK-263), pure. Written by an AI
/// and not run (section 0.5 of docs/Task_Details.md).
/// </summary>
public sealed class SalesPrintRulesTests
{
    [Theory]
    [InlineData(CreditNoteReason.SalesReturn, "Goods returned")]
    [InlineData(CreditNoteReason.PriceCorrection, "Price correction")]
    [InlineData(CreditNoteReason.PostSaleDiscount, "Post-sale discount")]
    [InlineData(CreditNoteReason.Deficiency, "Short delivery or damage")]
    [InlineData(CreditNoteReason.Cancellation, "Invoice cancelled")]
    public void A_credit_note_reason_reads_as_the_customer_would_say_it(CreditNoteReason reason, string expected)
    {
        Assert.Equal(expected, SalesPrintRepository.ReasonLabel(reason));
    }

    [Fact]
    public void Every_credit_note_reason_has_a_label_of_its_own()
    {
        string[] labels = [.. Enum.GetValues<CreditNoteReason>().Select(SalesPrintRepository.ReasonLabel)];

        Assert.Equal(labels.Length, labels.Distinct().Count());
        Assert.All(labels, label => Assert.DoesNotContain("Cancellation", label));
    }

    [Theory]
    [InlineData(DocumentStatus.Posted, null, null)]
    [InlineData(DocumentStatus.Posted, EInvoiceStatus.Registered, null)]
    [InlineData(DocumentStatus.Posted, EInvoiceStatus.Pending, "IRN PENDING")]
    [InlineData(DocumentStatus.Posted, EInvoiceStatus.Failed, "IRN PENDING")]
    [InlineData(DocumentStatus.Draft, null, "DRAFT")]
    [InlineData(DocumentStatus.Draft, EInvoiceStatus.Pending, "DRAFT")]
    [InlineData(DocumentStatus.Void, null, "VOID")]
    public void A_posted_credit_note_without_its_irn_says_so_and_otherwise_the_status_decides(
        DocumentStatus status, EInvoiceStatus? eInvoice, string? expected)
    {
        Assert.Equal(expected, SalesPrintRepository.CreditNoteWatermark(status, eInvoice));
    }
}

/// <summary>
/// The four sales documents with no print route before TK-263, laid out through their repository: what each
/// sends Printing, which template type it asks for, and what it tells the view page about itself. Written by
/// an AI and not run (section 0.5 of docs/Task_Details.md).
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class SalesPrintTests
{
    private static readonly DateOnly Dated = new(2026, 11, 2);

    private readonly PostgresFixture _postgres;

    public SalesPrintTests(PostgresFixture postgres) => _postgres = postgres;

    private sealed class RecordingPrinting : IPrintingClient
    {
        public List<(string Type, long? TemplateId, PrintPayload Payload, string? Watermark)> Printed { get; } = [];

        /// <summary>The same documents asked for as a PDF (TK-266).</summary>
        public List<(string Type, long? TemplateId, PrintPayload Payload, string? Watermark)> Pdfs { get; } = [];

        public Task<PrintedDocument> RenderAsync(
            string documentTypeCode, long? printTemplateId, PrintPayload payload, string? watermark, CancellationToken ct)
        {
            Printed.Add((documentTypeCode, printTemplateId, payload, watermark));
            return Task.FromResult(new PrintedDocument { Html = "<p>laid out</p>", PageCount = 1 });
        }

        public Task<byte[]> RenderPdfAsync(
            string documentTypeCode, long? printTemplateId, PrintPayload payload, string? watermark, CancellationToken ct)
        {
            Pdfs.Add((documentTypeCode, printTemplateId, payload, watermark));
            return Task.FromResult("%PDF"u8.ToArray());
        }
    }

    private sealed record Arrangement(SalesDbContext Db, SalesPrintRepository Printing, RecordingPrinting Recorder);

    private Arrangement Arrange(Guid? customerId = null, Guid? orgId = null, bool resolvesBranch = true)
    {
        SalesDbContext db = _postgres.CreateContext(customerId ?? Guid.NewGuid(), orgId ?? Guid.NewGuid());
        var recorder = new RecordingPrinting();
        var names = new StubNameLookup();

        return new Arrangement(
            db,
            new SalesPrintRepository(db, new StubOrgIdentity { Resolves = resolvesBranch }, names, names, recorder),
            recorder);
    }

    private static string Number(string prefix) => $"{prefix}/{Guid.NewGuid():N}"[..20];

    private static T Fill<T>(T document, string code, DocumentStatus status, string number) where T : DocumentHeaderBase
    {
        document.TransactionTypeCode = code;
        document.DocumentNo = number;
        document.DocumentDate = Dated;
        document.ContactId = 4;
        document.ContactGstin = "29AABCU9603R1ZX";
        document.BillingAddress = "1 MG Road";
        document.CurrencyCode = "INR";
        document.ExchangeRate = 1m;
        document.SubTotal = 200m;
        document.TaxableAmount = 200m;
        document.CgstAmount = 18m;
        document.SgstAmount = 18m;
        document.TotalAmount = 236m;
        document.Status = status;
        return document;
    }

    private static QuoteDetail QuoteLine() => new()
    {
        LineNumber = 1,
        ItemId = 7,
        Quantity = 2m,
        UnitPrice = 100m,
        TaxableAmount = 200m,
        LineTotal = 236m,
        Taxes =
        [
            new QuoteDetailTax { TaxComponent = TaxComponent.Cgst, Rate = 9m, TaxableAmount = 200m, Amount = 18m },
            new QuoteDetailTax { TaxComponent = TaxComponent.Sgst, Rate = 9m, TaxableAmount = 200m, Amount = 18m },
        ],
    };

    [SkippableFact]
    public async Task A_quote_is_laid_out_as_a_quote_and_says_which_one_it_is()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        Arrangement a = Arrange();
        await using (a.Db)
        {
            string number = Number("QT");
            Quote quote = Fill(new Quote { ValidUntil = Dated.AddDays(30), Lines = [QuoteLine()] }, "QTE", DocumentStatus.Posted, number);
            a.Db.Quotes.Add(quote);
            await a.Db.SaveChangesAsync();

            PrintedDocument? printed = await a.Printing.PrintQuoteAsync(quote.QuoteId, default);

            Assert.NotNull(printed);
            Assert.Equal("<p>laid out</p>", printed.Html);
            Assert.Equal(number, printed.DocumentNo);
            Assert.Equal("QTE", printed.DocumentTypeCode);
            Assert.Equal("Posted", printed.Status);

            var sent = Assert.Single(a.Recorder.Printed);
            Assert.Equal("QTE", sent.Type);
            Assert.Null(sent.Watermark);
            Assert.Equal(number, sent.Payload.Singles["Document.No"]);
            Assert.Equal("Name 4", sent.Payload.Singles["Party.Name"]);
            Assert.Equal("Test Traders", sent.Payload.Singles["Organization.Name"]);

            // A sale's place of supply is the customer's state from their GSTIN.
            Assert.Equal("29", sent.Payload.Singles["Document.PlaceOfSupply"]);
            Assert.Equal(2, sent.Payload.Rows("Tax").Count);
        }
    }

    [SkippableFact]
    public async Task A_draft_quote_is_stamped_draft()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        Arrangement a = Arrange();
        await using (a.Db)
        {
            Quote quote = Fill(new Quote { ValidUntil = Dated, Lines = [QuoteLine()] }, "QTE", DocumentStatus.Draft, Number("QT"));
            a.Db.Quotes.Add(quote);
            await a.Db.SaveChangesAsync();

            await a.Printing.PrintQuoteAsync(quote.QuoteId, default);

            Assert.Equal("DRAFT", Assert.Single(a.Recorder.Printed).Watermark);
        }
    }

    [SkippableFact]
    public async Task A_sales_order_names_the_quote_it_came_from_and_its_delivery_date()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        Arrangement a = Arrange();
        await using (a.Db)
        {
            string quoteNo = Number("QT");
            Quote quote = Fill(new Quote { ValidUntil = Dated }, "QTE", DocumentStatus.Posted, quoteNo);
            a.Db.Quotes.Add(quote);
            await a.Db.SaveChangesAsync();

            SalesOrder order = Fill(
                new SalesOrder
                {
                    QuoteId = quote.QuoteId,
                    DeliveryDate = Dated.AddDays(7),
                    Lines = [new SalesOrderDetail { LineNumber = 1, ItemId = 7, Quantity = 1m, UnitPrice = 200m, TaxableAmount = 200m, LineTotal = 236m }],
                },
                "SOR", DocumentStatus.Posted, Number("SO"));
            a.Db.SalesOrders.Add(order);
            await a.Db.SaveChangesAsync();

            PrintedDocument? printed = await a.Printing.PrintOrderAsync(order.SalesOrderId, default);

            Assert.Equal("SOR", printed!.DocumentTypeCode);
            var sent = Assert.Single(a.Recorder.Printed);
            Assert.Equal(quoteNo, sent.Payload.Singles["Document.OriginalDocumentNo"]);
            Assert.Equal(Dated.AddDays(7), sent.Payload.Singles["Document.DeliveryDate"]);
        }
    }

    [SkippableFact]
    public async Task A_delivery_challan_carries_its_dispatch_and_transport_details()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        Arrangement a = Arrange();
        await using (a.Db)
        {
            DeliveryChallan challan = Fill(
                new DeliveryChallan
                {
                    DispatchDate = Dated.AddDays(1),
                    VehicleNo = "TN01AB1234",
                    TransporterName = "Fast Freight",
                    EwayBillNo = "331009876543",
                    EwayBillDate = Dated,
                    Lines = [new DeliveryChallanDetail { LineNumber = 1, ItemId = 7, Quantity = 1m, UnitPrice = 200m, TaxableAmount = 200m, LineTotal = 236m }],
                },
                "DLC", DocumentStatus.Posted, Number("DC"));
            a.Db.DeliveryChallans.Add(challan);
            await a.Db.SaveChangesAsync();

            PrintedDocument? printed = await a.Printing.PrintChallanAsync(challan.DeliveryChallanId, default);

            Assert.Equal("DLC", printed!.DocumentTypeCode);
            var singles = Assert.Single(a.Recorder.Printed).Payload.Singles;
            Assert.Equal(Dated.AddDays(1), singles["Document.DeliveryDate"]);
            Assert.Equal("TN01AB1234", singles["Document.VehicleNo"]);
            Assert.Equal("Fast Freight", singles["Document.TransporterName"]);
            Assert.Equal("331009876543", singles["Document.EwayBillNo"]);
            Assert.Equal(Dated, singles["Document.EwayBillDate"]);
        }
    }

    [SkippableFact]
    public async Task A_credit_note_names_its_invoice_and_its_reason()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        Arrangement a = Arrange();
        await using (a.Db)
        {
            string invoiceNo = Number("INV");
            Invoice invoice = Fill(new Invoice { DueDate = Dated }, "INV", DocumentStatus.Posted, invoiceNo);
            a.Db.Invoices.Add(invoice);
            await a.Db.SaveChangesAsync();

            CreditNote note = Fill(
                new CreditNote
                {
                    InvoiceId = invoice.InvoiceId,
                    ReasonCode = CreditNoteReason.PriceCorrection,
                    Lines = [new CreditNoteDetail { LineNumber = 1, ItemId = 7, Quantity = 1m, UnitPrice = 200m, TaxableAmount = 200m, LineTotal = 236m }],
                },
                "CRN", DocumentStatus.Posted, Number("CN"));
            a.Db.CreditNotes.Add(note);
            await a.Db.SaveChangesAsync();

            PrintedDocument? printed = await a.Printing.PrintCreditNoteAsync(note.CreditNoteId, default);

            Assert.Equal("CRN", printed!.DocumentTypeCode);
            var sent = Assert.Single(a.Recorder.Printed);
            Assert.Equal("Price correction", sent.Payload.Singles["Document.Reason"]);
            Assert.Equal(invoiceNo, sent.Payload.Singles["Document.OriginalDocumentNo"]);

            // Never registered with the IRP, so no IRN is printed and nothing says one is pending.
            Assert.False(sent.Payload.Singles.ContainsKey("EInvoice.Irn"));
            Assert.Null(sent.Watermark);
        }
    }

    [SkippableFact]
    public async Task A_document_in_no_branch_of_the_callers_is_not_found_and_nothing_is_rendered()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        Arrangement a = Arrange();
        await using (a.Db)
        {
            Assert.Null(await a.Printing.PrintQuoteAsync(987_654, default));
            Assert.Null(await a.Printing.PrintOrderAsync(987_654, default));
            Assert.Null(await a.Printing.PrintChallanAsync(987_654, default));
            Assert.Null(await a.Printing.PrintCreditNoteAsync(987_654, default));
            Assert.Empty(a.Recorder.Printed);
        }
    }

    [SkippableFact]
    public async Task Another_branchs_quote_reads_as_not_found()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        Guid customerId = Guid.NewGuid();
        Arrangement mine = Arrange(customerId, Guid.NewGuid());
        Arrangement theirs = Arrange(customerId, Guid.NewGuid());
        await using (mine.Db)
        await using (theirs.Db)
        {
            Quote quote = Fill(new Quote { ValidUntil = Dated }, "QTE", DocumentStatus.Posted, Number("QT"));
            mine.Db.Quotes.Add(quote);
            await mine.Db.SaveChangesAsync();

            Assert.NotNull(await mine.Printing.PrintQuoteAsync(quote.QuoteId, default));
            Assert.Null(await theirs.Printing.PrintQuoteAsync(quote.QuoteId, default));
        }
    }

    [SkippableFact]
    public async Task A_branch_that_cannot_be_identified_stops_the_print_rather_than_printing_a_placeholder()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        Arrangement a = Arrange(resolvesBranch: false);
        await using (a.Db)
        {
            Quote quote = Fill(new Quote { ValidUntil = Dated }, "QTE", DocumentStatus.Posted, Number("QT"));
            a.Db.Quotes.Add(quote);
            await a.Db.SaveChangesAsync();

            await Assert.ThrowsAsync<InvalidOperationException>(() => a.Printing.PrintQuoteAsync(quote.QuoteId, default));
            Assert.Empty(a.Recorder.Printed);
        }
    }

    /// <summary>
    /// TK-266: a document's PDF and the page that shows it are laid out from the same payload, by the same
    /// template and with the same stamp — which is what keeps the file a customer is sent from ever
    /// differing from what the user saw.
    /// </summary>
    [SkippableFact]
    public async Task A_challans_pdf_is_laid_out_from_the_same_payload_template_and_stamp_as_its_page()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        Arrangement a = Arrange();
        await using (a.Db)
        {
            DeliveryChallan challan = Fill(
                new DeliveryChallan
                {
                    DispatchDate = Dated.AddDays(1),
                    VehicleNo = "TN01AB1234",
                    PrintTemplateId = 52,
                    Lines = [new DeliveryChallanDetail { LineNumber = 1, ItemId = 7, Quantity = 1m, UnitPrice = 200m, TaxableAmount = 200m, LineTotal = 236m }],
                },
                "DLC", DocumentStatus.Draft, Number("DC"));
            a.Db.DeliveryChallans.Add(challan);
            await a.Db.SaveChangesAsync();

            PrintedDocument? page = await a.Printing.PrintChallanAsync(challan.DeliveryChallanId, default);
            PrintedPdf? pdf = await a.Printing.PrintChallanPdfAsync(challan.DeliveryChallanId, default);

            Assert.NotNull(page);
            Assert.NotNull(pdf);
            Assert.Equal(challan.DocumentNo, pdf!.DocumentNo);
            Assert.StartsWith("%PDF", System.Text.Encoding.ASCII.GetString(pdf.Content));

            var shown = Assert.Single(a.Recorder.Printed);
            var filed = Assert.Single(a.Recorder.Pdfs);
            Assert.Equal("DLC", filed.Type);
            Assert.Equal(52, filed.TemplateId);
            Assert.Equal("DRAFT", filed.Watermark);
            Assert.Equal(shown.Type, filed.Type);
            Assert.Equal(shown.TemplateId, filed.TemplateId);
            Assert.Equal(shown.Watermark, filed.Watermark);
            Assert.Equal(shown.Payload.Singles, filed.Payload.Singles);
            Assert.Equal(shown.Payload.Rows("Item").Count, filed.Payload.Rows("Item").Count);
        }
    }

    [SkippableFact]
    public async Task A_credit_notes_pdf_is_named_after_the_note_and_asks_for_the_template_it_names()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        Arrangement a = Arrange();
        await using (a.Db)
        {
            Invoice invoice = Fill(new Invoice { DueDate = Dated }, "INV", DocumentStatus.Posted, Number("INV"));
            a.Db.Invoices.Add(invoice);
            await a.Db.SaveChangesAsync();

            string noteNo = Number("CN");
            CreditNote note = Fill(
                new CreditNote
                {
                    InvoiceId = invoice.InvoiceId,
                    ReasonCode = CreditNoteReason.SalesReturn,
                    PrintTemplateId = 31,
                    Lines = [new CreditNoteDetail { LineNumber = 1, ItemId = 7, Quantity = 1m, UnitPrice = 200m, TaxableAmount = 200m, LineTotal = 236m }],
                },
                "CRN", DocumentStatus.Posted, noteNo);
            a.Db.CreditNotes.Add(note);
            await a.Db.SaveChangesAsync();

            PrintedPdf? pdf = await a.Printing.PrintCreditNotePdfAsync(note.CreditNoteId, default);

            Assert.Equal(noteNo, pdf!.DocumentNo);
            var filed = Assert.Single(a.Recorder.Pdfs);
            Assert.Equal(("CRN", (long?)31, (string?)null), (filed.Type, filed.TemplateId, filed.Watermark));
            Assert.Equal("Goods returned", filed.Payload.Singles["Document.Reason"]);
            Assert.Empty(a.Recorder.Printed);
        }
    }

    [SkippableFact]
    public async Task A_pdf_for_a_document_in_no_branch_of_the_callers_is_not_found_and_nothing_is_rendered()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        Arrangement a = Arrange();
        await using (a.Db)
        {
            Assert.Null(await a.Printing.PrintChallanPdfAsync(987_654, default));
            Assert.Null(await a.Printing.PrintCreditNotePdfAsync(987_654, default));
            Assert.Empty(a.Recorder.Pdfs);
        }
    }

    [SkippableFact]
    public async Task A_branch_that_cannot_be_identified_stops_the_pdf_rather_than_printing_a_placeholder()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        Arrangement a = Arrange(resolvesBranch: false);
        await using (a.Db)
        {
            DeliveryChallan challan = Fill(
                new DeliveryChallan
                {
                    DispatchDate = Dated,
                    Lines = [new DeliveryChallanDetail { LineNumber = 1, ItemId = 7, Quantity = 1m, UnitPrice = 200m, TaxableAmount = 200m, LineTotal = 236m }],
                },
                "DLC", DocumentStatus.Posted, Number("DC"));
            a.Db.DeliveryChallans.Add(challan);
            await a.Db.SaveChangesAsync();

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => a.Printing.PrintChallanPdfAsync(challan.DeliveryChallanId, default));
            Assert.Empty(a.Recorder.Pdfs);
        }
    }
}
