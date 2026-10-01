using System;
using Shared.Kernel.Tenancy;
using Shared.Kernel.Restaurant;

namespace Sales.Entity.TableEntities;

public enum RestaurantOrderStatus
{
    Open = 1,
    Billed = 2,
    Cancelled = 3
}

public class RestaurantOrder : OrgScopedEntity
{
    public long RestaurantOrderId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public OrderType OrderType { get; set; }
    public long? RestaurantTableId { get; set; }
    public Guid? WaiterUserId { get; set; }
    public long? ContactId { get; set; }
    public RestaurantOrderStatus Status { get; set; }
    public string? CancelReason { get; set; }
    public decimal TotalAmount { get; set; } // Updated dynamically as lines are added
}

public class RestaurantOrderDetail : OrgScopedEntity
{
    public long RestaurantOrderDetailId { get; set; }
    public long RestaurantOrderId { get; set; }
    public long MenuItemId { get; set; }
    public long? VariantId { get; set; }
    public long[]? ModifierIds { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice { get; set; }
    public string? Notes { get; set; }
}

