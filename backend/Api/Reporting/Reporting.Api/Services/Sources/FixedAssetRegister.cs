using Reporting.Entity.Enums;
using Reporting.Repository;
using Reporting.Repository.ReadModels;

namespace Reporting.Api.Services.Sources;

/// <summary>
/// The fixed-asset register rolled forward over a period: one row per asset,
/// saying what it cost, what it was depreciated by and what became of it.
///
/// <b>One query behind four reports, deliberately.</b> The Depreciation Schedule,
/// the Disposal Schedule, the Fixed Assets Schedule and the register side of the
/// Fixed Asset Reconciliation are four views of the same arithmetic. Written four
/// times, they would be four chances to disagree about whether an asset bought on
/// the first day of the period is an opening balance or an addition — and each
/// report would look right on its own.
///
/// <b>The rules, all of them here and nowhere else:</b>
/// <list type="bullet">
/// <item>A <c>Draft</c> asset is not on the register yet, and appears nowhere.</item>
/// <item>An asset appears if it was bought on or before the end of the period and
/// not disposed of before its start.</item>
/// <item>Bought before the start is opening cost; bought within the period is an
/// addition. With no start, everything is an addition and nothing is opening.</item>
/// <item>Depreciation is the sum of the asset's <c>Depreciation</c> transactions
/// against its <c>Books</c> schedule — the only schedule
/// <c>DepreciationService</c> charges. Before the start is opening; within the
/// period is the charge.</item>
/// <item>Disposed of within the period: the cost and every charge to date leave
/// the register, so closing cost, closing accumulated depreciation and closing
/// book value are all zero, and the disposal columns carry what left. Disposed of
/// after the period: still held at its end, and the disposal is not shown.</item>
/// <item>On disposal, proceeds up to cost less book value is a gain on disposal;
/// proceeds above cost is a capital gain; book value above proceeds is a loss.
/// Only one of gain and loss is ever non-zero.</item>
/// </list>
///
/// Takes the five sets rather than the context so that the arithmetic can be
/// tested over lists; <see cref="Rows(ReportingDbContext, DateOnly?, DateOnly?)"/>
/// is what a report calls.
/// </summary>
public static class FixedAssetRegister
{
    public static IQueryable<FixedAssetPeriodRow> Rows(
        ReportingDbContext db, DateOnly? start, DateOnly? end) =>
        Rows(
            db.FixedAssets,
            db.FixedAssetCategories,
            db.DepreciationSchedules,
            db.AssetTransactions,
            db.Accounts,
            start,
            end);

