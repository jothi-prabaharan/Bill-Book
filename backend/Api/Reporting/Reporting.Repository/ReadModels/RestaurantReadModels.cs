#pragma warning disable CS8618
using Shared.Kernel.Tenancy;

namespace Reporting.Repository.ReadModels;

// -----------------------------------------------------------------------------
// Restaurant (sal, inv) read models, for the restaurant reports (TK-150).
// Sales and Inventory store these enums by name, so they are read as names.
// -----------------------------------------------------------------------------

/// <summary><c>sal.Floors</c>, read-only.</summary>
public class FloorRead : OrgScopedEntity
{
    public long FloorId { get; set; }
    public string Name { get; set; }
    public int DisplayOrder { get; set; }
}

/// <summary><c>sal.RestaurantTables</c>, read-only.</summary>
public class RestaurantTableRead : OrgScopedEntity
{
    public long RestaurantTableId { get; set; }
    public long FloorId { get; set; }
    public string Code { get; set; }
    public int Seats { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; }
}

/// <summary><c>sal.RestaurantOrders</c>, read-only.</summary>
public class RestaurantOrderRead : OrgScopedEntity
{
    public long RestaurantOrderId { get; set; }
    public string OrderNo { get; set; }
    public DateOnly OrderDate { get; set; }
    public DateTime OpenedAt { get; set; }

    /// <summary><c>DineIn</c>, <c>Takeaway</c> or <c>Delivery</c>.</summary>
    public string OrderType { get; set; }

    public long? RestaurantTableId { get; set; }
    public Guid? WaiterUserId { get; set; }
    public int? Guests { get; set; }

    /// <summary><c>Open</c>, <c>Billed</c>, <c>Cancelled</c> or <c>Merged</c>.</summary>
    public string Status { get; set; }
}

/// <summary><c>sal.RestaurantOrderLines</c>, read-only. <c>UnitPrice</c> already carries the modifiers.</summary>
public class RestaurantOrderLineRead : OrgScopedEntity
{
    public long RestaurantOrderLineId { get; set; }
    public long RestaurantOrderId { get; set; }
    public long ItemId { get; set; }
    public string ItemName { get; set; }
    public string? VariantName { get; set; }

    /// <summary>The kitchen station, by name.</summary>
    public string Station { get; set; }

    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }

    /// <summary><c>NotSent</c> to <c>Served</c>, or <c>Cancelled</c>.</summary>
    public string KitchenStatus { get; set; }

    public string? CancelReason { get; set; }
    public long? RestaurantBillId { get; set; }
}

/// <summary><c>sal.RestaurantBills</c>, read-only: the restaurant's part of a sales invoice.</summary>
public class RestaurantBillRead : OrgScopedEntity
{
    public long RestaurantBillId { get; set; }
    public long InvoiceId { get; set; }
    public decimal ServiceChargeAmount { get; set; }
    public decimal TipAmount { get; set; }
    public DateTime BilledAt { get; set; }
}

/// <summary><c>sal.Kots</c>, read-only.</summary>
public class KotRead : OrgScopedEntity
{
    public long KotId { get; set; }
    public long RestaurantOrderId { get; set; }
    public string KotNo { get; set; }
    public string Station { get; set; }
    public DateTime SentAt { get; set; }
}

/// <summary><c>sal.KotLines</c>, read-only. <c>ModifiedBy</c> on a cancelled line is who cancelled it.</summary>
public class KotLineRead : OrgScopedEntity
{
    public long KotLineId { get; set; }
    public long KotId { get; set; }
    public long RestaurantOrderLineId { get; set; }
    public decimal Quantity { get; set; }

    /// <summary><c>Sent</c> to <c>Served</c>, or <c>Cancelled</c>.</summary>
    public string Status { get; set; }

    public string? CancelReason { get; set; }
    public DateTime StatusChangedAt { get; set; }
}

/// <summary><c>sal.InvoiceTenders</c>, read-only: how an invoice was paid at the counter.</summary>
public class InvoiceTenderRead : OrgScopedEntity
{
    public long InvoiceTenderId { get; set; }
    public long InvoiceId { get; set; }

    /// <summary><c>Cash</c>, <c>Card</c> or <c>Upi</c>.</summary>
    public string Mode { get; set; }

    public decimal Amount { get; set; }
}

/// <summary><c>inv.WastageEntries</c>, read-only. Its stock went out on the named stock adjustment.</summary>
public class WastageEntryRead : OrgScopedEntity
{
    public long WastageEntryId { get; set; }
    public DateOnly EntryDate { get; set; }
    public long ItemId { get; set; }
    public decimal Quantity { get; set; }
    public long UomId { get; set; }

    /// <summary><c>Spoiled</c>, <c>Expired</c>, <c>Burnt</c> and so on, by name.</summary>
    public string Reason { get; set; }

    public string? Note { get; set; }
    public long StockAdjustmentId { get; set; }
}
