using System;
using Shared.Kernel.Tenancy;
using Shared.Kernel.Restaurant;

namespace Sales.Entity.TableEntities;

public enum KotLineStatus
{
    Sent = 1,
    Preparing = 2,
    Ready = 3,
    Served = 4,
    Cancelled = 5
}

public class Kot : OrgScopedEntity
{
    public long KotId { get; set; }
    public long RestaurantOrderId { get; set; }
    public MenuStation Station { get; set; }
    public string KotNumber { get; set; } = string.Empty;
    public int PrintedCount { get; set; }
}

public class KotLine : OrgScopedEntity
{
    public long KotLineId { get; set; }
    public long KotId { get; set; }
    public long RestaurantOrderDetailId { get; set; }
    public decimal Quantity { get; set; }
    public KotLineStatus Status { get; set; }
    public string? CancelReason { get; set; }
}
