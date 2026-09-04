using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;

namespace Shared.Kernel.Internal;

/// <summary>
/// Answers, over a whole assembly, which endpoints carry no authority check.
///
/// <b>Why this is reflection over an assembly rather than a list of routes.</b>
/// Every authorization hole this project has had was an <i>absence</i>: a
/// controller that looked exactly like its neighbours except for the attribute
/// nobody noticed was missing. <c>ApiClientsController</c>,
/// <c>CreditNotesController</c> and <c>PriceListsController</c> carried no
/// permission attribute; <c>InternalApiKeysController</c> was anonymous with no
/// shared-key guard; <c>GstController</c> served GSTR-1, GSTR-2 and GSTR-3B to
/// any signed-in user. The first four were found and fixed, and the claim
/// "every endpoint is behind a credential and a permission" was written down as
/// settled — by an audit that read the four files it had changed. Two more
/// turned up in a service nobody re-read.
///
/// A missing attribute is invisible in a file you are not reading, so the
/// question has to be asked of every file at once, by something that cannot get
/// bored. A controller added tomorrow with no guard fails the build.
///
/// <b>The four legitimate guards, and no fifth.</b>
///
/// <list type="bullet">
/// <item><see cref="RequireModulePermissionAttribute"/> — a staff route, whose
/// authority is <c>{module}.{action}</c>.</item>
/// <item><see cref="RequirePermissionAttribute"/> — one named permission on one
/// action, for routes whose authority is not the controller's module:
/// <c>platform.view</c> on an operator screen, <c>settings.edit</c> on a
/// per-customer mailbox.</item>
/// <item><see cref="InternalOnlyAttribute"/> — a service-to-service route,
/// authenticated by the shared key because the caller is a worker with no user
/// token.</item>
/// <item><see cref="RequirePortalAccessAttribute"/> — a client-portal route. A
/// contact reading their own statement holds no staff permission and never
/// should.</item>
/// </list>
///
/// A route with none of the four is relying on the <c>FallbackPolicy</c>, and
/// that is a credential rather than an authority: it proves somebody signed in,
/// never that they are entitled to what they asked for.
///
/// <b>Two exemptions, and they have to be named.</b> Sign-in and signup run
/// before a token exists, and the shell's own per-user data — the menu, the
/// branch's display formats — belongs to no module: every role needs it to draw
/// any screen at all, and the nearest permission, <c>settings.view</c>, is not
/// held by Accountant or Sales. Both kinds are passed in by the caller as
/// <c>exempt</c>, so an exemption is a line in a test that somebody had to
/// write, rather than an attribute that was quietly never added.
/// </summary>
public static class EndpointGuardAudit
{
    /// <summary>
    /// The controllers and actions in <paramref name="assembly"/> that carry no
    /// guard, named as <c>Controller.Action</c> or <c>Controller</c>.
    ///
    /// An action is guarded when it carries a guard itself or its controller
    /// does. <paramref name="exempt"/> names controllers that legitimately have
    /// none.
    /// </summary>
    public static IReadOnlyList<string> Unguarded(
        Assembly assembly, params string[] exempt)
    {
        var exemptions = exempt.ToHashSet(StringComparer.Ordinal);
        List<string> open = [];

        foreach (Type controller in Controllers(assembly))
        {
            if (exemptions.Contains(controller.Name))
            {
                continue;
            }

            if (HasGuard(controller))
            {
                continue;
            }

            // No controller-level guard, so every action must carry its own.
            // This is the shape Master uses: one controller serving an operator
            // screen and a customer screen, each action naming its own
            // authority.
            List<MethodInfo> actions = [.. Actions(controller).Where(a => !HasGuard(a))];

            if (actions.Count == 0 && Actions(controller).Any())
            {
                continue;
            }

            open.AddRange(actions.Count == 0
                ? [controller.Name]
                : actions.Select(a => $"{controller.Name}.{a.Name}"));
        }

        return open;
    }

    /// <summary>
    /// Every module named by a <see cref="RequireModulePermissionAttribute"/> in
    /// the assembly.
    ///
    /// <b>A module the catalogue does not seed is a permission nobody can
    /// hold</b>, so the route is unreachable for every role including Owner.
    /// That is how Leads and Tickets came to be locked to everyone: the schema
    /// merged Crm and Support into Customer and the controllers demanded
    /// <c>customer.*</c>, which is seeded nowhere.
    /// </summary>
    public static IReadOnlyList<string> DemandedModules(Assembly assembly) =>
        [.. Controllers(assembly)
            .SelectMany(c => c.GetCustomAttributes<RequireModulePermissionAttribute>(inherit: true))
            .Select(a => a.Module)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(m => m, StringComparer.Ordinal)];

