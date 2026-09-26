using Inventory.Api.Services;
using Inventory.Entity.Enums;
using Inventory.Entity.Models;
using Inventory.Entity.TableEntities;
using Inventory.Repository;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Shared.Kernel.Approvals;
using Shared.Kernel.Interfaces;
using Shared.Kernel.Tenancy;
using Shared.Kernel.Numbering;
using Xunit;
using Microsoft.Extensions.Logging.Abstractions;

namespace Inventory.Api.Tests;

/// <summary>
/// The adjustment sheet, against a real database.
///
/// The claim these exist to check is the one T9 was written for: a sheet of
/// several items posts as <b>one document</b>, not as several loose movements.
/// That claim is not visible in the sheet's own tables — it is visible in the
/// source key on the movements it wrote, because that key is what the
/// movement-to-ledger mapping files a posting under.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class StockAdjustmentServiceTests
{
    private readonly PostgresFixture _postgres;

    public StockAdjustmentServiceTests(PostgresFixture postgres) => _postgres = postgres;

    /// <summary>
    /// Three items counted, one document. Every movement carries the sheet's id
    /// as its source and its own line number, which is exactly what makes the
    /// ledger show one adjustment with three lines rather than three
    /// adjustments.
    /// </summary>
    [SkippableFact]
    public async Task A_count_of_three_items_posts_as_one_document()
    {
        await using Harness h = await Harness.CreateAsync(_postgres);
        CancellationToken ct = CancellationToken.None;

        long a = await h.Item("WIDGET-A", onHand: 10m);
        long b = await h.Item("WIDGET-B", onHand: 10m);
        long c = await h.Item("WIDGET-C", onHand: 10m);

        StockAdjustmentResult draft = await h.Adjustments.SaveAsync(null, new()
        {
            AdjustmentDate = new DateOnly(2026, 8, 11),
            Kind = nameof(StockAdjustmentKind.PhysicalCount),
            Reason = nameof(StockAdjustmentReason.CountCorrection),
            Lines =
            [
                new() { ItemId = a, CountedQuantity = 8m },   // two short
                new() { ItemId = b, CountedQuantity = 10m },  // agrees
                new() { ItemId = c, CountedQuantity = 13m },  // three found
            ],
        }, ct);

        Assert.Equal(StockAdjustmentOutcome.Ok, draft.Outcome);

        // The line that agreed with the books moves nothing and is not kept:
        // on a real count sheet most lines agree, and keeping them would post
        // rows of zero.
        List<StockAdjustmentLine> lines = await h.Db.StockAdjustmentLines
            .Where(l => l.StockAdjustmentId == draft.StockAdjustmentId)
            .OrderBy(l => l.LineNumber)
            .ToListAsync(ct);

        Assert.Equal(2, lines.Count);
        Assert.Equal(2m, lines[0].Quantity);
        Assert.Equal(StockDirection.Out, lines[0].Direction);
        Assert.Equal(3m, lines[1].Quantity);
        Assert.Equal(StockDirection.In, lines[1].Direction);

        // The books at the moment of counting, snapshotted so the arithmetic can
        // be re-checked later against what was believed then.
        Assert.Equal(10m, lines[0].SystemQuantity);

        StockAdjustmentResult posted =
            await h.Adjustments.PostAsync(draft.StockAdjustmentId!.Value, ct);

        Assert.Equal(StockAdjustmentOutcome.Ok, posted.Outcome);

        List<StockMovement> movements = await h.Db.StockMovements
            .Where(m => m.SourceType == "STA" && m.SourceId == draft.StockAdjustmentId)
            .OrderBy(m => m.SourceLineId)
            .ToListAsync(ct);

        // One document, two movements, each on its own line — the ledger key.
        Assert.Equal(2, movements.Count);
        Assert.All(movements, m => Assert.Equal(draft.StockAdjustmentId, m.SourceId));
        Assert.Equal([1L, 2L], movements.Select(m => m.SourceLineId));

        Assert.Equal(8m, await h.OnHand(a));
        Assert.Equal(10m, await h.OnHand(b));
        Assert.Equal(13m, await h.OnHand(c));
    }

    /// <summary>
    /// A number is taken at post and never at draft, so abandoning a half-typed
    /// count cannot leave a hole in the series.
    /// </summary>
    [SkippableFact]
    public async Task A_draft_holds_no_number_and_a_posted_sheet_does()
    {
        await using Harness h = await Harness.CreateAsync(_postgres);
        CancellationToken ct = CancellationToken.None;

        long item = await h.Item("NUMBERED", onHand: 5m);

        StockAdjustmentResult draft = await h.Adjustments.SaveAsync(null, h.WriteOff(item, 1m), ct);
        long id = draft.StockAdjustmentId!.Value;

        Assert.Null((await h.Db.StockAdjustments.FirstAsync(x => x.StockAdjustmentId == id, ct))
            .AdjustmentNo);

        await h.Adjustments.PostAsync(id, ct);

        StockAdjustment after = await h.Db.StockAdjustments
            .FirstAsync(x => x.StockAdjustmentId == id, ct);

        Assert.NotNull(after.AdjustmentNo);
        Assert.StartsWith("ADJ/", after.AdjustmentNo);
        Assert.Equal(StockAdjustmentStatus.Posted, after.Status);
    }

    /// <summary>
    /// The whole sheet or none of it. A line that cannot post takes the rest with
    /// it — a half-posted count is worse than one that did not post, because only
    /// the second is obvious.
    /// </summary>
    [SkippableFact]
    public async Task A_line_that_cannot_post_rolls_the_whole_sheet_back()
    {
        await using Harness h = await Harness.CreateAsync(_postgres);
        CancellationToken ct = CancellationToken.None;

        long good = await h.Item("GOOD", onHand: 10m);
        long short_ = await h.Item("SHORT", onHand: 1m);

        StockAdjustmentResult draft = await h.Adjustments.SaveAsync(null, new()
        {
            Kind = nameof(StockAdjustmentKind.Adjustment),
            Reason = nameof(StockAdjustmentReason.Damage),
            Lines =
            [
                new() { ItemId = good, Quantity = 2m, Direction = "Out" },
                // More than is on hand: the guarded decrement refuses it.
                new() { ItemId = short_, Quantity = 99m, Direction = "Out" },
            ],
        }, ct);

        StockAdjustmentResult posted =
            await h.Adjustments.PostAsync(draft.StockAdjustmentId!.Value, ct);

        Assert.Equal(StockAdjustmentOutcome.LineRefused, posted.Outcome);
        Assert.Contains("Nothing on the sheet was posted", posted.Detail);

        // The first line's stock is untouched, and no number was spent.
        Assert.Equal(10m, await h.OnHand(good));
        Assert.Equal(1m, await h.OnHand(short_));

        StockAdjustment after = await h.Db.StockAdjustments
            .FirstAsync(x => x.StockAdjustmentId == draft.StockAdjustmentId, ct);

        Assert.Null(after.AdjustmentNo);
        Assert.Equal(StockAdjustmentStatus.Draft, after.Status);
    }

    /// <summary>
    /// Reversing writes a mirror sheet rather than deleting anything: stock
    /// returns, both documents keep their numbers, and each points at the other.
    /// </summary>
    [SkippableFact]
    public async Task Reversing_puts_the_stock_back_and_links_both_documents()
    {
        await using Harness h = await Harness.CreateAsync(_postgres);
        CancellationToken ct = CancellationToken.None;

        long item = await h.Item("REVERSIBLE", onHand: 20m);

        StockAdjustmentResult draft = await h.Adjustments.SaveAsync(null, h.WriteOff(item, 5m), ct);
        long id = draft.StockAdjustmentId!.Value;

        await h.Adjustments.PostAsync(id, ct);
        Assert.Equal(15m, await h.OnHand(item));

        StockAdjustmentResult reversal = await h.Adjustments.ReverseAsync(id, new(), ct);
        Assert.Equal(StockAdjustmentOutcome.Ok, reversal.Outcome);

        Assert.Equal(20m, await h.OnHand(item));

        StockAdjustment original = await h.Db.StockAdjustments
            .FirstAsync(x => x.StockAdjustmentId == id, ct);
        StockAdjustment mirror = await h.Db.StockAdjustments
            .FirstAsync(x => x.StockAdjustmentId == reversal.StockAdjustmentId, ct);

        Assert.Equal(StockAdjustmentStatus.Reversed, original.Status);
        Assert.Equal(mirror.StockAdjustmentId, original.ReversedByStockAdjustmentId);
        Assert.Equal(original.StockAdjustmentId, mirror.ReversesStockAdjustmentId);

        // Both keep their numbers. A series with a hole in it is what an auditor
        // asks about.
        Assert.NotNull(original.AdjustmentNo);
        Assert.NotNull(mirror.AdjustmentNo);
        Assert.NotEqual(original.AdjustmentNo, mirror.AdjustmentNo);

        // And it cannot be reversed twice, which would double the correction.
        Assert.Equal(
            StockAdjustmentOutcome.AlreadyReversed,
            (await h.Adjustments.ReverseAsync(id, new(), ct)).Outcome);
    }

    /// <summary>A posted sheet is immutable: its stock has already moved.</summary>
    [SkippableFact]
    public async Task A_posted_sheet_cannot_be_edited_or_deleted()
    {
        await using Harness h = await Harness.CreateAsync(_postgres);
        CancellationToken ct = CancellationToken.None;

        long item = await h.Item("FROZEN", onHand: 9m);
        StockAdjustmentResult draft = await h.Adjustments.SaveAsync(null, h.WriteOff(item, 1m), ct);
        long id = draft.StockAdjustmentId!.Value;

        await h.Adjustments.PostAsync(id, ct);

        Assert.Equal(
            StockAdjustmentOutcome.NotDraft,
            (await h.Adjustments.SaveAsync(id, h.WriteOff(item, 2m), ct)).Outcome);

        Assert.Equal(
            StockAdjustmentOutcome.NotDraft,
            (await h.Adjustments.DeleteAsync(id, ct)).Outcome);
    }

    /// <summary>
    /// The list and the detail read, which the other tests never touch. Worth its
    /// own case because both project through subqueries EF has to translate — a
    /// projection that cannot be translated compiles perfectly and throws the
    /// first time a screen opens.
    /// </summary>
    [SkippableFact]
    public async Task The_list_and_the_detail_both_read()
    {
        await using Harness h = await Harness.CreateAsync(_postgres);
        CancellationToken ct = CancellationToken.None;

        long item = await h.Item("LISTED", onHand: 12m);
        StockAdjustmentResult draft = await h.Adjustments.SaveAsync(null, h.WriteOff(item, 3m), ct);
        long id = draft.StockAdjustmentId!.Value;

        IReadOnlyList<StockAdjustmentListItem> drafts = await h.Adjustments.ListAsync("Draft", ct);
        Assert.Contains(drafts, a => a.StockAdjustmentId == id);
        Assert.Empty(await h.Adjustments.ListAsync("Posted", ct));

        StockAdjustmentDetail? detail = await h.Adjustments.GetAsync(id, ct);
        Assert.NotNull(detail);
        Assert.Single(detail!.Lines);
        Assert.Equal("LISTED", detail.Lines[0].ItemCode);
        Assert.Equal(1, detail.LineCount);

        await h.Adjustments.PostAsync(id, ct);

        StockAdjustmentDetail after = (await h.Adjustments.GetAsync(id, ct))!;
        Assert.NotNull(after.Lines[0].StockMovementId);
        Assert.Single(await h.Adjustments.ListAsync("Posted", ct));
    }

    /// <summary>A sheet a workflow covers posts only once the chain approves it (TK-102).</summary>
    [SkippableFact]
    public async Task A_sheet_under_a_workflow_posts_only_after_approval()
    {
        await using Harness h = await Harness.CreateAsync(_postgres, new StorekeeperChain());
        CancellationToken ct = CancellationToken.None;
        long item = await h.Item("WIDGET-A", onHand: 10m);

        StockAdjustmentResult draft = await h.Adjustments.SaveAsync(null, h.WriteOff(item, 2m), ct);
        long id = draft.StockAdjustmentId!.Value;

        Assert.Equal(StockAdjustmentOutcome.AwaitingApproval, (await h.Adjustments.PostAsync(id, ct)).Outcome);
        Assert.Equal(10m, await h.OnHand(item));

        Assert.Equal(ApprovalResultOutcome.Ok, (await h.Approvals!.SubmitAsync(ApprovalRequestKind.StockAdjustment, id, ct)).Outcome);
        Assert.Equal(StockAdjustmentOutcome.AwaitingApproval, (await h.Adjustments.PostAsync(id, ct)).Outcome);

        h.User.Become(Guid.NewGuid(), StorekeeperChain.StorekeeperRole);
        Assert.Equal(ApprovalResultOutcome.Ok,
            (await h.Approvals.ActAsync(ApprovalRequestKind.StockAdjustment, id, ApprovalAction.Approve, null, ct)).Outcome);

        Assert.Equal(StockAdjustmentOutcome.Ok, (await h.Adjustments.PostAsync(id, ct)).Outcome);
        Assert.Equal(8m, await h.OnHand(item));
    }

    [SkippableFact]
    public async Task Editing_a_sheet_in_approval_returns_it_to_draft()
    {
        await using Harness h = await Harness.CreateAsync(_postgres, new StorekeeperChain());
        CancellationToken ct = CancellationToken.None;
        long item = await h.Item("WIDGET-A", onHand: 10m);

        StockAdjustmentResult draft = await h.Adjustments.SaveAsync(null, h.WriteOff(item, 2m), ct);
        long id = draft.StockAdjustmentId!.Value;
        await h.Approvals!.SubmitAsync(ApprovalRequestKind.StockAdjustment, id, ct);

        await h.Adjustments.SaveAsync(id, h.WriteOff(item, 3m), ct);

        h.Db.ChangeTracker.Clear();
        StockAdjustment sheet = await h.Db.StockAdjustments.AsNoTracking().SingleAsync(a => a.StockAdjustmentId == id, ct);
        Assert.Null(sheet.ApprovalStatus);
        Assert.Equal(ApprovalStepStatus.Cancelled,
            (await h.Db.ApprovalSteps.AsNoTracking().SingleAsync(ct)).StepStatus);
    }

    private sealed class Harness : IAsyncDisposable
    {
        public required InventoryDbContext Db { get; init; }

        public required Guid OrgId { get; init; }

        public required StockAdjustmentService Adjustments { get; init; }

        private long UomId { get; init; }

        private long UomTypeId { get; init; }

        public SaveStockAdjustmentRequest WriteOff(long itemId, decimal quantity) => new()
        {
            Kind = nameof(StockAdjustmentKind.Adjustment),
            Reason = nameof(StockAdjustmentReason.Damage),
            Lines = [new() { ItemId = itemId, Quantity = quantity, Direction = "Out" }],
        };

        public async Task<decimal> OnHand(long itemId) => await Db.ItemStock
            .Where(s => s.ItemId == itemId)
            .Select(s => s.QuantityOnHand)
            .FirstOrDefaultAsync();

        /// <summary>An item with stock already on the shelf.</summary>
        public async Task<long> Item(string code, decimal onHand)
        {
            var item = new Item
            {
                OrgId = OrgId,
                ItemCode = code,
                ItemName = code,
                UomTypeId = UomTypeId,
                InventoryUomId = UomId,
                SalesUomId = UomId,
                PurchaseUomId = UomId,
                ReportUomId = UomId,
                TrackInventory = true,
                CostingType = CostingType.WeightedAverage,
                IsActive = true,
            };

            Db.Items.Add(item);
            await Db.SaveChangesAsync();

            Db.ItemStock.Add(new ItemStock
            {
                OrgId = OrgId,
                ItemId = item.ItemId,
                QuantityOnHand = onHand,
                WeightedAverageCost = 100m,
            });

            await Db.SaveChangesAsync();
            return item.ItemId;
        }

        /// <summary>Set when the harness was built with an approval workflow (TK-102).</summary>
        public InventoryApprovalService? Approvals { get; private init; }

        public SwitchableUser User { get; private init; } = new();

        public static async Task<Harness> CreateAsync(PostgresFixture postgres, IApprovalChainClient? chains = null)
        {
            Skip.If(postgres.SkipReason is not null, postgres.SkipReason ?? string.Empty);

            var orgId = Guid.NewGuid();
            var customerId = Guid.NewGuid();
            InventoryDbContext db = postgres.CreateContext(customerId, orgId);

            var uomType = new UomType
            {
                OrgId = orgId,
                UomTypeName = "Count",
                UomTypeSystemName = "COUNT",
                IsActive = true,
            };

            db.UomTypes.Add(uomType);
            await db.SaveChangesAsync();

            var uom = new UnitOfMeasure
            {
                OrgId = orgId,
                UomTypeId = uomType.UomTypeId,
                UomCode = "PCS",
                UomName = "Pieces",
                ConversionToBase = 1m,
                IsBaseUnit = true,
                IsActive = true,
            };

            db.UnitOfMeasures.Add(uom);

            // The STA series, exactly as the branch seed writes it — the number
            // is allocated inside the post's own transaction, so it has to be
            // real rather than stubbed.
            db.NumberingSeries.AddRange(Repository.SeedData.NumberingSeriesSeed.Build(orgId));
            await db.SaveChangesAsync();

            var numbers = new NumberGenerator(
                db, Options.Create(new NumberingOptions()), new StubFinancialYear());

            var clock = TimeProvider.System;
            var costing = new CostingService(db);
            var stock = new StockService(db, costing, clock);

            var user = new SwitchableUser();
            InventoryApprovalService? approvals = chains is null
                ? null
                : new InventoryApprovalService(
                    db, chains, new TenantContext { CustomerId = customerId, OrgId = orgId }, user, clock);

            return new Harness
            {
                Db = db,
                OrgId = orgId,
                UomTypeId = uomType.UomTypeId,
                UomId = uom.UomId,
                User = user,
                Approvals = approvals,
                Adjustments = new StockAdjustmentService(
                    db, stock, numbers, user, clock,
                    NullLogger<StockAdjustmentService>.Instance, approvals),
            };
        }

        public async ValueTask DisposeAsync() => await Db.DisposeAsync();
    }

    private sealed class StubFinancialYear : IFinancialYearProvider
    {
        public Task<int> GetStartMonthAsync(CancellationToken ct = default) => Task.FromResult(4);
    }

    /// <summary>A user whose identity a test can change between calls.</summary>
    private sealed class SwitchableUser : ICurrentUser
    {
        public Guid? UserId { get; private set; } = Guid.NewGuid();

        public Guid? CustomerId => null;

        public Guid? OrgId => null;

        public int? RoleId { get; private set; } = 5;

        public void Become(Guid userId, int role)
        {
            UserId = userId;
            RoleId = role;
        }
    }

    /// <summary>Master's resolver: a stock adjustment goes to the storekeeper role (TK-102).</summary>
    private sealed class StorekeeperChain : IApprovalChainClient
    {
        public const int StorekeeperRole = 7;

        public Task<ResolveChainResponse?> ResolveAsync(ResolveChainRequest request, CancellationToken ct) =>
            Task.FromResult<ResolveChainResponse?>(new ResolveChainResponse
            {
                Outcome = ResolveChainOutcome.Resolved,
                WorkflowName = "Stock adjustments",
                Steps = [new ResolvedStep { Sequence = 1, Label = "Storekeeper", RoleId = StorekeeperRole }],
            });

        public Task<bool> IsDelegateAsync(DelegateCheckRequest request, CancellationToken ct) => Task.FromResult(false);
    }

    private sealed class StubCurrentUser : ICurrentUser
    {
        public Guid? UserId { get; } = Guid.NewGuid();

        public Guid? CustomerId => null;

        public Guid? OrgId => null;

        public int? RoleId => null;
    }
}
