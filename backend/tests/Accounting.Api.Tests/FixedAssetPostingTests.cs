using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Accounting.Api.Services;
using Accounting.Entity.Enums;
using Accounting.Entity.Models;
using Accounting.Entity.TableEntities;
using Accounting.Repository;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Shared.Kernel.Numbering;
using Shared.Kernel.Tenancy;
using Xunit;

namespace Accounting.Api.Tests;

[Collection(nameof(PostgresCollection))]
public class FixedAssetPostingTests
{
    private readonly PostgresFixture _postgres;

    public FixedAssetPostingTests(PostgresFixture postgres) => _postgres = postgres;

    [SkippableFact]
    public async Task Running_depreciation_posts_balanced_legs_to_ledger()
    {
        await using Harness h = await Harness.CreateAsync(_postgres);
        CancellationToken ct = CancellationToken.None;

        var runDate = new DateOnly(2026, 8, 31);
        await h.Depreciation.RunDepreciationAsync(runDate, ct);

        // Verify the asset transaction is recorded
        var txns = await h.Db.AssetTransactions.ToListAsync(ct);
        Assert.Single(txns);
        Assert.Equal(AssetTransactionType.Depreciation, txns[0].TransactionType);
        
        // Ensure journal was created and posted
        Assert.NotNull(txns[0].JournalId);
        
        var journal = await h.Db.Journals.FindAsync(txns[0].JournalId);
        Assert.NotNull(journal);
        Assert.Equal(JournalStatus.Posted, journal.Status);

        // Verify Ledger rows are balanced
        var ledgerRows = await h.Db.JournalLedger
            .Where(l => l.JournalId == journal.JournalId)
            .ToListAsync(ct);

        Assert.Equal(2, ledgerRows.Count);
        Assert.Equal(
            ledgerRows.Sum(l => l.DebitAmountBase),
            ledgerRows.Sum(l => l.CreditAmountBase)
        );

        Assert.Contains(ledgerRows, l => l.AccountId == h.DepreciationExpenseId && l.DebitAmount > 0);
        Assert.Contains(ledgerRows, l => l.AccountId == h.AccumulatedDepreciationId && l.CreditAmount > 0);
    }

    [SkippableFact]
    public async Task A_run_repeated_for_the_same_month_charges_once()
    {
        await using Harness h = await Harness.CreateAsync(_postgres);
        CancellationToken ct = CancellationToken.None;

        // The same month, asked for twice — a re-run after a partial failure,
        // or two operators closing the period between them.
        DepreciationRunResult first = await h.Depreciation.RunDepreciationAsync(new DateOnly(2026, 8, 31), ct);
        DepreciationRunResult second = await h.Depreciation.RunDepreciationAsync(new DateOnly(2026, 8, 15), ct);

        // A repeat is a success that did nothing — not an error, and no journal.
        Assert.Equal(DepreciationRunOutcome.Ok, first.Outcome);
        Assert.Equal(1, first.AssetsCharged);
        Assert.Equal(DepreciationRunOutcome.Ok, second.Outcome);
        Assert.Equal(0, second.AssetsCharged);
        Assert.Null(second.JournalId);
        Assert.Single(await h.Db.Journals.ToListAsync(ct));

        var txns = await h.Db.AssetTransactions.ToListAsync(ct);

        // One charge, one journal. Before the guard the second run posted a
        // second month's expense into the same period and the asset quietly
        // depreciated at twice its schedule — every journal balancing, so
        // nothing downstream disagreed.
        Assert.Single(txns);

        var ledgerRows = await h.Db.JournalLedger.ToListAsync(ct);
        Assert.Equal(2, ledgerRows.Count);
        Assert.Equal(
            ledgerRows.Where(l => l.AccountId == h.DepreciationExpenseId).Sum(l => l.DebitAmount),
            txns[0].Amount);
    }

