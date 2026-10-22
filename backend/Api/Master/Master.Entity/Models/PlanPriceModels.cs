using Master.Entity.Enums;

namespace Master.Entity.Models;

/// <summary>One billing cycle's price of each paid plan for a trade (TK-211).</summary>
public class PlanCyclePrice
{
    public BillingCycle Cycle { get; set; }

    public int Months { get; set; }

    /// <summary>What Professional costs for the whole cycle.</summary>
    public decimal Professional { get; set; }

    /// <summary>What Elite costs for the whole cycle.</summary>
    public decimal Elite { get; set; }
}

/// <summary>What one paid plan allows, as a licence is given it (TK-211).</summary>
public class PlanTierInfo
{
    /// <summary>The name a person reads: Professional or Elite.</summary>
    public string Plan { get; set; } = null!;

    public LicenseType LicenseType { get; set; }

    /// <summary>Users a branch may hold, deactivated ones included; null is no limit.</summary>
    public int? MaxUsers { get; set; }

    /// <summary>Sales documents a month; null is no limit.</summary>
    public int? MonthlySalesTransactionLimit { get; set; }

    /// <summary>Purchase documents a month; null is no limit.</summary>
    public int? MonthlyPurchaseTransactionLimit { get; set; }
}

/// <summary>
/// What the Plan screen shows beside the signed-in branch's plan: what each paid
/// plan allows, and what each costs for the branch's trade. A branch with no
/// trade, or a trade with no price set, has no cycles: its price is agreed with
/// the platform's team (TK-211).
/// </summary>
public class PlanPricesResponse
{
    public string? BusinessType { get; set; }

    public string CurrencyCode { get; set; } = "INR";

    /// <summary>Professional then Elite, with the limits the licence is given — the one place those figures are written.</summary>
    public List<PlanTierInfo> Tiers { get; set; } = [];

    public List<PlanCyclePrice> Cycles { get; set; } = [];
}

/// <summary>A platform operator moving a customer to another plan (TK-211).</summary>
public class SwitchPlanRequest
{
    [System.ComponentModel.DataAnnotations.EnumDataType(typeof(LicenseType), ErrorMessage = "Choose a valid plan.")]
    public LicenseType Plan { get; set; }
}
