using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Shared.Kernel.Internal;

/// <summary>
/// Names the permission action a single route needs, replacing the one the HTTP
/// method would imply.
///
/// <b>An override, not an extra check.</b> A route marked
/// <c>[PermissionAction("void")]</c> requires <c>{module}.void</c> and
/// <b>not</b> <c>{module}.edit</c> — the two are different authorities and a
/// clerk who may raise invoices is precisely the person who should not be able
/// to withdraw one. Adding a second requirement on top of <c>.edit</c> would
/// instead mean only people who can edit may void, which is the opposite of the
/// separation the permission exists to express.
///
/// Use it sparingly. The method-derived mapping on
/// <see cref="RequireModulePermissionAttribute"/> is right for almost every
/// route; this is for the handful that are a different authority wearing a
/// POST — void, approve, and print.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class PermissionActionAttribute : Attribute
{
    public PermissionActionAttribute(string action) => Action = action;

    /// <summary>The bare action — <c>void</c>, <c>approve</c>, <c>print</c>. No module prefix.</summary>
    public string Action { get; }
}

/// <summary>
/// Requires the caller to hold the right permission in a module, working out
/// which one from the HTTP method: a read needs <c>.view</c>, a create or an
/// update needs <c>.edit</c>, a delete needs <c>.delete</c>.
///
/// One attribute per controller rather than one per action, deliberately. The
/// alternative is roughly a hundred hand-written attributes across twenty-three
/// controllers, each an opportunity to tag a delete as a read — and the mapping
/// they would encode is the same three lines every time. Here it is those three
/// lines, in one place, where it can be read and argued with.
///
/// <b>The module is the data's owner, not the menu it appears under.</b> Tax
/// rates live on a Settings screen and belong to Accounting, so they take
/// <c>accounting.*</c>; an accountant who could not edit a GST rate because it
/// is filed under Settings would be a menu deciding an access rule.
///
/// POST is treated as <c>.edit</c> rather than <c>.create</c> because most POSTs
/// here are state changes — set-default, deactivate, reorder — not insertions,
/// and the seeded roles grant a module's permissions all together anyway. A
/// customer-defined role that wants to separate them can, and this is the line
/// to change when one does.
///
/// <b>Trading documents are where three lines stopped being enough</b> (T0.4).
/// <c>sales.void</c> and <c>sales.approve</c> have been seeded and granted since
/// the beginning and nothing ever read one, so voiding an invoice was reachable
/// by anyone who could edit one. A route says which authority it needs with
/// <see cref="PermissionActionAttribute"/>, and that <i>replaces</i> the derived
/// action rather than adding to it — see the note there for why the distinction
/// matters.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class RequireModulePermissionAttribute : Attribute, IActionFilter
{
    public RequireModulePermissionAttribute(string module) => Module = module;

    public string Module { get; }

    /// <summary>
    /// The action a request needs, given its method and whatever the route
    /// declared. Public and static so a test can assert the mapping without
    /// standing up a filter pipeline, and so the frontend's guard can be checked
    /// against the same table.
    /// </summary>
    public static string ActionFor(string httpMethod, string? declaredAction)
    {
        if (!string.IsNullOrWhiteSpace(declaredAction))
        {
            return declaredAction.Trim().ToLowerInvariant();
        }

        return httpMethod.ToUpperInvariant() switch
        {
            "GET" or "HEAD" or "OPTIONS" => "view",
            "DELETE" => "delete",
            _ => "edit",
        };
    }

    public void OnActionExecuting(ActionExecutingContext context)
    {
        // EndpointMetadata carries the action's own attributes, so a route can
        // declare its authority beside the route itself rather than in a list
        // somewhere else that would fall out of step with it.
        string? declared = context.ActionDescriptor.EndpointMetadata
            .OfType<PermissionActionAttribute>()
            .FirstOrDefault()?.Action;

        string required = $"{Module}.{ActionFor(context.HttpContext.Request.Method, declared)}";

        bool held = context.HttpContext.User
            .FindAll("permission")
            .Any(c => string.Equals(c.Value, required, StringComparison.OrdinalIgnoreCase));

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
