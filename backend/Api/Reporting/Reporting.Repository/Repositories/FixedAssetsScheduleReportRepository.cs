using Microsoft.EntityFrameworkCore;
using Reporting.Entity.Models;
using Reporting.Repository.ReadModels;

namespace Reporting.Repository.Repositories;

public sealed class FixedAssetsScheduleReportRepository
{
    private readonly ReportingDbContext _db;

    public FixedAssetsScheduleReportRepository(ReportingDbContext db)
    {
        _db = db;
    }

    public static IEnumerable<(string Field, string Message)> Refusals(FixedAssetsScheduleReportRequest request)
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
        FixedAssetsScheduleReportRequest request,
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

    public async Task<FixedAssetsScheduleReportResponse> RunAsync(
        FixedAssetsScheduleReportRequest request,
        CancellationToken ct = default)
    {
        DateOnly from = request.From ?? throw new ArgumentException("From date is required.");
        DateOnly to = request.To ?? throw new ArgumentException("To date is required.");

        var query = FixedAssetRegisterRepository.Rows(_db, from, to);
        var rows = await query.ToListAsync(ct);

        return Compose(rows, request, from, to);
    }

    public async Task<IReadOnlyList<FixedAssetCategoryOptionDto>> CategoriesAsync(CancellationToken ct = default) =>
        await _db.FixedAssetCategories
            .OrderBy(c => c.CategoryName)
            .Select(c => new FixedAssetCategoryOptionDto
            {
                Id = c.FixedAssetCategoryId,
                Name = c.CategoryName,
            })
            .ToListAsync(ct);

    public async Task<FixedAssetsScheduleLookupsDto> GetLookupsAsync(CancellationToken ct = default)
    {
        var categories = await CategoriesAsync(ct);

        return new FixedAssetsScheduleLookupsDto
        {
            Categories = categories.ToList(),
            Statuses = ["All", "Active", "Disposed"],
            Books = ["Books", "Tax"],
        };
    }

    public static FixedAssetsScheduleReportResponse Compose(
        IEnumerable<FixedAssetPeriodRow> rows,
        FixedAssetsScheduleReportRequest request,
        DateOnly from,
        DateOnly to)
    {
        IEnumerable<FixedAssetPeriodRow> filteredQuery = rows;

        if (request.CategoryId.HasValue && request.CategoryId.Value > 0)
        {
            filteredQuery = filteredQuery.Where(r => r.FixedAssetCategoryId == request.CategoryId.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.Status) && !request.Status.Equals("All", StringComparison.OrdinalIgnoreCase))
        {
            if (request.Status.Equals("Active", StringComparison.OrdinalIgnoreCase))
            {
                filteredQuery = filteredQuery.Where(r => r.DisposalDate == null);
            }
            else if (request.Status.Equals("Disposed", StringComparison.OrdinalIgnoreCase))
            {
                filteredQuery = filteredQuery.Where(r => r.DisposalDate != null);
            }
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            string term = request.Search.Trim();
            filteredQuery = filteredQuery.Where(r =>
                (r.AssetNumber != null && r.AssetNumber.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
                (r.AssetName != null && r.AssetName.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
                (r.SerialNumber != null && r.SerialNumber.Contains(term, StringComparison.OrdinalIgnoreCase)));
        }

        var filtered = filteredQuery.ToList();

        var groups = filtered
            .GroupBy(r => new { r.FixedAssetCategoryId, r.AssetType, r.AssetAccount, r.AccumDepAccount })
            .OrderBy(g => g.Key.AssetType)
            .Select(g => new FixedAssetScheduleCategoryGroupDto
            {
                CategoryId = g.Key.FixedAssetCategoryId,
                CategoryName = g.Key.AssetType,
                AssetAccount = g.Key.AssetAccount,
                AccumDepAccount = g.Key.AccumDepAccount,
                OpeningCost = g.Sum(x => x.OpeningCost),
                AdditionCost = g.Sum(x => x.AdditionCost),
                DisposalCost = g.Sum(x => x.DisposalCost),
                ClosingCost = g.Sum(x => x.ClosingCost),
                OpeningAccumDep = g.Sum(x => x.OpeningAccumDep),
                Depreciation = g.Sum(x => x.Depreciation),
                DisposalAccumDep = g.Sum(x => x.DisposalAccumDep),
                ClosingAccumDep = g.Sum(x => x.ClosingAccumDep),
                OpeningNbv = g.Sum(x => x.OpeningNbv),
                ClosingNbv = g.Sum(x => x.ClosingNbv),
                Assets = g.OrderBy(x => x.AssetNumber).Select(MapItem).ToList(),
            })
            .ToList();

        var summary = new FixedAssetsScheduleSummaryDto
        {
            TotalAssets = filtered.Count,
            ActiveAssets = filtered.Count(x => x.DisposalDate == null),
            DisposedAssets = filtered.Count(x => x.DisposalDate != null),
            TotalOpeningCost = filtered.Sum(x => x.OpeningCost),
            TotalAdditionCost = filtered.Sum(x => x.AdditionCost),
            TotalDisposalCost = filtered.Sum(x => x.DisposalCost),
            TotalClosingCost = filtered.Sum(x => x.ClosingCost),
            TotalOpeningAccumDep = filtered.Sum(x => x.OpeningAccumDep),
            TotalDepreciation = filtered.Sum(x => x.Depreciation),
            TotalDisposalAccumDep = filtered.Sum(x => x.DisposalAccumDep),
            TotalClosingAccumDep = filtered.Sum(x => x.ClosingAccumDep),
            TotalOpeningNbv = filtered.Sum(x => x.OpeningNbv),
            TotalClosingNbv = filtered.Sum(x => x.ClosingNbv),
        };

        return new FixedAssetsScheduleReportResponse
        {
            FromDate = from,
            ToDate = to,
            Book = string.IsNullOrWhiteSpace(request.Book) ? "Books" : request.Book,
            Summary = summary,
            Categories = groups,
        };
    }

    private static FixedAssetScheduleItemDto MapItem(FixedAssetPeriodRow r) =>
        new()
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
            DepExpenseAccount = r.DepExpenseAccount,
            PurchaseDate = r.PurchaseDate,
            PurchasePrice = r.PurchasePrice,
            DepMethod = r.DepMethod,
            DepStartDate = r.DepStartDate,
            Rate = r.Rate,
            EffectiveLife = r.EffectiveLife,
            ResidualValue = r.ResidualValue,
            OpeningCost = r.OpeningCost,
            AdditionCost = r.AdditionCost,
            DisposalCost = r.DisposalCost,
            ClosingCost = r.ClosingCost,
            OpeningAccumDep = r.OpeningAccumDep,
            Depreciation = r.Depreciation,
            DisposalAccumDep = r.DisposalAccumDep,
            ClosingAccumDep = r.ClosingAccumDep,
            OpeningNbv = r.OpeningNbv,
            ClosingNbv = r.ClosingNbv,
            DisposalDate = r.DisposalDate,
            SaleProceeds = r.SaleProceeds,
            NbvAtDisposal = r.NbvAtDisposal,
            GainOnDisposal = r.GainOnDisposal,
            LossOnDisposal = r.LossOnDisposal,
            CapitalGain = r.CapitalGain,
            Status = r.DisposalDate == null ? "Active" : "Disposed",
        };
}