    [SkippableFact]
    public async Task The_next_month_is_charged_even_though_this_one_was()
    {
        await using Harness h = await Harness.CreateAsync(_postgres);
        CancellationToken ct = CancellationToken.None;

        await h.Depreciation.RunDepreciationAsync(new DateOnly(2026, 8, 31), ct);
        await h.Depreciation.RunDepreciationAsync(new DateOnly(2026, 9, 30), ct);

        // The guard is per month, not per asset for all time — an asset
        // charged in August is still due in September.
        var txns = await h.Db.AssetTransactions.OrderBy(t => t.TransactionDate).ToListAsync(ct);

        Assert.Equal(2, txns.Count);
        Assert.Equal(new DateOnly(2026, 8, 31), txns[0].TransactionDate);
        Assert.Equal(new DateOnly(2026, 9, 30), txns[1].TransactionDate);
    }

    [SkippableFact]
    public async Task Straight_line_charges_cost_less_salvage_over_the_life_for_one_month()
    {
        await using Harness h = await Harness.CreateAsync(_postgres);
        CancellationToken ct = CancellationToken.None;

        DepreciationRunResult result = await h.Depreciation.RunDepreciationAsync(new DateOnly(2026, 8, 31), ct);

        // 1,200 over three years is 400 a year, 33.33 a month.
        Assert.Equal(DepreciationRunOutcome.Ok, result.Outcome);
        AssetTransaction charge = Assert.Single(await h.Db.AssetTransactions.ToListAsync(ct));
        Assert.Equal(33.33m, charge.Amount);
        Assert.Equal(result.JournalId, charge.JournalId);
    }

    [SkippableFact]
    public async Task Written_down_value_charges_the_rate_on_what_is_left_each_month()
    {
        await using Harness h = await Harness.CreateAsync(
            _postgres, DepreciationMethod.WrittenDownValue, rate: 20m, usefulLifeYears: 0);
        CancellationToken ct = CancellationToken.None;

        await h.Depreciation.RunDepreciationAsync(new DateOnly(2026, 8, 31), ct);
        await h.Depreciation.RunDepreciationAsync(new DateOnly(2026, 9, 30), ct);

        // August: 1,200 × 20% ÷ 12 = 20.00. September on the 1,180 left: 19.67.
        // Before TK-11 the method was stored and never read, so both were zero
        // and the run posted nothing at all.
        var charges = await h.Db.AssetTransactions.OrderBy(t => t.TransactionDate).ToListAsync(ct);
        Assert.Equal([20.00m, 19.67m], charges.Select(c => c.Amount));

        var ledger = await h.Db.JournalLedger.ToListAsync(ct);
        Assert.Equal(39.67m, ledger.Where(l => l.AccountId == h.AccumulatedDepreciationId).Sum(l => l.CreditAmount));
        Assert.Equal(ledger.Sum(l => l.DebitAmountBase), ledger.Sum(l => l.CreditAmountBase));
    }

    [SkippableFact]
    public async Task An_asset_whose_schedule_starts_later_is_not_charged_yet()
    {
        await using Harness h = await Harness.CreateAsync(_postgres, startDate: new DateOnly(2026, 10, 1));
        CancellationToken ct = CancellationToken.None;

        DepreciationRunResult result = await h.Depreciation.RunDepreciationAsync(new DateOnly(2026, 8, 31), ct);

        Assert.Equal(0, result.AssetsCharged);
        Assert.Empty(await h.Db.AssetTransactions.ToListAsync(ct));
    }

