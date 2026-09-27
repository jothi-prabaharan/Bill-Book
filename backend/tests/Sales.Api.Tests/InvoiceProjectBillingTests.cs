using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Sales.Api.Services;
using Sales.Entity.Models;
using Sales.Repository;
using Shared.Kernel.Documents;
using Shared.Kernel.Numbering;
using Shared.Kernel.Projects;
using Shared.Kernel.Tenancy;
using Xunit;

namespace Sales.Api.Tests;

/// <summary>
/// An invoice billing project items (TK-106): they are saved with the draft,
/// claimed when it posts, given back when the post fails or the invoice is
/// voided. The claim itself is stood in for; its row-count guard is tested
/// against Accounting's database in <c>ProjectBillingTests</c>.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class InvoiceProjectBillingTests
{
    private readonly PostgresFixture _pg;

    public InvoiceProjectBillingTests(PostgresFixture pg) => _pg = pg;

    [SkippableFact]
    public async Task Posting_claims_the_items_the_draft_bills()
    {
        Harness h = await Harness.CreateAsync(_pg);
        long invoice = await h.InvoiceAsync(Items(5, 6));

        Assert.Equal(InvoiceOutcome.Ok, (await h.Invoices.PostAsync(invoice, default)).Outcome);

        (long claimedFor, List<ProjectItemRef> claimed) = Assert.Single(h.Billing.Claims);
        Assert.Equal(invoice, claimedFor);
        Assert.Equal([5L, 6L], claimed.Select(i => i.ItemId).Order());
        Assert.Empty(h.Billing.Releases);
    }

    [SkippableFact]
    public async Task A_lost_claim_refuses_the_post_and_leaves_the_invoice_a_draft()
    {
        Harness h = await Harness.CreateAsync(_pg);
        long invoice = await h.InvoiceAsync(Items(5));
        h.Billing.RefuseWith = ProjectBillingMessages.AlreadyBilled;

        InvoiceResult result = await h.Invoices.PostAsync(invoice, default);

        Assert.Equal(InvoiceOutcome.ProjectItemsRefused, result.Outcome);
        Assert.Equal(ProjectBillingMessages.AlreadyBilled, result.Detail);
        Assert.Equal(DocumentStatus.Draft, (await h.StatusAsync(invoice)));
        Assert.Empty(h.Ledger.Posts);
    }

    [SkippableFact]
    public async Task A_post_that_fails_after_the_claim_gives_the_items_back()
    {
        Harness h = await Harness.CreateAsync(_pg);
        long invoice = await h.InvoiceAsync(Items(5));
        h.Ledger.RefusePostWith = "Accounting refused.";

        InvoiceResult result = await h.Invoices.PostAsync(invoice, default);

        Assert.NotEqual(InvoiceOutcome.Ok, result.Outcome);
        Assert.Single(h.Billing.Claims);
        Assert.Equal([invoice], h.Billing.Releases);
    }

    [SkippableFact]
    public async Task Voiding_the_invoice_releases_what_it_billed()
    {
        Harness h = await Harness.CreateAsync(_pg);
        long invoice = await h.InvoiceAsync(Items(5));
        await h.Invoices.PostAsync(invoice, default);
        h.Db.ChangeTracker.Clear();

        Assert.Equal(InvoiceOutcome.Ok, (await h.Invoices.VoidAsync(invoice, new VoidInvoiceRequest { Reason = "Wrong client" }, default)).Outcome);

        Assert.Equal([invoice], h.Billing.Releases);
    }

    [SkippableFact]
    public async Task The_items_are_saved_with_the_draft_and_read_back()
    {
        Harness h = await Harness.CreateAsync(_pg);
        long invoice = await h.InvoiceAsync(Items(5, 6));

        InvoiceView view = (await h.Invoices.GetAsync(invoice, default))!;

        Assert.Equal(2, view.ProjectItems.Count);
        Assert.All(view.ProjectItems, i => Assert.Equal(11L, i.ProjectId));
        Assert.Empty(h.Billing.Claims);
    }

    private static List<ProjectItemRef> Items(params long[] ids) =>
        [.. ids.Select(id => new ProjectItemRef { Kind = ProjectItemKind.TimeEntry, ItemId = id, ProjectId = 11 })];

    /// <summary>The claim and release, recorded; refused when a test says so.</summary>
    private sealed class RecordingBilling : IProjectBillingClient
    {
        public List<(long InvoiceId, List<ProjectItemRef> Items)> Claims { get; } = [];

        public List<long> Releases { get; } = [];

        public string? RefuseWith { get; set; }

        public Task<string?> ClaimAsync(Guid customerId, Guid orgId, long invoiceId, IReadOnlyCollection<ProjectItemRef> items, CancellationToken ct)
        {
            if (RefuseWith is not null)
            {
                return Task.FromResult<string?>(RefuseWith);
            }

            Claims.Add((invoiceId, [.. items]));
            return Task.FromResult<string?>(null);
        }

        public Task ReleaseAsync(Guid customerId, Guid orgId, long invoiceId, CancellationToken ct)
        {
            Releases.Add(invoiceId);
            return Task.CompletedTask;
        }
    }

    private sealed record Harness(SalesDbContext Db, InvoiceService Invoices, RecordingLedger Ledger, RecordingBilling Billing)
    {
        public static async Task<Harness> CreateAsync(PostgresFixture pg)
        {
            Skip.If(pg.SkipReason is not null, pg.SkipReason ?? string.Empty);

            Guid customerId = Guid.NewGuid();
            Guid orgId = Guid.NewGuid();
            SalesDbContext db = pg.CreateContext(customerId, orgId);
            db.NumberingSeries.AddRange(Repository.SeedData.NumberingSeriesSeed.Build(orgId));
            await db.SaveChangesAsync();

            TenantContext tenant = new() { CustomerId = customerId, OrgId = orgId, CustomerCode = "0000000042" };
            StubNameLookup names = new();
            RecordingLedger ledger = new();
            RecordingBilling billing = new();
            NumberGenerator numbering = new(db, Options.Create(new NumberingOptions()), new StubFinancialYear());

            InvoiceService invoices = new(
                db, tenant, numbering, new StubBaseCurrency(), new StubBranchSettings(), new StubTaxRates(),
                names, names, new StubCurrentUser(), TimeProvider.System, new RecordingInventory(), ledger,
                new StubCreditCheck(), new StubDocumentStorage(), new StubInvoicePdf(), new StubOrgIdentity(), new StubUqcLookup(), new StubEInvoicing(),
                projectBilling: billing);

            return new Harness(db, invoices, ledger, billing);
        }

        public async Task<long> InvoiceAsync(List<ProjectItemRef> items)
        {
            InvoiceResult created = await Invoices.CreateAsync(new SaveInvoiceRequest
            {
                DocumentDate = new DateOnly(2026, 9, 1),
                DueDate = new DateOnly(2026, 10, 1),
                ContactId = 42,
                PlaceOfSupplyStateCode = "33",
                CurrencyCode = "INR",
                ExchangeRate = 1m,
                ProjectItems = items,
                Lines =
                [
                    new SaveInvoiceLineRequest
                    {
                        ItemId = 7,
                        Description = "Installation hours",
                        LineType = DocumentLineType.Stock,
                        Quantity = 3m,
                        ConversionFactor = 1m,
                        UnitPrice = 800m,
                        TaxGroupId = 1,
                        ProjectId = 11,
                    },
                ],
            }, default);
            Assert.Equal(InvoiceOutcome.Ok, created.Outcome);
            Db.ChangeTracker.Clear();
            return created.InvoiceId;
        }

        public async Task<DocumentStatus> StatusAsync(long id)
        {
            Db.ChangeTracker.Clear();
            return await Db.Invoices.AsNoTracking().Where(i => i.InvoiceId == id).Select(i => i.Status).SingleAsync();
        }
    }
}
