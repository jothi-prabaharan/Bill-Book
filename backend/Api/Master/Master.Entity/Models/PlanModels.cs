using System.ComponentModel.DataAnnotations;
using Master.Entity.Enums;
using Shared.Kernel.Licensing;

namespace Master.Entity.Models;

/// <summary>
/// The customer's plan, what it allows the signed-in branch, and how many of
/// its user places are taken (TK-209). The monthly document counts belong to
/// Sales and Purchase, which are the only services that can read them.
/// </summary>
public class PlanSummaryResponse
{
    /// <summary>The name a person reads: Trial, Professional or Elite.</summary>
    public string Plan { get; set; } = null!;

    public LicenseType LicenseType { get; set; }

    /// <summary>Users the branch may hold, deactivated ones included; null is no limit.</summary>
    public int? MaxUsers { get; set; }

    /// <summary>Users the branch holds, deactivated ones included.</summary>
    public int UsersUsed { get; set; }

    /// <summary>Sales documents a month; null is no limit.</summary>
    public int? MonthlySalesTransactionLimit { get; set; }

    /// <summary>Purchase documents a month; null is no limit.</summary>
    public int? MonthlyPurchaseTransactionLimit { get; set; }

    /// <summary>
    /// The sentence an invitation would be refused with, or null while the
    /// branch has a place left. Here so a screen shows the server's words
    /// beside a disabled button and does not write a copy of its own.
    /// </summary>
    public string? UserLimitMessage { get; set; }

    /// <summary>False on Elite, and for a customer that is never billed.</summary>
    public bool CanUpgrade { get; set; }

    /// <summary>An upgrade request from this customer is waiting for an operator.</summary>
    public bool UpgradeRequested { get; set; }
}

/// <summary>A customer asking to move to a higher plan (D-70, TK-209).</summary>
public class UpgradeRequestInput
{
    /// <summary>The plan asked for; Elite when left out.</summary>
    [EnumDataType(typeof(LicenseType), ErrorMessage = "Choose a valid plan.")]
    public LicenseType? ToPlan { get; set; }

    /// <summary>The limit that sent them here; empty when they asked from the Plan screen.</summary>
    [EnumDataType(typeof(PlanLimitKind), ErrorMessage = "Choose a valid reason.")]
    public PlanLimitKind? Reason { get; set; }

    [MaxLength(500, ErrorMessage = "The note cannot exceed 500 characters.")]
    public string? Note { get; set; }
}