    /// <summary>
    /// Every permission named by a <see cref="RequirePermissionAttribute"/>, at
    /// either level. Same argument as <see cref="DemandedModules"/>: a
    /// permission the catalogue does not seed cannot be granted.
    /// </summary>
    public static IReadOnlyList<string> DemandedPermissions(Assembly assembly) =>
        [.. Controllers(assembly)
            .SelectMany(c => c
                .GetCustomAttributes<RequirePermissionAttribute>(inherit: true)
                .Concat(Actions(c).SelectMany(a =>
                    a.GetCustomAttributes<RequirePermissionAttribute>(inherit: true))))
            .Select(a => a.Permission)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(p => p, StringComparer.Ordinal)];

    /// <summary>
    /// Controllers whose route takes an <c>orgId</c> or a <c>customerId</c>
    /// without the matching attribute comparing it to the token.
    ///
    /// <b>A permission is not a tenancy check.</b> <c>settings.edit</c> says
    /// this user may edit settings; it does not say <i>whose</i>. Where the id
    /// comes off the URL there is nothing else to stop one customer naming
    /// another's — three of Platform's controllers did exactly that, which is
    /// why the attributes exist.
    ///
    /// Reported per action, since the route value can be declared on either.
    /// </summary>
    public static IReadOnlyList<string> RouteTenantIdsNotChecked(
        Assembly assembly, params string[] exempt)
    {
        var exemptions = exempt.ToHashSet(StringComparer.Ordinal);
        List<string> open = [];

        foreach (Type controller in Controllers(assembly))
        {
            foreach (MethodInfo action in Actions(controller))
            {
                if (exemptions.Contains($"{controller.Name}.{action.Name}")
                    || exemptions.Contains(controller.Name))
                {
                    continue;
                }

                string template = RouteTemplate(controller) + " " + RouteTemplate(action);

                // `:guid` and nothing else. A tenant id is a Guid throughout this
                // product; a `{customerId:long}` is a *contact* — Sales calls the
                // party on an invoice the customer — and flagging those would
                // bury the real findings under a naming collision.
                bool namesOrg = template.Contains("{orgId:guid}", StringComparison.Ordinal);
                bool namesCustomer = template.Contains("{customerId:guid}", StringComparison.Ordinal);

                if (!namesOrg && !namesCustomer)
                {
                    continue;
                }

                // An internal route takes the organization as a parameter by
                // design — the caller is a worker with no token to compare it
                // to, and the shared key is what stands in for one.
                if (Has<InternalOnlyAttribute>(controller) || Has<InternalOnlyAttribute>(action))
                {
                    continue;
                }

                // A platform route is cross-customer by definition: an operator
                // is never signed in to any one customer, so there is no
                // customer_id claim to compare the route to and demanding one
                // would break the only screen that has to see across them all.
                // `platform.*` is the authority instead, and it is not granted
                // to any tenant role.
                if (Permissions(controller).Concat(Permissions(action))
                    .Any(p => p.StartsWith("platform.", StringComparison.Ordinal)))
                {
                    continue;
                }

                bool checkedOrg =
                    Has<OrgRouteMustMatchTokenAttribute>(controller)
                    || Has<OrgRouteMustMatchTokenAttribute>(action);

                bool checkedCustomer =
                    Has<CustomerRouteMustMatchTokenAttribute>(controller)
                    || Has<CustomerRouteMustMatchTokenAttribute>(action);

                if ((namesOrg && !checkedOrg) || (namesCustomer && !checkedCustomer))
                {
                    open.Add($"{controller.Name}.{action.Name}");
                }
            }
        }

        return open;
    }

    private static IEnumerable<Type> Controllers(Assembly assembly) =>
        assembly.GetTypes()
            .Where(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract)
            .OrderBy(t => t.Name, StringComparer.Ordinal);

    /// <summary>
    /// The action methods: public, declared here rather than inherited from
    /// <see cref="ControllerBase"/>, and not marked <c>[NonAction]</c>.
    /// </summary>
    private static IEnumerable<MethodInfo> Actions(Type controller) =>
        controller
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => !m.IsSpecialName)
            .Where(m => m.GetCustomAttribute<NonActionAttribute>() is null)
            .Where(m => m.GetCustomAttributes<HttpMethodAttribute>(inherit: true).Any());

    private static bool HasGuard(MemberInfo member) =>
        Has<RequireModulePermissionAttribute>(member)
        || Has<RequirePermissionAttribute>(member)
        || Has<InternalOnlyAttribute>(member)
        || Has<RequirePortalAccessAttribute>(member);

    private static IEnumerable<string> Permissions(MemberInfo member) =>
        member.GetCustomAttributes<RequirePermissionAttribute>(inherit: true)
            .Select(a => a.Permission);

    private static bool Has<T>(MemberInfo member) where T : Attribute =>
        member.GetCustomAttributes<T>(inherit: true).Any();

    private static string RouteTemplate(MemberInfo member) =>
        string.Join(
            ' ',
            member.GetCustomAttributes<RouteAttribute>(inherit: true)
                .Select(r => r.Template)
                .Concat(member.GetCustomAttributes<HttpMethodAttribute>(inherit: true)
                    .Select(h => h.Template ?? string.Empty)));
}
