using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Shared.Kernel.Tenancy;

namespace Site.Entity.TableEntities;

/// <summary>
/// Live tracked balances per item at a job site (TK-173).
/// </summary>
[Table("SiteStockBalances", Schema = "sit")]
public class SiteStockBalance : OrgScopedEntity
{
    [Key]
    public long BalanceId { get; set; }

    [Required(ErrorMessage = "Site is required.")]
    public long SiteId { get; set; }

    [Required(ErrorMessage = "Item is required.")]
    public long ItemId { get; set; }

    public decimal DispatchedQty { get; set; }

    public decimal ReturnedQty { get; set; }

    public decimal UsedQty { get; set; }

    public decimal OnSiteQty { get; set; }

    public decimal UnbilledUsedQty { get; set; }

    public decimal BilledQty { get; set; }
}
