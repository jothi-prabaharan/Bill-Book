using Master.Entity.Models;
using Master.Entity.TableEntities;
using Master.Repository;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Apps;

namespace Master.Api.Services;

public sealed class RoleService
{
    private readonly AdminDbContext _db;

    public RoleService(AdminDbContext db) => _db = db;

    /// <summary>System roles plus this customer's own.</summary>
    public async Task<IReadOnlyList<RoleListItem>> ListAsync(Guid customerId, CancellationToken ct, App? app = null)
    {
        IQueryable<Role> roles = _db.Roles.Where(r => r.CustomerId == null || r.CustomerId == customerId);

        // Inside an app, only that app's roles (TK-43): a RetailErp Owner and a
        // Payroll Owner are different rows, and each app edits its own.
        if (app is App only)
        {
            roles = roles.Where(r => r.App == only);
        }

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
                App = r.App.ToString(),
                UserCount = _db.UserOrganizationRoles.Count(u => u.RoleId == r.RoleId && u.IsActive),
                PermissionCount = _db.RolePermissions.Count(p => p.RoleId == r.RoleId),
            })
            .ToListAsync(ct);
    }

    public async Task<RoleDetail?> GetAsync(Guid customerId, int roleId, CancellationToken ct)
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

        return new RoleDetail
        {
            RoleId = role.RoleId,
            DisplayName = role.DisplayName,
            Description = role.Description,
            IsSystemRole = role.IsSystemRole,
            IsActive = role.IsActive,
            App = role.App.ToString(),
            UserCount = await _db.UserOrganizationRoles.CountAsync(
                u => u.RoleId == roleId && u.IsActive, ct),
            PermissionCount = permissionIds.Count,
            PermissionIds = permissionIds,
        };
    }

    /// <summary>
    /// The permission matrix, grouped by module. With an app, only the
    /// permissions a role of that app may be granted (TK-42).
    /// </summary>
    public async Task<IReadOnlyList<PermissionGroup>> PermissionMatrixAsync(
        bool includePlatform, CancellationToken ct, App? forApp = null)
    {
        IQueryable<Permission> query = _db.Permissions;
        if (!includePlatform)
        {
            query = query.Where(p => p.Module != "platform");
        }

        if (forApp is App app)
        {
            query = query.Where(p => p.Apps.HasFlag(app));
        }

        List<Permission> all = await query.ToListAsync(ct);

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
    /// Creates a customer role in one app. Refused, with nothing written, when
    /// the app is not a single app or any permission belongs to other apps only.
    /// </summary>
    public async Task<(SaveRoleResult Result, int RoleId)> CreateAsync(
        Guid customerId, SaveRoleRequest request, CancellationToken ct)
    {
        App app = App.RetailErp;
        if (request.App is not null && !AppRules.TryParseSingle(request.App, out app))
        {
            return (SaveRoleResult.InvalidApp, 0);
        }

        if (!await AllGrantableAsync(app, request.PermissionIds, ct))
        {
            return (SaveRoleResult.PermissionNotInApp, 0);
        }

        var role = new Role
        {
            CustomerId = customerId,
            SystemName = request.DisplayName,
            DisplayName = request.DisplayName,
            Description = request.Description,
            App = app,
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

        // A role never changes app: its users signed in to that app with it.
        if (!role.IsSystemRole && !await AllGrantableAsync(role.App, request.PermissionIds, ct))
        {
            return SaveRoleResult.PermissionNotInApp;
        }

        // A system role may be relabelled but its permission set is fixed, and
        // SystemName never changes — code and reports key on it.
        role.DisplayName = request.DisplayName;
        role.Description = request.Description;

        if (!role.IsSystemRole)
        {
            await ReplacePermissionsAsync(roleId, request.PermissionIds, ct);
        }

        await _db.SaveChangesAsync(ct);
        return SaveRoleResult.Ok;
    }

    /// <summary>
    /// The grant rule (TK-42): every requested permission's apps include the
    /// role's app. platform.* is left out here, as it is dropped on save anyway.
    /// An id that names no permission is not a grant and is ignored, as before.
    /// </summary>
    private async Task<bool> AllGrantableAsync(App roleApp, IReadOnlyList<int> permissionIds, CancellationToken ct)
    {
        if (permissionIds.Count == 0)
        {
            return true;
        }

        List<App> apps = await _db.Permissions
            .Where(p => permissionIds.Contains(p.PermissionId) && p.Module != "platform")
            .Select(p => p.Apps)
            .ToListAsync(ct);

        return apps.All(permissionApps => AppRules.MayGrant(roleApp, permissionApps));
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

    /// <summary>A permission belongs to other apps only (the grant rule, TK-42).</summary>
    PermissionNotInApp = 3,

    /// <summary>The role's app is not one of the four apps.</summary>
    InvalidApp = 4,
}

public enum DeleteRoleResult
{
    Ok = 1,
    NotFound = 2,
    InUse = 3,
    SystemRole = 4,
}
