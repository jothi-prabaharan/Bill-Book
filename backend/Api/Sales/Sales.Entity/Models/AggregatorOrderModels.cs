using System.ComponentModel.DataAnnotations;
using Sales.Entity.Enums;

namespace Sales.Entity.Models;

/// <summary>
/// An order from Zomato or Swiggy (TK-167). The dishes are named by this
/// branch's own item ids, which is what the restaurant gives each aggregator
/// as its partner item id, so no mapping table is needed.
/// </summary>
public sealed class ReceiveAggregatorOrderRequest
{
    [EnumDataType(typeof(OrderAggregator), ErrorMessage = "Choose Zomato or Swiggy.")]
    public OrderAggregator Aggregator { get; set; }

    [Required(ErrorMessage = "The aggregator's order id is required.")]
    [MaxLength(64, ErrorMessage = "The aggregator's order id can be at most 64 characters.")]
    public string ExternalOrderId { get; set; } = null!;

    [MaxLength(100, ErrorMessage = "A customer's name can be at most 100 characters.")]
    public string? CustomerName { get; set; }

    [MaxLength(30, ErrorMessage = "A phone number can be at most 30 characters.")]
    public string? CustomerPhone { get; set; }

    [MaxLength(500, ErrorMessage = "An address can be at most 500 characters.")]
    public string? DeliveryAddress { get; set; }

    [Range(typeof(decimal), "0", "9999999", ErrorMessage = "The aggregator's total must be 0 or more.")]
    public decimal? AggregatorTotal { get; set; }

    [MaxLength(200, ErrorMessage = "A note can be at most 200 characters.")]
    public string? Notes { get; set; }

    [MinLength(1, ErrorMessage = "The order needs at least one dish.")]
    public List<AddOrderLineRequest> Lines { get; set; } = [];
}

/// <summary>What intake answers: the order, and whether this id had been received already.</summary>
public sealed class AggregatorOrderReceipt
{
    public long RestaurantOrderId { get; set; }

    public string OrderNo { get; set; } = null!;

    /// <summary>True when the same aggregator order id had come in before; nothing was created this time.</summary>
    public bool AlreadyReceived { get; set; }

    /// <summary>The kitchen tickets made, empty when the branch does not use the kitchen or the order was already received.</summary>
    public List<long> KotIds { get; set; } = [];
}

public sealed class AggregatorOrderView
{
    public long AggregatorOrderId { get; set; }

    public OrderAggregator Aggregator { get; set; }

    public string ExternalOrderId { get; set; } = null!;

    public long RestaurantOrderId { get; set; }

    public string OrderNo { get; set; } = null!;

    public DateTime ReceivedAt { get; set; }

    public string? CustomerName { get; set; }

    public string? CustomerPhone { get; set; }

    public string? DeliveryAddress { get; set; }

    public decimal? AggregatorTotal { get; set; }
}
