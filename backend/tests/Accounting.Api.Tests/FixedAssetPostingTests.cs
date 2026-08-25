using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Accounting.Api.Services;
using Accounting.Entity.Enums;
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

    private sealed class Harness : IAsyncDisposable
    {
        public required AccountingDbContext Db { get; init; }
        public required DepreciationService Depreciation { get; init; }
        
        public required long AssetAccountId { get; init; }
        public required long AccumulatedDepreciationId { get; init; }
        public required long DepreciationExpenseId { get; init; }

        public static async Task<Harness> CreateAsync(PostgresFixture postgres)
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
                DepreciationMethod = DepreciationMethod.StraightLine,
                UsefulLifeYears = 3,
                SalvageValue = 0,
                DepreciationStartDate = new DateOnly(2026, 8, 1)
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
                DepreciationExpenseId = depExp
            };
        }

        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }
}
