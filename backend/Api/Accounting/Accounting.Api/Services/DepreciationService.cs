using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Accounting.Entity.Enums;
using Accounting.Entity.Models;
using Accounting.Entity.TableEntities;
using Accounting.Repository;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Persistence;

namespace Accounting.Api.Services;

public class DepreciationService
{
    private readonly AccountingDbContext _db;
    private readonly JournalService _journals;

    public DepreciationService(AccountingDbContext db, JournalService journals)
    {
        _db = db;
        _journals = journals;
    }

    /// <summary>
    /// Charges one month's depreciation, for every active asset that has not
    /// been charged for the month <paramref name="runDate"/> falls in, in one
    /// posted journal.
    ///
    /// <b>Running it twice charges once.</b> The guard is the asset's own
    /// <c>Depreciation</c> transactions rather than a flag on the run, because
    /// an asset capitalized mid-month joins a period some of its neighbours
    /// have already been charged for — so "has this run happened" is the wrong
    /// question and "has this asset been charged for this month" is the right
    /// one. Without it a second run posted a second month's expense against
    /// the same period, which nothing downstream would have contradicted:
    /// the journal balances either way, and the asset simply depreciates twice
    /// as fast as its schedule says. A run that finds nothing due returns
    /// <see cref="DepreciationRunOutcome.Ok"/> with no journal.
    ///
    /// <b>The journal and the asset transactions commit together</b>, inside
    /// one scope: a journal refused after the numbering took its number rolls
    /// everything back, rather than leaving a posted journal no asset points
    /// at, or a charge the ledger never saw.
    /// </summary>
    public async Task<DepreciationRunResult> RunDepreciationAsync(DateOnly runDate, CancellationToken ct)
    {
        var activeAssets = await _db.FixedAssets
            .Where(a => a.Status == FixedAssetStatus.Active)
            .ToListAsync(ct);

        if (activeAssets.Count == 0) return new DepreciationRunResult(DepreciationRunOutcome.Ok);

        var assetIds = activeAssets.Select(a => a.FixedAssetId).ToList();
        var schedules = await _db.DepreciationSchedules
            .Where(s => assetIds.Contains(s.FixedAssetId) && s.ScheduleType == DepreciationScheduleType.Books)
            .ToListAsync(ct);

        var categories = await _db.FixedAssetCategories.ToListAsync(ct);

        // The month the run falls in, and who has already been charged for it.
        var periodStart = new DateOnly(runDate.Year, runDate.Month, 1);
        var periodEnd = periodStart.AddMonths(1).AddDays(-1);

        var alreadyCharged = (await _db.AssetTransactions
            .Where(t => assetIds.Contains(t.FixedAssetId)
                && t.TransactionType == AssetTransactionType.Depreciation
                && t.TransactionDate >= periodStart
                && t.TransactionDate <= periodEnd)
            .Select(t => t.FixedAssetId)
            .Distinct()
            .ToListAsync(ct))
            .ToHashSet();

        // What each asset has been charged on its Books schedule so far, which
        // is what written-down value charges on and what the salvage floor
        // stops at.
        var scheduleIds = schedules.Select(s => s.DepreciationScheduleId).ToList();
        var chargedToDate = await _db.AssetTransactions
            .Where(t => t.TransactionType == AssetTransactionType.Depreciation
                && t.DepreciationScheduleId != null
                && scheduleIds.Contains(t.DepreciationScheduleId.Value))
            .GroupBy(t => t.DepreciationScheduleId!.Value)
            .Select(g => new { ScheduleId = g.Key, Total = g.Sum(t => t.Amount) })
            .ToDictionaryAsync(x => x.ScheduleId, x => x.Total, ct);

        var journalLines = new List<SaveJournalLineRequest>();
        var transactions = new List<AssetTransaction>();

        foreach (var asset in activeAssets)
        {
            if (alreadyCharged.Contains(asset.FixedAssetId)) continue;

            var schedule = schedules.FirstOrDefault(s => s.FixedAssetId == asset.FixedAssetId);
            if (schedule == null) continue;

            // Not in service yet this month.
            if (schedule.DepreciationStartDate > periodEnd) continue;

            var category = categories.First(c => c.FixedAssetCategoryId == asset.FixedAssetCategoryId);

            decimal depreciationAmount = MonthlyCharge(
                asset.PurchasePrice,
                schedule,
                chargedToDate.GetValueOrDefault(schedule.DepreciationScheduleId));

            if (depreciationAmount <= 0) continue;

            journalLines.Add(new SaveJournalLineRequest
            {
                AccountId = category.DepreciationExpenseAccountId,
                DebitAmount = depreciationAmount,
                CreditAmount = 0,
                LineMemo = $"Depreciation for {asset.AssetName}"
            });

            journalLines.Add(new SaveJournalLineRequest
            {
                AccountId = category.AccumulatedDepreciationAccountId,
                DebitAmount = 0,
                CreditAmount = depreciationAmount,
                LineMemo = $"Depreciation for {asset.AssetName}"
            });

            transactions.Add(new AssetTransaction
            {
                FixedAssetId = asset.FixedAssetId,
                TransactionType = AssetTransactionType.Depreciation,
                DepreciationScheduleId = schedule.DepreciationScheduleId,
                TransactionDate = runDate,
                Amount = depreciationAmount,
                Notes = $"Automated depreciation run up to {runDate:yyyy-MM-dd}"
            });
        }

        if (journalLines.Count == 0) return new DepreciationRunResult(DepreciationRunOutcome.Ok);

        var journalRequest = new SaveJournalRequest
        {
            JournalDate = runDate,
            Reference = $"DEP-RUN-{runDate:yyyyMMdd}",
            Memo = $"Automated Depreciation Run for {runDate:MMM yyyy}",
            Lines = journalLines
        };

        await using ITransactionScope tx = await _db.Database.BeginScopeAsync(ct);

        var saveResult = await _journals.CreateAsync(journalRequest, ct);
        if (saveResult.Outcome != SaveJournalOutcome.Ok)
            return Refused(saveResult);

        var postResult = await _journals.PostAsync(saveResult.JournalId, ct);
        if (postResult.Outcome != SaveJournalOutcome.Ok)
            return Refused(postResult);

        foreach (var txn in transactions)
        {
            txn.JournalId = saveResult.JournalId;
        }

        _db.AssetTransactions.AddRange(transactions);
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return new DepreciationRunResult(
            DepreciationRunOutcome.Ok, transactions.Count, saveResult.JournalId);
    }

