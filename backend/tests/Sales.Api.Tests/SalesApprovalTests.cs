using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Sales.Api.Services;
using Sales.Entity.Enums;
using Sales.Entity.Models;
using Sales.Entity.TableEntities;
using Sales.Repository;
using Shared.Kernel.Approvals;
using Shared.Kernel.Contacts;
using Shared.Kernel.Documents;
using Shared.Kernel.Interfaces;
using Shared.Kernel.Numbering;
using Shared.Kernel.Tax;
using Shared.Kernel.Tenancy;
using Xunit;

namespace Sales.Api.Tests;

/// <summary>
/// The credit-limit and discount overrides (TK-102), through the invoice
/// service the controller uses and against a real PostgreSQL. Master's chain
/// resolver is stood in for with "the Owner approves"; the credit check and
/// the discount limit are stubs a test sets.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class SalesApprovalTests
{
    private const long ContactId = 42;
    private const int OwnerRole = 1;

    private static readonly Guid Clerk = Guid.NewGuid();
    private static readonly Guid Owner = Guid.NewGuid();

    private readonly PostgresFixture _pg;

    public SalesApprovalTests(PostgresFixture pg) => _pg = pg;

    /// <summary>The card's test and its "done when": the Owner approves a credit-limit breach and the invoice posts.</summary>
    [SkippableFact]
    public async Task A_sale_past_the_credit_limit_is_refused_without_an_override_and_posts_with_one()
    {
        Harness h = await Harness.CreateAsync(_pg);
        h.Credit.RefuseWith = "This sale takes the customer past their credit limit of ₹50,000.";

        InvoiceResult refused = await h.Invoices.CreateAsync(Request(unitPrice: 1_000m), default);
        Assert.Equal(InvoiceOutcome.CreditLimitExceeded, refused.Outcome);
        Assert.Contains("asking for approval", refused.Detail);
        Assert.Empty(await h.Db.Invoices.AsNoTracking().ToListAsync());

        InvoiceResult asked = await h.Invoices.CreateAsync(Request(unitPrice: 1_000m, requestApproval: true), default);
        Assert.Equal(InvoiceOutcome.Ok, asked.Outcome);

        Invoice waiting = await h.ReadAsync(asked.InvoiceId);
        Assert.Equal(ApprovalStatus.InApproval, waiting.CreditOverrideStatus);
        Assert.Null(waiting.DiscountOverrideStatus);
        Assert.Equal("Owner", waiting.CurrentStepLabel);

        Assert.Equal(InvoiceOutcome.AwaitingApproval, (await h.Invoices.PostAsync(asked.InvoiceId, default)).Outcome);

        h.User.Become(Owner, OwnerRole);
        ApprovalResult approved = await h.Overrides.ActAsync(
            ApprovalRequestKind.CreditLimitOverride, asked.InvoiceId, ApprovalAction.Approve, null, default);
        Assert.Equal(ApprovalResultOutcome.Ok, approved.Outcome);
        Assert.Equal(ApprovalStatus.Approved, (await h.ReadAsync(asked.InvoiceId)).CreditOverrideStatus);

        Assert.Equal(InvoiceOutcome.Ok, (await h.Invoices.PostAsync(asked.InvoiceId, default)).Outcome);
        Assert.Equal(DocumentStatus.Posted, (await h.ReadAsync(asked.InvoiceId)).Status);
    }

    [SkippableFact]
    public async Task An_approved_override_does_not_carry_to_another_document()
    {
        Harness h = await Harness.CreateAsync(_pg);
        h.Credit.RefuseWith = "Past the credit limit.";

        InvoiceResult first = await h.Invoices.CreateAsync(Request(unitPrice: 1_000m, requestApproval: true), default);
        h.User.Become(Owner, OwnerRole);
        await h.Overrides.ActAsync(ApprovalRequestKind.CreditLimitOverride, first.InvoiceId, ApprovalAction.Approve, null, default);

        h.User.Become(Clerk, role: 4);
        InvoiceResult second = await h.Invoices.CreateAsync(Request(unitPrice: 1_000m), default);

        Assert.Equal(InvoiceOutcome.CreditLimitExceeded, second.Outcome);
    }

    [SkippableFact]
    public async Task Editing_an_invoice_ends_its_override_and_the_check_runs_again()
    {
        Harness h = await Harness.CreateAsync(_pg);
        h.Credit.RefuseWith = "Past the credit limit.";

        InvoiceResult asked = await h.Invoices.CreateAsync(Request(unitPrice: 1_000m, requestApproval: true), default);
        h.User.Become(Owner, OwnerRole);
        await h.Overrides.ActAsync(ApprovalRequestKind.CreditLimitOverride, asked.InvoiceId, ApprovalAction.Approve, null, default);
        h.Db.ChangeTracker.Clear();

        h.User.Become(Clerk, role: 4);
        InvoiceResult edited = await h.Invoices.UpdateAsync(asked.InvoiceId, Request(unitPrice: 1_100m), default);
        Assert.Equal(InvoiceOutcome.CreditLimitExceeded, edited.Outcome);
        h.Db.ChangeTracker.Clear();

        InvoiceResult reasked = await h.Invoices.UpdateAsync(asked.InvoiceId, Request(unitPrice: 1_100m, requestApproval: true), default);
        Assert.Equal(InvoiceOutcome.Ok, reasked.Outcome);
        Assert.Equal(ApprovalStatus.InApproval, (await h.ReadAsync(asked.InvoiceId)).CreditOverrideStatus);
        Assert.Equal(1, await h.Db.ApprovalSteps.AsNoTracking().CountAsync(s => s.Round == 2));
    }

    [SkippableFact]
    public async Task A_line_past_the_contacts_discount_limit_asks_for_a_discount_override()
    {
        Harness h = await Harness.CreateAsync(_pg);
        h.Discounts.Limit = new DiscountLimitResponse { LimitPercent = 10m, Source = "Contact" };

        InvoiceResult within = await h.Invoices.CreateAsync(Request(unitPrice: 1_000m, discountPercent: 10m), default);
        Assert.Equal(InvoiceOutcome.Ok, within.Outcome);

        InvoiceResult past = await h.Invoices.CreateAsync(Request(unitPrice: 1_000m, discountPercent: 15m), default);
        Assert.Equal(InvoiceOutcome.DiscountLimitExceeded, past.Outcome);
        Assert.Contains("10% allowed for this customer", past.Detail);

        InvoiceResult asked = await h.Invoices.CreateAsync(Request(unitPrice: 1_000m, discountPercent: 15m, requestApproval: true), default);
        Invoice waiting = await h.ReadAsync(asked.InvoiceId);
        Assert.Equal(ApprovalStatus.InApproval, waiting.DiscountOverrideStatus);
        Assert.Null(waiting.CreditOverrideStatus);
    }

    [SkippableFact]
    public async Task An_unreadable_discount_limit_refuses_the_save_rather_than_reading_it_as_no_limit()
    {
        Harness h = await Harness.CreateAsync(_pg);
        h.Discounts.Unreachable = true;

        InvoiceResult result = await h.Invoices.CreateAsync(Request(unitPrice: 1_000m, discountPercent: 5m), default);

        Assert.Equal(InvoiceOutcome.LimitsUnavailable, result.Outcome);
    }

    [SkippableFact]
    public async Task With_no_workflow_for_the_override_approval_cannot_be_asked_for()
    {
        Harness h = await Harness.CreateAsync(_pg, new FakeChains { NoWorkflow = true });
        h.Credit.RefuseWith = "Past the credit limit.";

        InvoiceResult result = await h.Invoices.CreateAsync(Request(unitPrice: 1_000m, requestApproval: true), default);

        Assert.Equal(InvoiceOutcome.OverrideRefused, result.Outcome);
        Assert.Contains("No approval workflow", result.Detail);
    }

    [SkippableFact]
    public async Task A_rejected_override_keeps_the_invoice_from_posting()
    {
        Harness h = await Harness.CreateAsync(_pg);
        h.Credit.RefuseWith = "Past the credit limit.";

        InvoiceResult asked = await h.Invoices.CreateAsync(Request(unitPrice: 1_000m, requestApproval: true), default);
        h.User.Become(Owner, OwnerRole);
        await h.Overrides.ActAsync(ApprovalRequestKind.CreditLimitOverride, asked.InvoiceId, ApprovalAction.Reject, "Collect first", default);

        InvoiceResult post = await h.Invoices.PostAsync(asked.InvoiceId, default);

        Assert.Equal(InvoiceOutcome.AwaitingApproval, post.Outcome);
        Assert.Contains("rejected", post.Detail);
    }

    [Fact]
    public void The_worst_line_is_the_first_past_the_limit_and_a_limit_of_100_is_none()
    {
        TaxLineResult Line(decimal gross, decimal discount) => new()
        {
            GrossAmount = gross,
            DiscountAmount = discount,
            TaxableAmount = gross - discount,
            TaxAmount = 0m,
            LineTotal = gross - discount,
            BaseQuantity = 1m,
            Components = [],
        };

        List<TaxLineResult> lines = [Line(1_000m, 100m), Line(1_000m, 150m), Line(1_000m, 200m)];

        Assert.Equal(2, SalesLimits.WorstLine(lines, 10m));
        Assert.Null(SalesLimits.WorstLine(lines, 20m));
        Assert.Null(SalesLimits.WorstLine(lines, 100m));
    }

    [Fact]
    public void Only_an_approved_or_never_asked_override_lets_a_document_through()
    {
        Assert.Null(SalesOverrideService<Invoice>.Blocks(new Invoice()));
        Assert.Null(SalesOverrideService<Invoice>.Blocks(new Invoice { CreditOverrideStatus = ApprovalStatus.Approved }));
        Assert.NotNull(SalesOverrideService<Invoice>.Blocks(new Invoice { CreditOverrideStatus = ApprovalStatus.InApproval }));
        Assert.NotNull(SalesOverrideService<Invoice>.Blocks(new Invoice
        {
            CreditOverrideStatus = ApprovalStatus.Approved,
            DiscountOverrideStatus = ApprovalStatus.Rejected,
        }));
    }

    private static SaveInvoiceRequest Request(decimal unitPrice, decimal? discountPercent = null, bool requestApproval = false) => new()
    {
        DocumentDate = new DateOnly(2026, 6, 1),
        DueDate = new DateOnly(2026, 7, 1),
        ContactId = ContactId,
        PlaceOfSupplyStateCode = "33",
        CurrencyCode = "INR",
        ExchangeRate = 1m,
        RequestApproval = requestApproval,
        Lines =
        [
            new SaveInvoiceLineRequest
            {
                ItemId = 7,
                Quantity = 10m,
                ConversionFactor = 1m,
                UnitPrice = unitPrice,
                DiscountPercent = discountPercent,
                TaxGroupId = 1,
                LineType = DocumentLineType.Stock,
                Description = "Test Stock Item",
            },
        ],
    };

    /// <summary>A user whose identity a test can change between calls.</summary>
    private sealed class SwitchableUser : ICurrentUser
    {
        public Guid? UserId { get; private set; } = Clerk;

        public Guid? CustomerId => null;

        public Guid? OrgId => null;

        public int? RoleId { get; private set; } = 4;

        public void Become(Guid userId, int role)
        {
            UserId = userId;
            RoleId = role;
        }
    }

    /// <summary>Master's discount limit (D-29): no limit unless a test sets one.</summary>
    private sealed class StubDiscountLimits : IDiscountLimitClient
    {
        public DiscountLimitResponse Limit { get; set; } = new();

        public bool Unreachable { get; set; }

        public Task<DiscountLimitResponse?> LimitForAsync(long contactId, CancellationToken ct) =>
            Task.FromResult(Unreachable ? null : Limit);
    }

    /// <summary>Master's resolver: every override goes to the Owner.</summary>
    private sealed class FakeChains : IApprovalChainClient
    {
        public bool NoWorkflow { get; init; }

        public Task<ResolveChainResponse?> ResolveAsync(ResolveChainRequest request, CancellationToken ct) =>
            Task.FromResult<ResolveChainResponse?>(NoWorkflow
                ? new ResolveChainResponse { Outcome = ResolveChainOutcome.NoWorkflow }
                : new ResolveChainResponse
                {
                    Outcome = ResolveChainOutcome.Resolved,
                    WorkflowName = "Overrides",
                    Steps = [new ResolvedStep { Sequence = 1, Label = "Owner", ApproverUserId = Owner }],
                });

        public Task<bool> IsDelegateAsync(DelegateCheckRequest request, CancellationToken ct) => Task.FromResult(false);
    }

    private sealed record Harness(
        SalesDbContext Db,
        InvoiceService Invoices,
        InvoiceOverrideService Overrides,
        StubCreditCheck Credit,
        StubDiscountLimits Discounts,
        SwitchableUser User)
    {
        public static async Task<Harness> CreateAsync(PostgresFixture pg, FakeChains? chains = null)
        {
            Skip.If(pg.SkipReason is not null, pg.SkipReason ?? string.Empty);

            Guid customerId = Guid.NewGuid();
            Guid orgId = Guid.NewGuid();
            SalesDbContext db = pg.CreateContext(customerId, orgId);
            db.NumberingSeries.AddRange(Repository.SeedData.NumberingSeriesSeed.Build(orgId));
            await db.SaveChangesAsync();

            TenantContext tenant = new() { CustomerId = customerId, OrgId = orgId, CustomerCode = "0000000042" };
            StubNameLookup names = new();
            SwitchableUser user = new();
            StubCreditCheck credit = new();
            StubDiscountLimits discounts = new();
            NumberGenerator numbering = new(db, Options.Create(new NumberingOptions()), new StubFinancialYear());
            InvoiceOverrideService overrides = new(db, chains ?? new FakeChains(), tenant, user, TimeProvider.System);

            InvoiceService invoices = new(
                db, tenant, numbering, new StubBaseCurrency(), new StubBranchSettings(), new StubTaxRates(),
                names, names, user, TimeProvider.System, new RecordingInventory(), new RecordingLedger(),
                credit, new StubDocumentStorage(), new StubInvoicePdf(), new StubOrgIdentity(), new StubUqcLookup(), new StubEInvoicing(),
                discounts, overrides);

            return new Harness(db, invoices, overrides, credit, discounts, user);
        }

        public async Task<Invoice> ReadAsync(long id)
        {
            Db.ChangeTracker.Clear();
            return await Db.Invoices.AsNoTracking().SingleAsync(i => i.InvoiceId == id);
        }
    }
}
