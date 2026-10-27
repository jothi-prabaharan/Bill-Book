namespace Reporting.Entity.Models;

public sealed class FixedAssetsScheduleReportRequest
{
    public Guid? OrgId { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public long? CategoryId { get; set; }
    public string? Status { get; set; } // "All", "Active", "Disposed"
    public string? Book { get; set; } // "Books", "Tax"
    public string? Search { get; set; }
}

public sealed class FixedAssetScheduleItemDto
{
    public long FixedAssetId { get; set; }
    public long CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public string AssetCode { get; set; } = string.Empty;
    public string AssetName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? SerialNumber { get; set; }
    public string AssetAccount { get; set; } = string.Empty;
    public string AccumDepAccount { get; set; } = string.Empty;
    public string DepExpenseAccount { get; set; } = string.Empty;
    public DateOnly PurchaseDate { get; set; }
    public decimal PurchasePrice { get; set; }
    public string? DepMethod { get; set; }
    public DateOnly? DepStartDate { get; set; }
    public decimal? Rate { get; set; }
    public int? EffectiveLife { get; set; }
    public decimal? ResidualValue { get; set; }

    // Cost roll-forward
    public decimal OpeningCost { get; set; }
    public decimal AdditionCost { get; set; }
    public decimal DisposalCost { get; set; }
    public decimal ClosingCost { get; set; }

    // Accumulated Depreciation roll-forward
    public decimal OpeningAccumDep { get; set; }
    public decimal Depreciation { get; set; }
    public decimal DisposalAccumDep { get; set; }
    public decimal ClosingAccumDep { get; set; }

    // Net Book Value
    public decimal OpeningNbv { get; set; }
    public decimal ClosingNbv { get; set; }

    // Disposal specifics (available in column chooser)
    public DateOnly? DisposalDate { get; set; }
    public decimal? SaleProceeds { get; set; }
    public decimal? NbvAtDisposal { get; set; }
    public decimal? GainOnDisposal { get; set; }
    public decimal? LossOnDisposal { get; set; }
    public decimal? CapitalGain { get; set; }

    public string Status { get; set; } = "Active";
}

public sealed class FixedAssetScheduleCategoryGroupDto
{
    public long CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public string AssetAccount { get; set; } = string.Empty;
    public string AccumDepAccount { get; set; } = string.Empty;

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

    public List<FixedAssetScheduleItemDto> Assets { get; set; } = [];
}

public sealed class FixedAssetsScheduleSummaryDto
{
    public int TotalAssets { get; set; }
    public int ActiveAssets { get; set; }
    public int DisposedAssets { get; set; }

    public decimal TotalOpeningCost { get; set; }
    public decimal TotalAdditionCost { get; set; }
    public decimal TotalDisposalCost { get; set; }
    public decimal TotalClosingCost { get; set; }

    public decimal TotalOpeningAccumDep { get; set; }
    public decimal TotalDepreciation { get; set; }
    public decimal TotalDisposalAccumDep { get; set; }
    public decimal TotalClosingAccumDep { get; set; }

    public decimal TotalOpeningNbv { get; set; }
    public decimal TotalClosingNbv { get; set; }
}

public sealed class FixedAssetsScheduleReportResponse
{
    public DateOnly FromDate { get; set; }
    public DateOnly ToDate { get; set; }
    public string Book { get; set; } = "Books";
    public FixedAssetsScheduleSummaryDto Summary { get; set; } = new();
    public List<FixedAssetScheduleCategoryGroupDto> Categories { get; set; } = [];
}

public sealed class FixedAssetCategoryOptionDto
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public sealed class FixedAssetsScheduleLookupsDto
{
    public List<FixedAssetCategoryOptionDto> Categories { get; set; } = [];
    public List<string> Statuses { get; set; } = ["All", "Active", "Disposed"];
    public List<string> Books { get; set; } = ["Books", "Tax"];
}
