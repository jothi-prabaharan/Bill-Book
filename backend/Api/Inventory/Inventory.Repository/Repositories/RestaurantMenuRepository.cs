using Inventory.Entity.Enums;
using Inventory.Entity.Models;
using Inventory.Entity.TableEntities;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Persistence;
using Shared.Kernel.Restaurant;

namespace Inventory.Repository.Repositories;

/// <summary>
/// The restaurant menu (TK-144): categories, timed menus, dishes with their
/// portions, choices and order-type prices, the menu a waiter orders from, and
/// the pricing Sales asks for when a line is added to an order. A dish is an
/// <c>inv.Items</c> row; nothing here duplicates its tax, unit or name.
/// </summary>
public sealed class RestaurantMenuRepository
{
    private const int Gap = 10;

    /// <summary>The headings a new Restaurant branch starts with. Seed data: add to it, never rename.</summary>
    public static readonly string[] SeedCategories = ["Starters", "Soups", "Main course", "Breads", "Rice", "Desserts", "Beverages"];

    private readonly InventoryDbContext _db;

    public RestaurantMenuRepository(InventoryDbContext db) => _db = db;

    // ---- Categories ----

    public async Task<List<MenuCategoryView>> ListCategoriesAsync(bool includeInactive, CancellationToken ct)
    {
        Dictionary<long, int> counts = await _db.MenuItems
            .GroupBy(m => m.MenuCategoryId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.Key, g => g.Count, ct);

        List<MenuCategory> rows = await _db.MenuCategories
            .Where(c => includeInactive || c.IsActive)
            .OrderBy(c => c.DisplayOrder).ThenBy(c => c.Name)
            .ToListAsync(ct);

        return rows.Select(c => new MenuCategoryView
        {
            MenuCategoryId = c.MenuCategoryId,
            Name = c.Name,
            DisplayOrder = c.DisplayOrder,
            IsActive = c.IsActive,
            ItemCount = counts.GetValueOrDefault(c.MenuCategoryId),
        }).ToList();
    }

    public async Task<MenuResult<long>> SaveCategoryAsync(long? categoryId, SaveMenuCategoryRequest request, CancellationToken ct)
    {
        string name = request.Name.Trim();
        if (await _db.MenuCategories.AnyAsync(c => c.Name == name && c.MenuCategoryId != (categoryId ?? 0), ct))
        {
            return MenuResult<long>.Refuse("A category with that name already exists.");
        }

        MenuCategory? category;
        if (categoryId is null)
        {
            int highest = await _db.MenuCategories.Select(c => (int?)c.DisplayOrder).MaxAsync(ct) ?? 0;
            category = new MenuCategory { DisplayOrder = highest + Gap };
            _db.MenuCategories.Add(category);
        }
        else
        {
            category = await _db.MenuCategories.FirstOrDefaultAsync(c => c.MenuCategoryId == categoryId, ct);
            if (category is null)
            {
                return MenuResult<long>.Missing();
            }
        }

        category.Name = name;
        category.IsActive = request.IsActive;
        await _db.SaveChangesAsync(ct);
        return MenuResult<long>.Done(category.MenuCategoryId);
    }

    public async Task<MenuResult<bool>> DeleteCategoryAsync(long categoryId, CancellationToken ct)
    {
        MenuCategory? category = await _db.MenuCategories.FirstOrDefaultAsync(c => c.MenuCategoryId == categoryId, ct);
        if (category is null)
        {
            return MenuResult<bool>.Missing();
        }

        if (await _db.MenuItems.AnyAsync(m => m.MenuCategoryId == categoryId, ct))
        {
            return MenuResult<bool>.Refuse("Move this category's dishes to another category first, or make it inactive.");
        }

        _db.MenuCategories.Remove(category);
        await _db.SaveChangesAsync(ct);
        return MenuResult<bool>.Done(true);
    }

