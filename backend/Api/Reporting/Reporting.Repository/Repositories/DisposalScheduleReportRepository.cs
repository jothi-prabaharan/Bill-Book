using Microsoft.EntityFrameworkCore;
using Reporting.Entity.Enums;
using Reporting.Entity.Models;
using Reporting.Repository.ReadModels;

namespace Reporting.Repository.Repositories;

public sealed class DisposalScheduleReportRepository
{
    private readonly ReportingDbContext _db;

    public DisposalScheduleReportRepository(ReportingDbContext db)
    {
        _db = db;
    }

    public static IEnumerable<(string Field, string Message)> Refusals(DisposalScheduleReportRequest request)
    {
        if (request.From == null)
        {
            yield return (nameof(request.From), "From date is required.");
        }

        if (request.To == null)
        {
            yield return (nameof(request.To), "To date is required.");
        }

        if (request.From != null && request.To != null && request.To < request.From)
        {
            yield return (nameof(request.To), "To date cannot be earlier than From date.");
        }
    }

    public async Task<IEnumerable<(string Field, string Message)>> LookupRefusalsAsync(
        DisposalScheduleReportRequest request,
        CancellationToken ct = default)
    {
        var refusals = new List<(string, string)>();
        if (request.CategoryId.HasValue && request.CategoryId.Value > 0)
        {
            bool exists = await _db.FixedAssetCategories.AnyAsync(c => c.FixedAssetCategoryId == request.CategoryId.Value, ct);
            if (!exists)
            {
                refusals.Add((nameof(request.CategoryId), "Fixed asset category was not found on this branch."));
            }
        }
        return refusals;
    }

    public async Task<DisposalScheduleReportResponse> RunAsync(
        DisposalScheduleReportRequest request,
        CancellationToken ct = default)
    {
        DateOnly from = request.From ?? throw new ArgumentException("From date is required.");
        DateOnly to = request.To ?? throw new ArgumentException("To date is required.");

        var query = FixedAssetRegisterRepository.Rows(_db, from, to)
            .Where(r => r.DisposalDate != null);

        var rows = await query.ToListAsync(ct);

        var notesList = await _db.AssetTransactions
            .Where(t => t.TransactionType == AssetTransactionType.Disposal
                && t.TransactionDate >= from
                && t.TransactionDate <= to)
            .Select(t => new { t.FixedAssetId, t.Notes })
            .ToListAsync(ct);

        var notesMap = notesList
            .GroupBy(n => n.FixedAssetId)
            .ToDictionary(g => g.Key, g => g.First().Notes);

        return Compose(rows, notesMap, request, from, to);
    }

    public async Task<DisposalScheduleLookupsDto> GetLookupsAsync(CancellationToken ct = default)
    {
        var categories = await _db.FixedAssetCategories
            .OrderBy(c => c.CategoryName)
            .Select(c => new DisposalScheduleCategoryLookupDto
            {
                Id = c.FixedAssetCategoryId,
                Name = c.CategoryName,
                DisposedCount = _db.AssetTransactions.Count(t =>
                    t.TransactionType == AssetTransactionType.Disposal
                    && _db.FixedAssets.Any(a => a.FixedAssetId == t.FixedAssetId && a.FixedAssetCategoryId == c.FixedAssetCategoryId)),
            })
            .ToListAsync(ct);

        return new DisposalScheduleLookupsDto
        {
            Categories = categories,
        };
    }

