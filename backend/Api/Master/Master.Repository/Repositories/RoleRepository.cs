using Master.Repository.Clients;
using Master.Entity.Models;
using Master.Entity.TableEntities;
using Master.Repository;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Apps;

namespace Master.Repository.Repositories;

public sealed class RoleRepository
{
    private readonly AdminDbContext _db;

    public RoleRepository(AdminDbContext db) => _db = db;

    /// <summary>System roles plus this customer's own (D-36, TK-127: roles span types).</summary>
    public async Task<IReadOnlyList<RoleListItem>> ListAsync(Guid customerId, Guid? orgId, CancellationToken ct)
    {
        IQueryable<Role> roles = _db.Roles.Where(r => r.CustomerId == null || r.CustomerId == customerId);

        if (!orgId.HasValue)
        {
            return await roles
                .OrderBy(r => r.CustomerId == null ? 0 : 1)
                .ThenBy(r => r.RoleId)
                .Select(r => new RoleListItem
                {
                    RoleId = r.RoleId,
                    DisplayName = r.DisplayName,
                    Description = r.Description,
                    IsSystemRole = r.IsSystemRole,
                    IsActive = r.IsActive,
                    UserCount = 0,
                    PermissionCount = _db.RolePermissions.Count(p => p.RoleId == r.RoleId),
                })
                .ToListAsync(ct);
        }

        Guid branchId = orgId.Value;
        return await roles
            .OrderBy(r => r.CustomerId == null ? 0 : 1)
            .ThenBy(r => r.RoleId)
            .Select(r => new RoleListItem
            {
                RoleId = r.RoleId,
                DisplayName = r.DisplayName,
                Description = r.Description,
                IsSystemRole = r.IsSystemRole,
                IsActive = r.IsActive,
                UserCount = _db.UserOrganizationRoles.Count(u => u.RoleId == r.RoleId && u.OrgId == branchId && u.IsActive),
                PermissionCount = _db.RolePermissions.Count(p => p.RoleId == r.RoleId),
            })
            .ToListAsync(ct);
    }

    public async Task<RoleDetail?> GetAsync(Guid customerId, int roleId, Guid? orgId, CancellationToken ct)
    {
        Role? role = await _db.Roles.FirstOrDefaultAsync(
            r => r.RoleId == roleId && (r.CustomerId == null || r.CustomerId == customerId), ct);
        if (role is null)
        {
            return null;
        }

        List<int> permissionIds = await _db.RolePermissions
            .Where(p => p.RoleId == roleId)
            .Select(p => p.PermissionId)
            .ToListAsync(ct);

        int userCount = orgId.HasValue
            ? await _db.UserOrganizationRoles.CountAsync(
                u => u.RoleId == roleId && u.OrgId == orgId.Value && u.IsActive, ct)
            : 0;

        return new RoleDetail
        {
            RoleId = role.RoleId,
            DisplayName = role.DisplayName,
            Description = role.Description,
            IsSystemRole = role.IsSystemRole,
            IsActive = role.IsActive,
            UserCount = userCount,
            PermissionCount = permissionIds.Count,
            PermissionIds = permissionIds,
        };
    }

    /// <summary>
    /// The permission matrix, grouped by module, narrowed to what a branch of
    /// <paramref name="branchType"/> with <paramref name="branchAddOns"/> can
    /// use (D-50, TK-136): one role list per customer, and its matrix shows the
    /// permissions the current branch's trade and add-ons allow. The rule is
    /// the menu's own (<see cref="InScope"/>), so a permission and the screen it
    /// opens are offered together or not at all.
    /// </summary>
    public async Task<IReadOnlyList<PermissionGroup>> PermissionMatrixAsync(
        bool includePlatform, BusinessType? branchType, AddOn branchAddOns, CancellationToken ct)
    {
        IQueryable<Permission> query = _db.Permissions;
        if (!includePlatform)
        {
            query = query.Where(p => p.Module != "platform");
        }

        List<Permission> all = [.. (await query.ToListAsync(ct))
            .Where(p => InScope(p.BusinessType, p.AddOns, branchType, branchAddOns))];

        return all
            .GroupBy(p => p.Module)
            .OrderBy(g => g.Key)
            .Select(g => new PermissionGroup
            {
                Module = g.Key,
                Permissions = g
                    .Select(p => new PermissionItem
                    {
                        PermissionId = p.PermissionId,
                        Code = p.Code,
                        Action = p.Code[(p.Code.IndexOf('.') + 1)..],
                        Description = p.Description,
                    })
                    .OrderBy(p => p.PermissionId)
                    .ToList(),
            })
            .ToList();
    }

