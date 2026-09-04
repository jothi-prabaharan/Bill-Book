using Shared.Kernel.Internal;
using Xunit;

namespace Sales.Api.Tests;

/// <summary>
/// Every endpoint in this service carries an authority check, and every
/// authority it names is one the catalogue can grant.
///
/// <b>Asserted over the whole assembly, because every hole this project has had
/// was an absence.</b> A controller missing its attribute looks exactly like its
/// neighbours, and reading the files you changed cannot find one in a file you
/// did not change — which is how the claim "every endpoint is behind a
/// credential and a permission" came to be written down as settled twice while
/// two open routes were still serving. See <see cref="EndpointGuardAudit"/>.
///
/// Reporting had this test and the other six did not. Now they all do.
/// </summary>
public sealed class EndpointGuardTests
{
    private static System.Reflection.Assembly Service =>
        typeof(Sales.Api.Controllers.CreditNotesController).Assembly;

    [Fact]
    public void Every_endpoint_carries_a_guard()
    {
        // Compared as joined text rather than as an empty collection, so a
        // failure names every open endpoint instead of the first few.
        Assert.Equal(
            string.Empty,
            string.Join(", ", EndpointGuardAudit.Unguarded(Service)));
    }

    [Fact]
    public void Every_demanded_module_is_one_the_catalogue_seeds()
    {
        IReadOnlySet<string> seeded =
            Master.Repository.AdminDbContext.PermissionModules.ToHashSet(StringComparer.Ordinal);

        // Proves the catalogue was read rather than silently empty, which would
        // make the assertion below vacuous.
        Assert.NotEmpty(seeded);

        List<string> unknown =
            [.. EndpointGuardAudit.DemandedModules(Service).Where(m => !seeded.Contains(m))];

        Assert.Equal(string.Empty, string.Join(", ", unknown));
    }

    [Fact]
    public void Every_demanded_permission_is_one_the_catalogue_seeds()
    {
        IReadOnlySet<string> modules =
            Master.Repository.AdminDbContext.PermissionModules.ToHashSet(StringComparer.Ordinal);

        // A named permission is `{module}.{action}`; the module half has to
        // exist or nothing can grant it.
        List<string> unknown = [.. EndpointGuardAudit.DemandedPermissions(Service)
            .Where(p => !modules.Contains(p.Split('.')[0]))];

        Assert.Equal(string.Empty, string.Join(", ", unknown));
    }

    [Fact]
    public void A_tenant_id_taken_from_a_route_is_checked_against_the_token()
    {
        // A permission says this user may do the thing; it never says whose
        // books they may do it to. Where the id comes off the URL, only the
        // route-matches-token attribute stands between one customer and
        // another's.
        Assert.Equal(
            string.Empty,
            string.Join(", ", EndpointGuardAudit.RouteTenantIdsNotChecked(Service)));
    }
}
