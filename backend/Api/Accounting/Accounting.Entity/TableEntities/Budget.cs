using System.ComponentModel.DataAnnotations;
using Accounting.Entity.Enums;
using Shared.Kernel.Tenancy;

namespace Accounting.Entity.TableEntities;

/// <summary>
/// A set of monthly amounts per account for one financial year of one branch
/// (TK-108, design "Budgeting"). Amounts are in base currency and positive in
/// the account's normal direction. An approved budget is locked; changing it
/// is a revision, which copies it into a new draft that supersedes it once
/// approved.
/// </summary>
public class Budget : OrgScopedEntity
{
    public long BudgetId { get; set; }

    [Required(ErrorMessage = "Budget name is required.")]
    [MaxLength(100, ErrorMessage = "Budget name cannot exceed 100 characters.")]
    public string Name { get; set; } = null!;

    /// <summary>The first day of the financial year, from the branch's start month.</summary>
    public DateOnly FinancialYearStart { get; set; }

    public BudgetStatus Status { get; set; } = BudgetStatus.Draft;

    /// <summary>The branch's approved budget for the year. At most one per branch and year.</summary>
    public bool IsBranchDefault { get; set; }

    /// <summary>The approved budget this one revises.</summary>
    public long? RevisionOf { get; set; }

    [MaxLength(500, ErrorMessage = "Revision reason cannot exceed 500 characters.")]
    public string? RevisionReason { get; set; }

    /// <summary>Whether lines carry a project.</summary>
    public bool ByProject { get; set; }

    public DateTimeOffset? ApprovedAt { get; set; }

    public Guid? ApprovedBy { get; set; }
}
