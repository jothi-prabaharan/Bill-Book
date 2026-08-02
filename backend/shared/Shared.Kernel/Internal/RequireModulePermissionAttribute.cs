using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Shared.Kernel.Internal;

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
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class RequireModulePermissionAttribute : Attribute, IActionFilter
{
    public RequireModulePermissionAttribute(string module) => Module = module;

    public string Module { get; }

    public void OnActionExecuting(ActionExecutingContext context)
    {
        string action = context.HttpContext.Request.Method switch
        {
            "GET" or "HEAD" or "OPTIONS" => "view",
            "DELETE" => "delete",
            _ => "edit",
        };

        string required = $"{Module}.{action}";

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
