using Shared.Kernel.Tenancy;
using Shared.Kernel.Restaurant;

namespace Inventory.Entity.TableEntities;

public class RestaurantTable : OrgScopedEntity
{
    public long RestaurantTableId { get; set; }
    public string Number { get; set; } = string.Empty;
    public int Capacity { get; set; }
    public TableStatus Status { get; set; }
}
