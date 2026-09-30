using Microsoft.AspNetCore.Mvc.Filters;
using Shared.Kernel.Apps;

namespace Shared.Kernel.Internal;

/// <summary>
/// Restricts access to a controller or action to organizations with the specified add-on enabled (D-36, TK-128).
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public sealed class RequireAddOnAttribute : Attribute, IActionFilter
{
    public AddOn AddOn { get; }

    public RequireAddOnAttribute(AddOn addOn)
    {
        AddOn = addOn;
    }

    public bool Allows(AddOn tokenAddOns)
    {
        return (tokenAddOns & AddOn) != 0;
    }

    public void OnActionExecuting(ActionExecutingContext context) => ScopeGuard.Enforce(context);

    public void OnActionExecuted(ActionExecutedContext context) { }
}
