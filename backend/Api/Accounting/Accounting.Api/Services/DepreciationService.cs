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

    public async Task RunDepreciationAsync(DateOnly runDate, CancellationToken ct)
    {
        var activeAssets = await _db.FixedAssets
            .Where(a => a.Status == FixedAssetStatus.Active)
            .ToListAsync(ct);

        if (activeAssets.Count == 0) return;

        var assetIds = activeAssets.Select(a => a.FixedAssetId).ToList();
        var schedules = await _db.DepreciationSchedules
            .Where(s => assetIds.Contains(s.FixedAssetId) && s.ScheduleType == DepreciationScheduleType.Books)
            .ToListAsync(ct);

        var categories = await _db.FixedAssetCategories.ToListAsync(ct);

        var journalLines = new List<SaveJournalLineRequest>();
        var transactions = new List<AssetTransaction>();

        foreach (var asset in activeAssets)
        {
            var schedule = schedules.FirstOrDefault(s => s.FixedAssetId == asset.FixedAssetId);
            if (schedule == null) continue;

            var category = categories.First(c => c.FixedAssetCategoryId == asset.FixedAssetCategoryId);

            decimal depreciationAmount = 0;
            if (schedule.DepreciationMethod == DepreciationMethod.StraightLine)
            {
                var annualDepreciation = (asset.PurchasePrice - schedule.SalvageValue) * (schedule.Rate / 100);
                if (schedule.UsefulLifeYears > 0)
                {
                    annualDepreciation = (asset.PurchasePrice - schedule.SalvageValue) / schedule.UsefulLifeYears;
                }
                depreciationAmount = Math.Round(annualDepreciation / 12m, 2);
            }

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

        if (journalLines.Count == 0) return;

        var journalRequest = new SaveJournalRequest
        {
            JournalDate = runDate,
            Reference = $"DEP-RUN-{runDate:yyyyMMdd}",
            Memo = $"Automated Depreciation Run for {runDate:MMM yyyy}",
            Lines = journalLines
        };

        var saveResult = await _journals.CreateAsync(journalRequest, ct);
        if (saveResult.Outcome != SaveJournalOutcome.Ok)
            throw new Exception($"Failed to create journal: {saveResult.Outcome}");

        var postResult = await _journals.PostAsync(saveResult.JournalId, ct);
        if (postResult.Outcome != SaveJournalOutcome.Ok)
            throw new Exception($"Failed to post journal: {postResult.Outcome}");

        foreach (var txn in transactions)
        {
            txn.JournalId = saveResult.JournalId;
        }

        _db.AssetTransactions.AddRange(transactions);
        await _db.SaveChangesAsync(ct);
    }
}
