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
    public async Task<IReadOnlyList<RoleListItem>> ListAsync(Guid customerId, CancellationToken ct, App? app = null)
    {
        IQueryable<Role> roles = _db.Roles.Where(r => r.CustomerId == null || r.CustomerId == customerId);

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
                App = string.Empty,
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
            App = string.Empty,
            UserCount = await _db.UserOrganizationRoles.CountAsync(
                u => u.RoleId == roleId && u.IsActive, ct),
            PermissionCount = permissionIds.Count,
            PermissionIds = permissionIds,
        };
    }

    /// <summary>
    /// The permission matrix, grouped by module.
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
            if (app == App.Payroll)
            {
                query = query.Where(p => (p.AddOns & AddOn.Payroll) == AddOn.Payroll || (p.BusinessType == null && p.AddOns == AddOn.None));
            }
            else if (app == App.Hrms)
            {
                query = query.Where(p => (p.AddOns & AddOn.Hrms) == AddOn.Hrms || (p.BusinessType == null && p.AddOns == AddOn.None));
            }
            else if (app == App.School)
            {
                query = query.Where(p => p.BusinessType == BusinessType.School || p.Module == "contacts" || (p.BusinessType == null && p.AddOns == AddOn.None));
            }
            else if (app == App.RetailErp)
            {
                query = query.Where(p => p.BusinessType == BusinessType.General || (p.BusinessType == null && p.AddOns == AddOn.None));
            }
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
    /// Creates a customer role (D-36, TK-127).
    /// </summary>
    public async Task<(SaveRoleResult Result, int RoleId)> CreateAsync(
        Guid customerId, SaveRoleRequest request, CancellationToken ct)
    {
        if (request.App is not null)
        {
            if (!AppRules.TryParseSingle(request.App, out App app))
            {
                return (SaveRoleResult.InvalidApp, 0);
            }

            if (!await AllGrantableAsync(app, request.PermissionIds, ct))
            {
                return (SaveRoleResult.PermissionNotInApp, 0);
            }
        }

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

        if (request.App is not null && AppRules.TryParseSingle(request.App, out App app))
        {
            if (!await AllGrantableAsync(app, request.PermissionIds, ct))
            {
                return SaveRoleResult.PermissionNotInApp;
            }
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

    private async Task<bool> AllGrantableAsync(App roleApp, IReadOnlyList<int> permissionIds, CancellationToken ct)
    {
        if (permissionIds.Count == 0)
        {
            return true;
        }

        List<Permission> perms = await _db.Permissions
            .Where(p => permissionIds.Contains(p.PermissionId) && p.Module != "platform")
            .ToListAsync(ct);

        foreach (Permission p in perms)
        {
            if (roleApp == App.RetailErp)
            {
                if (p.BusinessType != BusinessType.General && p.BusinessType != null) return false;
                if ((p.AddOns & (AddOn.Hrms | AddOn.Payroll)) != 0 && p.AddOns != AddOn.None) return false;
            }
            else if (roleApp == App.Payroll)
            {
                if ((p.AddOns & AddOn.Payroll) == 0 && (p.BusinessType != null || p.AddOns != AddOn.None)) return false;
            }
            else if (roleApp == App.Hrms)
            {
                if ((p.AddOns & AddOn.Hrms) == 0 && (p.BusinessType != null || p.AddOns != AddOn.None)) return false;
            }
            else if (roleApp == App.School)
            {
                if (p.BusinessType != BusinessType.School && p.Module != "contacts" && (p.BusinessType != null || p.AddOns != AddOn.None)) return false;
            }
        }

        return true;
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
