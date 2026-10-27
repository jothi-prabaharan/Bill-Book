using System.ComponentModel.DataAnnotations;

namespace Reporting.Entity.Models;

/// <summary>
/// Query parameters for the Fixed Asset Reconciliation report.
/// Both dates are required by D-67.
/// </summary>
public sealed class FixedAssetReconciliationReportRequest
{
    [Required(ErrorMessage = "A from date is required.")]
    public DateOnly? From { get; set; }

    [Required(ErrorMessage = "A to date is required.")]
    public DateOnly? To { get; set; }

    public Guid? OrgId { get; set; }
}

public sealed class ReconciliationSideBalanceDto
{
    public decimal OpeningCost { get; set; }
    public decimal CostDebits { get; set; }
    public decimal CostCredits { get; set; }
    public decimal ClosingCost { get; set; }

    public decimal OpeningAccumDep { get; set; }
    public decimal AccumDepDebits { get; set; }
    public decimal AccumDepCredits { get; set; }
    public decimal ClosingAccumDep { get; set; }

    public decimal OpeningBookValue { get; set; }
    public decimal ClosingBookValue { get; set; }

    public decimal ClosingBalance { get; set; }
}

public sealed class FixedAssetReconciliationAccountDto
{
    public long AccountId { get; set; }
    public string AccountCode { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public string AccountType { get; set; } = "Asset"; // "Asset" or "AccumulatedDepreciation"

    public ReconciliationSideBalanceDto Register { get; set; } = new();
    public ReconciliationSideBalanceDto Ledger { get; set; } = new();

    public decimal ClosingDifference { get; set; }
    public bool IsReconciled { get; set; }
    public string DifferenceExplanation { get; set; } = string.Empty;
}

public sealed class FixedAssetReconciliationSummaryDto
{
    public int TotalAccounts { get; set; }
    public int ReconciledAccounts { get; set; }
    public int DiscrepantAccounts { get; set; }

    public decimal RegisterTotalNbv { get; set; }
    public decimal LedgerTotalNbv { get; set; }
    public decimal TotalNbvDifference { get; set; }
    public string Explanation { get; set; } = string.Empty;
}

public sealed class FixedAssetReconciliationReportResponse
{
    public DateOnly FromDate { get; set; }
    public DateOnly ToDate { get; set; }
    public string Currency { get; set; } = "INR";
    public IReadOnlyList<FixedAssetReconciliationAccountDto> Accounts { get; set; } = [];
    public FixedAssetReconciliationSummaryDto Summary { get; set; } = new();
}
