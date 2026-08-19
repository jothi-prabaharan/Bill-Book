using Master.Entity.Models;
using Master.Repository;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Tenancy;

namespace Master.Api.Services;

public sealed class MenuService
{
    private readonly AdminDbContext _db;
    private readonly ITenantContext _tenant;

    public MenuService(AdminDbContext db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<IReadOnlyList<MenuView>> GetUserMenuAsync(CancellationToken ct)
    {
        var permissions = _tenant.Permissions is { Count: > 0 } ? _tenant.Permissions : new HashSet<string>();

        var menus = await _db.Menus
            .Where(m => m.IsActive)
            .OrderBy(m => m.DisplayOrder)
            .Select(m => new MenuView
            {
                MenuId = m.MenuId,
                Code = m.Code,
                Name = m.Name,
                Icon = m.Icon,
                SubMenus = m.SubMenus
                    .Where(sm => sm.IsActive)
                    .OrderBy(sm => sm.DisplayOrder)
                    .Select(sm => new SubMenuView
                    {
                        SubMenuId = sm.SubMenuId,
                        Code = sm.Code,
                        Name = sm.Name,
                        RoutePath = sm.RoutePath,
                        Icon = sm.Icon,
                        HasAccess = sm.Permissions.Any(p => permissions.Contains(p.PermissionCode)),
                        AllowedActions = sm.Permissions
                            .Where(p => permissions.Contains(p.PermissionCode))
                            .Select(p => p.Action)
                            .ToList()
                    })
                    .ToList()
            })
            .ToListAsync(ct);

        // Filter out menus with no accessible submenus
        return menus
            .Where(m => m.SubMenus.Any(sm => sm.HasAccess))
            .ToList();
    }
}