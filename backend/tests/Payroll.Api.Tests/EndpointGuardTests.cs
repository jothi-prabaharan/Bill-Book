using Shared.Kernel.Internal;
using Xunit;

namespace Payroll.Api.Tests;

/// <summary>Every Payroll endpoint carries a guard, names its apps, and demands only seeded modules (TK-51).</summary>
public sealed class EndpointGuardTests
{
    private static System.Reflection.Assembly Service =>
        typeof(Payroll.Api.Controllers.PayrollRunsController).Assembly;

    [Fact]
    public void Every_endpoint_carries_a_guard() =>
        Assert.Equal(string.Empty, string.Join(", ", EndpointGuardAudit.Unguarded(Service)));

    [Fact]
    public void Every_controller_names_its_apps() =>
        Assert.Equal(string.Empty, string.Join(", ", EndpointGuardAudit.WithoutApp(Service)));

    [Fact]
    public void Every_demanded_module_is_one_the_catalogue_seeds()
    {
        IReadOnlySet<string> seeded =
            Master.Repository.AdminDbContext.PermissionModules.ToHashSet(StringComparer.Ordinal);

        Assert.Contains("payroll", seeded);
        Assert.Equal(string.Empty, string.Join(", ", EndpointGuardAudit.DemandedModules(Service).Where(m => !seeded.Contains(m))));
    }

    [Fact]
    public void Every_route_that_names_a_tenant_id_compares_it_to_the_token() =>
        Assert.Equal(string.Empty, string.Join(", ", EndpointGuardAudit.RouteTenantIdsNotChecked(Service)));
}
