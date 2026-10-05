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

    /// <summary>The bill the dish went on; null until it is billed (TK-148).</summary>
    public long? RestaurantBillId { get; set; }

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

/// <summary>A dish's next step in the kitchen (TK-147).</summary>
public sealed class SetKitchenStatusRequest
{
    public KitchenStatus Status { get; set; }
}

public sealed class CancelKotLineRequest
{
    [Required(ErrorMessage = "Say why the dish is cancelled.")]
    [MaxLength(250, ErrorMessage = "A reason can be at most 250 characters.")]
    public string Reason { get; set; } = string.Empty;
}

/// <summary>A kitchen ticket as the kitchen display and the order screen show it (TK-147).</summary>
public sealed class KotView
{
    public long KotId { get; set; }

    public string KotNo { get; set; } = string.Empty;

    public long RestaurantOrderId { get; set; }

    public string OrderNo { get; set; } = string.Empty;

    public OrderType OrderType { get; set; }

    public string? TableCode { get; set; }

    public string? WaiterName { get; set; }

    public MenuStation Station { get; set; }

    public DateTime SentAt { get; set; }

    public int PrintedCount { get; set; }

    public List<KotLineView> Lines { get; set; } = [];
}

public sealed class KotLineView
{
    public long KotLineId { get; set; }

    public long RestaurantOrderLineId { get; set; }

    public string ItemName { get; set; } = string.Empty;

    public string? VariantName { get; set; }

    public decimal Quantity { get; set; }

    public string? Note { get; set; }

    public List<string> Modifiers { get; set; } = [];

    public KitchenStatus Status { get; set; }

    public string? CancelReason { get; set; }

    public DateTime StatusChangedAt { get; set; }
}

/// <summary>
/// Billing restaurant orders (TK-148). One order billed whole is the common
/// case; several orders make one bill (merging tables' bills), and naming some
/// lines bills only those (splitting a bill) — each dish is billed once.
/// </summary>
public class BillRestaurantOrdersRequest
{
    [MinLength(1, ErrorMessage = "Choose at least one order to bill.")]
    public List<long> OrderIds { get; set; } = [];

    /// <summary>The dishes to bill; empty bills every dish not yet billed.</summary>
    public List<long> LineIds { get; set; } = [];

    /// <summary>The customer the invoice is made out to; empty takes the order's, then the walk-in customer.</summary>
    public long? ContactId { get; set; }

    /// <summary>Off waives the branch's service charge on this bill.</summary>
    public bool ApplyServiceCharge { get; set; } = true;

    [Range(typeof(decimal), "0", "9999999", ErrorMessage = "A tip cannot be negative.")]
    public decimal TipAmount { get; set; }

    /// <summary>The cash or bank account the tip was paid into.</summary>
    public long? TipBankAccountId { get; set; }

    public List<PosTenderRequest> Tenders { get; set; } = [];
}

/// <summary>What a bill will come to before it is made (TK-148): the same lines and tax the invoice will have.</summary>
public sealed class BillPreview
{
    public decimal SubTotal { get; set; }

    public decimal ServiceChargePercent { get; set; }

    public decimal ServiceChargeAmount { get; set; }

    public decimal TaxAmount { get; set; }

    public decimal RoundOff { get; set; }

    public decimal Total { get; set; }

    public decimal TipAmount { get; set; }

    /// <summary>The bill and the tip: what the guests pay.</summary>
    public decimal AmountToPay { get; set; }

    public List<long> LineIds { get; set; } = [];

    /// <summary>What the settings still require of the bill, the first one only; null when it can be billed.</summary>
    public string? Problem { get; set; }
}

public sealed class RestaurantBillView
{
    public long RestaurantBillId { get; set; }

    public long InvoiceId { get; set; }

    public string InvoiceNo { get; set; } = string.Empty;

    public decimal Total { get; set; }

    public decimal ServiceChargeAmount { get; set; }

    public decimal TipAmount { get; set; }

    public decimal Change { get; set; }

    public DateTime BilledAt { get; set; }

    /// <summary>The orders now fully billed; an order with dishes left stays open.</summary>
    public List<long> ClosedOrderIds { get; set; } = [];
}
