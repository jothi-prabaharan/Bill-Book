using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Shared.Kernel.Tenancy;

namespace Site.Entity.TableEntities;

[Table("SiteReturnLines", Schema = "sit")]
public class SiteReturnLine : OrgScopedEntity
{
    [Key]
    public long SiteReturnLineId { get; set; }

    [Required(ErrorMessage = "Site return is required.")]
    public long SiteReturnId { get; set; }

    [Required(ErrorMessage = "Item is required.")]
    public long ItemId { get; set; }

    public long? ItemBatchId { get; set; }

    public long? ItemSerialId { get; set; }

    [Range(0.0001, 10000000, ErrorMessage = "Returned quantity must be greater than zero.")]
    public decimal ReturnedQuantity { get; set; }

    [Required(ErrorMessage = "Restock warehouse is required.")]
    public long RestockWarehouseId { get; set; }
}
