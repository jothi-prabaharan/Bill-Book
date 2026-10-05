using System.ComponentModel.DataAnnotations;
using Sales.Entity.Enums;
using Shared.Kernel.Restaurant;
using Shared.Kernel.Tenancy;

namespace Sales.Entity.TableEntities;

/// <summary>
/// A running restaurant order (TK-146): opened when guests order, added to
/// round by round, and turned into one or more invoices at the bill (TK-148).
/// Nothing here posts; the invoice does.
/// </summary>
public class RestaurantOrder : OrgScopedEntity
{
    public long RestaurantOrderId { get; set; }

    [Required(ErrorMessage = "The order needs a number.")]
    [MaxLength(30, ErrorMessage = "An order number can be at most 30 characters.")]
    public string OrderNo { get; set; } = null!;

    public long NumberingSeriesId { get; set; }

    public DateOnly OrderDate { get; set; }

    /// <summary>When it was opened, in branch local time: what the menu was priced at.</summary>
    public DateTime OpenedAt { get; set; }

    public OrderType OrderType { get; set; }

    /// <summary>The table, or its group's head. Null for takeaway, delivery, or dine-in without tables.</summary>
    public long? RestaurantTableId { get; set; }

    /// <summary>A user in the master database; no foreign key (rule 8).</summary>
    public Guid? WaiterUserId { get; set; }

    /// <summary>A contact in the contacts database; no foreign key (rule 8).</summary>
    public long? ContactId { get; set; }

    [Range(1, 500, ErrorMessage = "Guests must be between 1 and 500.")]
    public int? Guests { get; set; }

    public RestaurantOrderStatus Status { get; set; } = RestaurantOrderStatus.Open;

    [MaxLength(250, ErrorMessage = "A note can be at most 250 characters.")]
    public string? Notes { get; set; }

    [MaxLength(250, ErrorMessage = "A reason can be at most 250 characters.")]
    public string? CancelReason { get; set; }

    /// <summary>The order its lines went to when it was merged.</summary>
    public long? MergedIntoOrderId { get; set; }

    public List<RestaurantOrderLine> Lines { get; set; } = [];
}

/// <summary>
/// One dish on an order (TK-146), priced by the menu when it was added. Names
/// and prices are copied, so a later menu change never alters an order taken.
/// </summary>
public class RestaurantOrderLine : OrgScopedEntity
{
    public long RestaurantOrderLineId { get; set; }

    public long RestaurantOrderId { get; set; }

    public long ItemId { get; set; }

    [Required(ErrorMessage = "The dish needs a name.")]
    [MaxLength(200, ErrorMessage = "A dish name can be at most 200 characters.")]
    public string ItemName { get; set; } = null!;

    public long? ItemVariantId { get; set; }

    [MaxLength(50, ErrorMessage = "A portion's name can be at most 50 characters.")]
    public string? VariantName { get; set; }

    public decimal PortionFactor { get; set; } = 1m;

    public MenuStation Station { get; set; }

    [Range(typeof(decimal), "0.001", "9999", ErrorMessage = "The quantity must be more than 0.")]
    public decimal Quantity { get; set; }

    /// <summary>The portion's price for the order type plus every choice.</summary>
    public decimal UnitPrice { get; set; }

    [MaxLength(200, ErrorMessage = "A note can be at most 200 characters.")]
    public string? Note { get; set; }

    public KitchenStatus KitchenStatus { get; set; } = KitchenStatus.NotSent;

    [MaxLength(250, ErrorMessage = "A reason can be at most 250 characters.")]
    public string? CancelReason { get; set; }

    public int LineNo { get; set; }

    // What the bill needs (TK-148), copied from the item master when the dish was added.

    public long? TaxGroupId { get; set; }

    public long UomId { get; set; }

    public bool IsPriceInclusive { get; set; }

    public Shared.Kernel.Documents.TaxTreatment TaxTreatment { get; set; }

    /// <summary>Whether billing it issues stock: a bottled drink does, a dish made to order does not.</summary>
    public bool IsStocked { get; set; }

    /// <summary>The bill this dish went on; null until billed. A dish is billed once (TK-148).</summary>
    public long? RestaurantBillId { get; set; }

    public List<RestaurantOrderLineModifier> Modifiers { get; set; } = [];
}

/// <summary>A choice on a line (TK-146), copied from the menu with its price change and any stock it uses.</summary>
public class RestaurantOrderLineModifier : OrgScopedEntity
{
    public long RestaurantOrderLineModifierId { get; set; }

    public long RestaurantOrderLineId { get; set; }

    public long ModifierId { get; set; }

    [Required(ErrorMessage = "The choice needs a name.")]
    [MaxLength(100, ErrorMessage = "A choice's name can be at most 100 characters.")]
    public string Name { get; set; } = null!;

    public decimal PriceChange { get; set; }

    public long? LinkedItemId { get; set; }

    public decimal? LinkedQuantity { get; set; }
}

/// <summary>
/// A restaurant bill (TK-148): the invoice one or more orders' dishes were
/// billed on, with what the invoice cannot say itself — the service charge
/// rate and the tip, which is not the branch's income and is posted apart.
/// </summary>
public class RestaurantBill : OrgScopedEntity
{
    public long RestaurantBillId { get; set; }

    /// <summary>The invoice in Sales' own tables; a bill has exactly one.</summary>
    public long InvoiceId { get; set; }

    public decimal ServiceChargePercent { get; set; }

    public decimal ServiceChargeAmount { get; set; }

    public decimal TipAmount { get; set; }

    /// <summary>The cash or bank account the tip was paid into; null with no tip.</summary>
    public long? TipBankAccountId { get; set; }

    /// <summary>When it was billed, in branch local time.</summary>
    public DateTime BilledAt { get; set; }

    /// <summary>
    /// The loyalty points the bill earned its contact (TK-166), worked out at
    /// billing from the branch's rate; 0 for none. Kept so a retry records
    /// what was due then, not what today's rate would give.
    /// </summary>
    public int LoyaltyPoints { get; set; }
}