    [SkippableFact]
    public async Task Another_branchs_asset_cannot_be_disposed_and_answers_not_found()
    {
        await using Harness h = await Harness.CreateAsync(_postgres);
        CancellationToken ct = CancellationToken.None;

        // Same customer, a second branch. The query filter hides branch A's
        // asset from it, so the answer is 404 rather than a 403 that would
        // confirm the id exists in someone else's books (CLAUDE.md, TK-71).
        await using AccountingDbContext other = _postgres.CreateContext(h.CustomerId, Guid.NewGuid());

        FixedAssetResult result = await new FixedAssetService(other).DisposeAsync(
            h.AssetId, new DisposeAssetRequest { DisposalDate = new DateOnly(2026, 9, 1) }, ct);

        Assert.Equal(FixedAssetOutcome.NotFound, result.Outcome);

        h.Db.ChangeTracker.Clear();
        FixedAsset asset = await h.Db.FixedAssets.SingleAsync(a => a.FixedAssetId == h.AssetId, ct);
        Assert.Equal(FixedAssetStatus.Active, asset.Status);
        Assert.Empty(await h.Db.AssetTransactions.ToListAsync(ct));
    }

    [SkippableFact]
    public async Task Disposing_twice_is_refused_the_second_time()
    {
        await using Harness h = await Harness.CreateAsync(_postgres);
        CancellationToken ct = CancellationToken.None;
        var service = new FixedAssetService(h.Db);
        var request = new DisposeAssetRequest { DisposalDate = new DateOnly(2026, 9, 1), SaleAmount = 500m };

        Assert.Equal(FixedAssetOutcome.Ok, (await service.DisposeAsync(h.AssetId, request, ct)).Outcome);
        Assert.Equal(FixedAssetOutcome.NotActive, (await service.DisposeAsync(h.AssetId, request, ct)).Outcome);

        AssetTransaction disposal = Assert.Single(await h.Db.AssetTransactions.ToListAsync(ct));
        Assert.Equal(AssetTransactionType.Disposal, disposal.TransactionType);
    }

    [SkippableFact]
    public async Task A_disposal_dated_before_the_purchase_is_refused()
    {
        await using Harness h = await Harness.CreateAsync(_postgres);

        FixedAssetResult result = await new FixedAssetService(h.Db).DisposeAsync(
            h.AssetId, new DisposeAssetRequest { DisposalDate = new DateOnly(2026, 7, 31) }, CancellationToken.None);

        Assert.Equal(FixedAssetOutcome.DisposalBeforePurchase, result.Outcome);
    }

    [SkippableFact]
    public async Task Registering_writes_the_asset_and_its_schedules_and_refuses_a_duplicate_code()
    {
        await using Harness h = await Harness.CreateAsync(_postgres);
        CancellationToken ct = CancellationToken.None;
        var service = new FixedAssetService(h.Db);

        CreateFixedAssetRequest Request(string code) => new()
        {
            FixedAssetCategoryId = h.CategoryId,
            AssetCode = code,
            AssetName = "Printer",
            PurchaseDate = new DateOnly(2026, 8, 1),
            PurchasePrice = 600m,
            Status = FixedAssetStatus.Active,
            Schedules =
            [
                new() { ScheduleType = DepreciationScheduleType.Books, DepreciationMethod = DepreciationMethod.StraightLine, UsefulLifeYears = 5, DepreciationStartDate = new DateOnly(2026, 8, 1) },
                new() { ScheduleType = DepreciationScheduleType.Tax, DepreciationMethod = DepreciationMethod.WrittenDownValue, Rate = 15m, DepreciationStartDate = new DateOnly(2026, 8, 1) },
            ],
        };

        FixedAssetResult created = await service.RegisterAsync(Request("PR-001"), ct);
        Assert.Equal(FixedAssetOutcome.Ok, created.Outcome);
        Assert.Equal(2, await h.Db.DepreciationSchedules.CountAsync(s => s.FixedAssetId == created.FixedAssetId, ct));

        // PC-001 is the harness's own asset.
        Assert.Equal(FixedAssetOutcome.DuplicateCode, (await service.RegisterAsync(Request("PC-001"), ct)).Outcome);

        CreateFixedAssetRequest noCategory = Request("PR-002");
        noCategory.FixedAssetCategoryId = long.MaxValue;
        Assert.Equal(FixedAssetOutcome.CategoryMissing, (await service.RegisterAsync(noCategory, ct)).Outcome);
    }

