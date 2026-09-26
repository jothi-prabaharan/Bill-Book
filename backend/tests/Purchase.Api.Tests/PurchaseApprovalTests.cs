using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Purchase.Api.Services;
using Purchase.Entity.Enums;
using Purchase.Entity.Models;
using Purchase.Entity.TableEntities;
using Purchase.Repository;
using Shared.Kernel.Approvals;
using Shared.Kernel.Documents;
using Shared.Kernel.Interfaces;
using Shared.Kernel.Numbering;
using Shared.Kernel.Tenancy;
using Xunit;

namespace Purchase.Api.Tests;

/// <summary>
/// Approval chains on purchase documents (TK-100), through the service the
/// controller uses and against a real PostgreSQL, with Master's chain resolver
/// stood in for: "the Accountant, then the Owner above ₹1,00,000".
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class PurchaseApprovalTests
{
    private const int AccountantRole = 3;

    private static readonly Guid Clerk = Guid.NewGuid();
    private static readonly Guid Accountant = Guid.NewGuid();
    private static readonly Guid Owner = Guid.NewGuid();

    private readonly PostgresFixture _pg;

    public PurchaseApprovalTests(PostgresFixture pg) => _pg = pg;

    [SkippableFact]
    public async Task An_order_above_the_limit_reaches_ready_to_post_only_after_both_levels_approve()
    {
        Harness h = await Harness.CreateAsync(_pg);
        long order = await h.OrderAsync(unitPrice: 1_000m, quantity: 150m); // ₹1,50,000 + GST

        Assert.Equal(PurchaseApprovalOutcome.Ok, (await h.Approvals.SubmitAsync(ApprovalRequestKind.PurchaseOrder, order, default)).Outcome);
        Assert.Equal(ApprovalStatus.InApproval, (await h.ReadAsync(order)).ApprovalStatus);
        Assert.Equal("Accountant", (await h.ReadAsync(order)).CurrentStepLabel);

        h.User.Become(Accountant, AccountantRole);
        Assert.Equal(PurchaseApprovalOutcome.Ok, (await h.Approvals.ActAsync(ApprovalRequestKind.PurchaseOrder, order, ApprovalAction.Approve, null, default)).Outcome);

        PurchaseOrder halfway = await h.ReadAsync(order);
        Assert.Equal(DocumentStatus.Draft, halfway.Status);
        Assert.Equal("Owner", halfway.CurrentStepLabel);
        Assert.Equal(Owner, halfway.CurrentApproverUserId);

        h.User.Become(Owner, role: 1);
        Assert.Equal(PurchaseApprovalOutcome.Ok, (await h.Approvals.ActAsync(ApprovalRequestKind.PurchaseOrder, order, ApprovalAction.Approve, null, default)).Outcome);

        PurchaseOrder approved = await h.ReadAsync(order);
        Assert.Equal(DocumentStatus.ReadyToPost, approved.Status);
        Assert.Equal(ApprovalStatus.Approved, approved.ApprovalStatus);
        Assert.Null(approved.CurrentStepLabel);
    }

    [SkippableFact]
    public async Task An_order_below_the_limit_needs_only_the_accountant()
    {
        Harness h = await Harness.CreateAsync(_pg);
        long order = await h.OrderAsync(unitPrice: 100m, quantity: 10m);

        await h.Approvals.SubmitAsync(ApprovalRequestKind.PurchaseOrder, order, default);
        h.User.Become(Accountant, AccountantRole);
        await h.Approvals.ActAsync(ApprovalRequestKind.PurchaseOrder, order, ApprovalAction.Approve, null, default);

        Assert.Equal(DocumentStatus.ReadyToPost, (await h.ReadAsync(order)).Status);
        Assert.Single(await h.Db.ApprovalSteps.AsNoTracking().ToListAsync());
    }

    [SkippableFact]
    public async Task A_user_who_is_not_the_approver_is_refused()
    {
        Harness h = await Harness.CreateAsync(_pg);
        long order = await h.OrderAsync(unitPrice: 100m, quantity: 10m);
        await h.Approvals.SubmitAsync(ApprovalRequestKind.PurchaseOrder, order, default);

        h.User.Become(Guid.NewGuid(), role: 5);
        PurchaseApprovalResult result = await h.Approvals.ActAsync(
            ApprovalRequestKind.PurchaseOrder, order, ApprovalAction.Approve, null, default);

        Assert.Equal(PurchaseApprovalOutcome.NotTheApprover, result.Outcome);
        Assert.Equal(ApprovalStatus.InApproval, (await h.ReadAsync(order)).ApprovalStatus);
    }

    [SkippableFact]
    public async Task Editing_an_order_mid_chain_returns_it_to_draft_and_cancels_its_open_steps()
    {
        Harness h = await Harness.CreateAsync(_pg);
        long order = await h.OrderAsync(unitPrice: 1_000m, quantity: 150m);
        await h.Approvals.SubmitAsync(ApprovalRequestKind.PurchaseOrder, order, default);
        h.User.Become(Accountant, AccountantRole);
        await h.Approvals.ActAsync(ApprovalRequestKind.PurchaseOrder, order, ApprovalAction.Approve, null, default);

        h.User.Become(Clerk, role: 5);
        PurchaseOrderResult edited = await h.Orders.UpdateAsync(order, Request(1_100m, 150m), default);

        Assert.Equal(PurchaseOrderOutcome.Ok, edited.Outcome);
        Assert.NotNull(edited.Detail);

        PurchaseOrder draft = await h.ReadAsync(order);
        Assert.Null(draft.ApprovalStatus);
        Assert.Equal(DocumentStatus.Draft, draft.Status);

        List<PurchaseApprovalStep> steps = await h.Db.ApprovalSteps.AsNoTracking().OrderBy(s => s.Sequence).ToListAsync();
        Assert.Equal([ApprovalStepStatus.Approved, ApprovalStepStatus.Cancelled], steps.Select(s => s.StepStatus));

        // Submitting again starts a new round: the old approval does not count.
        await h.Approvals.SubmitAsync(ApprovalRequestKind.PurchaseOrder, order, default);
        Assert.Equal("Accountant", (await h.ReadAsync(order)).CurrentStepLabel);
        Assert.Equal(2, await h.Db.ApprovalSteps.AsNoTracking().CountAsync(s => s.Round == 2));
    }

    [SkippableFact]
    public async Task The_ordinary_approve_and_confirm_are_refused_while_a_workflow_applies()
    {
        Harness h = await Harness.CreateAsync(_pg);
        long order = await h.OrderAsync(unitPrice: 100m, quantity: 10m);

        Assert.Equal(PurchaseOrderOutcome.LifecycleRefused, (await h.Orders.ApproveAsync(order, default)).Outcome);
        Assert.Equal(PurchaseOrderOutcome.LifecycleRefused, (await h.Orders.ConfirmAsync(order, default)).Outcome);

        await h.Approvals.SubmitAsync(ApprovalRequestKind.PurchaseOrder, order, default);
        Assert.Equal(PurchaseOrderOutcome.LifecycleRefused, (await h.Orders.ConfirmAsync(order, default)).Outcome);
    }

    [SkippableFact]
    public async Task With_no_workflow_the_ordinary_approve_works_as_it_always_has()
    {
        Harness h = await Harness.CreateAsync(_pg, new FakeChains { NoWorkflow = true });
        long order = await h.OrderAsync(unitPrice: 100m, quantity: 10m);

        Assert.Equal(PurchaseApprovalOutcome.NoWorkflow, (await h.Approvals.SubmitAsync(ApprovalRequestKind.PurchaseOrder, order, default)).Outcome);
        Assert.Equal(PurchaseOrderOutcome.Ok, (await h.Orders.ApproveAsync(order, default)).Outcome);
        Assert.Equal(DocumentStatus.ReadyToPost, (await h.ReadAsync(order)).Status);
    }

    [SkippableFact]
    public async Task An_unreachable_master_refuses_the_approve_rather_than_reading_it_as_no_rules()
    {
        Harness h = await Harness.CreateAsync(_pg, new FakeChains { Unreachable = true });
        long order = await h.OrderAsync(unitPrice: 100m, quantity: 10m);

        Assert.Equal(PurchaseOrderOutcome.LifecycleRefused, (await h.Orders.ApproveAsync(order, default)).Outcome);
        Assert.Equal(PurchaseApprovalOutcome.Unavailable, (await h.Approvals.SubmitAsync(ApprovalRequestKind.PurchaseOrder, order, default)).Outcome);
    }

    [SkippableFact]
    public async Task Rejecting_needs_a_comment_and_leaves_the_order_a_draft()
    {
        Harness h = await Harness.CreateAsync(_pg);
        long order = await h.OrderAsync(unitPrice: 100m, quantity: 10m);
        await h.Approvals.SubmitAsync(ApprovalRequestKind.PurchaseOrder, order, default);
        h.User.Become(Accountant, AccountantRole);

        Assert.Equal(PurchaseApprovalOutcome.Refused,
            (await h.Approvals.ActAsync(ApprovalRequestKind.PurchaseOrder, order, ApprovalAction.Reject, null, default)).Outcome);
        Assert.Equal(PurchaseApprovalOutcome.Ok,
            (await h.Approvals.ActAsync(ApprovalRequestKind.PurchaseOrder, order, ApprovalAction.Reject, "Wrong vendor", default)).Outcome);

        PurchaseOrder rejected = await h.ReadAsync(order);
        Assert.Equal(ApprovalStatus.Rejected, rejected.ApprovalStatus);
        Assert.Equal(DocumentStatus.Draft, rejected.Status);
    }

    [SkippableFact]
    public async Task The_inbox_shows_what_waits_on_a_user_or_their_role()
    {
        Harness h = await Harness.CreateAsync(_pg);
        long order = await h.OrderAsync(unitPrice: 1_000m, quantity: 150m);
        await h.Approvals.SubmitAsync(ApprovalRequestKind.PurchaseOrder, order, default);

        h.User.Become(Accountant, AccountantRole);
        ApprovalInboxItem waiting = Assert.Single(await h.Approvals.MineAsync(default));
        Assert.Equal(("purchase-orders", order, "Accountant"), (waiting.Document, waiting.RequestId, waiting.Label));

        h.User.Become(Owner, role: 1);
        Assert.Empty(await h.Approvals.MineAsync(default));
    }

    private static SavePurchaseOrderRequest Request(decimal unitPrice, decimal quantity) => new()
    {
        DocumentDate = new DateOnly(2026, 6, 1),
        ContactId = 42,
        ExpectedDate = new DateOnly(2026, 6, 15),
        PlaceOfSupplyStateCode = "33",
        Lines =
        [
            new SavePurchaseOrderLineRequest
            {
                ItemId = 7,
                Quantity = quantity,
                ConversionFactor = 1m,
                UnitPrice = unitPrice,
                TaxGroupId = 1,
                LineType = DocumentLineType.Stock,
            },
        ],
    };

    /// <summary>A user whose identity a test can change between calls.</summary>
    private sealed class SwitchableUser : ICurrentUser
    {
        public Guid? UserId { get; private set; } = Clerk;

        public Guid? CustomerId => null;

        public Guid? OrgId => null;

        public int? RoleId { get; private set; } = 5;

        public void Become(Guid userId, int role)
        {
            UserId = userId;
            RoleId = role;
        }
    }

    /// <summary>Master's resolver: the Accountant role, then the Owner above ₹1,00,000.</summary>
    private sealed class FakeChains : IApprovalChainClient
    {
        public bool NoWorkflow { get; init; }

        public bool Unreachable { get; init; }

        public Task<ResolveChainResponse?> ResolveAsync(ResolveChainRequest request, CancellationToken ct)
        {
            if (Unreachable)
            {
                return Task.FromResult<ResolveChainResponse?>(null);
            }

            if (NoWorkflow)
            {
                return Task.FromResult<ResolveChainResponse?>(new ResolveChainResponse { Outcome = ResolveChainOutcome.NoWorkflow });
            }

            var response = new ResolveChainResponse
            {
                Outcome = ResolveChainOutcome.Resolved,
                WorkflowName = "Purchase orders",
                Steps = [new ResolvedStep { Sequence = 1, Label = "Accountant", RoleId = AccountantRole }],
            };

            if (request.Amount > 100_000m)
            {
                response.Steps.Add(new ResolvedStep { Sequence = 2, Label = "Owner", ApproverUserId = Owner });
            }

            return Task.FromResult<ResolveChainResponse?>(response);
        }

        public Task<bool> IsDelegateAsync(DelegateCheckRequest request, CancellationToken ct) => Task.FromResult(false);
    }

    private sealed record Harness(PurchaseDbContext Db, PurchaseOrderService Orders, PurchaseApprovalService Approvals, SwitchableUser User)
    {
        public static async Task<Harness> CreateAsync(PostgresFixture pg, FakeChains? chains = null)
        {
            Skip.If(pg.SkipReason is not null, pg.SkipReason ?? string.Empty);

            var tenant = new TenantContext { CustomerId = Guid.NewGuid(), OrgId = Guid.NewGuid() };
            PurchaseDbContext db = pg.CreateContext(tenant.CustomerId!.Value, tenant.OrgId!.Value);
            db.NumberingSeries.AddRange(Repository.SeedData.NumberingSeriesSeed.Build(tenant.OrgId.Value));
            await db.SaveChangesAsync();

            StubNameLookup names = new();
            SwitchableUser user = new();
            var numbering = new NumberGenerator(db, Options.Create(new NumberingOptions()), new StubFinancialYear());
            var approvals = new PurchaseApprovalService(db, chains ?? new FakeChains(), tenant, user, TimeProvider.System);

            PurchaseOrderService orders = new(
                db, numbering, new StubBaseCurrency(), new StubBranchSettings(), new StubTaxRates(),
                names, names, user, TimeProvider.System, approvals);

            return new Harness(db, orders, approvals, user);
        }

        public async Task<long> OrderAsync(decimal unitPrice, decimal quantity)
        {
            PurchaseOrderResult created = await Orders.CreateAsync(Request(unitPrice, quantity), default);
            Assert.Equal(PurchaseOrderOutcome.Ok, created.Outcome);
            Db.ChangeTracker.Clear();
            return created.PurchaseOrderId;
        }

        public async Task<PurchaseOrder> ReadAsync(long id)
        {
            Db.ChangeTracker.Clear();
            return await Db.PurchaseOrders.AsNoTracking().SingleAsync(o => o.PurchaseOrderId == id);
        }
    }
}
