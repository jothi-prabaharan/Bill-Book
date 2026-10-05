using System.Security.Claims;
using Shared.Kernel.Apps;

namespace Shared.Kernel.Internal;

/// <summary>
/// Extracts business type and add-ons scope from token claims or provides
/// backwards-compatible fallbacks from older token claims (D-36, TK-128).
/// </summary>
public static class ScopeClaims
{
    public const string BusinessTypeClaim = "business_type";
    public const string AddOnsClaim = "add_ons";
    public const string PlanTierClaim = "plan_tier";

    public static BusinessType? BusinessTypeOf(ClaimsPrincipal user)
    {
        string? claim = user.FindFirst(BusinessTypeClaim)?.Value;
        // AddOnRules reads a pre-TK-136 "General" as Retail, so a token minted
        // before the rename keeps working until it expires.
        if (claim is not null && AddOnRules.TryParseBusinessType(claim, out BusinessType? bt) && bt is not null)
        {
            return bt;
        }

        if (claim is not null)
        {
            return null;
        }

        // An add_ons claim with no business_type is a customer with no trade
        // (HRMS or Payroll only).
        if (user.FindFirst(AddOnsClaim) is not null)
        {
            return null;
        }

        return BusinessType.Retail;
    }

    public static AddOn AddOnsOf(ClaimsPrincipal user)
    {
        string? claim = user.FindFirst(AddOnsClaim)?.Value;
        if (claim is not null && int.TryParse(claim, out var intVal))
        {
            return (AddOn)intVal;
        }
        if (claim is not null && Enum.TryParse<AddOn>(claim, ignoreCase: true, out var parsed))
        {
            return parsed;
        }

        return AddOn.None;
    }

    public static Tenancy.PlanTier PlanTierOf(ClaimsPrincipal user)
    {
        string? claim = user.FindFirst(PlanTierClaim)?.Value
            ?? user.FindFirst("plan_type")?.Value;
        if (claim is not null && Enum.TryParse<Tenancy.PlanTier>(claim, ignoreCase: true, out var tier))
        {
            return tier;
        }
        return Tenancy.PlanTier.Standard;
    }
}
