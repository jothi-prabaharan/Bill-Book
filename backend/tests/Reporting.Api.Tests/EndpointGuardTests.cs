using System.Reflection;
using Master.Repository;
using Microsoft.AspNetCore.Mvc;
using Shared.Kernel.Internal;
using Xunit;

namespace Reporting.Api.Tests;

/// <summary>
/// Every controller in this service has to carry a guard, and the guard has to
/// be one a caller can actually satisfy.
///
/// <b>A FallbackPolicy is a credential, not an authority.</b> Reporting sets
/// <c>RequireAuthenticatedUser</c>, and that made two open routes look closed.
/// <c>GstController</c> served GSTR-1, GSTR-2 and GSTR-3B — the branch's whole
/// outward supply position — to any signed-in user, because nothing asked which
/// user; the Sales role holds no <c>reports</c> permission and could read the
/// returns anyway. <c>InternalCreditCheckController</c> failed the other way:
/// the fallback demanded a user token from a caller that has none, since Sales'
/// <c>CreditCheckClient</c> is registered with <c>InternalKeyHandler</c> and
/// sends the shared key alone.
///
/// Both survived an audit of the controller attributes because the absence of
/// an attribute is what you have to notice, and the two that mattered were
/// missing from files that otherwise looked like their neighbours. So this
/// asserts over the whole assembly rather than over the routes someone thought
/// to list: a controller added tomorrow with no guard fails the build.
/// </summary>
public sealed class EndpointGuardTests
{
    private static IEnumerable<Type> Controllers =>
        typeof(Reporting.Api.Controllers.ReportsController).Assembly
            .GetTypes()
            .Where(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract);

    [Fact]
    public void Every_controller_is_guarded()
    {
        List<string> unguarded = [];

        foreach (Type controller in Controllers)
        {
            bool internalOnly = controller
                .GetCustomAttributes<InternalOnlyAttribute>(inherit: true).Any();

            bool moduleGuarded = controller
                .GetCustomAttributes<RequireModulePermissionAttribute>(inherit: true).Any();

            bool portalGuarded = controller
                .GetCustomAttributes<RequirePortalAccessAttribute>(inherit: true).Any();

            // Three legitimate guards, and no fourth. A staff route takes a
            // module permission; a service route takes the shared internal key;
            // a client-portal route takes the portal claims, because a customer
            // reading their own statement holds no staff permission and never
            // should. Nothing legitimately takes none of the three — that is
            // the FallbackPolicy standing in for an authority it cannot
            // express.
            if (!internalOnly && !moduleGuarded && !portalGuarded)
            {
                unguarded.Add(controller.Name);
            }
        }

        Assert.Empty(unguarded);
    }

    [Fact]
    public void Every_demanded_module_is_one_the_catalogue_seeds()
    {
        IReadOnlySet<string> seeded =
            AdminDbContext.PermissionModules.ToHashSet(StringComparer.Ordinal);

        // Proves the catalogue was read rather than silently empty, which would
        // make the assertion below vacuous.
        Assert.Contains("reports", seeded);

        List<string> unknown = [];

        foreach (Type controller in Controllers)
        {
            foreach (RequireModulePermissionAttribute attribute in
                controller.GetCustomAttributes<RequireModulePermissionAttribute>(inherit: true))
            {
                if (!seeded.Contains(attribute.Module))
                {
                    unknown.Add($"{controller.Name} demands \"{attribute.Module}\"");
                }
            }
        }

        Assert.Empty(unknown);
    }
}
