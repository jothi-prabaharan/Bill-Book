using System.ComponentModel.DataAnnotations;
using Sales.Entity.Enums;
using Shared.Kernel.Restaurant;

namespace Sales.Entity.Models;

/// <summary>A dish to add to an order (TK-146). The menu prices it; the caller never sends a price.</summary>
public sealed class AddOrderLineRequest
{
    [Range(1, long.MaxValue, ErrorMessage = "Choose a dish.")]
    public long ItemId { get; set; }

    public long? ItemVariantId { get; set; }

    public List<long> ModifierIds { get; set; } = [];

    [Range(typeof(decimal), "0.001", "9999", ErrorMessage = "The quantity must be more than 0.")]
    public decimal Quantity { get; set; } = 1m;

    [MaxLength(200, ErrorMessage = "A note can be at most 200 characters.")]
    public string? Note { get; set; }
}

public sealed class OpenRestaurantOrderRequest
{
    public OrderType OrderType { get; set; } = OrderType.DineIn;

    public long? RestaurantTableId { get; set; }

    public Guid? WaiterUserId { get; set; }

    public long? ContactId { get; set; }

    [Range(1, 500, ErrorMessage = "Guests must be between 1 and 500.")]
    public int? Guests { get; set; }

    [MaxLength(250, ErrorMessage = "A note can be at most 250 characters.")]
    public string? Notes { get; set; }

    public List<AddOrderLineRequest> Lines { get; set; } = [];
}

public sealed class AddOrderLinesRequest
{
    [MinLength(1, ErrorMessage = "Add at least one dish.")]
    public List<AddOrderLineRequest> Lines { get; set; } = [];
}

public sealed class UpdateOrderLineRequest
{
    [Range(typeof(decimal), "0.001", "9999", ErrorMessage = "The quantity must be more than 0.")]
    public decimal Quantity { get; set; }

    [MaxLength(200, ErrorMessage = "A note can be at most 200 characters.")]
    public string? Note { get; set; }
}

public sealed class UpdateOrderDetailsRequest
{
    public Guid? WaiterUserId { get; set; }

    public long? ContactId { get; set; }

    [Range(1, 500, ErrorMessage = "Guests must be between 1 and 500.")]
    public int? Guests { get; set; }

    [MaxLength(250, ErrorMessage = "A note can be at most 250 characters.")]
    public string? Notes { get; set; }
}

public sealed class ChangeOrderTypeRequest
{
    public OrderType OrderType { get; set; }
}

public sealed class MoveOrderRequest
{
    [Range(1, long.MaxValue, ErrorMessage = "Choose a table.")]
    public long RestaurantTableId { get; set; }
}

public sealed class CancelOrderRequest
{
    [Required(ErrorMessage = "Say why the order is cancelled.")]
    [MaxLength(250, ErrorMessage = "A reason can be at most 250 characters.")]
    public string Reason { get; set; } = string.Empty;
}

public sealed class RestaurantOrderView
{
    public long RestaurantOrderId { get; set; }

    public string OrderNo { get; set; } = string.Empty;

    public DateOnly OrderDate { get; set; }

    public DateTime OpenedAt { get; set; }

    public OrderType OrderType { get; set; }

    public long? RestaurantTableId { get; set; }

    public string? TableCode { get; set; }

    public Guid? WaiterUserId { get; set; }

    public string? WaiterName { get; set; }

    public long? ContactId { get; set; }

    public string? ContactName { get; set; }

    public int? Guests { get; set; }

    public RestaurantOrderStatus Status { get; set; }

    public string? Notes { get; set; }

    public string? CancelReason { get; set; }

    /// <summary>Every line not cancelled, at its quantity and unit price. Tax and service charge come at the bill.</summary>
    public decimal Total { get; set; }

    public int LineCount { get; set; }

    /// <summary>Lines not yet sent to the kitchen.</summary>
    public int UnsentCount { get; set; }

    public List<RestaurantOrderLineView> Lines { get; set; } = [];
}

public sealed class RestaurantOrderLineView
{
    public long RestaurantOrderLineId { get; set; }

    public int LineNo { get; set; }

    public long ItemId { get; set; }

    public string ItemName { get; set; } = string.Empty;

    public long? ItemVariantId { get; set; }

    public string? VariantName { get; set; }

    public MenuStation Station { get; set; }

    public decimal Quantity { get; set; }

    public decimal UnitPrice { get; set; }

    public decimal Amount { get; set; }

    public string? Note { get; set; }

    public KitchenStatus KitchenStatus { get; set; }

    public string? CancelReason { get; set; }

    public List<RestaurantOrderModifierView> Modifiers { get; set; } = [];
}

public sealed class RestaurantOrderModifierView
{
    public long ModifierId { get; set; }

    public string Name { get; set; } = string.Empty;

    public decimal PriceChange { get; set; }
}

/// <summary>How an order write ended (TK-146).</summary>
public enum OrderOutcome
{
    Ok = 0,
    NotFound = 1,
    Refused = 2,

    /// <summary>A service the order depends on (Master's settings, Inventory's menu) could not be reached.</summary>
    Unavailable = 3,
}

public sealed record OrderResult<T>(OrderOutcome Outcome, T? Value = default, string? Message = null)
{
    public static OrderResult<T> Done(T value) => new(OrderOutcome.Ok, value);

    public static OrderResult<T> Missing() => new(OrderOutcome.NotFound);

    public static OrderResult<T> Refuse(string message) => new(OrderOutcome.Refused, default, message);

    public static OrderResult<T> Down(string message) => new(OrderOutcome.Unavailable, default, message);
}