    public static IQueryable<FixedAssetPeriodRow> Rows(
        IQueryable<FixedAssetRead> assets,
        IQueryable<FixedAssetCategoryRead> categories,
        IQueryable<DepreciationScheduleRead> schedules,
        IQueryable<AssetTransactionRead> transactions,
        IQueryable<AccountRead> accounts,
        DateOnly? start,
        DateOnly? end)
    {
        IQueryable<DepreciationScheduleRead> books =
            schedules.Where(s => s.ScheduleType == DepreciationScheduleType.Books);

        // A charge with no schedule is counted: it can only have come from the one
        // path that charges, which charges Books. A charge against a Tax schedule
        // is not, so that the day tax depreciation is recorded it does not double
        // every figure here.
        IQueryable<AssetTransactionRead> charges = transactions.Where(t =>
            t.TransactionType == AssetTransactionType.Depreciation
            && (t.DepreciationScheduleId == null
                || books.Any(s => s.DepreciationScheduleId == t.DepreciationScheduleId)));

        IQueryable<AssetTransactionRead> disposals =
            transactions.Where(t => t.TransactionType == AssetTransactionType.Disposal);

        var held =
            from a in assets
            where a.Status != FixedAssetStatus.Draft
            join c in categories on a.FixedAssetCategoryId equals c.FixedAssetCategoryId
            join assetAccount in accounts on c.AssetAccountId equals assetAccount.AccountId
            join accumAccount in accounts
                on c.AccumulatedDepreciationAccountId equals accumAccount.AccountId
            join expenseAccount in accounts
                on c.DepreciationExpenseAccountId equals expenseAccount.AccountId
            join s in books on a.FixedAssetId equals s.FixedAssetId into schedule
            from s in schedule.DefaultIfEmpty()
            let disposedOn = disposals
                .Where(t => t.FixedAssetId == a.FixedAssetId)
                .Min(t => (DateOnly?)t.TransactionDate)
            where (end == null || a.PurchaseDate <= end)
                && (start == null || disposedOn == null || disposedOn >= start)
            select new
            {
                a.FixedAssetId,
                a.FixedAssetCategoryId,
                a.AssetCode,
                a.AssetName,
                a.Description,
                a.SerialNumber,
                a.PurchaseDate,
                Cost = a.PurchasePrice,
                c.CategoryName,
                c.AssetAccountId,
                c.AccumulatedDepreciationAccountId,
                AssetAccount = assetAccount.AccountName,
                AccumDepAccount = accumAccount.AccountName,
                DepExpenseAccount = expenseAccount.AccountName,
                DepMethod = s == null
                    ? null
                    : s.DepreciationMethod == DepreciationMethod.StraightLine
                        ? "Straight Line"
                        : "Written Down Value",
                DepStartDate = s == null ? (DateOnly?)null : s.DepreciationStartDate,
                Rate = s == null ? (decimal?)null : s.Rate,
                EffectiveLife = s == null ? (int?)null : s.UsefulLifeYears,
                ResidualValue = s == null ? (decimal?)null : s.SalvageValue,
                DisposedOn = disposedOn,
                Proceeds = disposals
                    .Where(t => t.FixedAssetId == a.FixedAssetId)
                    .Sum(t => t.Amount),
                ChargedBefore = charges
                    .Where(t => t.FixedAssetId == a.FixedAssetId
                        && start != null
                        && t.TransactionDate < start)
                    .Sum(t => t.Amount),
                ChargedDuring = charges
                    .Where(t => t.FixedAssetId == a.FixedAssetId
                        && (start == null || t.TransactionDate >= start)
                        && (end == null || t.TransactionDate <= end))
                    .Sum(t => t.Amount),
            };

        var moved = held.Select(x => new
        {
            Held = x,
            Disposed = x.DisposedOn != null && (end == null || x.DisposedOn <= end),
            OpeningCost = start != null && x.PurchaseDate < start ? x.Cost : 0m,
            AdditionCost = start == null || x.PurchaseDate >= start ? x.Cost : 0m,
            BookValue = x.Cost - x.ChargedBefore - x.ChargedDuring,
            ProceedsUpToCost = x.Proceeds < x.Cost ? x.Proceeds : x.Cost,
        });

        return moved.Select(m => new FixedAssetPeriodRow
        {
            FixedAssetId = m.Held.FixedAssetId,
            FixedAssetCategoryId = m.Held.FixedAssetCategoryId,
            AssetAccountId = m.Held.AssetAccountId,
            AccumDepAccountId = m.Held.AccumulatedDepreciationAccountId,
            AssetNumber = m.Held.AssetCode,
            AssetName = m.Held.AssetName,
            Description = m.Held.Description,
            SerialNumber = m.Held.SerialNumber,
            AssetType = m.Held.CategoryName,
            AssetAccount = m.Held.AssetAccount,
            AccumDepAccount = m.Held.AccumDepAccount,
            DepExpenseAccount = m.Held.DepExpenseAccount,
            PurchaseDate = m.Held.PurchaseDate,
            PurchasePrice = m.Held.Cost,
            Cost = m.Held.Cost,
            DepMethod = m.Held.DepMethod,
            DepStartDate = m.Held.DepStartDate,
            Rate = m.Held.Rate,
            EffectiveLife = m.Held.EffectiveLife,
            ResidualValue = m.Held.ResidualValue,

            OpeningCost = m.OpeningCost,
            AdditionCost = m.AdditionCost,
            DisposalCost = m.Disposed ? m.Held.Cost : 0m,
            ClosingCost = m.Disposed ? 0m : m.OpeningCost + m.AdditionCost,

            OpeningAccumDep = m.Held.ChargedBefore,
            Depreciation = m.Held.ChargedDuring,
            DisposalAccumDep = m.Disposed ? m.Held.ChargedBefore + m.Held.ChargedDuring : 0m,
            ClosingAccumDep = m.Disposed ? 0m : m.Held.ChargedBefore + m.Held.ChargedDuring,

            OpeningNbv = m.OpeningCost - m.Held.ChargedBefore,
            ClosingNbv = m.Disposed
                ? 0m
                : m.OpeningCost + m.AdditionCost - m.Held.ChargedBefore - m.Held.ChargedDuring,

            DisposalDate = m.Disposed ? m.Held.DisposedOn : (DateOnly?)null,
            SaleProceeds = m.Disposed ? m.Held.Proceeds : (decimal?)null,
            NbvAtDisposal = m.Disposed ? m.BookValue : (decimal?)null,
            GainOnDisposal = m.Disposed
                ? (m.ProceedsUpToCost > m.BookValue ? m.ProceedsUpToCost - m.BookValue : 0m)
                : (decimal?)null,
            LossOnDisposal = m.Disposed
                ? (m.BookValue > m.Held.Proceeds ? m.BookValue - m.Held.Proceeds : 0m)
                : (decimal?)null,
            CapitalGain = m.Disposed
                ? (m.Held.Proceeds > m.Held.Cost ? m.Held.Proceeds - m.Held.Cost : 0m)
                : (decimal?)null,
        });
    }
}

