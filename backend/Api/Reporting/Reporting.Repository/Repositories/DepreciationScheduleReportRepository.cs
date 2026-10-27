using Microsoft.EntityFrameworkCore;
using Reporting.Entity.Models;
using Reporting.Repository.ReadModels;

namespace Reporting.Repository.Repositories;

public sealed class DepreciationScheduleReportRepository
{
    private readonly ReportingDbContext _db;

    public DepreciationScheduleReportRepository(ReportingDbContext db)
    {
        _db = db;
    }

    public static IEnumerable<(string Field, string Message)> Refusals(DepreciationScheduleReportRequest request)
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
        DepreciationScheduleReportRequest request,
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

    public async Task<DepreciationScheduleReportResponse> RunAsync(
        DepreciationScheduleReportRequest request,
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

    public async Task<DepreciationScheduleLookupsDto> GetLookupsAsync(CancellationToken ct = default)
    {
        var categories = await CategoriesAsync(ct);

        return new DepreciationScheduleLookupsDto
        {
            Categories = categories.ToList(),
            Methods = ["All", "Straight Line", "Written Down Value"],
            Books = ["Books", "Tax"],
        };
    }

    public static DepreciationScheduleReportResponse Compose(
        IEnumerable<FixedAssetPeriodRow> rows,
        DepreciationScheduleReportRequest request,
        DateOnly from,
        DateOnly to)
    {
        IEnumerable<FixedAssetPeriodRow> filteredQuery = rows;

        if (request.CategoryId.HasValue && request.CategoryId.Value > 0)
        {
            filteredQuery = filteredQuery.Where(r => r.FixedAssetCategoryId == request.CategoryId.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.Method) && !request.Method.Equals("All", StringComparison.OrdinalIgnoreCase))
        {
            filteredQuery = filteredQuery.Where(r =>
                r.DepMethod != null && r.DepMethod.Equals(request.Method, StringComparison.OrdinalIgnoreCase));
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
            .GroupBy(r => new { r.FixedAssetCategoryId, r.AssetType, r.AssetAccount, r.AccumDepAccount, r.DepExpenseAccount })
            .OrderBy(g => g.Key.AssetType)
            .Select(g => new DepreciationScheduleCategoryGroupDto
            {
                CategoryId = g.Key.FixedAssetCategoryId,
                CategoryName = g.Key.AssetType,
                AssetAccount = g.Key.AssetAccount,
                AccumDepAccount = g.Key.AccumDepAccount,
                DepExpenseAccount = g.Key.DepExpenseAccount,
                TotalCost = g.Sum(x => x.Cost),
                OpeningAccumDep = g.Sum(x => x.OpeningAccumDep),
                Depreciation = g.Sum(x => x.Depreciation),
                DisposalAccumDep = g.Sum(x => x.DisposalAccumDep),
                ClosingAccumDep = g.Sum(x => x.ClosingAccumDep),
                OpeningNbv = g.Sum(x => x.OpeningNbv),
                ClosingNbv = g.Sum(x => x.ClosingNbv),
                Assets = g.OrderBy(x => x.AssetNumber).Select(MapItem).ToList(),
            })
            .ToList();

        var summary = new DepreciationScheduleSummaryDto
        {
            TotalAssets = filtered.Count,
            ActiveAssets = filtered.Count(x => x.DisposalDate == null),
            DisposedAssets = filtered.Count(x => x.DisposalDate != null),
            TotalCost = filtered.Sum(x => x.Cost),
            TotalOpeningAccumDep = filtered.Sum(x => x.OpeningAccumDep),
            TotalDepreciation = filtered.Sum(x => x.Depreciation),
            TotalDisposalAccumDep = filtered.Sum(x => x.DisposalAccumDep),
            TotalClosingAccumDep = filtered.Sum(x => x.ClosingAccumDep),
            TotalOpeningNbv = filtered.Sum(x => x.OpeningNbv),
            TotalClosingNbv = filtered.Sum(x => x.ClosingNbv),
        };

        return new DepreciationScheduleReportResponse
        {
            FromDate = from,
            ToDate = to,
            Book = string.IsNullOrWhiteSpace(request.Book) ? "Books" : request.Book,
            Summary = summary,
            Categories = groups,
        };
    }

    private static DepreciationScheduleItemDto MapItem(FixedAssetPeriodRow r) =>
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
            Cost = r.Cost,
            DepMethod = r.DepMethod,
            DepStartDate = r.DepStartDate,
            Rate = r.Rate,
            EffectiveLife = r.EffectiveLife,
            ResidualValue = r.ResidualValue,
            OpeningAccumDep = r.OpeningAccumDep,
            Depreciation = r.Depreciation,
            DisposalAccumDep = r.DisposalAccumDep,
            ClosingAccumDep = r.ClosingAccumDep,
            OpeningNbv = r.OpeningNbv,
            ClosingNbv = r.ClosingNbv,
            DisposalDate = r.DisposalDate,
            Status = r.DisposalDate == null ? "Active" : "Disposed",
        };
}
