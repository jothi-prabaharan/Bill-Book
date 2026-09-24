using Master.Api.Services;
using Master.Entity.TableEntities;
using Master.Repository;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Master.Api.Tests;

/// <summary>
/// Which roles an API key may hold and what they let it do (D-07, TK-29): a
/// system or own-customer role's codes, never <c>platform.*</c>, and nothing
/// from another customer's role, an inactive one, or role 0.
/// </summary>
[Collection(nameof(AdminCollection))]
public sealed class ApiClientRoleTests
{
    private readonly AdminFixture _admin;

    public ApiClientRoleTests(AdminFixture admin) => _admin = admin;

    private static async Task<int> PermissionIdAsync(AdminDbContext db, string code) =>
        await db.Permissions.Where(p => p.Code == code).Select(p => p.PermissionId).SingleAsync();

    private static async Task<Role> RoleAsync(AdminDbContext db, Guid? customerId, bool active, params string[] codes)
    {
        string name = $"KEY{Guid.NewGuid():N}"[..20];
        var role = new Role { CustomerId = customerId, SystemName = name, DisplayName = name, IsActive = active };
        db.Roles.Add(role);
        await db.SaveChangesAsync();

        foreach (string code in codes)
        {
            db.RolePermissions.Add(new RolePermission { RoleId = role.RoleId, PermissionId = await PermissionIdAsync(db, code) });
        }

        await db.SaveChangesAsync();
        return role;
    }

    [SkippableFact]
    public async Task A_key_carries_its_roles_codes_and_never_a_platform_one()
    {
        Skip.If(_admin.SkipReason is not null, _admin.SkipReason ?? string.Empty);

        await using AdminDbContext db = _admin.CreateContext();
        Guid customer = Guid.NewGuid();
        Role role = await RoleAsync(db, customer, active: true, "sales.view", "platform.view");

        List<string> codes = await ApiClientRoles.PermissionsAsync(db, customer, role.RoleId, default);

        Assert.Equal(["sales.view"], codes);
    }

    [SkippableFact]
    public async Task Another_customers_role_an_inactive_role_and_role_zero_grant_nothing()
    {
        Skip.If(_admin.SkipReason is not null, _admin.SkipReason ?? string.Empty);

        await using AdminDbContext db = _admin.CreateContext();
        Guid customer = Guid.NewGuid();
        Role theirs = await RoleAsync(db, Guid.NewGuid(), active: true, "sales.view");
        Role inactive = await RoleAsync(db, customer, active: false, "sales.view");

        Assert.Empty(await ApiClientRoles.PermissionsAsync(db, customer, theirs.RoleId, default));
        Assert.Empty(await ApiClientRoles.PermissionsAsync(db, customer, inactive.RoleId, default));
        Assert.Empty(await ApiClientRoles.PermissionsAsync(db, customer, 0, default));
    }

    [SkippableFact]
    public async Task Only_own_or_system_roles_without_platform_access_are_offered()
    {
        Skip.If(_admin.SkipReason is not null, _admin.SkipReason ?? string.Empty);

        await using AdminDbContext db = _admin.CreateContext();
        Guid customer = Guid.NewGuid();
        Role mine = await RoleAsync(db, customer, active: true, "sales.view");
        Role platform = await RoleAsync(db, customer, active: true, "sales.view", "platform.edit");
        Role theirs = await RoleAsync(db, Guid.NewGuid(), active: true, "sales.view");
        Role system = await RoleAsync(db, null, active: true, "sales.view");

        Assert.True(await ApiClientRoles.IsAssignableAsync(db, customer, mine.RoleId, default));
        Assert.True(await ApiClientRoles.IsAssignableAsync(db, customer, system.RoleId, default));
        Assert.False(await ApiClientRoles.IsAssignableAsync(db, customer, platform.RoleId, default));
        Assert.False(await ApiClientRoles.IsAssignableAsync(db, customer, theirs.RoleId, default));
        Assert.False(await ApiClientRoles.IsAssignableAsync(db, customer, 0, default));
    }
}