    public static DisposalScheduleReportResponse Compose(
        IEnumerable<FixedAssetPeriodRow> rows,
        IReadOnlyDictionary<long, string?>? notesMap,
        DisposalScheduleReportRequest request,
        DateOnly from,
        DateOnly to)
    {
        IEnumerable<FixedAssetPeriodRow> filteredQuery = rows.Where(r => r.DisposalDate != null);

        if (request.CategoryId.HasValue && request.CategoryId.Value > 0)
        {
            filteredQuery = filteredQuery.Where(r => r.FixedAssetCategoryId == request.CategoryId.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            string term = request.Search.Trim();
            filteredQuery = filteredQuery.Where(r =>
                (r.AssetNumber != null && r.AssetNumber.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
                (r.AssetName != null && r.AssetName.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
                (r.SerialNumber != null && r.SerialNumber.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
                (r.Description != null && r.Description.Contains(term, StringComparison.OrdinalIgnoreCase)));
        }

        var filtered = filteredQuery.ToList();

        var categoryGroups = filtered
            .GroupBy(r => new { r.FixedAssetCategoryId, r.AssetType })
            .OrderBy(g => g.Key.AssetType)
            .Select(g =>
            {
                var items = g.Select(r =>
                {
                    decimal cost = r.Cost;
                    decimal accumDep = r.DisposalAccumDep;
                    decimal nbv = r.NbvAtDisposal ?? (cost - accumDep);
                    decimal proceeds = r.SaleProceeds ?? 0m;
                    decimal gain = r.GainOnDisposal ?? (proceeds > nbv && proceeds <= cost ? proceeds - nbv : (proceeds > cost ? cost - nbv : 0m));
                    decimal capitalGain = r.CapitalGain ?? (proceeds > cost ? proceeds - cost : 0m);
                    decimal loss = r.LossOnDisposal ?? (nbv > proceeds ? nbv - proceeds : 0m);

                    string? reference = null;
                    if (notesMap != null && notesMap.TryGetValue(r.FixedAssetId, out var note))
                    {
                        reference = note;
                    }

                    return new DisposalScheduleItemDto
                    {
                        FixedAssetId = r.FixedAssetId,
                        CategoryId = r.FixedAssetCategoryId,
                        CategoryName = r.AssetType,
                        AssetCode = r.AssetNumber,
                        AssetName = r.AssetName,
                        Description = r.Description,
                        SerialNumber = r.SerialNumber,
                        AssetAccount = r.AssetAccount,
                        AccumDepAccount = r.AccumDepAccount,
                        PurchaseDate = r.PurchaseDate,
                        Cost = cost,
                        DepMethod = r.DepMethod,
                        DisposalDate = r.DisposalDate ?? to,
                        AccumDep = accumDep,
                        NbvAtDisposal = nbv,
                        SaleProceeds = proceeds,
                        GainOnDisposal = gain,
                        CapitalGain = capitalGain,
                        LossOnDisposal = loss,
                        Reference = reference,
                    };
                }).OrderBy(i => i.DisposalDate).ThenBy(i => i.AssetCode).ToList();

                decimal totalCost = items.Sum(i => i.Cost);
                decimal totalAccumDep = items.Sum(i => i.AccumDep);
                decimal totalNbv = items.Sum(i => i.NbvAtDisposal);
                decimal totalProceeds = items.Sum(i => i.SaleProceeds);
                decimal totalGain = items.Sum(i => i.GainOnDisposal);
                decimal totalCapGain = items.Sum(i => i.CapitalGain);
                decimal totalLoss = items.Sum(i => i.LossOnDisposal);

                return new DisposalScheduleCategoryGroupDto
                {
                    CategoryId = g.Key.FixedAssetCategoryId,
                    CategoryName = g.Key.AssetType,
                    AssetCount = items.Count,
                    TotalCost = totalCost,
                    TotalAccumDep = totalAccumDep,
                    TotalNbvAtDisposal = totalNbv,
                    TotalProceeds = totalProceeds,
                    TotalGain = totalGain,
                    TotalCapitalGain = totalCapGain,
                    TotalLoss = totalLoss,
                    NetGainLoss = totalGain + totalCapGain - totalLoss,
                    Disposals = items,
                };
            }).ToList();

        var allItems = categoryGroups.SelectMany(g => g.Disposals).ToList();

        decimal grandCost = allItems.Sum(i => i.Cost);
        decimal grandAccumDep = allItems.Sum(i => i.AccumDep);
        decimal grandNbv = allItems.Sum(i => i.NbvAtDisposal);
        decimal grandProceeds = allItems.Sum(i => i.SaleProceeds);
        decimal grandGain = allItems.Sum(i => i.GainOnDisposal);
        decimal grandCapGain = allItems.Sum(i => i.CapitalGain);
        decimal grandLoss = allItems.Sum(i => i.LossOnDisposal);

        return new DisposalScheduleReportResponse
        {
            FromDate = from,
            ToDate = to,
            Currency = "INR",
            Categories = categoryGroups,
            Summary = new DisposalScheduleSummaryDto
            {
                TotalDisposals = allItems.Count,
                TotalCost = grandCost,
                TotalAccumDep = grandAccumDep,
                TotalNbvAtDisposal = grandNbv,
                TotalProceeds = grandProceeds,
                TotalGain = grandGain,
                TotalCapitalGain = grandCapGain,
                TotalLoss = grandLoss,
                NetGainLoss = grandGain + grandCapGain - grandLoss,
            },
        };
    }
}
