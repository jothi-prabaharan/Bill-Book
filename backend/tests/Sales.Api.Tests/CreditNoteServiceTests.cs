using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Sales.Api.Services;
using Sales.Entity.Models;
using Sales.Entity.TableEntities;
using Sales.Repository;
using Shared.Kernel.Documents;
using Shared.Kernel.Numbering;
using Shared.Kernel.Tenancy;
using Xunit;

namespace Sales.Api.Tests;

/// <summary>
/// The credit note's save path — against a real PostgreSQL, the same
/// discipline <c>SalesOrderServiceTests</c> uses.
///
/// <b>The same two bugs <c>DeliveryChallanServiceTests</c> covers, on the
/// return leg.</b> <c>SaveAsync</c> never set <c>BaseQuantity</c>,
/// <c>TaxableAmount</c>, <c>GrossAmount</c> or <c>LineNumber</c>, so
/// <c>chk_creditnotedetails_base_quantity</c> refused every save outright —
/// and the tax split never branched on <c>IsInterState</c>, so a credit note
/// against an inter-state invoice would have reversed the wrong head of tax
/// even once the first bug was fixed.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class CreditNoteServiceTests
{
    private readonly PostgresFixture _pg;

    public CreditNoteServiceTests(PostgresFixture pg) => _pg = pg;

    [SkippableFact]
    public async Task An_intra_state_credit_note_saves_with_cgst_and_sgst_and_a_real_base_quantity()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason ?? string.Empty);

        Harness h = await Harness.CreateAsync(_pg);
        InvoiceDetail invoiceLine = await h.SeedInvoiceLineAsync();

        long id = await h.Service.SaveAsync(
            null,
            Request(
                h.InvoiceId,
                invoiceLine.InvoiceDetailId,
                contactGstin: "33AAAAA0000A1Z5",
                [Line(quantity: 4m, unitPrice: 100m)]),
            default);

        CreditNote saved = await h.Db.CreditNotes
            .Include(x => x.Lines).ThenInclude(l => l.Taxes)
            .SingleAsync(x => x.CreditNoteId == id);

        Assert.False(saved.IsInterState);

        // 4 × 100 = 400 taxable, 18% = 72 split 36 CGST / 36 SGST, total 472.
        Assert.Equal(400m, saved.TaxableAmount);
        Assert.Equal(36m, saved.CgstAmount);
        Assert.Equal(36m, saved.SgstAmount);
        Assert.Equal(0m, saved.IgstAmount);
        Assert.Equal(472m, saved.TotalAmount);

        // The line itself — the four columns SaveAsync used to leave at zero.
        CreditNoteDetail line = saved.Lines.Single();
        Assert.Equal(1, line.LineNumber);
        Assert.Equal(4m, line.BaseQuantity);
        Assert.Equal(400m, line.GrossAmount);
        Assert.Equal(400m, line.TaxableAmount);
        Assert.Equal(invoiceLine.InvoiceDetailId, line.InvoiceDetailId);

        Assert.Equal(2, line.Taxes.Count);
        Assert.Contains(line.Taxes, t => t.TaxComponent == TaxComponent.Cgst && t.Amount == 36m);
        Assert.Contains(line.Taxes, t => t.TaxComponent == TaxComponent.Sgst && t.Amount == 36m);
    }

    [SkippableFact]
    public async Task An_inter_state_credit_note_saves_with_igst_only()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason ?? string.Empty);

        Harness h = await Harness.CreateAsync(_pg);
        InvoiceDetail invoiceLine = await h.SeedInvoiceLineAsync();

        // 07 is Delhi; the branch is seeded at 33 (Tamil Nadu) — see StubBranchSettings.
        long id = await h.Service.SaveAsync(
            null,
            Request(
                h.InvoiceId,
                invoiceLine.InvoiceDetailId,
                contactGstin: "07AAAAA0000A1Z5",
                [Line(quantity: 2m, unitPrice: 250m)]),
            default);

        CreditNote saved = await h.Db.CreditNotes
            .Include(x => x.Lines).ThenInclude(l => l.Taxes)
            .SingleAsync(x => x.CreditNoteId == id);

        Assert.True(saved.IsInterState);
        Assert.Equal(0m, saved.CgstAmount);
        Assert.Equal(0m, saved.SgstAmount);
        Assert.Equal(90m, saved.IgstAmount); // 500 taxable × 18%
        Assert.Equal(590m, saved.TotalAmount);

        CreditNoteDetailTax tax = Assert.Single(saved.Lines.Single().Taxes);
        Assert.Equal(TaxComponent.Igst, tax.TaxComponent);
        Assert.Equal(90m, tax.Amount);
    }

    private static SaveCreditNoteRequest Request(
        long invoiceId,
        long invoiceDetailId,
        string contactGstin,
        List<SaveCreditNoteLineRequest> lines)
    {
        foreach (SaveCreditNoteLineRequest line in lines)
        {
            line.InvoiceDetailId = invoiceDetailId;
        }

        return new SaveCreditNoteRequest
        {
            InvoiceId = invoiceId,
            DocumentDate = new DateOnly(2026, 6, 1),
            ContactId = 42,
            ContactGstin = contactGstin,
            ExchangeRate = 1m,
            Lines = lines,
        };
    }

    private static SaveCreditNoteLineRequest Line(decimal quantity, decimal unitPrice) =>
        new()
        {
            ItemId = 7,
            Quantity = quantity,
            UnitPrice = unitPrice,
            TaxGroupIds = [1],
        };

    /// <summary>
    /// One branch, an invoice with a line to credit against — <see
    /// cref="CreditNoteDetail.InvoiceDetailId"/> is a real foreign key — and
    /// the service wired to stubs.
    /// </summary>
    private sealed record Harness(SalesDbContext Db, CreditNoteService Service, long InvoiceId)
    {
        public static async Task<Harness> CreateAsync(PostgresFixture pg)
        {
            Guid customerId = Guid.NewGuid();
            Guid orgId = Guid.NewGuid();

            SalesDbContext db = pg.CreateContext(customerId, orgId);

            db.NumberingSeries.AddRange(Repository.SeedData.NumberingSeriesSeed.Build(orgId));

            Invoice invoice = new()
            {
                TransactionTypeCode = "INV",
                DocumentNo = $"INV/{Guid.NewGuid():N}"[..20],
                DocumentDate = new DateOnly(2026, 6, 1),
                DueDate = new DateOnly(2026, 6, 30),
                ContactId = 42,
                CurrencyCode = "INR",
                ExchangeRate = 1m,
                Status = DocumentStatus.Draft,
            };
            db.Invoices.Add(invoice);
            await db.SaveChangesAsync();

            StubNameLookup names = new();
            NumberGenerator numbering = new(
                db, Options.Create(new NumberingOptions()), new StubFinancialYear());

            CreditNoteService service = new(
                db,
                new TenantContext { CustomerId = customerId, OrgId = orgId },
                numbering,
                new StubBaseCurrency(),
                new StubBranchSettings(),
                new StubTaxRates(),
                names,
                names,
                new StubCurrentUser(),
                TimeProvider.System,
                new RecordingInventory(),
                new RecordingLedger());

            return new Harness(db, service, invoice.InvoiceId);
        }

        /// <summary>
        /// The invoice line the credit note reverses — built the same minimal
        /// way <c>DocumentLineFieldTests</c> does, since the invoice's own
        /// save path is not what these tests are about.
        /// </summary>
        public async Task<InvoiceDetail> SeedInvoiceLineAsync()
        {
            InvoiceDetail line = new()
            {
                InvoiceId = InvoiceId,
                LineNumber = 1,
                ItemId = 7,
                Quantity = 10m,
                ConversionFactor = 1m,
                BaseQuantity = 10m,
                UnitPrice = 100m,
                DiscountPercent = 0m,
                DiscountAmount = 0m,
                TaxableAmount = 1000m,
                GrossAmount = 1000m,
                LineTotal = 1000m,
            };

            Db.InvoiceDetails.Add(line);
            await Db.SaveChangesAsync();
            return line;
        }
    }
}