/// <summary>
/// One asset over one period. Every fixed-asset report but the reconciliation
/// reads this row directly and differs only in which columns it declares.
/// </summary>
public sealed class FixedAssetPeriodRow
{
    public long FixedAssetId { get; set; }

    public long FixedAssetCategoryId { get; set; }

    public long AssetAccountId { get; set; }

    public long AccumDepAccountId { get; set; }

    public string AssetNumber { get; set; } = null!;

    public string AssetName { get; set; } = null!;

    public string? Description { get; set; }

    public string? SerialNumber { get; set; }

    /// <summary>The category's name — what the reports call the asset type.</summary>
    public string AssetType { get; set; } = null!;

    public string AssetAccount { get; set; } = null!;

    public string AccumDepAccount { get; set; } = null!;

    public string DepExpenseAccount { get; set; } = null!;

    public DateOnly PurchaseDate { get; set; }

    public decimal PurchasePrice { get; set; }

    /// <summary>Equal to <see cref="PurchasePrice"/>: the register records no other cost.</summary>
    public decimal Cost { get; set; }

    /// <summary>The <c>Books</c> schedule's. Null for an asset with no schedule.</summary>
    public string? DepMethod { get; set; }

    public DateOnly? DepStartDate { get; set; }

    public decimal? Rate { get; set; }

    public int? EffectiveLife { get; set; }

    public decimal? ResidualValue { get; set; }

    public decimal OpeningCost { get; set; }

    public decimal AdditionCost { get; set; }

    public decimal DisposalCost { get; set; }

    public decimal ClosingCost { get; set; }

    public decimal OpeningAccumDep { get; set; }

    public decimal Depreciation { get; set; }

    public decimal DisposalAccumDep { get; set; }

    public decimal ClosingAccumDep { get; set; }

    public decimal OpeningNbv { get; set; }

    public decimal ClosingNbv { get; set; }

    /// <summary>Set only when the asset was disposed of within the period.</summary>
    public DateOnly? DisposalDate { get; set; }

    public decimal? SaleProceeds { get; set; }

    public decimal? NbvAtDisposal { get; set; }

    public decimal? GainOnDisposal { get; set; }

    public decimal? LossOnDisposal { get; set; }

    public decimal? CapitalGain { get; set; }
}
