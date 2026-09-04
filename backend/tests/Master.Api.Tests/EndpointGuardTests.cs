using Shared.Kernel.Internal;
using Xunit;

namespace Master.Api.Tests;

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
        typeof(Master.Api.Controllers.AuthController).Assembly;

    [Fact]
    public void Every_endpoint_carries_a_guard()
    {
        // Compared as joined text rather than as an empty collection, so a
        // failure names every open endpoint instead of the first few.
        //
        // Four exemptions, each a route with no module authority to name rather
        // than a route somebody forgot:
        //
        //   AuthController      — sign-in, password reset, refresh and logout
        //                         all run before a token exists, or take the
        //                         token itself as the credential.
        //   CustomersController — public self-service signup, and the status
        //                         poll the signup screen makes while waiting for
        //                         it. Both are pre-token by definition; the
        //                         status route is addressed by an unguessable
        //                         Guid, which is the only thing standing in for
        //                         a credential that does not exist yet.
        //   MasterController    — global reference data: countries, states,
        //                         currencies, HSN/SAC, ledger and account types.
        //                         Not scoped by customer at all, so there is no
        //                         tenant authority to check; countries and
        //                         states are additionally anonymous because the
        //                         signup form needs them.
        //   FormatsController   — the branch's date and money formats. Every
        //   MenuController        role needs both to draw any screen, and the
        //                         nearest permission, settings.view, is not held
        //                         by Accountant or Sales. Shell data, not a
        //                         module's. See the note on FormatsController.
        //
        // An exemption is a line here that somebody had to write and defend,
        // which is the difference between this and an attribute nobody added.
        Assert.Equal(
            string.Empty,
            string.Join(", ", EndpointGuardAudit.Unguarded(
                Service,
                "AuthController",
                "CustomersController",
                "MasterController",
                "FormatsController",
                "MenuController")));
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
        // Two exemptions, both branch-management routes that take an {orgId}
        // and resolve it *within the caller's own customer* rather than against
        // the token's org.
        //
        // OrgRouteMustMatchToken would be the wrong check here: it compares to
        // the branch the caller is signed in to, and administering the other
        // branches of your own account is exactly what these screens are for.
        // The real invariant is that the org is only ever looked up with
        // `CustomerId == <the token's customer>` beside it — OrganizationService
        // does that in every method, and LicenseService the same — so a branch
        // belonging to another customer resolves to nothing rather than to
        // somebody else's books.
        //
        // Named here rather than waved through, because the invariant lives in a
        // service method and the next one added could forget it.
        Assert.Equal(
            string.Empty,
            string.Join(", ", EndpointGuardAudit.RouteTenantIdsNotChecked(
                Service,
                "OrganizationsController",
                "LicensesController.UpgradeBranch",
                "CustomersController.GetStatus")));
    }
}
