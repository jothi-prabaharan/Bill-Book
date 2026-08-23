using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Shared.Kernel.Internal;

/// <summary>
/// Refuses an action whose route names an organization the caller's token does
/// not. <c>CLAUDE.md</c>: validate the caller's OrgId against the target
/// resource's, always, and answer <c>Forbid()</c> rather than <c>NotFound()</c>.
///
/// An attribute rather than a check in each action because the failure mode is
/// omission. Three of Platform's controllers took the organization straight off
/// the URL with nothing comparing it to the token, and the next action added to
/// one of them would have done the same — a per-action check is only as good as
/// whoever remembers to write it.
///
/// <b>Only for endpoints that read the org from the route.</b> A service whose
/// query filter already scopes by <c>OrgId</c> does not need this; a master
/// database like <c>mst</c> has no filter to fall back on, which is exactly why
/// these three needed it.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class OrgRouteMustMatchTokenAttribute : Attribute, IActionFilter
{
    public const string RouteKey = "orgId";
    public const string ClaimType = "org_id";

    public void OnActionExecuting(ActionExecutingContext context)
    {
        if (!TenantClaims.RouteMatchesClaim(context, RouteKey, ClaimType))
        {
            context.Result = new ForbidResult();
        }
    }

    public void OnActionExecuted(ActionExecutedContext context)
    {
        // Nothing to do after the action; the check is entirely up front.
    }
}

/// <summary>
/// The same rule for a route naming a customer — the head office rather than a
/// branch. Used where the resource belongs to the account rather than to one set
/// of books, which for now is the per-customer mail account.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class CustomerRouteMustMatchTokenAttribute : Attribute, IActionFilter
{
    public const string RouteKey = "customerId";
    public const string ClaimType = "customer_id";

    public void OnActionExecuting(ActionExecutingContext context)
    {
        if (!TenantClaims.RouteMatchesClaim(context, RouteKey, ClaimType))
        {
            context.Result = new ForbidResult();
        }
    }

    public void OnActionExecuted(ActionExecutedContext context)
    {
        // Nothing to do after the action; the check is entirely up front.
    }
}

/// <summary>
/// Requires a named permission claim. Permissions are minted into every access
/// token by Identity and, until now, read by nothing on the server — so this is
/// the first place one is actually enforced.
///
/// It is used only where there is no other boundary: the platform's own settings
/// belong to the operator, not to any customer, so no tenant claim can protect
/// them. Applying permissions across the whole API is a larger piece of work and
/// is tracked separately (master.md 5.17).
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class RequirePermissionAttribute : Attribute, IActionFilter
{
    public RequirePermissionAttribute(string permission) => Permission = permission;

    public string Permission { get; }

    public void OnActionExecuting(ActionExecutingContext context)
    {
        bool held = context.HttpContext.User
            .FindAll("permission")
            .Any(c => string.Equals(c.Value, Permission, StringComparison.OrdinalIgnoreCase));

        if (!held)
        {
            context.Result = new ForbidResult();
        }
    }

    public void OnActionExecuted(ActionExecutedContext context)
    {
        // Nothing to do after the action; the check is entirely up front.
    }
}

internal static class TenantClaims
{
    /// <summary>
    /// True when the route value and the claim are both present and equal as
    /// Guids. Anything else — a missing claim, a missing route value, an
    /// unparseable one — is false, so a malformed request is refused rather than
    /// compared loosely.
    /// </summary>
    internal static bool RouteMatchesClaim(
        ActionExecutingContext context, string routeKey, string claimType)
    {
        if (!context.RouteData.Values.TryGetValue(routeKey, out object? routeValue)
            || !Guid.TryParse(routeValue?.ToString(), out Guid fromRoute))
        {
            return false;
        }

        return Guid.TryParse(context.HttpContext.User.FindFirstValue(claimType), out Guid fromToken)
            && fromToken == fromRoute;
    }
}