    /// <summary>
    /// Whether a permission scoped to <paramref name="permissionType"/> and
    /// <paramref name="permissionAddOns"/> is for a branch of
    /// <paramref name="branchType"/> with <paramref name="branchAddOns"/>: the
    /// menu's rule (<see cref="MenuRepository.MenuMatches"/>). Pure.
    /// </summary>
    public static bool InScope(
        BusinessType? permissionType, AddOn permissionAddOns, BusinessType? branchType, AddOn branchAddOns) =>
        MenuRepository.MenuMatches(branchType, branchAddOns, permissionType, permissionAddOns);

    /// <summary>
    /// Creates a customer role (D-36, TK-127).
    /// </summary>
    public async Task<(SaveRoleResult Result, int RoleId)> CreateAsync(
        Guid customerId, SaveRoleRequest request, CancellationToken ct)
    {
        var role = new Role
        {
            CustomerId = customerId,
            SystemName = request.DisplayName,
            DisplayName = request.DisplayName,
            Description = request.Description,
            IsSystemRole = false,
            IsActive = true,
        };
        _db.Roles.Add(role);
        await _db.SaveChangesAsync(ct);

        await ReplacePermissionsAsync(role.RoleId, request.PermissionIds, ct);
        return (SaveRoleResult.Ok, role.RoleId);
    }

    public async Task<SaveRoleResult> UpdateAsync(
        Guid customerId, int roleId, SaveRoleRequest request, CancellationToken ct)
    {
        Role? role = await _db.Roles.FirstOrDefaultAsync(
            r => r.RoleId == roleId && (r.CustomerId == null || r.CustomerId == customerId), ct);
        if (role is null)
        {
            return SaveRoleResult.NotFound;
        }

        // System default roles are completely locked and cannot be edited, modified, or updated.
        if (role.IsSystemRole || role.CustomerId == null)
        {
            return SaveRoleResult.SystemRole;
        }

        role.DisplayName = request.DisplayName;
        role.Description = request.Description;

        await ReplacePermissionsAsync(roleId, request.PermissionIds, ct);

        await _db.SaveChangesAsync(ct);
        return SaveRoleResult.Ok;
    }

    /// <summary>Soft delete. Blocked when the role is assigned to an active user.</summary>
    public async Task<DeleteRoleResult> DeleteAsync(Guid customerId, int roleId, CancellationToken ct)
    {
        Role? role = await _db.Roles.FirstOrDefaultAsync(
            r => r.RoleId == roleId && r.CustomerId == customerId, ct);
        if (role is null)
        {
            // Either it does not exist or it is a system role, which is never deletable.
            bool isSystem = await _db.Roles.AnyAsync(r => r.RoleId == roleId && r.CustomerId == null, ct);
            return isSystem ? DeleteRoleResult.SystemRole : DeleteRoleResult.NotFound;
        }

        bool inUse = await _db.UserOrganizationRoles.AnyAsync(
            u => u.RoleId == roleId && u.IsActive, ct);
        if (inUse)
        {
            return DeleteRoleResult.InUse;
        }

        role.IsActive = false;
        await _db.SaveChangesAsync(ct);
        return DeleteRoleResult.Ok;
    }

    private async Task ReplacePermissionsAsync(
        int roleId, IReadOnlyList<int> permissionIds, CancellationToken ct)
    {
        List<RolePermission> existing = await _db.RolePermissions
            .Where(p => p.RoleId == roleId)
            .ToListAsync(ct);
        _db.RolePermissions.RemoveRange(existing);

        // platform.* is operator-only — never grantable to a customer role.
        List<int> allowed = await _db.Permissions
            .Where(p => permissionIds.Contains(p.PermissionId) && p.Module != "platform")
            .Select(p => p.PermissionId)
            .ToListAsync(ct);

        foreach (int permissionId in allowed)
        {
            _db.RolePermissions.Add(new RolePermission
            {
                RoleId = roleId,
                PermissionId = permissionId,
            });
        }

        await _db.SaveChangesAsync(ct);
    }
}

public enum SaveRoleResult
{
    Ok = 1,
    NotFound = 2,
    SystemRole = 3,
}

public enum DeleteRoleResult
{
    Ok = 1,
    NotFound = 2,
    InUse = 3,
    SystemRole = 4,
}
