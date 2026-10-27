using System.ComponentModel.DataAnnotations;

namespace Reporting.Entity.Models;

/// <summary>
/// Query parameters for the Disposal Schedule report.
/// Both dates are required by D-67.
/// </summary>
public sealed class DisposalScheduleReportRequest
{
    [Required(ErrorMessage = "A from date is required.")]
    public DateOnly? From { get; set; }

    [Required(ErrorMessage = "A to date is required.")]
    public DateOnly? To { get; set; }

    public long? CategoryId { get; set; }

    public string? Search { get; set; }

    public Guid? OrgId { get; set; }
}

public sealed class DisposalScheduleCategoryLookupDto
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int DisposedCount { get; set; }
}

public sealed class DisposalScheduleLookupsDto
{
    public IReadOnlyList<DisposalScheduleCategoryLookupDto> Categories { get; set; } = [];
}

public sealed class DisposalScheduleItemDto
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
    public DateOnly PurchaseDate { get; set; }
    public decimal Cost { get; set; }
    public string? DepMethod { get; set; }
    public DateOnly DisposalDate { get; set; }
    public decimal AccumDep { get; set; }
    public decimal NbvAtDisposal { get; set; }
    public decimal SaleProceeds { get; set; }
    public decimal GainOnDisposal { get; set; }
    public decimal CapitalGain { get; set; }
    public decimal LossOnDisposal { get; set; }
    public string? Reference { get; set; }
}

public sealed class DisposalScheduleCategoryGroupDto
{
    public long CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public int AssetCount { get; set; }
    public decimal TotalCost { get; set; }
    public decimal TotalAccumDep { get; set; }
    public decimal TotalNbvAtDisposal { get; set; }
    public decimal TotalProceeds { get; set; }
    public decimal TotalGain { get; set; }
    public decimal TotalCapitalGain { get; set; }
    public decimal TotalLoss { get; set; }
    public decimal NetGainLoss { get; set; }
    public IReadOnlyList<DisposalScheduleItemDto> Disposals { get; set; } = [];
}

public sealed class DisposalScheduleSummaryDto
{
    public int TotalDisposals { get; set; }
    public decimal TotalCost { get; set; }
    public decimal TotalAccumDep { get; set; }
    public decimal TotalNbvAtDisposal { get; set; }
    public decimal TotalProceeds { get; set; }
    public decimal TotalGain { get; set; }
    public decimal TotalCapitalGain { get; set; }
    public decimal TotalLoss { get; set; }
    public decimal NetGainLoss { get; set; }
}

public sealed class DisposalScheduleReportResponse
{
    public DateOnly FromDate { get; set; }
    public DateOnly ToDate { get; set; }
    public string Currency { get; set; } = "INR";
    public IReadOnlyList<DisposalScheduleCategoryGroupDto> Categories { get; set; } = [];
    public DisposalScheduleSummaryDto Summary { get; set; } = new();
}
