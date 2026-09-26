using Microsoft.EntityFrameworkCore;
using Sales.Api.Services;
using Sales.Api.Services.Pdf;
using Sales.Entity.TableEntities;
using Sales.Repository;
using Shared.Kernel.Documents;
using Shared.Kernel.Storage;
using Shared.Kernel.Tenancy;
using Xunit;

namespace Sales.Api.Tests;

/// <summary>
/// A contact's own invoices in the client portal (TK-95): posted and voided
/// ones only, never a draft, and another contact's invoice answers as one that
/// does not exist — on the list, the invoice and its PDF.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class PortalInvoiceTests
{
    private const long Contact = 42;
    private const long Stranger = 43;

    private static readonly DateOnly Today = new(2026, 9, 26);

    private readonly PostgresFixture _pg;

    public PortalInvoiceTests(PostgresFixture pg) => _pg = pg;

    [Fact]
    public void An_invoices_status_reads_paid_overdue_part_paid_open_or_void()
    {
        Invoice due(DateOnly? on, DocumentStatus status = DocumentStatus.Posted) => new() { DueDate = on, Status = status };

        Assert.Equal("Void", PortalInvoiceService.StatusOf(due(null, DocumentStatus.Void), null, Today));
        Assert.Equal("Paid", PortalInvoiceService.StatusOf(due(Today.AddDays(-9)), new Settlement(100m, 99.995m, 0.005m), Today));
        Assert.Equal("Overdue", PortalInvoiceService.StatusOf(due(Today.AddDays(-1)), new Settlement(100m, 40m, 60m), Today));
        Assert.Equal("PartPaid", PortalInvoiceService.StatusOf(due(Today), new Settlement(100m, 40m, 60m), Today));
        Assert.Equal("Open", PortalInvoiceService.StatusOf(due(Today.AddDays(5)), new Settlement(100m, 0m, 100m), Today));

        // Without Accounting's answer an invoice is never called paid.
        Assert.Equal("Open", PortalInvoiceService.StatusOf(due(Today.AddDays(5)), null, Today));
        Assert.Equal("Overdue", PortalInvoiceService.StatusOf(due(Today.AddDays(-5)), null, Today));
    }

    [SkippableFact]
    public async Task The_list_holds_the_contacts_posted_and_voided_invoices_and_never_a_draft()
    {
        Harness h = await Harness.CreateAsync(_pg);
        long posted = await h.InvoiceAsync(Contact, DocumentStatus.Posted, "INV/26/0001");
        long voided = await h.InvoiceAsync(Contact, DocumentStatus.Void, "INV/26/0002");
        await h.InvoiceAsync(Contact, DocumentStatus.Draft, "INV/26/0003");
        await h.InvoiceAsync(Stranger, DocumentStatus.Posted, "INV/26/0004");
        h.Ledger.Settlements[("INV", posted)] = new Settlement(1_180m, 180m, 1_000m);

        List<PortalInvoiceItem> list = await h.Portal.ListAsync(Contact, default);

        Assert.Equal([posted, voided], list.Select(i => i.InvoiceId).Order());
        PortalInvoiceItem open = list.Single(i => i.InvoiceId == posted);
        Assert.Equal(1_000m, open.OutstandingAmount);
        Assert.Equal("PartPaid", open.Status);
        Assert.Equal("Void", list.Single(i => i.InvoiceId == voided).Status);

        // Only posted invoices are asked about; a voided one owes nothing.
        Assert.Equal([posted], h.Ledger.SettlementQueries.SelectMany(q => q.TransactionIds));
    }

    [SkippableFact]
    public async Task Another_contacts_invoice_and_a_draft_are_not_found_as_an_invoice_or_a_pdf()
    {
        Harness h = await Harness.CreateAsync(_pg);
        long theirs = await h.InvoiceAsync(Stranger, DocumentStatus.Posted, "INV/26/0010");
        long draft = await h.InvoiceAsync(Contact, DocumentStatus.Draft, "INV/26/0011");
        await h.FileAsync(theirs);

        Assert.Null(await h.Portal.GetAsync(Contact, theirs, default));
        Assert.Null(await h.Portal.PdfAsync(Contact, theirs, default));
        Assert.Null(await h.Portal.GetAsync(Contact, draft, default));
        Assert.Null(await h.Portal.PdfAsync(Contact, draft, default));
    }

    [SkippableFact]
    public async Task The_contacts_own_invoice_opens_with_its_lines_and_its_archived_pdf()
    {
        Harness h = await Harness.CreateAsync(_pg);
        long mine = await h.InvoiceAsync(Contact, DocumentStatus.Posted, "INV/26/0020");
        await h.FileAsync(mine);

        PortalInvoiceDetail detail = (await h.Portal.GetAsync(Contact, mine, default))!;
        Assert.Equal("INV/26/0020", detail.Invoice.DocumentNo);
        PortalInvoiceLine line = Assert.Single(detail.Lines);
        Assert.Equal("Widget", line.Description);
        Assert.Equal(180m, detail.TaxAmount);

        ArchivedPdf pdf = (await h.Portal.PdfAsync(Contact, mine, default))!;
        Assert.Equal("INV-26-0020.pdf", pdf.FileName);
    }

    private sealed record Harness(SalesDbContext Db, TenantContext Tenant, PortalInvoiceService Portal, RecordingLedger Ledger, RecordingDocumentStorage Storage)
    {
        public static async Task<Harness> CreateAsync(PostgresFixture pg)
        {
            Skip.If(pg.SkipReason is not null, pg.SkipReason ?? string.Empty);

            TenantContext tenant = new() { CustomerId = Guid.NewGuid(), OrgId = Guid.NewGuid(), CustomerCode = "0000000042" };
            SalesDbContext db = pg.CreateContext(tenant.CustomerId!.Value, tenant.OrgId!.Value);
            RecordingLedger ledger = new();
            RecordingDocumentStorage storage = new();

            PortalInvoiceService portal = new(
                db, ledger, tenant, new StubNameLookup(), TestArchive.For(db, tenant, storage), new FixedClock());

            return new Harness(db, tenant, portal, ledger, storage);
        }

        public async Task<long> InvoiceAsync(long contactId, DocumentStatus status, string number)
        {
            bool posted = status != DocumentStatus.Draft;
            var invoice = new Invoice
            {
                OrgId = Tenant.OrgId!.Value,
                TransactionTypeCode = "INV",
                DocumentNo = number,
                DocumentDate = new DateOnly(2026, 9, 1),
                DueDate = new DateOnly(2026, 10, 1),
                ContactId = contactId,
                CurrencyCode = "INR",
                ExchangeRate = 1m,
                SubTotal = 1_000m,
                TaxableAmount = 1_000m,
                CgstAmount = 90m,
                SgstAmount = 90m,
                TotalAmount = 1_180m,
                TotalAmountBase = 1_180m,
                Status = status,
                PostedAt = posted ? DateTimeOffset.UtcNow : null,
                PostedBy = posted ? Guid.NewGuid() : null,
                VoidedAt = status == DocumentStatus.Void ? DateTimeOffset.UtcNow : null,
                VoidedBy = status == DocumentStatus.Void ? Guid.NewGuid() : null,
                VoidReason = status == DocumentStatus.Void ? "Wrong customer" : null,
                Lines =
                [
                    new InvoiceDetail
                    {
                        OrgId = Tenant.OrgId!.Value,
                        LineNumber = 1,
                        Description = "Widget",
                        Quantity = 10m,
                        BaseQuantity = 10m,
                        ConversionFactor = 1m,
                        UnitPrice = 100m,
                        GrossAmount = 1_000m,
                        TaxableAmount = 1_000m,
                        TaxAmount = 180m,
                        LineTotal = 1_180m,
                    },
                ],
            };

            Db.Invoices.Add(invoice);
            await Db.SaveChangesAsync();
            Db.ChangeTracker.Clear();
            return invoice.InvoiceId;
        }

        /// <summary>Files a PDF where posting would have archived it.</summary>
        public async Task FileAsync(long invoiceId) =>
            await Storage.SaveAsync(
                SalesDocumentArchive.Key(
                    StorageScope.For(Tenant, StorageApp.RetailErp, StorageModule.Sales),
                    ArchivedSalesDocument.Invoice,
                    invoiceId),
                new MemoryStream("%PDF"u8.ToArray()),
                "application/pdf");
    }

    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 9, 26, 6, 0, 0, TimeSpan.Zero);
    }
}
