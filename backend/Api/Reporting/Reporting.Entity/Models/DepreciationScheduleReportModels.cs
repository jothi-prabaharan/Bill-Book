namespace Reporting.Entity.Models;

public sealed class DepreciationScheduleReportRequest
{
    public Guid? OrgId { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public long? CategoryId { get; set; }
    public string? Method { get; set; } // "All", "Straight Line", "Written Down Value"
    public string? Book { get; set; } // "Books", "Tax"
    public string? Search { get; set; }
}

public sealed class DepreciationScheduleItemDto
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
    public decimal Cost { get; set; }
    public string? DepMethod { get; set; }
    public DateOnly? DepStartDate { get; set; }
    public decimal? Rate { get; set; }
    public int? EffectiveLife { get; set; }
    public decimal? ResidualValue { get; set; }

    // Depreciation roll-forward
    public decimal OpeningAccumDep { get; set; }
    public decimal Depreciation { get; set; }
    public decimal DisposalAccumDep { get; set; }
    public decimal ClosingAccumDep { get; set; }

    // Book Value
    public decimal OpeningNbv { get; set; }
    public decimal ClosingNbv { get; set; }

    public DateOnly? DisposalDate { get; set; }
    public string Status { get; set; } = "Active";
}

public sealed class DepreciationScheduleCategoryGroupDto
{
    public long CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public string AssetAccount { get; set; } = string.Empty;
    public string AccumDepAccount { get; set; } = string.Empty;
    public string DepExpenseAccount { get; set; } = string.Empty;

    public decimal TotalCost { get; set; }
    public decimal OpeningAccumDep { get; set; }
    public decimal Depreciation { get; set; }
    public decimal DisposalAccumDep { get; set; }
    public decimal ClosingAccumDep { get; set; }
    public decimal OpeningNbv { get; set; }
    public decimal ClosingNbv { get; set; }

    public List<DepreciationScheduleItemDto> Assets { get; set; } = [];
}

public sealed class DepreciationScheduleSummaryDto
{
    public int TotalAssets { get; set; }
    public int ActiveAssets { get; set; }
    public int DisposedAssets { get; set; }

    public decimal TotalCost { get; set; }
    public decimal TotalOpeningAccumDep { get; set; }
    public decimal TotalDepreciation { get; set; }
    public decimal TotalDisposalAccumDep { get; set; }
    public decimal TotalClosingAccumDep { get; set; }
    public decimal TotalOpeningNbv { get; set; }
    public decimal TotalClosingNbv { get; set; }
}

public sealed class DepreciationScheduleReportResponse
{
    public DateOnly FromDate { get; set; }
    public DateOnly ToDate { get; set; }
    public string Book { get; set; } = "Books";
    public DepreciationScheduleSummaryDto Summary { get; set; } = new();
    public List<DepreciationScheduleCategoryGroupDto> Categories { get; set; } = [];
}

public sealed class DepreciationScheduleLookupsDto
{
    public List<FixedAssetCategoryOptionDto> Categories { get; set; } = [];
    public List<string> Methods { get; set; } = ["All", "Straight Line", "Written Down Value"];
    public List<string> Books { get; set; } = ["Books", "Tax"];
}
