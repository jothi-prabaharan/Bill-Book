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

    public static BusinessType? BusinessTypeOf(ClaimsPrincipal user)
    {
        string? claim = user.FindFirst(BusinessTypeClaim)?.Value;
        if (claim is not null && Enum.TryParse<BusinessType>(claim, ignoreCase: true, out var bt))
        {
            return bt;
        }

        if (claim is not null)
        {
            return null;
        }

        // If add_ons claim is present without business_type claim, this is a token
        // without a business type (standalone add-on like HRMS/Payroll).
        if (user.FindFirst(AddOnsClaim) is not null)
        {
            return null;
        }

        // Backwards compatibility with legacy 'app' claim:
        string? appClaim = user.FindFirst(RequireAppAttribute.ClaimType)?.Value;
        if (appClaim is not null && Enum.TryParse<App>(appClaim, ignoreCase: true, out var app))
        {
            if (app == App.School) return BusinessType.School;
            if (app == App.RetailErp) return BusinessType.General;
            return null;
        }

        return BusinessType.General;
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

        // Backwards compatibility with 'app' claim:
        string? appClaim = user.FindFirst(RequireAppAttribute.ClaimType)?.Value;
        if (appClaim is not null && Enum.TryParse<App>(appClaim, ignoreCase: true, out var app))
        {
            AddOn res = AddOn.None;
            if (app.HasFlag(App.Hrms)) res |= AddOn.Hrms;
            if (app.HasFlag(App.Payroll)) res |= AddOn.Payroll;
            if (app.HasFlag(App.RetailErp)) res |= AddOn.All;
            return res;
        }

        return AddOn.None;
    }
}