    /// <summary>Adds the starting categories a Restaurant branch is missing. Re-runnable; other trades get none.</summary>
    public async Task<int> SeedCategoriesAsync(Guid orgId, string vertical, CancellationToken ct)
    {
        if (!string.Equals(vertical, "Restaurant", StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        HashSet<string> present = new(
            await _db.MenuCategories.IgnoreQueryFilters().Where(c => c.OrgId == orgId).Select(c => c.Name).ToListAsync(ct),
            StringComparer.OrdinalIgnoreCase);

        int order = 0;
        int added = 0;
        foreach (string name in SeedCategories)
        {
            order += Gap;
            if (present.Contains(name))
            {
                continue;
            }

            _db.MenuCategories.Add(new MenuCategory { OrgId = orgId, Name = name, DisplayOrder = order, IsActive = true });
            added++;
        }

        if (added > 0)
        {
            await _db.SaveChangesAsync(ct);
        }

        return added;
    }

    // ---- Timed menus ----

    public async Task<List<MenuScheduleView>> ListSchedulesAsync(CancellationToken ct) =>
        await _db.MenuSchedules
            .OrderBy(s => s.StartTime).ThenBy(s => s.Name)
            .Select(s => new MenuScheduleView
            {
                MenuScheduleId = s.MenuScheduleId,
                Name = s.Name,
                Days = s.Days,
                StartTime = s.StartTime,
                EndTime = s.EndTime,
                IsActive = s.IsActive,
            })
            .ToListAsync(ct);

    public async Task<MenuResult<long>> SaveScheduleAsync(long? scheduleId, SaveMenuScheduleRequest request, CancellationToken ct)
    {
        if (RestaurantMenuRulesRepository.ScheduleRefusal(request) is string refusal)
        {
            return MenuResult<long>.Refuse(refusal);
        }

        string name = request.Name.Trim();
        if (await _db.MenuSchedules.AnyAsync(s => s.Name == name && s.MenuScheduleId != (scheduleId ?? 0), ct))
        {
            return MenuResult<long>.Refuse("A timed menu with that name already exists.");
        }

        MenuSchedule? schedule;
        if (scheduleId is null)
        {
            schedule = new MenuSchedule();
            _db.MenuSchedules.Add(schedule);
        }
        else
        {
            schedule = await _db.MenuSchedules.FirstOrDefaultAsync(s => s.MenuScheduleId == scheduleId, ct);
            if (schedule is null)
            {
                return MenuResult<long>.Missing();
            }
        }

        schedule.Name = name;
        schedule.Days = request.Days;
        schedule.StartTime = request.StartTime;
        schedule.EndTime = request.EndTime;
        schedule.IsActive = request.IsActive;
        await _db.SaveChangesAsync(ct);
        return MenuResult<long>.Done(schedule.MenuScheduleId);
    }

    /// <summary>Removes a timed menu; its dishes are served all day from then on.</summary>
    public async Task<MenuResult<bool>> DeleteScheduleAsync(long scheduleId, CancellationToken ct)
    {
        MenuSchedule? schedule = await _db.MenuSchedules.FirstOrDefaultAsync(s => s.MenuScheduleId == scheduleId, ct);
        if (schedule is null)
        {
            return MenuResult<bool>.Missing();
        }

        _db.MenuSchedules.Remove(schedule);
        await _db.SaveChangesAsync(ct);
        return MenuResult<bool>.Done(true);
    }

    // ---- Dishes ----

    public async Task<List<MenuItemView>> ListItemsAsync(long? categoryId, string? search, CancellationToken ct)
    {
        string? term = string.IsNullOrWhiteSpace(search) ? null : search.Trim();

        var rows = await (
                from m in _db.MenuItems
                join i in _db.Items on m.MenuItemId equals i.ItemId
                join c in _db.MenuCategories on m.MenuCategoryId equals c.MenuCategoryId
                where categoryId == null || m.MenuCategoryId == categoryId
                where term == null || EF.Functions.ILike(i.ItemName, "%" + term + "%") || EF.Functions.ILike(i.ItemCode, "%" + term + "%")
                orderby c.DisplayOrder, m.DisplayOrder, i.ItemName
                select new { Menu = m, i.ItemCode, i.ItemName, i.SalesPrice, CategoryName = c.Name })
            .ToListAsync(ct);

        List<long> ids = rows.Select(r => r.Menu.MenuItemId).ToList();
        Dictionary<long, string> schedules = await _db.MenuSchedules.ToDictionaryAsync(s => s.MenuScheduleId, s => s.Name, ct);
        Children children = await LoadChildrenAsync(ids, ct);

        return rows.Select(r => ToView(r.Menu, r.ItemCode, r.ItemName, r.SalesPrice, r.CategoryName, schedules, children)).ToList();
    }

    public async Task<MenuItemView?> GetItemAsync(long itemId, CancellationToken ct)
    {
        var row = await (
                from m in _db.MenuItems
                join i in _db.Items on m.MenuItemId equals i.ItemId
                join c in _db.MenuCategories on m.MenuCategoryId equals c.MenuCategoryId
                where m.MenuItemId == itemId
                select new { Menu = m, i.ItemCode, i.ItemName, i.SalesPrice, CategoryName = c.Name })
            .FirstOrDefaultAsync(ct);
        if (row is null)
        {
            return null;
        }

        Dictionary<long, string> schedules = await _db.MenuSchedules.ToDictionaryAsync(s => s.MenuScheduleId, s => s.Name, ct);
        return ToView(row.Menu, row.ItemCode, row.ItemName, row.SalesPrice, row.CategoryName, schedules, await LoadChildrenAsync([itemId], ct));
    }

    /// <summary>
    /// Puts an item on the menu, or saves a dish already on it, with its
    /// portions, prices and choices in one transaction. Prices are replaced
    /// wholesale; portions, groups and choices are matched by id.
    /// </summary>
    public async Task<MenuResult<long>> SaveItemAsync(long? existingItemId, SaveMenuItemRequest request, CancellationToken ct)
    {
        if (RestaurantMenuRulesRepository.MenuItemRefusal(request) is string refusal)
        {
            return MenuResult<long>.Refuse(refusal);
        }

        long itemId = existingItemId ?? request.ItemId;
        var item = await _db.Items
            .Where(i => i.ItemId == itemId)
            .Select(i => new { i.IsActive, i.IsSales })
            .FirstOrDefaultAsync(ct);
        if (item is null)
        {
            return existingItemId is null ? MenuResult<long>.Refuse("That item does not exist.") : MenuResult<long>.Missing();
        }

        if (!item.IsActive || !item.IsSales)
        {
            return MenuResult<long>.Refuse("Only an active item marked for sale can be on the menu.");
        }

        if (!await _db.MenuCategories.AnyAsync(c => c.MenuCategoryId == request.MenuCategoryId, ct))
        {
            return MenuResult<long>.Refuse("Choose a category that exists.");
        }

        if (request.MenuScheduleId is long scheduleId && !await _db.MenuSchedules.AnyAsync(s => s.MenuScheduleId == scheduleId, ct))
        {
            return MenuResult<long>.Refuse("Choose a timed menu that exists.");
        }

        List<long> linked = request.ModifierGroups.SelectMany(g => g.Modifiers).Where(m => m.LinkedItemId is not null).Select(m => m.LinkedItemId!.Value).Distinct().ToList();
        if (linked.Count > 0 && await _db.Items.CountAsync(i => linked.Contains(i.ItemId), ct) != linked.Count)
        {
            return MenuResult<long>.Refuse("A choice uses up an item that does not exist.");
        }

        await using ITransactionScope tx = await _db.Database.BeginScopeAsync(ct);

        MenuItem? menuItem = await _db.MenuItems.FirstOrDefaultAsync(m => m.MenuItemId == itemId, ct);
        if (existingItemId is null)
        {
            if (menuItem is not null)
            {
                return MenuResult<long>.Refuse("That item is already on the menu.");
            }

            int highest = await _db.MenuItems.Where(m => m.MenuCategoryId == request.MenuCategoryId).Select(m => (int?)m.DisplayOrder).MaxAsync(ct) ?? 0;
            menuItem = new MenuItem { MenuItemId = itemId, DisplayOrder = highest + Gap };
            _db.MenuItems.Add(menuItem);
        }
        else if (menuItem is null)
        {
            return MenuResult<long>.Missing();
        }

        menuItem.MenuCategoryId = request.MenuCategoryId;
        menuItem.MenuScheduleId = request.MenuScheduleId;
        menuItem.Station = request.Station;
        menuItem.IsAvailable = request.IsAvailable;

        if (await SaveVariantsAsync(itemId, request.Variants, ct) is string variantRefusal)
        {
            return MenuResult<long>.Refuse(variantRefusal);
        }

        if (await SaveGroupsAsync(itemId, request.ModifierGroups, ct) is string groupRefusal)
        {
            return MenuResult<long>.Refuse(groupRefusal);
        }

        await _db.SaveChangesAsync(ct);

        // Prices name portions by name, so they are written once every portion has its id.
        Dictionary<string, long> variantIds = await _db.ItemVariants
            .Where(v => v.MenuItemId == itemId)
            .ToDictionaryAsync(v => v.Name, v => v.ItemVariantId, StringComparer.OrdinalIgnoreCase, ct);

        await _db.MenuItemPrices.Where(p => p.MenuItemId == itemId).ExecuteDeleteAsync(ct);
        foreach (SaveMenuItemPriceRequest price in request.Prices)
        {
            _db.MenuItemPrices.Add(new MenuItemPrice
            {
                MenuItemId = itemId,
                ItemVariantId = string.IsNullOrWhiteSpace(price.VariantName) ? null : variantIds[price.VariantName.Trim()],
                OrderType = price.OrderType,
                Price = price.Price,
            });
        }

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return MenuResult<long>.Done(itemId);
    }

    /// <summary>Takes a dish off the menu with its portions, choices and prices. The item itself stays.</summary>
    public async Task<MenuResult<bool>> RemoveItemAsync(long itemId, CancellationToken ct)
    {
        MenuItem? menuItem = await _db.MenuItems.FirstOrDefaultAsync(m => m.MenuItemId == itemId, ct);
        if (menuItem is null)
        {
            return MenuResult<bool>.Missing();
        }

        _db.MenuItems.Remove(menuItem);
        await _db.SaveChangesAsync(ct);
        return MenuResult<bool>.Done(true);
    }

    /// <summary>Marks a dish sold out, or back on.</summary>
    public async Task<MenuResult<bool>> SetItemAvailabilityAsync(long itemId, bool isAvailable, CancellationToken ct)
    {
        int changed = await _db.MenuItems
            .Where(m => m.MenuItemId == itemId)
            .ExecuteUpdateAsync(s => s.SetProperty(m => m.IsAvailable, isAvailable), ct);
        return changed == 0 ? MenuResult<bool>.Missing() : MenuResult<bool>.Done(isAvailable);
    }

    /// <summary>Marks one choice sold out, or back on: no more extra cheese.</summary>
    public async Task<MenuResult<bool>> SetModifierAvailabilityAsync(long modifierId, bool isAvailable, CancellationToken ct)
    {
        int changed = await _db.Modifiers
            .Where(m => m.ModifierId == modifierId)
            .ExecuteUpdateAsync(s => s.SetProperty(m => m.IsAvailable, isAvailable), ct);
        return changed == 0 ? MenuResult<bool>.Missing() : MenuResult<bool>.Done(isAvailable);
    }

    // ---- Ordering and pricing ----

    /// <summary>
    /// The menu as a waiter orders from it: active categories, dishes served at
    /// <paramref name="at"/>, priced for the order type. Sold-out dishes are
    /// listed and marked so the waiter can say so; a dish nothing prices is left out.
    /// </summary>
    public async Task<OrderingMenuView> OrderingMenuAsync(OrderType orderType, DateTime at, CancellationToken ct)
    {
        Dictionary<long, MenuSchedule> schedules = await _db.MenuSchedules.ToDictionaryAsync(s => s.MenuScheduleId, ct);

        var rows = await (
                from m in _db.MenuItems
                join i in _db.Items on m.MenuItemId equals i.ItemId
                join c in _db.MenuCategories on m.MenuCategoryId equals c.MenuCategoryId
                where c.IsActive && i.IsActive && i.IsSales
                orderby c.DisplayOrder, m.DisplayOrder, i.ItemName
                select new { Menu = m, Name = i.PrintName ?? i.ItemName, i.SalesPrice, c.MenuCategoryId, CategoryName = c.Name })
            .ToListAsync(ct);

        rows = rows.Where(r => RestaurantMenuRulesRepository.IsServedAt(Schedule(schedules, r.Menu.MenuScheduleId), at)).ToList();
        Children children = await LoadChildrenAsync(rows.Select(r => r.Menu.MenuItemId).ToList(), ct);

        OrderingMenuView view = new() { OrderType = orderType };
        foreach (var category in rows.GroupBy(r => new { r.MenuCategoryId, r.CategoryName }))
        {
            OrderingCategoryView heading = new() { MenuCategoryId = category.Key.MenuCategoryId, Name = category.Key.CategoryName };
            foreach (var r in category)
            {
                long id = r.Menu.MenuItemId;
                List<MenuItemPrice> prices = children.Prices.GetValueOrDefault(id) ?? [];
                List<ItemVariant> variants = children.Variants.GetValueOrDefault(id) ?? [];

                OrderingItemView dish = new()
                {
                    ItemId = id,
                    Name = r.Name,
                    Station = r.Menu.Station,
                    IsAvailable = r.Menu.IsAvailable,
                    Price = variants.Count == 0 ? RestaurantMenuRulesRepository.PriceOf(r.SalesPrice, null, prices, orderType) : null,
                    Variants = variants
                        .Select(v => new OrderingVariantView
                        {
                            ItemVariantId = v.ItemVariantId,
                            Name = v.Name,
                            Price = RestaurantMenuRulesRepository.PriceOf(r.SalesPrice, v, prices, orderType) ?? v.Price,
                        })
                        .ToList(),
                    ModifierGroups = GroupViews(id, children),
                };

                if (dish.Price is not null || dish.Variants.Count > 0)
                {
                    heading.Items.Add(dish);
                }
            }

            if (heading.Items.Count > 0)
            {
                view.Categories.Add(heading);
            }
        }

        return view;
    }

    /// <summary>
    /// Prices order lines for Sales (TK-146): each dish must be on the menu,
    /// served at <paramref name="at"/>, not sold out, ordered by portion when
    /// it has portions, and carry a valid set of choices. The first line that
    /// fails refuses the lot.
    /// </summary>
    public async Task<(MenuLineRefusal? Refusal, PriceMenuLinesResponse? Priced)> PriceLinesAsync(
        OrderType orderType, DateTime at, IReadOnlyList<PriceMenuLineRequest> lines, CancellationToken ct)
    {
        List<long> ids = lines.Select(l => l.ItemId).Distinct().ToList();

        var dishes = await (
                from m in _db.MenuItems
                join i in _db.Items on m.MenuItemId equals i.ItemId
                join c in _db.MenuCategories on m.MenuCategoryId equals c.MenuCategoryId
                where ids.Contains(m.MenuItemId)
                select new
                {
                    Menu = m,
                    Name = i.PrintName ?? i.ItemName,
                    i.SalesPrice,
                    ItemActive = i.IsActive && i.IsSales,
                    CategoryActive = c.IsActive,
                    i.TaxGroupId,
                    i.SalesUomId,
                    i.IsPriceInclusiveOfTax,
                    i.TaxPreference,
                    i.TrackInventory,
                })
            .ToDictionaryAsync(d => d.Menu.MenuItemId, ct);

        Dictionary<long, MenuSchedule> schedules = await _db.MenuSchedules.ToDictionaryAsync(s => s.MenuScheduleId, ct);
        Children children = await LoadChildrenAsync(ids, ct);

        PriceMenuLinesResponse response = new();
        for (int index = 0; index < lines.Count; index++)
        {
            PriceMenuLineRequest line = lines[index];
            MenuLineRefusal Refuse(string message) => new() { LineIndex = index, Message = message };

            if (!dishes.TryGetValue(line.ItemId, out var dish) || !dish.ItemActive || !dish.CategoryActive)
            {
                return (Refuse("That dish is not on the menu."), null);
            }

            if (!dish.Menu.IsAvailable)
            {
                return (Refuse($"{dish.Name} is sold out."), null);
            }

            if (!RestaurantMenuRulesRepository.IsServedAt(Schedule(schedules, dish.Menu.MenuScheduleId), at))
            {
                return (Refuse($"{dish.Name} is not served at this time."), null);
            }

            List<ItemVariant> variants = children.Variants.GetValueOrDefault(line.ItemId) ?? [];
            ItemVariant? variant = null;
            if (line.ItemVariantId is long variantId)
            {
                variant = variants.FirstOrDefault(v => v.ItemVariantId == variantId);
                if (variant is null)
                {
                    return (Refuse($"{dish.Name} has no such portion."), null);
                }
            }
            else if (variants.Count > 0)
            {
                return (Refuse($"Choose a portion of {dish.Name}."), null);
            }

            List<ModifierGroup> groups = children.Groups.GetValueOrDefault(line.ItemId) ?? [];
            List<Modifier> modifiers = groups.SelectMany(g => children.Modifiers.GetValueOrDefault(g.ModifierGroupId) ?? []).ToList();
            if (RestaurantMenuRulesRepository.ModifierRefusal(groups, modifiers, line.ModifierIds) is string modifierRefusal)
            {
                return (Refuse($"{dish.Name}: {modifierRefusal}"), null);
            }

            decimal? price = RestaurantMenuRulesRepository.PriceOf(dish.SalesPrice, variant, children.Prices.GetValueOrDefault(line.ItemId) ?? [], orderType);
            if (price is null)
            {
                return (Refuse($"{dish.Name} has no price. Give the item a sales price, or price it on the menu."), null);
            }

            List<Modifier> chosen = line.ModifierIds.Select(id => modifiers.First(m => m.ModifierId == id)).ToList();
            decimal unit = price.Value + chosen.Sum(m => m.PriceChange);
            if (unit < 0)
            {
                return (Refuse($"{dish.Name}: the choices take the price below zero."), null);
            }

            response.Lines.Add(new PricedMenuLine
            {
                ItemId = line.ItemId,
                ItemName = dish.Name,
                ItemVariantId = variant?.ItemVariantId,
                VariantName = variant?.Name,
                PortionFactor = variant?.PortionFactor ?? 1m,
                Station = dish.Menu.Station,
                UnitPrice = unit,
                TaxGroupId = dish.TaxGroupId,
                SalesUomId = dish.SalesUomId,
                IsPriceInclusive = dish.IsPriceInclusiveOfTax,
                TaxPreference = dish.TaxPreference.ToString(),
                TrackInventory = dish.TrackInventory,
                Modifiers = chosen.Select(m => new PricedModifier
                {
                    ModifierId = m.ModifierId,
                    Name = m.Name,
                    PriceChange = m.PriceChange,
                    LinkedItemId = m.LinkedItemId,
                    LinkedQuantity = m.LinkedQuantity,
                }).ToList(),
            });
        }

        return (null, response);
    }

    // ---- Helpers ----

    private static MenuSchedule? Schedule(Dictionary<long, MenuSchedule> schedules, long? id) =>
        id is long key && schedules.TryGetValue(key, out MenuSchedule? s) ? s : null;

    private async Task<string?> SaveVariantsAsync(long itemId, List<SaveItemVariantRequest> requested, CancellationToken ct)
    {
        List<ItemVariant> existing = await _db.ItemVariants.Where(v => v.MenuItemId == itemId).ToListAsync(ct);
        if (requested.Any(r => r.ItemVariantId is long id && existing.All(e => e.ItemVariantId != id)))
        {
            return "A portion was removed while you were editing. Reload the dish and try again.";
        }

        HashSet<long> kept = requested.Where(r => r.ItemVariantId is not null).Select(r => r.ItemVariantId!.Value).ToHashSet();
        List<ItemVariant> removed = existing.Where(e => !kept.Contains(e.ItemVariantId)).ToList();
        _db.ItemVariants.RemoveRange(removed);

        // A rename onto a name another row still holds would trip the unique key mid-save.
        if (removed.Count > 0)
        {
            await _db.SaveChangesAsync(ct);
        }

        int order = 0;
        foreach (SaveItemVariantRequest r in requested)
        {
            order += Gap;
            ItemVariant row = r.ItemVariantId is long id ? existing.First(e => e.ItemVariantId == id) : new ItemVariant { MenuItemId = itemId };
            row.Name = r.Name.Trim();
            row.Price = r.Price;
            row.PortionFactor = r.PortionFactor;
            row.DisplayOrder = order;
            if (r.ItemVariantId is null)
            {
                _db.ItemVariants.Add(row);
            }
        }

        return null;
    }

    private async Task<string?> SaveGroupsAsync(long itemId, List<SaveModifierGroupRequest> requested, CancellationToken ct)
    {
        List<ModifierGroup> existing = await _db.ModifierGroups.Where(g => g.MenuItemId == itemId).ToListAsync(ct);
        List<long> groupIds = existing.Select(g => g.ModifierGroupId).ToList();
        List<Modifier> existingModifiers = await _db.Modifiers.Where(m => groupIds.Contains(m.ModifierGroupId)).ToListAsync(ct);

        if (requested.Any(g => g.ModifierGroupId is long id && existing.All(e => e.ModifierGroupId != id))
            || requested.SelectMany(g => g.Modifiers.Select(m => (g.ModifierGroupId, m.ModifierId)))
                .Any(p => p.ModifierId is long id && existingModifiers.All(e => e.ModifierId != id || e.ModifierGroupId != p.ModifierGroupId)))
        {
            return "A choice was removed while you were editing. Reload the dish and try again.";
        }

        HashSet<long> keptGroups = requested.Where(g => g.ModifierGroupId is not null).Select(g => g.ModifierGroupId!.Value).ToHashSet();
        HashSet<long> keptModifiers = requested.SelectMany(g => g.Modifiers).Where(m => m.ModifierId is not null).Select(m => m.ModifierId!.Value).ToHashSet();
        _db.Modifiers.RemoveRange(existingModifiers.Where(m => !keptModifiers.Contains(m.ModifierId)));
        _db.ModifierGroups.RemoveRange(existing.Where(g => !keptGroups.Contains(g.ModifierGroupId)));
        await _db.SaveChangesAsync(ct);

        int groupOrder = 0;
        foreach (SaveModifierGroupRequest g in requested)
        {
            groupOrder += Gap;
            ModifierGroup group = g.ModifierGroupId is long id ? existing.First(e => e.ModifierGroupId == id) : new ModifierGroup { MenuItemId = itemId };
            group.Name = g.Name.Trim();
            group.MinChoices = g.MinChoices;
            group.MaxChoices = g.MaxChoices;
            group.DisplayOrder = groupOrder;
            if (g.ModifierGroupId is null)
            {
                _db.ModifierGroups.Add(group);
                await _db.SaveChangesAsync(ct);
            }

            int order = 0;
            foreach (SaveModifierRequest m in g.Modifiers)
            {
                order += Gap;
                Modifier modifier = m.ModifierId is long mid ? existingModifiers.First(e => e.ModifierId == mid) : new Modifier();
                modifier.ModifierGroupId = group.ModifierGroupId;
                modifier.Name = m.Name.Trim();
                modifier.PriceChange = m.PriceChange;
                modifier.LinkedItemId = m.LinkedItemId;
                modifier.LinkedQuantity = m.LinkedQuantity;
                modifier.IsAvailable = m.IsAvailable;
                modifier.DisplayOrder = order;
                if (m.ModifierId is null)
                {
                    _db.Modifiers.Add(modifier);
                }
            }
        }

        return null;
    }

    private sealed record Children(
        Dictionary<long, List<ItemVariant>> Variants,
        Dictionary<long, List<MenuItemPrice>> Prices,
        Dictionary<long, List<ModifierGroup>> Groups,
        Dictionary<long, List<Modifier>> Modifiers);

    /// <summary>Every portion, price, group and choice of the dishes, in four queries rather than four per dish.</summary>
    private async Task<Children> LoadChildrenAsync(List<long> itemIds, CancellationToken ct)
    {
        List<ItemVariant> variants = await _db.ItemVariants.Where(v => itemIds.Contains(v.MenuItemId)).OrderBy(v => v.DisplayOrder).ToListAsync(ct);
        List<MenuItemPrice> prices = await _db.MenuItemPrices.Where(p => itemIds.Contains(p.MenuItemId)).ToListAsync(ct);
        List<ModifierGroup> groups = await _db.ModifierGroups.Where(g => itemIds.Contains(g.MenuItemId)).OrderBy(g => g.DisplayOrder).ToListAsync(ct);
        List<long> groupIds = groups.Select(g => g.ModifierGroupId).ToList();
        List<Modifier> modifiers = await _db.Modifiers.Where(m => groupIds.Contains(m.ModifierGroupId)).OrderBy(m => m.DisplayOrder).ToListAsync(ct);

        return new Children(
            variants.GroupBy(v => v.MenuItemId).ToDictionary(g => g.Key, g => g.ToList()),
            prices.GroupBy(p => p.MenuItemId).ToDictionary(g => g.Key, g => g.ToList()),
            groups.GroupBy(g => g.MenuItemId).ToDictionary(g => g.Key, g => g.ToList()),
            modifiers.GroupBy(m => m.ModifierGroupId).ToDictionary(g => g.Key, g => g.ToList()));
    }

    private static List<ModifierGroupView> GroupViews(long itemId, Children children) =>
        (children.Groups.GetValueOrDefault(itemId) ?? [])
            .Select(g => new ModifierGroupView
            {
                ModifierGroupId = g.ModifierGroupId,
                Name = g.Name,
                MinChoices = g.MinChoices,
                MaxChoices = g.MaxChoices,
                IsRequired = g.MinChoices > 0,
                Modifiers = (children.Modifiers.GetValueOrDefault(g.ModifierGroupId) ?? [])
                    .Select(m => new ModifierView
                    {
                        ModifierId = m.ModifierId,
                        Name = m.Name,
                        PriceChange = m.PriceChange,
                        LinkedItemId = m.LinkedItemId,
                        LinkedQuantity = m.LinkedQuantity,
                        IsAvailable = m.IsAvailable,
                    })
                    .ToList(),
            })
            .ToList();

    private static MenuItemView ToView(
        MenuItem m, string code, string name, decimal? salesPrice, string categoryName,
        Dictionary<long, string> schedules, Children children)
    {
        List<ItemVariant> variants = children.Variants.GetValueOrDefault(m.MenuItemId) ?? [];
        return new MenuItemView
        {
            ItemId = m.MenuItemId,
            ItemCode = code,
            ItemName = name,
            BasePrice = salesPrice,
            MenuCategoryId = m.MenuCategoryId,
            CategoryName = categoryName,
            MenuScheduleId = m.MenuScheduleId,
            ScheduleName = m.MenuScheduleId is long s ? schedules.GetValueOrDefault(s) : null,
            Station = m.Station,
            IsAvailable = m.IsAvailable,
            DisplayOrder = m.DisplayOrder,
            Variants = variants.Select(v => new ItemVariantView
            {
                ItemVariantId = v.ItemVariantId,
                Name = v.Name,
                Price = v.Price,
                PortionFactor = v.PortionFactor,
            }).ToList(),
            Prices = (children.Prices.GetValueOrDefault(m.MenuItemId) ?? [])
                .OrderBy(p => p.OrderType)
                .Select(p => new MenuItemPriceView
                {
                    OrderType = p.OrderType,
                    ItemVariantId = p.ItemVariantId,
                    VariantName = variants.FirstOrDefault(v => v.ItemVariantId == p.ItemVariantId)?.Name,
                    Price = p.Price,
                })
                .ToList(),
            ModifierGroups = GroupViews(m.MenuItemId, children),
        };
    }
}
