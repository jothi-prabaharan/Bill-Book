using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Shared.Kernel.Tenancy;

namespace Site.Entity.TableEntities;

[Table("SiteMaterialUsageLines", Schema = "sit")]
public class SiteMaterialUsageLine : OrgScopedEntity
{
    [Key]
    public long SiteMaterialUsageLineId { get; set; }

    [Required(ErrorMessage = "Site material usage is required.")]
    public long SiteMaterialUsageId { get; set; }

    [Required(ErrorMessage = "Item is required.")]
    public long ItemId { get; set; }

    public long? ItemBatchId { get; set; }

    public long? ItemSerialId { get; set; }

    [Range(0.0001, 10000000, ErrorMessage = "Quantity must be greater than zero.")]
    public decimal Quantity { get; set; }

    [Range(0, 10000000, ErrorMessage = "Unit cost cannot be negative.")]
    public decimal UnitCost { get; set; }

    [Range(0, 10000000, ErrorMessage = "Selling price cannot be negative.")]
    public decimal SellingPrice { get; set; }
}
