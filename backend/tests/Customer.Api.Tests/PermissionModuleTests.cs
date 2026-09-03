using System.Reflection;
using Master.Repository;
using Microsoft.AspNetCore.Mvc;
using Shared.Kernel.Internal;
using Xunit;

namespace Customer.Api.Tests;

/// <summary>
/// Every module a controller demands has to be a module the permission
/// catalogue actually seeds.
///
/// <b>A module nobody can hold is a locked door, not a refused request.</b>
/// Leads and Tickets shipped asking for <c>customer.*</c>, which is seeded
/// nowhere: the catalogue carries <c>crm.*</c> and <c>support.*</c>, the two
/// halves this service was merged from. The schemas merged and the permissions
/// did not, so every request to either controller was refused for every role,
/// including Owner — while the menu still showed the pages, because the
/// browser-side guard was asking for a different permission again
/// (<c>contacts.view</c>, which people do hold).
///
/// This is the same shape as the <c>platform.*</c> gap that is still open by
/// decision: seeding a permission and granting it are different acts, and a
/// controller naming a module that was never seeded fails silently in the
/// direction of refusing everyone. Nothing else in the suite reads the two
/// sides against each other, which is why it survived being written down in
/// three places.
/// </summary>
public sealed class PermissionModuleTests
{
    [Fact]
    public void Every_controller_demands_a_module_the_catalogue_seeds()
    {
        // The catalogue itself, not a copy of it — a copy would agree with
        // itself forever while the seed moved underneath it.
        IReadOnlySet<string> seeded =
            AdminDbContext.PermissionModules.ToHashSet(StringComparer.Ordinal);

        // Proves the catalogue was actually read rather than silently empty —
        // an empty set would make the assertion below vacuous.
        Assert.Contains("crm", seeded);
        Assert.Contains("support", seeded);

        List<string> unknown = [];

        foreach (Type controller in typeof(Customer.Api.Controllers.LeadsController).Assembly
            .GetTypes()
            .Where(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract))
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
