using Master.Entity.Enums;
using Master.Entity.Models;
using Master.Entity.TableEntities;
using Master.Repository;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Apps;
using Shared.Kernel.Tenancy;

namespace Master.Api.Services;

/// <summary>
/// Builds the navigation tree for whoever is asking.
///
/// The rows live in one self-referencing table, so this reads them flat in a single
/// query and assembles the three levels here rather than joining the same table
/// three times. There are on the order of a hundred of them; the shaping is cheaper
/// than the round trips.
///
/// A screen is offered only when the caller holds at least one of its permissions,
/// an empty section is dropped, and a module left with nothing to show is dropped in
/// turn — so the rail never draws an entry that leads nowhere. Rows with no route
/// are inactive in the seed and filtered here, which is how screens that are designed
/// but not yet built stay out of a user's way while remaining in the table.
///
/// A screen that belongs to another trade than the branch's is dropped too
/// (<see cref="TradeScope"/>, TK-30).
/// </summary>
public sealed class MenuService
{
    private readonly AdminDbContext _db;
    private readonly ITenantContext _tenant;

    public MenuService(AdminDbContext db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    /// <summary>
    /// The caller's menu in one app (H0.3, TK-44): only rows whose
    /// <c>Menu.Apps</c> include it. A token that predates apps is RetailErp's.
    /// </summary>
    public async Task<IReadOnlyList<MenuView>> GetUserMenuAsync(CancellationToken ct, App app = App.RetailErp)
    {
        var permissions = _tenant.Permissions is { Count: > 0 }
            ? _tenant.Permissions
            : new HashSet<string>();

        // The branch's trade narrows the menu (D-10, TK-30): a screen that
        // belongs to another trade is not offered. General when unknown, which
        // shows everything — the same default the branch itself has.
        Guid? orgId = _tenant.OrgId;
        Vertical trade = orgId is null
            ? Vertical.General
            : await _db.Organizations.AsNoTracking()
                .Where(o => o.OrgId == orgId.Value)
                .Select(o => (Vertical?)o.Vertical)
                .FirstOrDefaultAsync(ct) ?? Vertical.General;

        var rows = await _db.Menus
            .AsNoTracking()
            .Where(m => m.IsActive && m.Apps.HasFlag(app))
            .OrderBy(m => m.DisplayOrder)
            .Select(m => new Row
            {
                MenuId = m.MenuId,
                ParentId = m.ParentId,
                Type = m.Type,
                Code = m.Code,
                Name = m.Name,
                Icon = m.Icon,
                Module = m.Module,
                RoutePath = m.RoutePath,
                IsSearchable = m.IsSearchable,
                CanCreate = m.CanCreate,
                SingularName = m.SingularName,
                AllowedActions = m.Permissions
                    .Where(p => permissions.Contains(p.PermissionCode))
                    .Select(p => p.Action)
                    .ToList()
            })
            .ToListAsync(ct);

        var childrenOf = rows
            .Where(r => r.ParentId is not null)
            .GroupBy(r => r.ParentId!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());

        var menus = new List<MenuView>();

        foreach (var rail in rows.Where(r => r.Type == MenuType.Rail))
        {
            var groups = new List<MenuGroupView>();

            foreach (var group in Children(childrenOf, rail.MenuId).Where(r => r.Type == MenuType.Group))
            {
                var items = Children(childrenOf, group.MenuId)
                    .Where(r => r.Type == MenuType.Item && r.AllowedActions.Count > 0
                        && TradeScope.ShowsMenu(r.Code, trade))
                    .Select(r => new SubMenuView
                    {
                        SubMenuId = r.MenuId,
                        Code = r.Code,
                        Name = r.Name ?? r.Code,
                        RoutePath = r.RoutePath,
                        Icon = r.Icon,
                        Module = r.Module ?? string.Empty,
                        CanCreate = r.CanCreate,
                        SingularName = r.SingularName,
                        HasAccess = true,
                        AllowedActions = r.AllowedActions,
                    })
                    .ToList();

                if (items.Count == 0)
                {
                    continue;
                }

                groups.Add(new MenuGroupView
                {
                    MenuGroupId = group.MenuId,
                    Code = group.Code,
                    Name = group.Name,
                    Icon = group.Icon,
                    SubMenus = items,
                });
            }

            // A module with a route of its own — Home — has no sections to be
            // emptied, so it survives on its own permission rather than a child's.
            var reachable = groups.Count > 0
                || (rail.RoutePath is not null
                    && rail.Module is not null
                    && permissions.Contains($"{rail.Module}.view"));

            if (!reachable)
            {
                continue;
            }

            menus.Add(new MenuView
            {
                MenuId = rail.MenuId,
                Code = rail.Code,
                Name = rail.Name ?? rail.Code,
                Icon = rail.Icon,
                Module = rail.Module ?? string.Empty,
                RoutePath = rail.RoutePath,
                IsSearchable = rail.IsSearchable,
                Groups = groups,
            });
        }

        return menus;
    }

    private static List<Row> Children(IReadOnlyDictionary<int, List<Row>> index, int parentId) =>
        index.TryGetValue(parentId, out var kids) ? kids : [];

    /// <summary>One row as it comes back from the database, before it is shaped.</summary>
    private sealed class Row
    {
        public int MenuId { get; init; }
        public int? ParentId { get; init; }
        public MenuType Type { get; init; }
        public string Code { get; init; } = null!;
        public string? Name { get; init; }
        public string? Icon { get; init; }
        public string? Module { get; init; }
        public string? RoutePath { get; init; }
        public bool IsSearchable { get; init; }
        public bool CanCreate { get; init; }
        public string? SingularName { get; init; }
        public List<string> AllowedActions { get; init; } = [];
    }
}
