using System;
using System.Collections.Generic;
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

        FixedAssetResult result = await new FixedAssetService(other, h.Journals).DisposeAsync(
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
        var service = h.Assets;
        var request = new DisposeAssetRequest
        {
            DisposalDate = new DateOnly(2026, 9, 1),
            SaleAmount = 500m,
            ProceedsBankAccountId = h.BankAccountId,
        };

        Assert.Equal(FixedAssetOutcome.Ok, (await service.DisposeAsync(h.AssetId, request, ct)).Outcome);
        Assert.Equal(FixedAssetOutcome.NotActive, (await service.DisposeAsync(h.AssetId, request, ct)).Outcome);

        AssetTransaction disposal = Assert.Single(await h.Db.AssetTransactions.ToListAsync(ct));
        Assert.Equal(AssetTransactionType.Disposal, disposal.TransactionType);
    }

    [SkippableFact]
    public async Task A_disposal_dated_before_the_purchase_is_refused()
    {
        await using Harness h = await Harness.CreateAsync(_postgres);

        FixedAssetResult result = await h.Assets.DisposeAsync(
            h.AssetId, new DisposeAssetRequest { DisposalDate = new DateOnly(2026, 7, 31) }, CancellationToken.None);

        Assert.Equal(FixedAssetOutcome.DisposalBeforePurchase, result.Outcome);
    }

    [SkippableFact]
    public async Task Registering_writes_the_asset_and_its_schedules_and_refuses_a_duplicate_code()
    {
        await using Harness h = await Harness.CreateAsync(_postgres);
        CancellationToken ct = CancellationToken.None;
        var service = h.Assets;

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

    // ── TK-12: the postings ────────────────────────────────────────────────

    private static CapitaliseBillRequest Bill(Harness h, long billId = 501, decimal amount = 1200m, long? categoryId = null) => new()
    {
        CustomerId = h.CustomerId,
        PurchaseBillId = billId,
        DocumentNo = $"BIL-{billId}",
        DocumentDate = new DateOnly(2026, 8, 1),
        Lines =
        [
            new CapitaliseBillLine
            {
                BillDetailId = billId * 10 + 1,
                LineNumber = 1,
                FixedAssetCategoryId = categoryId ?? h.CategoryId,
                Description = "Server rack",
                Amount = amount,
            },
        ],
    };

    /// <summary>What the bill itself posted for its capital line: Dr Fixed Asset, Cr the vendor (a stand-in account here).</summary>
    private static async Task PostBillLegAsync(Harness h, long billId, decimal amount)
    {
        PostLedgerResult posted = await h.Postings.PostAsync(new PostLedgerRequest
        {
            CustomerId = h.CustomerId,
            TransactionTypeCode = "BIL",
            TransactionId = billId,
            LedgerDate = new DateOnly(2026, 8, 1),
            Legs =
            [
                new LedgerLegRequest { LedgerTypeId = 1, LedgerSourceId = 1, TransactionDetailId = billId * 10 + 1, AccountId = h.FixedAssetHoldingId, DebitAmount = amount },
                new LedgerLegRequest { LedgerTypeId = 3, LedgerSourceId = 1, TransactionDetailId = 0, AccountId = h.BankLedgerId, CreditAmount = amount },
            ],
        }, CancellationToken.None);

        Assert.Equal(PostLedgerOutcome.Ok, posted.Outcome);
    }

    [SkippableFact]
    public async Task A_bill_asset_is_registered_depreciated_and_disposed_with_every_step_balanced()
    {
        await using Harness h = await Harness.CreateAsync(_postgres);
        CancellationToken ct = CancellationToken.None;

        // The bill: Dr Fixed Asset 1,200.
        await PostBillLegAsync(h, 501, 1200m);

        // Registered from the bill: the cost moves to the category's account (D-19).
        (FixedAssetResult capitalised, CapitaliseBillResponse? response) = await h.Assets.CapitaliseBillAsync(Bill(h), ct);
        Assert.Equal(FixedAssetOutcome.Ok, capitalised.Outcome);
        long assetId = Assert.Single(response!.FixedAssetIds);

        Assert.Equal(0m, await h.NetAsync(h.FixedAssetHoldingId));
        Assert.Equal(1200m, await h.NetAsync(h.AssetAccountId));

        FixedAsset asset = await h.Db.FixedAssets.SingleAsync(a => a.FixedAssetId == assetId, ct);
        Assert.Equal("BIL-501-1", asset.AssetCode);
        Assert.Equal(5011, asset.PurchaseBillDetailId);
        Assert.Equal(FixedAssetStatus.Active, asset.Status);

        // It arrives with no life, so it gets one; and the harness's own asset is
        // retired so only this one is charged.
        Assert.Equal(FixedAssetOutcome.Ok, (await h.Assets.SetSchedulesAsync(assetId, new SetDepreciationSchedulesRequest
        {
            Schedules = [new() { ScheduleType = DepreciationScheduleType.Books, DepreciationMethod = DepreciationMethod.StraightLine, UsefulLifeYears = 3, DepreciationStartDate = new DateOnly(2026, 8, 1) }],
        }, ct)).Outcome);

        FixedAsset harnessAsset = await h.Db.FixedAssets.SingleAsync(a => a.FixedAssetId == h.AssetId, ct);
        harnessAsset.Status = FixedAssetStatus.Draft;
        await h.Db.SaveChangesAsync(ct);

        // One month: 33.33.
        DepreciationRunResult run = await h.Depreciation.RunDepreciationAsync(new DateOnly(2026, 8, 31), ct);
        Assert.Equal(1, run.AssetsCharged);

        // Sold for 1,000 into the bank: Dr bank 1,000, Dr accumulated 33.33,
        // Cr asset 1,200, and the 166.67 short of book value is a loss.
        FixedAssetResult disposed = await h.Assets.DisposeAsync(assetId, new DisposeAssetRequest
        {
            DisposalDate = new DateOnly(2026, 9, 1),
            SaleAmount = 1000m,
            ProceedsBankAccountId = h.BankAccountId,
        }, ct);

        Assert.Equal(FixedAssetOutcome.Ok, disposed.Outcome);

        List<JournalLedger> disposal = await h.Db.JournalLedger
            .Where(l => l.JournalId == disposed.JournalId)
            .ToListAsync(ct);

        Assert.Equal(1000m, disposal.Where(l => l.AccountId == h.BankLedgerId).Sum(l => l.DebitAmount));
        Assert.Equal(33.33m, disposal.Where(l => l.AccountId == h.AccumulatedDepreciationId).Sum(l => l.DebitAmount));
        Assert.Equal(1200m, disposal.Where(l => l.AccountId == h.AssetAccountId).Sum(l => l.CreditAmount));
        Assert.Equal(166.67m, disposal.Where(l => l.AccountId == h.GainLossId).Sum(l => l.DebitAmount));

        // Afterwards the asset and its accumulated depreciation are both gone
        // from the books, and every entry along the way balanced.
        Assert.Equal(0m, await h.NetAsync(h.AssetAccountId));
        Assert.Equal(0m, await h.NetAsync(h.AccumulatedDepreciationId));
        Assert.Equal(0m, await h.NetAsync(h.FixedAssetHoldingId));
        await h.AssertEveryJournalBalancedAsync();

        h.Db.ChangeTracker.Clear();
        Assert.Equal(FixedAssetStatus.Disposed, (await h.Db.FixedAssets.SingleAsync(a => a.FixedAssetId == assetId, ct)).Status);
    }

    [SkippableFact]
    public async Task Capitalising_a_bill_twice_registers_and_reclassifies_once()
    {
        await using Harness h = await Harness.CreateAsync(_postgres);
        CancellationToken ct = CancellationToken.None;

        await PostBillLegAsync(h, 502, 800m);

        (_, CapitaliseBillResponse? first) = await h.Assets.CapitaliseBillAsync(Bill(h, 502, 800m), ct);
        (FixedAssetResult again, CapitaliseBillResponse? second) = await h.Assets.CapitaliseBillAsync(Bill(h, 502, 800m), ct);

        Assert.Equal(FixedAssetOutcome.Ok, again.Outcome);
        Assert.Equal(first!.FixedAssetIds, second!.FixedAssetIds);
        Assert.Null(second.JournalId);

        Assert.Equal(1, await h.Db.FixedAssets.CountAsync(a => a.PurchaseBillId == 502, ct));
        Assert.Equal(800m, await h.NetAsync(h.AssetAccountId));
        Assert.Equal(0m, await h.NetAsync(h.FixedAssetHoldingId));
    }

    [SkippableFact]
    public async Task Capitalising_by_hand_is_refused_for_a_bill_that_registered_itself()
    {
        await using Harness h = await Harness.CreateAsync(_postgres);
        CancellationToken ct = CancellationToken.None;

        await PostBillLegAsync(h, 503, 500m);
        await h.Assets.CapitaliseBillAsync(Bill(h, 503, 500m), ct);

        FixedAssetResult manual = await h.Assets.CapitalizeAsync(new CapitalizeAssetRequest
        {
            FixedAssetCategoryId = h.CategoryId,
            AssetCode = "MANUAL-1",
            AssetName = "Server rack",
            PurchaseBillId = 503,
            PurchasePrice = 500m,
            PurchaseDate = new DateOnly(2026, 8, 1),
        }, ct);

        Assert.Equal(FixedAssetOutcome.AlreadyCapitalised, manual.Outcome);
        Assert.Equal(500m, await h.NetAsync(h.AssetAccountId));
    }

    [SkippableFact]
    public async Task A_bill_line_naming_another_branchs_category_is_refused_and_writes_nothing()
    {
        await using Harness h = await Harness.CreateAsync(_postgres);

        (FixedAssetResult result, _) = await h.Assets.CapitaliseBillAsync(
            Bill(h, 504, 100m, categoryId: long.MaxValue), CancellationToken.None);

        Assert.Equal(FixedAssetOutcome.CategoryMissing, result.Outcome);
        Assert.Equal(0, await h.Db.FixedAssets.CountAsync(a => a.PurchaseBillId == 504));
    }

    [SkippableFact]
    public async Task A_migrated_asset_is_debited_against_opening_balance_equity()
    {
        await using Harness h = await Harness.CreateAsync(_postgres);
        CancellationToken ct = CancellationToken.None;

        FixedAssetResult result = await h.Assets.RegisterAsync(new CreateFixedAssetRequest
        {
            FixedAssetCategoryId = h.CategoryId,
            AssetCode = "OLD-001",
            AssetName = "Delivery van",
            PurchaseDate = new DateOnly(2026, 4, 1),
            PurchasePrice = 450000m,
            Status = FixedAssetStatus.Active,
        }, ct);

        Assert.Equal(FixedAssetOutcome.Ok, result.Outcome);
        Assert.NotNull(result.JournalId);

        // No bill behind it, so nothing sat in Fixed Asset to reclassify (D-19).
        Assert.Equal(450000m, await h.NetAsync(h.AssetAccountId));
        Assert.Equal(-450000m, await h.NetAsync(h.OpeningEquityId));
        Assert.Equal(0m, await h.NetAsync(h.FixedAssetHoldingId));

        AssetTransaction acquisition = await h.Db.AssetTransactions.SingleAsync(
            t => t.FixedAssetId == result.FixedAssetId && t.TransactionType == AssetTransactionType.Acquisition, ct);
        Assert.Equal(result.JournalId, acquisition.JournalId);
    }

    [SkippableFact]
    public async Task A_disposal_invoiced_to_the_buyer_takes_the_proceeds_back_off_the_sales_account()
    {
        await using Harness h = await Harness.CreateAsync(_postgres);
        CancellationToken ct = CancellationToken.None;

        // The sales invoice to the buyer: Dr their receivable 944, Cr the line
        // 800 and GST 144. Only the 800 is what the asset fetched.
        const long invoiceId = 9001;
        long gst = h.GainLossId; // any account stands in for Output GST here
        Assert.Equal(PostLedgerOutcome.Ok, (await h.Postings.PostAsync(new PostLedgerRequest
        {
            CustomerId = h.CustomerId,
            TransactionTypeCode = "INV",
            TransactionId = invoiceId,
            LedgerDate = new DateOnly(2026, 9, 1),
            Legs =
            [
                new LedgerLegRequest { LedgerTypeId = 3, LedgerSourceId = 1, TransactionDetailId = 0, AccountId = h.ReceivableId, DebitAmount = 944m },
                new LedgerLegRequest { LedgerTypeId = 1, LedgerSourceId = 1, TransactionDetailId = 1, AccountId = h.SalesId, CreditAmount = 800m },
                new LedgerLegRequest { LedgerTypeId = 2, LedgerSourceId = 1, TransactionDetailId = 1, AccountId = gst, CreditAmount = 144m },
            ],
        }, ct)).Outcome);

        // The harness's asset was never reclassified, so its 1,200 still sits in
        // Fixed Asset, where every pre-register capital purchase collected.
        FixedAssetResult disposed = await h.Assets.DisposeAsync(h.AssetId, new DisposeAssetRequest
        {
            DisposalDate = new DateOnly(2026, 9, 1),
            SalesInvoiceId = invoiceId,
            SaleAmount = 12345m, // ignored: the invoice says what it fetched
        }, ct);

        Assert.Equal(FixedAssetOutcome.Ok, disposed.Outcome);

        List<JournalLedger> legs = await h.Db.JournalLedger.Where(l => l.JournalId == disposed.JournalId).ToListAsync(ct);
        Assert.Equal(800m, legs.Where(l => l.AccountId == h.SalesId).Sum(l => l.DebitAmount));
        Assert.Equal(1200m, legs.Where(l => l.AccountId == h.FixedAssetHoldingId).Sum(l => l.CreditAmount));
        Assert.Equal(400m, legs.Where(l => l.AccountId == h.GainLossId).Sum(l => l.DebitAmount));

        // Not trading income: the sales account nets back to nothing.
        Assert.Equal(0m, await h.NetAsync(h.SalesId));

        AssetTransaction record = await h.Db.AssetTransactions.SingleAsync(
            t => t.FixedAssetId == h.AssetId && t.TransactionType == AssetTransactionType.Disposal, ct);
        Assert.Equal(800m, record.Amount);
        await h.AssertEveryJournalBalancedAsync();
    }

    [SkippableFact]
    public async Task A_disposal_that_fetched_money_must_say_where_it_went()
    {
        await using Harness h = await Harness.CreateAsync(_postgres);
        CancellationToken ct = CancellationToken.None;

        var nowhere = new DisposeAssetRequest { DisposalDate = new DateOnly(2026, 9, 1), SaleAmount = 100m };
        var both = new DisposeAssetRequest
        {
            DisposalDate = new DateOnly(2026, 9, 1),
            SaleAmount = 100m,
            ProceedsBankAccountId = h.BankAccountId,
            SalesInvoiceId = 1,
        };
        var unposted = new DisposeAssetRequest { DisposalDate = new DateOnly(2026, 9, 1), SalesInvoiceId = 424242 };

        Assert.Equal(FixedAssetOutcome.ProceedsDestinationRequired, (await h.Assets.DisposeAsync(h.AssetId, nowhere, ct)).Outcome);
        Assert.Equal(FixedAssetOutcome.ProceedsDestinationRequired, (await h.Assets.DisposeAsync(h.AssetId, both, ct)).Outcome);
        Assert.Equal(FixedAssetOutcome.InvoiceNotPosted, (await h.Assets.DisposeAsync(h.AssetId, unposted, ct)).Outcome);

        // Scrapped for nothing needs no destination: the whole book value is a loss.
        FixedAssetResult scrapped = await h.Assets.DisposeAsync(
            h.AssetId, new DisposeAssetRequest { DisposalDate = new DateOnly(2026, 9, 1) }, ct);

        Assert.Equal(FixedAssetOutcome.Ok, scrapped.Outcome);
        Assert.Equal(1200m, await h.NetAsync(h.GainLossId));
    }

    [SkippableFact]
    public async Task Schedules_cannot_be_changed_once_depreciation_has_been_charged()
    {
        await using Harness h = await Harness.CreateAsync(_postgres);
        CancellationToken ct = CancellationToken.None;

        await h.Depreciation.RunDepreciationAsync(new DateOnly(2026, 8, 31), ct);

        FixedAssetResult result = await h.Assets.SetSchedulesAsync(h.AssetId, new SetDepreciationSchedulesRequest
        {
            Schedules = [new() { ScheduleType = DepreciationScheduleType.Books, DepreciationMethod = DepreciationMethod.StraightLine, UsefulLifeYears = 10 }],
        }, ct);

        Assert.Equal(FixedAssetOutcome.SchedulesInUse, result.Outcome);
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
        public required FixedAssetService Assets { get; init; }
        public required JournalService Journals { get; init; }
        public required LedgerPostingService Postings { get; init; }
        public required long FixedAssetHoldingId { get; init; }
        public required long OpeningEquityId { get; init; }
        public required long GainLossId { get; init; }
        public required long BankLedgerId { get; init; }
        public required long BankAccountId { get; init; }
        public required long SalesId { get; init; }
        public required long ReceivableId { get; init; }

        /// <summary>What an account nets to across the whole ledger, debits positive.</summary>
        public async Task<decimal> NetAsync(long accountId) =>
            await Db.JournalLedger.Where(l => l.AccountId == accountId).SumAsync(l => l.DebitAmountBase)
            - await Db.JournalLedger.Where(l => l.AccountId == accountId).SumAsync(l => l.CreditAmountBase);

        /// <summary>Every posted journal's ledger rows balance on their own.</summary>
        public async Task AssertEveryJournalBalancedAsync()
        {
            var sums = await Db.JournalLedger
                .GroupBy(l => new { l.TransactionTypeCode, l.TransactionId })
                .Select(g => new { g.Key, Dr = g.Sum(l => l.DebitAmountBase), Cr = g.Sum(l => l.CreditAmountBase) })
                .ToListAsync();

            Assert.All(sums, x => Assert.Equal(x.Dr, x.Cr));
        }

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

            async Task<long> Account(string code, string name, int typeId, SystemAccount? system = null)
            {
                var account = new Account
                {
                    OrgId = orgId,
                    AccountTypeId = typeId,
                    AccountCode = code,
                    AccountName = name,
                    AccountSystemName = system is SystemAccount named ? SystemAccountNames.Of(named) : null,
                    IsSystemDefault = system is not null,
                    IsActive = true
                };

                db.Accounts.Add(account);
                await db.SaveChangesAsync();
                return account.AccountId;
            }

            long assetAcc = await Account("1500", "Office Equipment", 1);
            long accDep = await Account("1550", "Accumulated Depreciation", 1);
            long depExp = await Account("6500", "Depreciation Expense", 5);

            // The seeded control accounts the register posts to — system
            // defaults, closed to hand entries, which is why it posts through
            // JournalService.PostSystemAsync.
            long holding = await Account("1600", "Fixed Asset", 1, SystemAccount.FixedAsset);
            long openingEquity = await Account("3100", "Opening Balance Equity", 3, SystemAccount.OpeningBalanceEquity);
            long gainLoss = await Account("4920", "Gain/Loss on Asset Disposal", 4, SystemAccount.AssetDisposalGainLoss);
            long bankLedger = await Account("1510", "Current Account", 1);
            long sales = await Account("4100", "Sales", 4);
            long receivable = await Account("1200", "Receivable", 1);

            var bank = new BankAccount
            {
                OrgId = orgId,
                LedgerAccountId = bankLedger,
                AccountName = "Current Account",
                AccountNumber = "000111222",
            };
            db.BankAccounts.Add(bank);
            await db.SaveChangesAsync();

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
                Assets = new FixedAssetService(db, journals),
                Journals = journals,
                Postings = postings,
                FixedAssetHoldingId = holding,
                OpeningEquityId = openingEquity,
                GainLossId = gainLoss,
                BankLedgerId = bankLedger,
                BankAccountId = bank.BankAccountId,
                SalesId = sales,
                ReceivableId = receivable,
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
