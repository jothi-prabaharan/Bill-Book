using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Inventory.Entity.TableEntities;
using Shared.Kernel.Restaurant;

namespace Inventory.Repository.Repositories;

public class MenuRepository
{
    private readonly InventoryDbContext _db;

    public MenuRepository(InventoryDbContext db)
    {
        _db = db;
    }

    public async Task<List<MenuItem>> GetMenuAsync(OrderType orderType, TimeSpan timeOfDay)
    {
        // filter by availability, schedule, and if price for order type exists
        var query = _db.MenuItems
            .Where(m => m.IsAvailable)
            .Where(m => m.MenuScheduleId == null || 
                        (_db.MenuSchedules.Any(s => s.MenuScheduleId == m.MenuScheduleId && 
                                                    timeOfDay >= s.StartTime && timeOfDay <= s.EndTime)))
            .Where(m => _db.MenuItemPrices.Any(p => p.MenuItemId == m.MenuItemId && p.OrderType == orderType));

        return await query.ToListAsync();
    }

    public async Task FlipAvailabilityAsync(long menuItemId, bool isAvailable)
    {
        var item = await _db.MenuItems.FirstOrDefaultAsync(m => m.MenuItemId == menuItemId);
        if (item != null)
        {
            item.IsAvailable = isAvailable;
            await _db.SaveChangesAsync();
        }
    }

    public async Task<decimal> PriceLineAsync(long menuItemId, long? variantId, List<long> modifierIds, OrderType orderType)
    {
        var basePrice = await _db.MenuItemPrices
            .Where(p => p.MenuItemId == menuItemId && p.OrderType == orderType)
            .Select(p => p.Price)
            .FirstOrDefaultAsync();

        if (variantId.HasValue)
        {
            var variantPrice = await _db.ItemVariants
                .Where(v => v.ItemVariantId == variantId.Value && v.MenuItemId == menuItemId)
                .Select(v => v.Price)
                .FirstOrDefaultAsync();
            basePrice = variantPrice; // variant price overrides base price? Or adds? Usually overrides.
        }

        if (modifierIds != null && modifierIds.Any())
        {
            var modifierPrices = await _db.Modifiers
                .Where(m => modifierIds.Contains(m.ModifierId))
                .SumAsync(m => m.PriceChange);
            basePrice += modifierPrices;
        }

        return basePrice;
    }
}

