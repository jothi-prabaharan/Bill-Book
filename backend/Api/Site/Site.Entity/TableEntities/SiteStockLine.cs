using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Shared.Kernel.Tenancy;

namespace Site.Entity.TableEntities;

/// <summary>
/// Line items for site stock documents (TK-173, TK-174).
/// </summary>
[Table("SiteStockLines", Schema = "sit")]
public class SiteStockLine : OrgScopedEntity
{
    [Key]
    public long LineId { get; set; }

    [Required(ErrorMessage = "Document is required.")]
    public long DocumentId { get; set; }

    [Required(ErrorMessage = "Item is required.")]
    public long ItemId { get; set; }

    [Range(0.0001, 1000000, ErrorMessage = "Quantity must be greater than zero.")]
    public decimal Quantity { get; set; }

    public decimal? UnitCost { get; set; }

    public decimal? MarginValue { get; set; }

    public decimal? SellingPrice { get; set; }

    public decimal BilledQuantity { get; set; }
}
