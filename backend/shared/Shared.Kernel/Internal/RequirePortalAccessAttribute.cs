using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Shared.Kernel.Internal;

/// <summary>
/// Guards a client-portal route, where the caller is one of the branch's own
/// contacts rather than a member of staff.
///
/// <b>The third kind of guard, and the reason it needs to be an attribute.</b>
/// A staff route takes a module permission and a service route takes the shared
/// internal key; a portal route takes neither, because a customer signing in to
/// look at their own statement holds no <c>reports.view</c> and never should.
/// What it holds is <c>portal_access</c> and a <c>contact_id</c>, minted by
/// JwtTokenService.
///
/// That check was written inside the action, which made it correct and
/// invisible: a controller carrying no guard attribute is indistinguishable, to
/// anyone reading the attributes or to a test reflecting over them, from one
/// that was never guarded at all — and this service shipped two that were
/// never guarded at all. Declaring it here puts the portal on the same footing
/// as the other two, so the assertion that every controller is guarded can be
/// made over the whole assembly instead of over a list with exceptions on it.
///
/// The action still reads <c>contact_id</c> itself, because it needs the value
/// and not merely the fact of it. This guarantees the claim is present and
/// parses, so what the action reads cannot be absent.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class RequirePortalAccessAttribute : Attribute, IActionFilter
{
    public const string AccessClaim = "portal_access";
    public const string ContactClaim = "contact_id";

    public void OnActionExecuting(ActionExecutingContext context)
    {
        if (context.HttpContext.User.FindFirst(AccessClaim)?.Value != "true")
        {
            context.Result = new ForbidResult();
            return;
        }

        // A portal token without a usable contact is a token that names no
        // statement to read. Refused here rather than left to each action.
        string? contactId = context.HttpContext.User.FindFirst(ContactClaim)?.Value;

        if (string.IsNullOrEmpty(contactId) || !long.TryParse(contactId, out _))
        {
            context.Result = new ForbidResult();
        }
    }

    public void OnActionExecuted(ActionExecutedContext context)
    {
        // Nothing to do after the action; the check is entirely up front.
    }
}
