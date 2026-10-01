using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Inventory.Entity.TableEntities;
using Shared.Kernel.Restaurant;

namespace Inventory.Repository.Repositories;

public class TableRepository
{
    private readonly InventoryDbContext _db;

    public TableRepository(InventoryDbContext db)
    {
        _db = db;
    }

    public async Task<List<RestaurantTable>> GetTablesAsync()
    {
        return await _db.RestaurantTables.OrderBy(t => t.Number).ToListAsync();
    }

    public async Task UpdateTableStatusAsync(long id, TableStatus status)
    {
        var table = await _db.RestaurantTables.FindAsync(id);
        if (table != null)
        {
            table.Status = status;
            await _db.SaveChangesAsync();
        }
    }
}
