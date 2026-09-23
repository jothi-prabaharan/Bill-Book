using System.ComponentModel.DataAnnotations;
using Sales.Entity.Enums;
using Shared.Kernel.Documents;
using Shared.Kernel.Tax;

namespace Sales.Entity.Models;

public class DeliveryChallanListItem
{
    public long DeliveryChallanId { get; set; }
    public long? SalesOrderId { get; set; }
    public DateOnly DocumentDate { get; set; }
    public string DocumentNo { get; set; } = null!;
    public long ContactId { get; set; }
    public string ContactName { get; set; } = null!;

    /// <summary>
    /// Draft / ReadyToPost / Posted / Void, by name. A string for the same reason
    /// the invoice's is: this API serializes enums as numbers, and the screen
    /// compares against the names.
    /// </summary>
    public string Status { get; set; } = null!;
    public DateOnly DispatchDate { get; set; }
    public decimal TotalAmount { get; set; }
}

public class DeliveryChallanView
{
    public long DeliveryChallanId { get; set; }
    public long? SalesOrderId { get; set; }
    public DateOnly DocumentDate { get; set; }
    public string DocumentNo { get; set; } = null!;
    public long ContactId { get; set; }
    public string ContactName { get; set; } = null!;

    /// <summary>By name — see <see cref="DeliveryChallanListItem.Status"/>.</summary>
    public string Status { get; set; } = null!;
    public ChallanType ChallanType { get; set; } = ChallanType.Sale;
    public string? ContactGstin { get; set; }
    public string? VoidReason { get; set; }
    public string? VehicleNo { get; set; }
    public string? TransporterName { get; set; }
    public string? EwayBillNo { get; set; }
    public DateOnly? EwayBillDate { get; set; }
    public DateOnly DispatchDate { get; set; }

    public string CurrencyCode { get; set; } = null!;
    public decimal ExchangeRate { get; set; }

    public string? Notes { get; set; }
    public string? BillingAddress { get; set; }
    public string? ShippingAddress { get; set; }

    public int PlaceOfSupplyStateId { get; set; }
    public bool IsInterState { get; set; }

    public decimal SubTotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TaxableAmount { get; set; }
    public decimal CgstAmount { get; set; }
    public decimal SgstAmount { get; set; }
    public decimal IgstAmount { get; set; }
    public decimal CessAmount { get; set; }
    public decimal RoundOffAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal TotalAmountBase { get; set; }

    public List<DeliveryChallanLineView> Lines { get; set; } = [];
}

public class DeliveryChallanLineView
{
    public long DeliveryChallanDetailId { get; set; }

