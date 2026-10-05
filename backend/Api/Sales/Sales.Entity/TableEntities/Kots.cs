using System.ComponentModel.DataAnnotations;
using Sales.Entity.Enums;
using Shared.Kernel.Restaurant;
using Shared.Kernel.Tenancy;

namespace Sales.Entity.TableEntities;

/// <summary>
/// A kitchen ticket (TK-147): what one station has been asked to make from one
/// order, each time new dishes are sent. An order sent twice to the kitchen
/// has two kitchen tickets; a dish is on exactly one.
/// </summary>
public class Kot : OrgScopedEntity
{
    public long KotId { get; set; }

    public long RestaurantOrderId { get; set; }

    [Required(ErrorMessage = "The kitchen ticket needs a number.")]
    [MaxLength(30, ErrorMessage = "A kitchen ticket number can be at most 30 characters.")]
    public string KotNo { get; set; } = null!;

    public long NumberingSeriesId { get; set; }

    public MenuStation Station { get; set; }

    /// <summary>When it was sent, in branch local time.</summary>
    public DateTime SentAt { get; set; }

    /// <summary>How many times it has been printed; a second print is marked REPRINT.</summary>
    public int PrintedCount { get; set; }

    public List<KotLine> Lines { get; set; } = [];
}

/// <summary>One dish on a kitchen ticket (TK-147) and where the kitchen is with it.</summary>
public class KotLine : OrgScopedEntity
{
    public long KotLineId { get; set; }

    public long KotId { get; set; }

    public long RestaurantOrderLineId { get; set; }

    public decimal Quantity { get; set; }

    public KitchenStatus Status { get; set; } = KitchenStatus.Sent;

    [MaxLength(250, ErrorMessage = "A reason can be at most 250 characters.")]
    public string? CancelReason { get; set; }

    /// <summary>When the status last changed, in branch local time: how long a dish has waited.</summary>
    public DateTime StatusChangedAt { get; set; }
}
