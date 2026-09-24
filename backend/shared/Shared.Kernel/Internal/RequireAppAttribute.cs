using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Shared.Kernel.Apps;

namespace Shared.Kernel.Internal;

/// <summary>
/// Names the apps whose tokens may call a controller or an action (H0.2, TK-43).
///
/// <b>The app check is separate from the permission check, and it has to be.</b>
/// A permission can belong to several apps: <c>settings.view</c> is every app's.
/// So a Payroll token that holds <c>settings.view</c> would reach RetailErp's
/// tax master through the permission alone. This attribute is what refuses it:
/// the tax master's controller names <see cref="App.RetailErp"/>, and the token
/// says <c>app = Payroll</c>.
///
/// <b>How the token's app is read:</b>
/// <list type="bullet">
/// <item>the <c>app</c> claim, by name;</item>
/// <item><b>no claim means RetailErp.</b> Tokens minted before apps existed,
/// API keys, portal tokens and service calls carry none, and every one of
/// them is a RetailErp caller;</item>
/// <item>a claim that names no single app is refused.</item>
/// </list>
///
/// An unauthenticated request passes through: sign-in and signup run before any
/// token exists, and the fallback policy and the other guards decide those.
///
/// Declared on an action, it replaces the controller's for that action.
/// Every controller that is not <see cref="InternalOnlyAttribute"/> must carry
/// one; <see cref="EndpointGuardAudit.WithoutApp"/> checks it.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public sealed class RequireAppAttribute : Attribute, IActionFilter
{
    /// <summary>The token claim carrying the app a token was minted for.</summary>
    public const string ClaimType = "app";

    public RequireAppAttribute(App apps) => Apps = apps;

    public App Apps { get; }

    /// <summary>
    /// The app a set of claims was minted for: the <c>app</c> claim, RetailErp
    /// when there is none, and <see cref="App.None"/> when it names nothing valid.
    /// </summary>
    public static App AppOf(System.Security.Claims.ClaimsPrincipal user)
    {
        string? claim = user.FindFirst(ClaimType)?.Value;
        if (claim is null)
        {
            return App.RetailErp;
        }

        return AppRules.TryParseSingle(claim, out App app) ? app : App.None;
    }

    /// <summary>Whether a token for <paramref name="tokenApp"/> may call a route open to <paramref name="allowed"/>.</summary>
    public static bool Allows(App allowed, App tokenApp) =>
        AppRules.IsSingle(tokenApp) && allowed.HasFlag(tokenApp);

    public void OnActionExecuting(ActionExecutingContext context)
    {
        // The most specific declaration wins: metadata lists the controller's
        // attributes before the action's, so the last one is the action's own
        // when it has one. Only that instance acts, so an action-level
        // declaration really replaces the controller's rather than adding to it.
        RequireAppAttribute? effective = context.ActionDescriptor.EndpointMetadata
            .OfType<RequireAppAttribute>()
            .LastOrDefault();

        if (!ReferenceEquals(effective, this))
        {
            return;
        }

        if (context.HttpContext.User.Identity?.IsAuthenticated != true)
        {
            return;
        }

        if (!Allows(Apps, AppOf(context.HttpContext.User)))
        {
            context.Result = new ForbidResult();
        }
    }

    public void OnActionExecuted(ActionExecutedContext context)
    {
    }
}
