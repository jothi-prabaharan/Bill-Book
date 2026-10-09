using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Shared.Kernel.Tenancy;

namespace Site.Entity.TableEntities;

[Table("SiteDispatchLines", Schema = "sit")]
public class SiteDispatchLine : OrgScopedEntity
{
    [Key]
    public long SiteDispatchLineId { get; set; }

    [Required(ErrorMessage = "Site dispatch is required.")]
    public long SiteDispatchId { get; set; }

    [Required(ErrorMessage = "Item is required.")]
    public long ItemId { get; set; }

    public long? ItemBatchId { get; set; }

    public long? ItemSerialId { get; set; }

    [Range(0.0001, 10000000, ErrorMessage = "Dispatched quantity must be greater than zero.")]
    public decimal DispatchedQuantity { get; set; }

    [Range(0, 10000000, ErrorMessage = "Received quantity cannot be negative.")]
    public decimal ReceivedQuantity { get; set; }

    [Range(0, 10000000, ErrorMessage = "Unit cost cannot be negative.")]
    public decimal UnitCost { get; set; }
}
