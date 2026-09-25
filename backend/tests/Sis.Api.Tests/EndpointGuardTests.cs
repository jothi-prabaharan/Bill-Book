using Shared.Kernel.Internal;
using Xunit;

namespace Sis.Api.Tests;

/// <summary>Every Sis endpoint carries a guard, names its apps, and demands only seeded modules (S1, TK-61).</summary>
public sealed class EndpointGuardTests
{
    private static System.Reflection.Assembly Service =>
        typeof(Sis.Api.Controllers.InternalSeedController).Assembly;

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

        Assert.Equal(string.Empty, string.Join(", ", EndpointGuardAudit.DemandedModules(Service).Where(m => !seeded.Contains(m))));
    }

    [Fact]
    public void Every_demanded_permission_is_one_the_catalogue_seeds()
    {
        HashSet<string> seeded = Master.Repository.AdminDbContext.PermissionModules
            .SelectMany(m => new[] { "view", "create", "edit", "approve", "void", "delete", "print", "export", "import", "AllUserData" }
                .Select(a => $"{m}.{a}"))
            .Concat(Master.Repository.AdminDbContext.ExtraPermissions.Select(e => $"{e.Module}.{e.Action}"))
            .ToHashSet(StringComparer.Ordinal);

        Assert.Equal(string.Empty, string.Join(", ", EndpointGuardAudit.DemandedPermissions(Service).Where(p => !seeded.Contains(p))));
    }

    [Fact]
    public void Every_route_that_names_a_tenant_id_compares_it_to_the_token() =>
        Assert.Equal(string.Empty, string.Join(", ", EndpointGuardAudit.RouteTenantIdsNotChecked(Service)));
}
