using System.ComponentModel.DataAnnotations;
using Sales.Entity.Enums;
using Shared.Kernel.Tenancy;

namespace Sales.Entity.TableEntities;

/// <summary>
/// An order that arrived from a delivery aggregator (TK-167): the aggregator's
/// own order id, kept so the same order delivered twice is one order, and so
/// a later settlement can be matched to it. The order itself is an ordinary
/// <see cref="RestaurantOrder"/> of type Delivery; this row only says where it
/// came from.
/// </summary>
public class AggregatorOrder : OrgScopedEntity
{
    public long AggregatorOrderId { get; set; }

    public OrderAggregator Aggregator { get; set; }

    [Required(ErrorMessage = "The aggregator's order id is required.")]
    [MaxLength(64, ErrorMessage = "The aggregator's order id can be at most 64 characters.")]
    public string ExternalOrderId { get; set; } = null!;

    public long RestaurantOrderId { get; set; }

    /// <summary>When the order reached us, in branch local time.</summary>
    public DateTime ReceivedAt { get; set; }

    [MaxLength(100, ErrorMessage = "A customer's name can be at most 100 characters.")]
    public string? CustomerName { get; set; }

    [MaxLength(30, ErrorMessage = "A phone number can be at most 30 characters.")]
    public string? CustomerPhone { get; set; }

    [MaxLength(500, ErrorMessage = "An address can be at most 500 characters.")]
    public string? DeliveryAddress { get; set; }

    /// <summary>What the aggregator says the customer was charged. Kept as sent, for reconciliation; it is never used to price the order.</summary>
    public decimal? AggregatorTotal { get; set; }
}
