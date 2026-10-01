using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Inventory.Entity.TableEntities;
using Inventory.Repository.Repositories;

namespace Inventory.Repository.Repositories;

public class RecipeRepository
{
    private readonly InventoryDbContext _db;

    public RecipeRepository(InventoryDbContext db)
    {
        _db = db;
    }

    public async Task<Recipe?> GetRecipeForMenuItemAsync(long menuItemId)
    {
        return await _db.Recipes
            .FirstOrDefaultAsync(r => r.MenuItemId == menuItemId);
    }
}

public class WastageRepository
{
    private readonly InventoryDbContext _db;

    public WastageRepository(InventoryDbContext db)
    {
        _db = db;
    }

    public async Task ReportWastageAsync(WastageEntry entry)
    {
        _db.WastageEntries.Add(entry);
        await _db.SaveChangesAsync();
    }
}
