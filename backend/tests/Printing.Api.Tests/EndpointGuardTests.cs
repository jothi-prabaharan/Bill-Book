using Shared.Kernel.Internal;
using Xunit;

namespace Printing.Api.Tests;

/// <summary>
/// Every endpoint in this service carries an authority check, and every
/// authority it names is one the catalogue can grant. Asserted over the whole
/// assembly, as every other service does — see <see cref="EndpointGuardAudit"/>.
/// </summary>
public sealed class EndpointGuardTests
{
    private static System.Reflection.Assembly Service =>
        typeof(Printing.Api.Controllers.PrintTemplatesController).Assembly;

    [Fact]
    public void Every_endpoint_carries_a_guard()
    {
        // One exemption:
        //
        //   PrintController — renders a payload its own service pushed. The
        //                     authority to print a document is the authority to
        //                     read it, checked by that service before it built
        //                     the payload; one route prints twelve document types
        //                     across three modules, so no single module guard
        //                     fits. It reads only the branch's own template. See
        //                     the note on PrintController.
        Assert.Equal(
            string.Empty,
            string.Join(", ", EndpointGuardAudit.Unguarded(Service, "PrintController")));
    }

    /// <summary>
    /// Every controller a user token can reach names the apps it serves
    /// (H0.2, TK-43), so a Payroll token cannot reach a RetailErp screen through
    /// a permission both apps share.
    /// </summary>
    [Fact]
    public void Every_controller_names_its_apps()
    {
        Assert.Equal(string.Empty, string.Join(", ", EndpointGuardAudit.WithoutApp(Service)));
    }

    [Fact]
    public void Every_demanded_module_is_one_the_catalogue_seeds()
    {
        IReadOnlySet<string> seeded =
            Master.Repository.AdminDbContext.PermissionModules.ToHashSet(StringComparer.Ordinal);

        Assert.NotEmpty(seeded);

        List<string> demanded = [.. EndpointGuardAudit.DemandedModules(Service)];

        // Proves the template controller was seen, which would otherwise make
        // the assertion below vacuous.
        Assert.Contains("settings", demanded);

        Assert.Equal(string.Empty, string.Join(", ", demanded.Where(m => !seeded.Contains(m))));
    }

    [Fact]
    public void Every_demanded_permission_is_one_the_catalogue_seeds()
    {
        IReadOnlySet<string> modules =
            Master.Repository.AdminDbContext.PermissionModules.ToHashSet(StringComparer.Ordinal);

        List<string> unknown = [.. EndpointGuardAudit.DemandedPermissions(Service)
            .Where(p => !modules.Contains(p.Split('.')[0]))];

        Assert.Equal(string.Empty, string.Join(", ", unknown));
    }

    [Fact]
    public void A_tenant_id_taken_from_a_route_is_checked_against_the_token()
    {
        Assert.Equal(
            string.Empty,
            string.Join(", ", EndpointGuardAudit.RouteTenantIdsNotChecked(Service)));
    }
}
