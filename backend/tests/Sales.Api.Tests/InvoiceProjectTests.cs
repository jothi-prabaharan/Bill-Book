using Microsoft.Extensions.Options;
using Sales.Api.Services;
using Sales.Entity.Enums;
using Sales.Entity.Models;
using Sales.Repository;
using Shared.Kernel.Documents;
using Shared.Kernel.Numbering;
using Shared.Kernel.Projects;
using Shared.Kernel.Tenancy;
using Xunit;

namespace Sales.Api.Tests;

/// <summary>
/// An invoice's lines hand their projects to its ledger legs (TK-105), through
/// the invoice service the controller uses, against a real PostgreSQL, with the
/// ledger recorded rather than called.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class InvoiceProjectTests
{
    private const int ItemLedgerType = 1;
    private const int ControlLedgerType = 3;

    private readonly PostgresFixture _pg;

    public InvoiceProjectTests(PostgresFixture pg) => _pg = pg;

    /// <summary>The card's test: two projects each take their revenue, and the receivable carries neither.</summary>
    [SkippableFact]
    public async Task An_invoice_with_two_projects_posts_revenue_to_each_and_an_untagged_receivable()
    {
        Harness h = await Harness.CreateAsync(_pg);
        long invoice = await h.InvoiceAsync((unitPrice: 1_000m, project: 11), (unitPrice: 3_000m, project: 22));

        Assert.Equal(InvoiceOutcome.Ok, (await h.Invoices.PostAsync(invoice, default)).Outcome);

        PostLedgerRequest posted = h.Ledger.Posts.Last();
        List<LedgerLegRequest> revenue = [.. posted.Legs.Where(l => l.LedgerTypeId == ItemLedgerType)];

        Assert.Equal(2, revenue.Count);
        Assert.Equal(10_000m, revenue.Single(l => l.ProjectId == 11).CreditAmount);
        Assert.Equal(30_000m, revenue.Single(l => l.ProjectId == 22).CreditAmount);
        Assert.Null(posted.Legs.Single(l => l.LedgerTypeId == ControlLedgerType).ProjectId);
        Assert.Equal(posted.Legs.Sum(l => l.DebitAmount), posted.Legs.Sum(l => l.CreditAmount));
    }

    /// <summary>The card's other half, and its "done when": one project tags the receivable too.</summary>
    [SkippableFact]
    public async Task An_invoice_with_one_project_tags_every_leg_with_it()
    {
        Harness h = await Harness.CreateAsync(_pg);
        long invoice = await h.InvoiceAsync((unitPrice: 1_000m, project: 11), (unitPrice: 500m, project: 11));

        await h.Invoices.PostAsync(invoice, default);

        PostLedgerRequest posted = h.Ledger.Posts.Last();
        LedgerLegRequest revenue = Assert.Single(posted.Legs, l => l.LedgerTypeId == ItemLedgerType);
        Assert.Equal(11L, revenue.ProjectId);
        Assert.All(posted.Legs, l => Assert.Equal(11L, l.ProjectId));
    }

    [SkippableFact]
    public async Task An_invoice_with_no_projects_posts_as_it_always_has()
    {
        Harness h = await Harness.CreateAsync(_pg);
        long invoice = await h.InvoiceAsync((unitPrice: 1_000m, project: null));

        await h.Invoices.PostAsync(invoice, default);

        Assert.All(h.Ledger.Posts.Last().Legs, l => Assert.Null(l.ProjectId));
    }

    [SkippableFact]
    public async Task A_line_naming_a_completed_project_is_refused_at_save()
    {
        Harness h = await Harness.CreateAsync(_pg);

        InvoiceResult result = await h.Invoices.CreateAsync(Harness.Request((unitPrice: 1_000m, project: 99)), default);

        Assert.Equal(InvoiceOutcome.LineInvalid, result.Outcome);
        Assert.Contains("completed", result.Detail);
    }

    [SkippableFact]
    public async Task A_line_naming_another_branchs_project_is_refused_at_save()
    {
        Harness h = await Harness.CreateAsync(_pg);

        InvoiceResult result = await h.Invoices.CreateAsync(Harness.Request((unitPrice: 1_000m, project: 12_345)), default);

        Assert.Equal(InvoiceOutcome.LineInvalid, result.Outcome);
        Assert.Contains("not a project of this branch", result.Detail);
    }

    /// <summary>Accounting's answer: 11 and 22 are open, 99 is completed, nothing else is the branch's.</summary>
    private sealed class StubProjects : IProjectDirectory
    {
        public Task<IReadOnlyDictionary<long, ProjectSummary>> FindAsync(IEnumerable<long> ids, CancellationToken ct)
        {
            var known = new Dictionary<long, ProjectSummary>
            {
                [11] = new() { ProjectId = 11, ProjectCode = "PRJ-0011", ProjectName = "Kitchen", Status = "Active", IsPostable = true },
                [22] = new() { ProjectId = 22, ProjectCode = "PRJ-0022", ProjectName = "Office", Status = "Active", IsPostable = true },
                [99] = new() { ProjectId = 99, ProjectCode = "PRJ-0099", ProjectName = "Old job", Status = "Completed", IsPostable = false },
            };

            return Task.FromResult<IReadOnlyDictionary<long, ProjectSummary>>(
                ids.Where(known.ContainsKey).ToDictionary(id => id, id => known[id]));
        }
    }

    private sealed record Harness(SalesDbContext Db, InvoiceService Invoices, RecordingLedger Ledger)
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
            NumberGenerator numbering = new(db, Options.Create(new NumberingOptions()), new StubFinancialYear());

            InvoiceService invoices = new(
                db, tenant, numbering, new StubBaseCurrency(), new StubBranchSettings(), new StubTaxRates(),
                names, names, new StubCurrentUser(), TimeProvider.System, new RecordingInventory(), ledger,
                new StubCreditCheck(), new StubDocumentStorage(), new StubInvoicePdf(), new StubOrgIdentity(), new StubUqcLookup(), new StubEInvoicing(),
                projects: new StubProjects());

            return new Harness(db, invoices, ledger);
        }

        public static SaveInvoiceRequest Request(params (decimal unitPrice, long? project)[] lines) => new()
        {
            DocumentDate = new DateOnly(2026, 6, 1),
            DueDate = new DateOnly(2026, 7, 1),
            ContactId = 42,
            PlaceOfSupplyStateCode = "33",
            CurrencyCode = "INR",
            ExchangeRate = 1m,
            Lines = [.. lines.Select(l => new SaveInvoiceLineRequest
            {
                ItemId = 7,
                Description = "Test Stock Item",
                LineType = DocumentLineType.Stock,
                Quantity = 10m,
                ConversionFactor = 1m,
                UnitPrice = l.unitPrice,
                TaxGroupId = 1,
                ProjectId = l.project,
            })],
        };

        public async Task<long> InvoiceAsync(params (decimal unitPrice, long? project)[] lines)
        {
            InvoiceResult created = await Invoices.CreateAsync(Request(lines), default);
            Assert.Equal(InvoiceOutcome.Ok, created.Outcome);
            Db.ChangeTracker.Clear();
            return created.InvoiceId;
        }
    }
}
