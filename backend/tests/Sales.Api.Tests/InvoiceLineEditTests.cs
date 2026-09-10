using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Sales.Api.Services;
using Sales.Entity.Models;
using Sales.Entity.TableEntities;
using Sales.Repository;
using Shared.Kernel.Documents;
using Shared.Kernel.Numbering;
using Shared.Kernel.Persistence;
using Shared.Kernel.Tenancy;
using Xunit;

namespace Sales.Api.Tests;

/// <summary>
/// Editing a draft invoice's lines — adding one, removing one, changing one —
/// and what happens to the originals when the edit is refused.
///
/// <c>UpdateAsync</c> replaces rather than merges: it deletes every line and
/// every line tax, saves, and then rebuilds the whole set from the request. So a
/// line the request omits is removed, a line it adds is inserted, and a line it
/// changes is written with the new figures. The client sends the invoice it
/// wants, not a list of edits.
///
/// <b>The order is what makes the transaction load-bearing.</b> The delete and
/// its save happen before any incoming line has been validated, and the
/// validation that follows can refuse — a free-text line with no description, a
/// tax group whose rate cannot be read for the document's date. Each of those
/// returns an outcome the controller turns into a 4xx or 5xx, which means that
/// before the request-wide transaction the delete had already committed on its
/// own: the invoice was emptied of its lines and the person was handed an error
/// telling them line 2 was invalid. The header kept totals for lines that no
/// longer existed.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class InvoiceLineEditTests
{
    private const long ItemId = 101;
    private const long ContactId = 42;

    private readonly PostgresFixture _pg;

    public InvoiceLineEditTests(PostgresFixture pg) => _pg = pg;

    [SkippableFact]
    public async Task An_edit_adds_removes_and_changes_lines_in_one_save()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason ?? string.Empty);

        Harness h = await Harness.CreateAsync(_pg);

        InvoiceResult created = await h.Invoices.CreateAsync(
            Request([Line(2m, 100m, "First"), Line(3m, 200m, "Second")]),
            CancellationToken.None);

        Assert.Equal(InvoiceOutcome.Ok, created.Outcome);

        // Second dropped, First repriced, Third added — all three operations at once.
        InvoiceResult edited = await h.Invoices.UpdateAsync(
            created.InvoiceId,
            Request([Line(5m, 150m, "First"), Line(1m, 400m, "Third")]),
            CancellationToken.None);

        Assert.Equal(InvoiceOutcome.Ok, edited.Outcome);

        h.Db.ChangeTracker.Clear();

        List<InvoiceDetail> lines = await h.Db.InvoiceDetails
            .Where(d => d.InvoiceId == created.InvoiceId)
            .OrderBy(d => d.LineNumber)
            .ToListAsync();

        Assert.Equal(2, lines.Count);

        Assert.Equal("First", lines[0].Description);
        Assert.Equal(5m, lines[0].Quantity);
        Assert.Equal(150m, lines[0].UnitPrice);

        Assert.Equal("Third", lines[1].Description);
        Assert.Equal(1m, lines[1].Quantity);

        Assert.DoesNotContain(lines, l => l.Description == "Second");

        // Line numbers are reassigned from the request's order, so the surviving
        // line is 1 and the added one is 2 — there is no gap where Second was.
        Assert.Equal([1, 2], lines.Select(l => l.LineNumber).ToArray());
    }

    [SkippableFact]
    public async Task The_headers_totals_follow_the_edited_lines()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason ?? string.Empty);

        Harness h = await Harness.CreateAsync(_pg);

        InvoiceResult created = await h.Invoices.CreateAsync(
            Request([Line(10m, 100m, "First")]), CancellationToken.None);

        Assert.Equal(InvoiceOutcome.Ok, created.Outcome);

        await h.Invoices.UpdateAsync(
            created.InvoiceId,
            Request([Line(1m, 100m, "First")]),
            CancellationToken.None);

        h.Db.ChangeTracker.Clear();

        Invoice invoice = await h.Db.Invoices.SingleAsync(i => i.InvoiceId == created.InvoiceId);

        // 1 x 100 taxable, 18% GST. If the header had kept the old lines' totals
        // this would still read 1000 and the invoice would not add up.
        Assert.Equal(100m, invoice.TaxableAmount);
        Assert.Equal(118m, invoice.TotalAmount);
    }

    [SkippableFact]
    public async Task A_refused_edit_inside_the_transaction_leaves_every_original_line_in_place()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason ?? string.Empty);

        Harness h = await Harness.CreateAsync(_pg);

        InvoiceResult created = await h.Invoices.CreateAsync(
            Request([Line(2m, 100m, "First"), Line(3m, 200m, "Second")]),
            CancellationToken.None);

        Assert.Equal(InvoiceOutcome.Ok, created.Outcome);

        // A free-text line with neither description nor account. UpdateAsync has
        // already deleted both originals and saved by the time it reaches this.
        SaveInvoiceRequest bad = Request([
            Line(1m, 100m, "First"),
            new SaveInvoiceLineRequest
            {
                ItemId = null,
                Description = null,
                Quantity = 1m,
                ConversionFactor = 1m,
                UnitPrice = 50m,
                LineType = DocumentLineType.Stock,
            },
        ]);

        InvoiceResult refused;

        // The transaction TransactionFilter opens around the whole action. The
        // service is called exactly as the controller calls it.
        await using (ITransactionScope tx = await h.Db.Database.BeginScopeAsync(CancellationToken.None))
        {
            refused = await h.Invoices.UpdateAsync(created.InvoiceId, bad, CancellationToken.None);

            Assert.Equal(InvoiceOutcome.LineInvalid, refused.Outcome);

            // LineInvalid is a 400, and the filter rolls back on anything from
            // 400 up rather than only on an exception. Nothing threw here.
            await tx.RollbackAsync(CancellationToken.None);
        }

        h.Db.ChangeTracker.Clear();

        await using SalesDbContext reader = _pg.CreateContext(h.Tenant.CustomerId!.Value, h.Tenant.OrgId!.Value);

        List<InvoiceDetail> lines = await reader.InvoiceDetails
            .Where(d => d.InvoiceId == created.InvoiceId)
            .OrderBy(d => d.LineNumber)
            .ToListAsync();

        // Both originals, untouched. Without the transaction this list is empty
        // and the customer's invoice has been emptied by a validation message.
        Assert.Equal(2, lines.Count);
        Assert.Equal("First", lines[0].Description);
        Assert.Equal("Second", lines[1].Description);
        Assert.Equal(3m, lines[1].Quantity);
    }

    [SkippableFact]
    public async Task Without_the_transaction_a_refused_edit_destroys_the_lines()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason ?? string.Empty);

        Harness h = await Harness.CreateAsync(_pg);

        InvoiceResult created = await h.Invoices.CreateAsync(
            Request([Line(2m, 100m, "First"), Line(3m, 200m, "Second")]),
            CancellationToken.None);

        Assert.Equal(InvoiceOutcome.Ok, created.Outcome);

        SaveInvoiceRequest bad = Request([
            Line(1m, 100m, "First"),
            new SaveInvoiceLineRequest
            {
                ItemId = null,
                Description = null,
                Quantity = 1m,
                ConversionFactor = 1m,
                UnitPrice = 50m,
                LineType = DocumentLineType.Stock,
            },
        ]);

        InvoiceResult refused = await h.Invoices.UpdateAsync(
            created.InvoiceId, bad, CancellationToken.None);

        Assert.Equal(InvoiceOutcome.LineInvalid, refused.Outcome);

        h.Db.ChangeTracker.Clear();

        await using SalesDbContext reader = _pg.CreateContext(h.Tenant.CustomerId!.Value, h.Tenant.OrgId!.Value);

        // This is the old behaviour, kept as a test so it cannot come back
        // unnoticed: the delete committed on its own and both lines are gone,
        // while the person was told line 2 needed a description. The header is
        // still there, still carrying totals for lines that no longer exist.
        Assert.Empty(await reader.InvoiceDetails
            .Where(d => d.InvoiceId == created.InvoiceId)
            .ToListAsync());

        Assert.NotNull(await reader.Invoices
            .FirstOrDefaultAsync(i => i.InvoiceId == created.InvoiceId));
    }

    private static SaveInvoiceRequest Request(List<SaveInvoiceLineRequest> lines) => new()
    {
        DocumentDate = new DateOnly(2026, 6, 1),
        DueDate = new DateOnly(2026, 7, 1),
        ContactId = ContactId,
        PlaceOfSupplyStateCode = "33",
        CurrencyCode = "INR",
        ExchangeRate = 1m,
        Lines = lines,
    };

    private static SaveInvoiceLineRequest Line(decimal quantity, decimal unitPrice, string description) => new()
    {
        ItemId = ItemId,
        Quantity = quantity,
        ConversionFactor = 1m,
        UnitPrice = unitPrice,
        TaxGroupId = 1,
        LineType = DocumentLineType.Stock,
        Description = description,
    };

    private sealed record Harness(SalesDbContext Db, InvoiceService Invoices, TenantContext Tenant)
    {
        public static async Task<Harness> CreateAsync(PostgresFixture pg)
        {
            Guid customerId = Guid.NewGuid();
            Guid orgId = Guid.NewGuid();

            TenantContext tenant = new() { CustomerId = customerId, OrgId = orgId };
            SalesDbContext db = pg.CreateContext(customerId, orgId);

            db.NumberingSeries.AddRange(Repository.SeedData.NumberingSeriesSeed.Build(orgId));
            await db.SaveChangesAsync();

            StubNameLookup names = new();

            InvoiceService invoices = new(
                db,
                tenant,
                new NumberGenerator(db, Options.Create(new NumberingOptions()), new StubFinancialYear()),
                new StubBaseCurrency(),
                new StubBranchSettings(),
                new StubTaxRates(),
                names,
                names,
                new StubCurrentUser(),
                TimeProvider.System,
                new RecordingInventory(),
                new RecordingLedger(),
                new StubCreditCheck(),
                new StubDocumentStorage(),
                new StubInvoicePdf(),
                new StubOrgIdentity());

            return new Harness(db, invoices, tenant);
        }
    }
}