    /// <summary>The order line this delivers against. Null on a challan raised without an order.</summary>
    public long? SalesOrderDetailId { get; set; }
    public long? ItemId { get; set; }
    public string? ItemLabel { get; set; }
    public string? HsnSacCode { get; set; }
    public string? Description { get; set; }
    public long? TaxGroupId { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal DiscountPercent { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal LineTotal { get; set; }
    public decimal TaxAmount { get; set; }

    public List<DeliveryChallanLineTaxView> Taxes { get; set; } = [];
}

public class DeliveryChallanLineTaxView
{
    public TaxComponent TaxComponent { get; set; }
    public long SubAccountId { get; set; }
    public decimal Amount { get; set; }
}

public class SaveDeliveryChallanRequest
{

    /// <summary>
    /// The print template this document should use. Null means the branch's
    /// default for this document type, which is also where a template that has
    /// since been deleted resolves to — so the choice can never make a document
    /// unprintable.
    /// </summary>
    public long? PrintTemplateId { get; set; }
    public long? DeliveryChallanId { get; set; }
    public long? SalesOrderId { get; set; }
    
    [Required]
    public DateOnly DocumentDate { get; set; }
    
    [Range(1, long.MaxValue, ErrorMessage = "Contact is required")]
    public long ContactId { get; set; }

    /// <summary>The contact's GSTIN, as of this document. See <see cref="PlaceOfSupplyStateCode"/>.</summary>
    [MaxLength(15, ErrorMessage = "GSTIN must be 15 characters.")]
    public string? ContactGstin { get; set; }

    /// <summary>
    /// The two-digit state code the supply is made in. Falls back to the state
    /// read off <see cref="ContactGstin"/> when left blank — see
    /// <c>Shared.Kernel.Tax.PlaceOfSupply</c>.
    /// </summary>
    public string? PlaceOfSupplyStateCode { get; set; }

    public ChallanType ChallanType { get; set; } = ChallanType.Sale;
    public string? VehicleNo { get; set; }
    public string? TransporterName { get; set; }
    public string? EwayBillNo { get; set; }
    public DateOnly? EwayBillDate { get; set; }
    public DateOnly DispatchDate { get; set; }

    [MaxLength(3)]
    public string? CurrencyCode { get; set; }
    
    public decimal ExchangeRate { get; set; } = 1m;

    [MaxLength(500)]
    public string? Notes { get; set; }

    [MaxLength(100)]
    public string? BillingAddress { get; set; }

    [MaxLength(100)]
    public string? ShippingAddress { get; set; }

    public List<SaveDeliveryChallanLineRequest> Lines { get; set; } = [];
}

public class SaveDeliveryChallanLineRequest
{
    /// <summary>
    /// Required. A challan moves goods, so every line names the item that moves;
    /// a free-text line has nothing for Inventory to issue.
    /// </summary>
    [Range(1, long.MaxValue, ErrorMessage = "Choose the item on every line.")]
    public long ItemId { get; set; }

    [Range(0.000001, double.MaxValue, ErrorMessage = "Quantity must be greater than zero.")]
    public decimal Quantity { get; set; }

    [Range(typeof(decimal), "0", "79228162514264337593543950335",
        ErrorMessage = "Unit price cannot be negative.")]
    public decimal UnitPrice { get; set; }

    [Range(typeof(decimal), "0", "100", ErrorMessage = "Discount must be between 0 and 100 percent.")]
    public decimal DiscountPercent { get; set; }

    /// <summary>
    /// The tax group, the way the invoice takes it. Read ahead of
    /// <see cref="TaxGroupIds"/>, which stays for callers that already send a list.
    /// </summary>
    public long? TaxGroupId { get; set; }

    public List<long> TaxGroupIds { get; set; } = [];

    /// <summary>
    /// The order line this delivers against. <b>Required on every line when the
    /// challan names a sales order, and refused on every line when it does not</b>
    /// — it is the only thing that tells posting which order line's delivered and
    /// reserved quantities to move. Without it a challan against an order issued
    /// stock and left the order exactly as it was.
    /// </summary>
    public long? SalesOrderDetailId { get; set; }
}

/// <summary>
/// Why a challan is being withdrawn.
///
/// The reason is <b>required</b>, and not merely as a matter of record: the
/// <c>chk_deliverychallans_void_stamp</c> constraint ties <c>VoidedAt</c> and
/// <c>VoidReason</c> together, so a void that carried only the timestamp was
/// refused by the database. Voiding a challan threw until this request existed.
/// </summary>
public class VoidDeliveryChallanRequest
{
    [Required(ErrorMessage = "Say why this delivery challan is being voided.")]
    [MaxLength(300, ErrorMessage = "Reason cannot exceed 300 characters.")]
    public string Reason { get; set; } = null!;
}

/// <summary>Why a delivery challan was refused. Every value is something a user can act on.</summary>
public enum DeliveryChallanOutcome
{
    Ok = 0,
    NotFound = 1,

    /// <summary>The lifecycle refused the move. <c>Detail</c> carries its own words.</summary>
    LifecycleRefused = 2,

    /// <summary>A line contradicts the challan or its order.</summary>
    LineInvalid = 3,

    /// <summary>Place of supply could not be resolved, or the GSTIN contradicts it.</summary>
    PlaceOfSupplyRefused = 4,

    /// <summary>Branch settings or a tax rate could not be read. Transient — retry.</summary>
    RatesUnavailable = 5,

    /// <summary>The sales order named is missing, unconfirmed, closed, or for another customer.</summary>
    SourceInvalid = 6,

    /// <summary>A line would deliver more than its order line still has outstanding.</summary>
    OverDelivered = 7,

    /// <summary>Inventory refused the issue — usually not enough on hand.</summary>
    StockRefused = 8,
}

public sealed record DeliveryChallanResult(
    DeliveryChallanOutcome Outcome, long DeliveryChallanId = 0, string? Detail = null);
