using Master.Repository.Clients;
using Master.Entity.Enums;
using Master.Entity.Models;
using Master.Entity.TableEntities;
using Master.Repository;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Apps;
using Shared.Kernel.Tenancy;

namespace Master.Repository.Repositories;

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
/// (<see cref="TradeScopeRepository"/>, TK-30).
/// </summary>
public sealed class MenuRepository
{
    private readonly AdminDbContext _db;
    private readonly ITenantContext _tenant;

    public MenuRepository(AdminDbContext db, ITenantContext tenant)
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
        var orgScope = orgId is null
            ? null
            : await _db.Organizations.AsNoTracking()
                .Where(o => o.OrgId == orgId.Value)
                .Select(o => new
                {
                    Trade = (Vertical?)o.Vertical ?? Vertical.General,
                    o.BusinessType,
                    o.AddOns
                })
                .FirstOrDefaultAsync(ct);

        Vertical trade = orgScope?.Trade ?? Vertical.General;
        BusinessType? businessType = orgScope != null ? orgScope.BusinessType : BusinessType.General;
        AddOn addOns = orgScope?.AddOns ?? AddOn.None;

        if (orgId is null && app != App.RetailErp)
        {
            if (app == App.School)
            {
                businessType = BusinessType.School;
            }
            else if (app == App.Hrms)
            {
                businessType = null;
                addOns = AddOn.Hrms;
            }
            else if (app == App.Payroll)
            {
                businessType = null;
                addOns = AddOn.Payroll;
            }
        }

        var rawRows = await _db.Menus
            .AsNoTracking()
            .Where(m => m.IsActive)
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
                BusinessType = m.BusinessType,
                AddOns = m.AddOns,
                AllowedActions = m.Permissions
                    .Where(p => permissions.Contains(p.PermissionCode))
                    .Select(p => p.Action)
                    .ToList()
            })
            .ToListAsync(ct);

        var rows = rawRows
            .Where(r => MenuMatches(businessType, addOns, r.BusinessType, r.AddOns))
            .ToList();

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
                        && TradeScopeRepository.ShowsMenu(r.Code, trade))
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

    /// <summary>
    /// Determines whether a menu row is visible to a branch given the branch's business type and add-ons (D-36, TK-127).
    /// </summary>
    public static bool MenuMatches(BusinessType? branchType, AddOn branchAddOns, BusinessType? menuType, AddOn menuAddOns)
    {
        // 1. Shared menus (no vertical, no add-on) are available to all branches.
        if (menuType == null && menuAddOns == AddOn.None)
        {
            return true;
        }

        // 2. If the menu belongs to an add-on the branch has enabled, show it.
        if (menuAddOns != AddOn.None && (branchAddOns & menuAddOns) != 0)
        {
            return true;
        }

        // 3. If the branch has no business vertical (e.g. standalone HRMS / Payroll),
        // only menus enabled by add-on flags (checked above) are shown.
        if (branchType == null)
        {
            return false;
        }

        // 4. Contacts menu (carrying BusinessType.General and AddOn.Crm in seed):
        // shown to any branch with a business type (retail or school) or with CRM add-on.
        if (menuType == BusinessType.General && menuAddOns == AddOn.Crm)
        {
            return true;
        }

        // 5. Retail verticals (General, Pharma, Jewellery) match general retail menus.
        if (menuType == BusinessType.General)
        {
            return branchType is BusinessType.General or BusinessType.Pharma or BusinessType.Jewellery;
        }

        // 6. School vertical matches school menus.
        if (menuType == BusinessType.School)
        {
            return branchType == BusinessType.School;
        }

        return branchType == menuType;
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
        public BusinessType? BusinessType { get; init; }
        public AddOn AddOns { get; init; }
        public List<string> AllowedActions { get; init; } = [];
    }
}
