using System.ComponentModel.DataAnnotations;
using Shared.Kernel.Tenancy;

namespace Accounting.Entity.TableEntities;

/// <summary>One account's (and, on a budget by project, one project's) amount for one month of a budget.</summary>
public class BudgetLine : OrgScopedEntity
{
    public long BudgetLineId { get; set; }

    public long BudgetId { get; set; }

    /// <summary>A leaf account of the branch.</summary>
    public long AccountId { get; set; }

    /// <summary>Only on a budget by project; null is "no project".</summary>
    public long? ProjectId { get; set; }

    /// <summary>1 to 12, counted from the financial year's first month.</summary>
    [Range(1, 12, ErrorMessage = "The period must be a month from 1 to 12.")]
    public short Period { get; set; }

    /// <summary>Base currency, positive in the account's normal direction.</summary>
    [Range(typeof(decimal), "0", "999999999999999", ErrorMessage = "A budget amount cannot be negative.")]
    public decimal Amount { get; set; }
}
