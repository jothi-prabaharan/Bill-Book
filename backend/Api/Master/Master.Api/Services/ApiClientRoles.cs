using Master.Entity.TableEntities;
using Master.Repository;
using Microsoft.EntityFrameworkCore;

namespace Master.Api.Services;

/// <summary>
/// Which roles an API client may hold, and what a client's role lets it do
/// (D-07, TK-29).
///
/// <b>A role is assignable</b> when it is active, is a system role or one of the
/// same customer's own, and grants no <c>platform.*</c> permission. A role row
/// can be shared across customers, so a platform permission on one would give a
/// customer's key access across every customer — which is why a key's role must
/// never carry one, and why its codes are filtered again when the key is used.
/// </summary>
public static class ApiClientRoles
{
    public static IQueryable<Role> Assignable(AdminDbContext db, Guid customerId) =>
        db.Roles.Where(r => r.IsActive
            && (r.CustomerId == null || r.CustomerId == customerId)
            && !db.RolePermissions.Any(rp => rp.RoleId == r.RoleId
                && db.Permissions.Any(p => p.PermissionId == rp.PermissionId
                    && p.Module == PlatformOperatorService.PlatformModule)));

    public static Task<bool> IsAssignableAsync(AdminDbContext db, Guid customerId, int roleId, CancellationToken ct) =>
        Assignable(db, customerId).AnyAsync(r => r.RoleId == roleId, ct);

    /// <summary>
    /// The permission codes a key with this role carries: the role's own, less
    /// any <c>platform.*</c>; nothing at all for a role that is inactive,
    /// belongs to another customer, or does not exist — including role 0, which
    /// every key minted before TK-29 holds.
    /// </summary>
    public static async Task<List<string>> PermissionsAsync(
        AdminDbContext db, Guid customerId, int roleId, CancellationToken ct)
    {
        bool usable = await db.Roles.AnyAsync(r => r.RoleId == roleId && r.IsActive
            && (r.CustomerId == null || r.CustomerId == customerId), ct);

        if (!usable)
        {
            return [];
        }

        return await (
            from rp in db.RolePermissions
            join p in db.Permissions on rp.PermissionId equals p.PermissionId
            where rp.RoleId == roleId && p.Module != PlatformOperatorService.PlatformModule
            orderby p.Code
            select p.Code).ToListAsync(ct);
    }
}