    /// <summary>
    /// One month's charge on a schedule, rounded to the paisa, never taking the
    /// asset below its salvage value.
    /// <list type="bullet">
    /// <item><b>Straight line</b>: (cost − salvage) ÷ useful life ÷ 12, or
    /// (cost − salvage) × rate ÷ 12 when no life is given.</item>
    /// <item><b>Written-down value</b>: the book value left (cost − charged so
    /// far) × rate ÷ 12. It charged nothing at all before TK-11 — the method was
    /// on the schedule and the calculation only knew straight line.</item>
    /// </list>
    /// Public and pure so each method is tested without a database.
    /// </summary>
    public static decimal MonthlyCharge(decimal cost, DepreciationSchedule schedule, decimal chargedToDate)
    {
        decimal depreciable = cost - schedule.SalvageValue;
        decimal remaining = depreciable - chargedToDate;

        if (remaining <= 0) return 0;

        decimal charge = schedule.DepreciationMethod switch
        {
            DepreciationMethod.StraightLine when schedule.UsefulLifeYears > 0
                => depreciable / schedule.UsefulLifeYears / 12m,
            DepreciationMethod.StraightLine
                => depreciable * (schedule.Rate / 100m) / 12m,
            DepreciationMethod.WrittenDownValue
                => (cost - chargedToDate) * (schedule.Rate / 100m) / 12m,
            _ => 0,
        };

        return Math.Min(Math.Round(charge, 2, MidpointRounding.AwayFromZero), remaining);
    }

    private static DepreciationRunResult Refused(SaveJournalResult result) =>
        new(DepreciationRunOutcome.JournalRefused, JournalOutcome: result.Outcome, Detail: result.Detail);
}
