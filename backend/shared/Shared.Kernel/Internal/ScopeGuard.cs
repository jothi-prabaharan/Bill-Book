using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Shared.Kernel.Apps;

namespace Shared.Kernel.Internal;

/// <summary>
/// Enforces branch business type and add-on scope guards on controllers and actions (D-36, TK-128).
/// </summary>
public static class ScopeGuard
{
    private const string GuardCheckedItemKey = "__ScopeGuardChecked";

    public static void Enforce(ActionExecutingContext context)
    {
        if (context.HttpContext.Items.ContainsKey(GuardCheckedItemKey))
        {
            return;
        }
        context.HttpContext.Items[GuardCheckedItemKey] = true;

        if (context.HttpContext.User.Identity?.IsAuthenticated != true)
        {
            return;
        }

        // Service-to-service internal calls skip user scope checks
        if (context.ActionDescriptor.EndpointMetadata.OfType<InternalOnlyAttribute>().Any())
        {
            return;
        }

        Type controllerType = context.Controller.GetType();
        var controllerBt = controllerType.GetCustomAttributes(typeof(RequireBusinessTypeAttribute), true)
            .OfType<RequireBusinessTypeAttribute>()
            .FirstOrDefault();
        var controllerAo = controllerType.GetCustomAttributes(typeof(RequireAddOnAttribute), true)
            .OfType<RequireAddOnAttribute>()
            .FirstOrDefault();

        var actionBt = (context.ActionDescriptor as Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor)?.MethodInfo
            .GetCustomAttributes(typeof(RequireBusinessTypeAttribute), true)
            .OfType<RequireBusinessTypeAttribute>()
            .LastOrDefault()
            ?? context.ActionDescriptor.EndpointMetadata.OfType<RequireBusinessTypeAttribute>().LastOrDefault();
        var actionAo = (context.ActionDescriptor as Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor)?.MethodInfo
            .GetCustomAttributes(typeof(RequireAddOnAttribute), true)
            .OfType<RequireAddOnAttribute>()
            .LastOrDefault()
            ?? context.ActionDescriptor.EndpointMetadata.OfType<RequireAddOnAttribute>().LastOrDefault();

        ClaimsPrincipal user = context.HttpContext.User;
        BusinessType? tokenBt = ScopeClaims.BusinessTypeOf(user);
        AddOn tokenAo = ScopeClaims.AddOnsOf(user);

        // Controller-level check
        if (controllerBt != null || controllerAo != null)
        {
            bool btMatch = controllerBt != null && controllerBt.Allows(tokenBt);
            bool aoMatch = controllerAo != null && controllerAo.Allows(tokenAo);

            bool controllerAllowed = (controllerBt, controllerAo) switch
            {
                (not null, not null) => btMatch || aoMatch,
                (not null, null) => btMatch,
                (null, not null) => aoMatch,
                _ => true,
            };

            if (!controllerAllowed)
            {
                context.Result = new ForbidResult();
                return;
            }
        }

        // Action-level check (further restricts if present)
        if (actionBt != null && !actionBt.Allows(tokenBt))
        {
            context.Result = new ForbidResult();
            return;
        }

        if (actionAo != null && !actionAo.Allows(tokenAo))
        {
            context.Result = new ForbidResult();
            return;
        }
    }
}
