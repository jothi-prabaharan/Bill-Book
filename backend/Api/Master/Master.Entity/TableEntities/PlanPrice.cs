using System.ComponentModel.DataAnnotations;
using Master.Entity.Enums;
using Shared.Kernel.Apps;
using Shared.Kernel.Entities;

namespace Master.Entity.TableEntities;

/// <summary>
/// What a paid plan costs a trade for one billing cycle (D-71, TK-211): the price
/// the Plan screen shows. A table rather than a constant so an operator can set a
/// price for a customer's trade without a release; the start-up seed only adds
/// rows that are missing, so a price somebody changed is never put back.
/// </summary>
public class PlanPrice : AuditableEntity
{
    public long PlanPriceId { get; set; }

    public BusinessType BusinessType { get; set; }

    /// <summary>Pro (Professional) or Elite. The others are not sold.</summary>
    public LicenseType Plan { get; set; }

    public BillingCycle BillingCycle { get; set; }

    /// <summary>The price for the whole cycle, before tax.</summary>
    [Range(0, 99999999.99, ErrorMessage = "The price must be between 0 and 99,999,999.99.")]
    public decimal Amount { get; set; }

    [Required(ErrorMessage = "Currency is required.")]
    [MaxLength(3, ErrorMessage = "Currency code cannot exceed 3 characters.")]
    public string CurrencyCode { get; set; } = "INR";

    /// <summary>False withdraws a price without deleting it.</summary>
    public bool IsActive { get; set; } = true;
}
