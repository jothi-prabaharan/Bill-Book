using Inventory.Api.Services;
using Inventory.Entity.Enums;
using Inventory.Entity.TableEntities;
using Inventory.Repository;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Shared.Kernel.Tenancy;
using Xunit;

namespace Inventory.Api.Tests;

/// <summary>
/// The weighted average recalculation against a real database: what it writes
/// back, what it leaves alone, and which lines it sends back to the ledger.
///
/// The arithmetic is <see cref="WeightedAverageCalculatorTests"/>'s. These are
/// about the rows — that the 12-decimal unit cost survives the column, that a
/// closed period is not touched, and that only a line whose value changed is
/// reposted.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class WeightedAverageRecostingTests
{
    private readonly PostgresFixture _postgres;

    public WeightedAverageRecostingTests(PostgresFixture postgres) => _postgres = postgres;

    private static readonly DateOnly Day1 = new(2026, 9, 1);

    [SkippableFact]
    public async Task The_worked_example_is_written_back_to_the_stock_outs()
    {
        await using Harness h = await Harness.CreateAsync(_postgres);
        CancellationToken ct = CancellationToken.None;

        long item = await h.Item(onHand: 5m, average: 10.5m);

        await h.In(item, 3m, 10m, Day1);
        await h.In(item, 4m, 10.5m, Day1.AddDays(1));
        long out3 = await h.Out(item, 2m, Day1.AddDays(2));
        long out4 = await h.Out(item, 2m, Day1.AddDays(3));
        await h.In(item, 6m, 11.3333m, Day1.AddDays(4));
        long out6 = await h.Out(item, 1m, Day1.AddDays(5));
        long out7 = await h.Out(item, 3m, Day1.AddDays(6));

        WeightedAverageRecostResult result = await h.Recosting.RecalculateAsync(item, null, ct);

        Assert.Equal(4, result.StockOutsRevalued);

        StockMovement Row(long id) => h.Db.StockMovements.AsNoTracking().Single(m => m.StockMovementId == id);

        Assert.Equal(20.57m, Row(out3).TotalCost);
        Assert.Equal(20.57m, Row(out4).TotalCost);
        Assert.Equal(10.99m, Row(out6).TotalCost);
        Assert.Equal(32.95m, Row(out7).TotalCost);

        // Twelve decimals survive the column rather than being cut to six.
        Assert.Equal(10.285714285714m, Row(out3).UnitCost);
        Assert.Equal(10.984104761905m, Row(out7).UnitCost);
        Assert.Equal(10.984104761905m, Row(out7).ResultingWeightedAverageCost);

        // The item's average is where the recalculation ended.
        decimal average = await h.Db.ItemStock
            .Where(s => s.ItemId == item)
            .Select(s => s.WeightedAverageCost)
            .SingleAsync(ct);

        Assert.Equal(10.984104761905m, average);
    }

    [SkippableFact]
    public async Task Only_a_posted_line_whose_value_changed_goes_back_to_the_ledger()
    {
        await using Harness h = await Harness.CreateAsync(_postgres);
        CancellationToken ct = CancellationToken.None;

        long item = await h.Item(onHand: 13m, average: 15m);

        await h.In(item, 10m, 10m, Day1);

        // Already costed at 10 and posted, before a receipt at 20 was keyed
        // with an earlier date. Its value is about to change.
        long restated = await h.Out(
            item, 5m, Day1.AddDays(5), unitCost: 10m, ledger: LedgerStatus.Posted);

        // Posted at the value the recalculation will also arrive at.
        long unchanged = await h.Out(
            item, 1m, Day1.AddDays(1), unitCost: 10m, ledger: LedgerStatus.Posted);

        // Not posted yet — it will post whatever it carries when it gets there.
        long waiting = await h.Out(
            item, 1m, Day1.AddDays(6), unitCost: 10m, ledger: LedgerStatus.Pending);

        await h.In(item, 10m, 20m, Day1.AddDays(2));

        WeightedAverageRecostResult result = await h.Recosting.RecalculateAsync(item, null, ct);

        StockMovement Row(long id) => h.Db.StockMovements.AsNoTracking().Single(m => m.StockMovementId == id);

        // After the day-2 receipt: (90 + 200) ÷ 19 = 15.263157894737.
        Assert.Equal(76.32m, Row(restated).TotalCost);
        Assert.Equal(LedgerStatus.Pending, Row(restated).LedgerStatus);
        Assert.Equal(0, Row(restated).LedgerAttempts);
        Assert.Null(Row(restated).LedgerPostedAt);

        Assert.Equal(10.00m, Row(unchanged).TotalCost);
        Assert.Equal(LedgerStatus.Posted, Row(unchanged).LedgerStatus);

        Assert.Equal(LedgerStatus.Pending, Row(waiting).LedgerStatus);

        Assert.Equal(1, result.StockOutsRequeuedForPosting);
    }

    [SkippableFact]
    public async Task A_stock_out_in_a_closed_period_is_not_touched()
    {
        await using Harness h = await Harness.CreateAsync(_postgres);
        CancellationToken ct = CancellationToken.None;

        long item = await h.Item(onHand: 15m, average: 15m);

        await h.In(item, 10m, 10m, Day1);
        long closed = await h.Out(
            item, 2m, Day1.AddDays(3), unitCost: 9m, ledger: LedgerStatus.Posted);
        long open = await h.Out(
            item, 5m, Day1.AddDays(10), unitCost: 9m, ledger: LedgerStatus.Posted);

        // Keyed later, dated inside the closed period.
        await h.In(item, 10m, 20m, Day1.AddDays(2));

        // After the lock date, and before the open stock-out.
        await h.In(item, 2m, 15m, Day1.AddDays(7));

        await h.Recosting.RecalculateAsync(item, lockDate: Day1.AddDays(5), ct);

        StockMovement Row(long id) => h.Db.StockMovements.AsNoTracking().Single(m => m.StockMovementId == id);

        // Stored at 9 when the average was 10, and left there: the period is closed.
        Assert.Equal(18.00m, Row(closed).TotalCost);
        Assert.Equal(9m, Row(closed).UnitCost);
        Assert.Equal(LedgerStatus.Posted, Row(closed).LedgerStatus);

        // The locked 18.00 still leaves the running value, so it shapes the next
        // average: (300 − 18 + 30) ÷ 20 = 15.6. Recalculated at 15 it would have
        // left 270, and the average would be 15.
        Assert.Equal(15.6m, Row(open).UnitCost);
        Assert.Equal(78.00m, Row(open).TotalCost);
        Assert.Equal(LedgerStatus.Pending, Row(open).LedgerStatus);
    }

    [SkippableFact]
    public async Task A_posting_overtaken_by_a_recalculation_is_not_marked_posted()
    {
        await using Harness h = await Harness.CreateAsync(_postgres);
        CancellationToken ct = CancellationToken.None;

        long item = await h.Item(onHand: 5m, average: 10m);
        await h.In(item, 10m, 10m, Day1);

        long sale = await h.Out(
            item, 5m, Day1.AddDays(1), unitCost: 10m, ledger: LedgerStatus.Pending, costed: true);

        // The ledger accepts the posting of 50.00, and while that call is in
        // flight the recalculation restates the sale at 60.00.
        var ledger = new RestatingLedger(h.Db, sale, restatedTo: 60m);
        var poster = new StockLedgerPoster(
            h.Db, ledger, h.Tenant, TimeProvider.System, NullLogger<StockLedgerPoster>.Instance);

        int posted = await poster.PostPendingAsync(10, 5, TimeSpan.FromMinutes(10), ct);

        Assert.Equal(50m, Assert.Single(ledger.Amounts));
        Assert.Equal(0, posted);

        StockMovement row = await h.Db.StockMovements.AsNoTracking()
            .SingleAsync(m => m.StockMovementId == sale, ct);

        // Still owed: the ledger holds 50.00 and the movement is worth 60.00.
        Assert.Equal(LedgerStatus.Pending, row.LedgerStatus);

        // The next pass posts what it now carries, and settles.
        ledger.StopRestating();
        Assert.Equal(1, await poster.PostPendingAsync(10, 5, TimeSpan.FromMinutes(10), ct));
        Assert.Equal(60m, ledger.Amounts[^1]);

        row = await h.Db.StockMovements.AsNoTracking()
            .SingleAsync(m => m.StockMovementId == sale, ct);

        Assert.Equal(LedgerStatus.Posted, row.LedgerStatus);
    }

    /// <summary>A ledger that accepts every posting, and restates the movement mid-call once.</summary>
    private sealed class RestatingLedger : IAccountingLedger
    {
        private readonly InventoryDbContext _db;
        private readonly long _movementId;
        private readonly decimal _restatedTo;
        private bool _restate = true;

        public RestatingLedger(InventoryDbContext db, long movementId, decimal restatedTo)
        {
            _db = db;
            _movementId = movementId;
            _restatedTo = restatedTo;
        }

        public List<decimal> Amounts { get; } = [];

        public void StopRestating() => _restate = false;

        public async Task<LedgerPostOutcome> PostAsync(LedgerPosting posting, CancellationToken ct)
        {
            Amounts.Add(posting.Legs.Sum(l => l.DebitAmount));

            if (_restate)
            {
                decimal restatedTo = _restatedTo;

                await _db.StockMovements
                    .Where(m => m.StockMovementId == _movementId)
                    .ExecuteUpdateAsync(
                        m => m
                            .SetProperty(x => x.TotalCost, restatedTo)
                            .SetProperty(x => x.LedgerStatus, LedgerStatus.Pending),
                        ct);

                _restate = false;
            }

            return LedgerPostOutcome.Posted;
        }
    }

    private sealed class Harness : IAsyncDisposable
    {
        public required InventoryDbContext Db { get; init; }

        public required TenantContext Tenant { get; init; }

        public required WeightedAverageRecosting Recosting { get; init; }

        private long UomId { get; init; }

        private long UomTypeId { get; init; }

        private Guid OrgId => Tenant.OrgId!.Value;

        public async Task<long> Item(decimal onHand, decimal average)
        {
            var item = new Item
            {
                OrgId = OrgId,
                ItemCode = $"WAC-{Guid.NewGuid():N}"[..20],
                ItemName = "Weighted average item",
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
                WeightedAverageCost = average,
            });

            await Db.SaveChangesAsync();
            return item.ItemId;
        }

        /// <summary>A receipt at its own cost, not posted by this service.</summary>
        public Task<long> In(long itemId, decimal quantity, decimal unitCost, DateOnly on) =>
            Add(itemId, StockMovementType.Receipt, StockDirection.In, quantity, unitCost, on,
                LedgerStatus.NotApplicable, costed: true);

        /// <summary>A sale, stored at whatever the request path valued it at.</summary>
        public Task<long> Out(
            long itemId,
            decimal quantity,
            DateOnly on,
            decimal unitCost = 0m,
            LedgerStatus ledger = LedgerStatus.Pending,
            bool costed = false) =>
            Add(itemId, StockMovementType.Issue, StockDirection.Out, quantity, unitCost, on,
                ledger, costed);

        private async Task<long> Add(
            long itemId,
            StockMovementType type,
            StockDirection direction,
            decimal quantity,
            decimal unitCost,
            DateOnly on,
            LedgerStatus ledger,
            bool costed)
        {
            var movement = new StockMovement
            {
                OrgId = OrgId,
                ItemId = itemId,
                MovementType = type,
                Direction = direction,
                MovementDate = on,
                EnteredQuantity = quantity,
                EnteredUomId = UomId,
                Quantity = quantity,
                ConversionFactor = 1m,
                UnitCost = unitCost,
                TotalCost = Math.Round(quantity * unitCost, 2, MidpointRounding.AwayFromZero),
                CostingStatus = costed ? CostingStatus.Costed : CostingStatus.Pending,
                LedgerStatus = ledger,
                LedgerPostedAt = ledger == LedgerStatus.Posted ? DateTimeOffset.UtcNow : null,
                LedgerAttempts = ledger == LedgerStatus.Posted ? 1 : 0,
            };

            Db.StockMovements.Add(movement);
            await Db.SaveChangesAsync();
            return movement.StockMovementId;
        }

        public static async Task<Harness> CreateAsync(PostgresFixture postgres)
        {
            Skip.If(postgres.SkipReason is not null, postgres.SkipReason ?? string.Empty);

            var tenant = new TenantContext { CustomerId = Guid.NewGuid(), OrgId = Guid.NewGuid() };
            InventoryDbContext db = postgres.CreateContext(tenant.CustomerId.Value, tenant.OrgId.Value);

            var uomType = new UomType
            {
                OrgId = tenant.OrgId.Value,
                UomTypeName = "Count",
                UomTypeSystemName = "COUNT",
                IsActive = true,
            };

            db.UomTypes.Add(uomType);
            await db.SaveChangesAsync();

            var uom = new UnitOfMeasure
            {
                OrgId = tenant.OrgId.Value,
                UomTypeId = uomType.UomTypeId,
                UomCode = "PCS",
                UomName = "Pieces",
                ConversionToBase = 1m,
                IsBaseUnit = true,
                IsActive = true,
            };

            db.UnitOfMeasures.Add(uom);
            await db.SaveChangesAsync();

            return new Harness
            {
                Db = db,
                Tenant = tenant,
                UomTypeId = uomType.UomTypeId,
                UomId = uom.UomId,
                Recosting = new WeightedAverageRecosting(
                    db, NullLogger<WeightedAverageRecosting>.Instance),
            };
        }

        public async ValueTask DisposeAsync() => await Db.DisposeAsync();
    }
}
