namespace Master.Entity.Enums;

/// <summary>
/// How often a customer on a paid plan is billed (D-71, TK-211). Professional is
/// billed a quarter or half a year at a time; there is no monthly cycle.
/// </summary>
public enum BillingCycle
{
    Quarterly = 1,

    BiAnnual = 2,
}
