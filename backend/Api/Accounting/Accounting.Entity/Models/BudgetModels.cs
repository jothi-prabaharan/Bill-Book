using System.ComponentModel.DataAnnotations;

namespace Accounting.Entity.Models;

// ---- Budgets (TK-108, design "Budgeting") ------------------------------------

public class BudgetListItem
{
    public long BudgetId { get; set; }

    public string Name { get; set; } = null!;

    public DateOnly FinancialYearStart { get; set; }

    /// <summary><c>Draft</c>, <c>Approved</c> or <c>Superseded</c>.</summary>
    public string Status { get; set; } = null!;

    public bool IsBranchDefault { get; set; }

    public bool ByProject { get; set; }

    public long? RevisionOf { get; set; }

    public string? RevisionReason { get; set; }

    public DateTimeOffset? ApprovedAt { get; set; }

    /// <summary>Every line's amount added up, in base currency.</summary>
    public decimal Total { get; set; }
}

public class BudgetView : BudgetListItem
{
    public List<BudgetLineView> Lines { get; set; } = [];
}

/// <summary>One account's (and project's) twelve months, the grid's row.</summary>
public class BudgetLineView
{
    public long AccountId { get; set; }

    public string AccountCode { get; set; } = null!;

    public string AccountName { get; set; } = null!;

    public int AccountTypeId { get; set; }

    public long? ProjectId { get; set; }

    public string? ProjectCode { get; set; }

    /// <summary>Twelve amounts, the first for the financial year's first month.</summary>
    public decimal[] Amounts { get; set; } = new decimal[12];

    public decimal Total { get; set; }
}

public class CreateBudgetRequest
{
    [Required(ErrorMessage = "Budget name is required.")]
    [MaxLength(100, ErrorMessage = "Budget name cannot exceed 100 characters.")]
    public string Name { get; set; } = null!;

    /// <summary>The calendar year the financial year starts in: 2026 for April 2026 to March 2027.</summary>
    [Range(2000, 2100, ErrorMessage = "Choose a year between 2000 and 2100.")]
    public int Year { get; set; }

    public bool ByProject { get; set; }

    /// <summary><c>Blank</c>, <c>LastYearActuals</c> or <c>OtherBudget</c>.</summary>
    [Required(ErrorMessage = "Choose what the budget starts from.")]
    [MaxLength(20, ErrorMessage = "The budget source cannot exceed 20 characters.")]
    public string Source { get; set; } = "Blank";

    /// <summary>The budget to copy, when <see cref="Source"/> is <c>OtherBudget</c>.</summary>
    public long? SourceBudgetId { get; set; }

    /// <summary>Applied to every account without an adjustment of its own type: +10 is ten percent more than the source.</summary>
    [Range(typeof(decimal), "-100", "1000", ErrorMessage = "An adjustment must be between -100% and 1000%.")]
    public decimal AdjustPercent { get; set; }

    /// <summary>Adjustments per account type (4 income, 5 expense and so on), overriding <see cref="AdjustPercent"/>.</summary>
    public List<BudgetTypeAdjustment> TypeAdjustments { get; set; } = [];
}

public class BudgetTypeAdjustment
{
    [Range(1, 5, ErrorMessage = "Choose an account type.")]
    public int AccountTypeId { get; set; }

    [Range(typeof(decimal), "-100", "1000", ErrorMessage = "An adjustment must be between -100% and 1000%.")]
    public decimal Percent { get; set; }
}

public class SaveBudgetRequest
{
    [Required(ErrorMessage = "Budget name is required.")]
    [MaxLength(100, ErrorMessage = "Budget name cannot exceed 100 characters.")]
    public string Name { get; set; } = null!;

    public List<SaveBudgetLineRequest> Lines { get; set; } = [];
}

public class SaveBudgetLineRequest
{
    [Range(1, long.MaxValue, ErrorMessage = "Choose an account.")]
    public long AccountId { get; set; }

    public long? ProjectId { get; set; }

    /// <summary>Exactly twelve amounts, none negative.</summary>
    [Required(ErrorMessage = "Each line needs its twelve monthly amounts.")]
    public List<decimal> Amounts { get; set; } = [];
}

public class ApproveBudgetRequest
{
    /// <summary>Make it the branch's budget for the year. A revision takes the default from the budget it revises regardless.</summary>
    public bool MakeDefault { get; set; }
}

public class ReviseBudgetRequest
{
    [Required(ErrorMessage = "Say why the budget is being revised.")]
    [MaxLength(500, ErrorMessage = "The reason cannot exceed 500 characters.")]
    public string Reason { get; set; } = null!;
}

public enum BudgetOutcome
{
    Ok,
    NotFound,
    Refused,

    /// <summary>The budget is approved or superseded, so its lines cannot change.</summary>
    Locked,

    /// <summary>An import named account or project codes the branch does not have.</summary>
    UnknownCodes,
}

public sealed record BudgetResult(BudgetOutcome Outcome, long BudgetId = 0, string? Detail = null, IReadOnlyList<string>? UnknownCodes = null);
