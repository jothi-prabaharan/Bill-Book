using Microsoft.AspNetCore.Mvc.Filters;
using Shared.Kernel.Apps;

namespace Shared.Kernel.Internal;

/// <summary>
/// Restricts access to a controller or action by the branch's business vertical (D-36, TK-128).
/// <para>
/// When specified with no parameters, requires any branch context.
/// When combined with <see cref="RequireAddOnAttribute"/> on the same controller, access is permitted
/// if either the business type or an add-on requirement matches.
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public sealed class RequireBusinessTypeAttribute : Attribute, IActionFilter
{
    public IReadOnlyList<BusinessType> BusinessTypes { get; }
    public bool AllowStandalone { get; init; }

    public RequireBusinessTypeAttribute(params BusinessType[] businessTypes)
    {
        BusinessTypes = businessTypes;
    }

    public bool Allows(BusinessType? tokenType)
    {
        if (AllowStandalone)
        {
            if (BusinessTypes.Count == 0 || tokenType is null)
            {
                return true;
            }
        }

        if (BusinessTypes.Count == 0)
        {
            return tokenType.HasValue;
        }

        return tokenType.HasValue && BusinessTypes.Contains(tokenType.Value);
    }

    public void OnActionExecuting(ActionExecutingContext context) => ScopeGuard.Enforce(context);

    public void OnActionExecuted(ActionExecutedContext context) { }
}
