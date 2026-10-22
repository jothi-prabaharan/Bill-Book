using Master.Entity.Enums;
using Master.Entity.TableEntities;
using Shared.Kernel.Apps;

namespace Master.Repository.SeedData;

/// <summary>
/// The first prices of the paid plans (D-71, TK-211). Pure.
///
/// <b>Where they come from.</b> The website's base monthly price for each trade
/// (<c>BUSINESS_PRICING_TIERS_SEED</c>), which also sells a quarter at 10% off and a
/// year at 20% off. The product bills a quarter or half a year, and the website has
/// no half year, so the half year is taken at 15%: midway between the two it does
/// sell. That is an assumption and it is written down here so it is easy to change;
/// the figures are rows an operator can edit, and the seed never overwrites one.
///
/// Elite costs three times Professional for the same trade and cycle.
/// </summary>
public static class PlanPriceSeed
{
    /// <summary>What Elite costs as a multiple of Professional.</summary>
    public const decimal EliteMultiple = 3m;

    /// <summary>The website's base monthly price per trade.</summary>
    public static readonly IReadOnlyList<(BusinessType Type, decimal Monthly)> MonthlyBase =
    [
        (BusinessType.Retail, 1500m),
        (BusinessType.Pharma, 1500m),
        (BusinessType.Jewellery, 4000m),
        (BusinessType.School, 5000m),
        (BusinessType.Restaurant, 2200m),
        (BusinessType.Service, 1500m),
    ];

    /// <summary>The months in a cycle.</summary>
    public static int Months(BillingCycle cycle) => cycle switch
    {
        BillingCycle.Quarterly => 3,
        BillingCycle.BiAnnual => 6,
        _ => throw new ArgumentOutOfRangeException(nameof(cycle)),
    };

    /// <summary>The share taken off the months' price for paying a cycle at a time.</summary>
    public static decimal Discount(BillingCycle cycle) => cycle switch
    {
        BillingCycle.Quarterly => 0.10m,
        BillingCycle.BiAnnual => 0.15m,
        _ => throw new ArgumentOutOfRangeException(nameof(cycle)),
    };

    /// <summary>Professional's price for a trade and cycle, to the paisa.</summary>
    public static decimal Professional(decimal monthly, BillingCycle cycle) =>
        Math.Round(monthly * Months(cycle) * (1m - Discount(cycle)), 2, MidpointRounding.AwayFromZero);

    /// <summary>Every price: each trade, each cycle, Professional then Elite.</summary>
    public static IReadOnlyList<PlanPrice> Build()
    {
        List<PlanPrice> rows = [];

        foreach ((BusinessType type, decimal monthly) in MonthlyBase)
        {
            foreach (BillingCycle cycle in Enum.GetValues<BillingCycle>())
            {
                decimal professional = Professional(monthly, cycle);

                rows.Add(Row(type, LicenseType.Pro, cycle, professional));
                rows.Add(Row(type, LicenseType.Elite, cycle, professional * EliteMultiple));
            }
        }

        return rows;
    }

    private static PlanPrice Row(BusinessType type, LicenseType plan, BillingCycle cycle, decimal amount) => new()
    {
        BusinessType = type,
        Plan = plan,
        BillingCycle = cycle,
        Amount = amount,
        CurrencyCode = "INR",
        IsActive = true,
    };
}