    private sealed class Harness : IAsyncDisposable
    {
        public required AccountingDbContext Db { get; init; }
        public required DepreciationService Depreciation { get; init; }
        
        public required long AssetAccountId { get; init; }
        public required long AccumulatedDepreciationId { get; init; }
        public required long DepreciationExpenseId { get; init; }
        public required long CategoryId { get; init; }
        public required long AssetId { get; init; }
        public required Guid CustomerId { get; init; }

        public static async Task<Harness> CreateAsync(
            PostgresFixture postgres,
            DepreciationMethod method = DepreciationMethod.StraightLine,
            decimal rate = 0m,
            int usefulLifeYears = 3,
            DateOnly? startDate = null)
        {
            Skip.If(postgres.SkipReason is not null, postgres.SkipReason ?? string.Empty);

            var orgId = Guid.NewGuid();
            var tenant = new TenantContext { CustomerId = Guid.NewGuid(), OrgId = orgId };
            AccountingDbContext db = postgres.CreateContext(tenant.CustomerId!.Value, tenant.OrgId!.Value);

            async Task<long> Account(string code, string name, int typeId)
            {
                var account = new Account
                {
                    OrgId = orgId,
                    AccountTypeId = typeId,
                    AccountCode = code,
                    AccountName = name,
                    IsActive = true
                };

                db.Accounts.Add(account);
                await db.SaveChangesAsync();
                return account.AccountId;
            }

            long assetAcc = await Account("1500", "Office Equipment", 1);
            long accDep = await Account("1550", "Accumulated Depreciation", 1);
            long depExp = await Account("6500", "Depreciation Expense", 5);

            var category = new FixedAssetCategory
            {
                OrgId = orgId,
                CategoryName = "Computers",
                AssetAccountId = assetAcc,
                AccumulatedDepreciationAccountId = accDep,
                DepreciationExpenseAccountId = depExp
            };
            db.FixedAssetCategories.Add(category);
            await db.SaveChangesAsync();

            var asset = new FixedAsset
            {
                OrgId = orgId,
                FixedAssetCategoryId = category.FixedAssetCategoryId,
                AssetCode = "PC-001",
                AssetName = "Developer Laptop",
                PurchaseDate = new DateOnly(2026, 8, 1),
                PurchasePrice = 1200m,
                Status = FixedAssetStatus.Active
            };
            db.FixedAssets.Add(asset);
            await db.SaveChangesAsync();

            var schedule = new DepreciationSchedule
            {
                OrgId = orgId,
                FixedAssetId = asset.FixedAssetId,
                ScheduleType = DepreciationScheduleType.Books,
                DepreciationMethod = method,
                Rate = rate,
                UsefulLifeYears = usefulLifeYears,
                SalvageValue = 0,
                DepreciationStartDate = startDate ?? new DateOnly(2026, 8, 1)
            };
            db.DepreciationSchedules.Add(schedule);
            
            db.NumberingSeries.AddRange(Repository.SeedData.NumberingSeriesSeed.Build(orgId).Where(s => s.SeriesCode == "JRN"));
            await db.SaveChangesAsync();

            var numbers = new NumberGenerator(db, Options.Create(new NumberingOptions()), new StubFinancialYear());
            var postings = new LedgerPostingService(db, tenant, new StubBaseCurrency());
            
            var journals = new JournalService(
                db, postings, new PeriodLockService(db, new StubCurrentUser()), numbers, 
                new StubBaseCurrency(), new StubCurrentUser(), tenant, TimeProvider.System);

            return new Harness
            {
                Db = db,
                Depreciation = new DepreciationService(db, journals),
                AssetAccountId = assetAcc,
                AccumulatedDepreciationId = accDep,
                DepreciationExpenseId = depExp,
                CategoryId = category.FixedAssetCategoryId,
                AssetId = asset.FixedAssetId,
                CustomerId = tenant.CustomerId!.Value
            };
        }

        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }
}
