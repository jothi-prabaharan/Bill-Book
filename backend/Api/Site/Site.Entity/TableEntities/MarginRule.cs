using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Shared.Kernel.Tenancy;

namespace Site.Entity.TableEntities;

[Table("MarginRules", Schema = "sit")]
public class MarginRule : OrgScopedEntity
{
    [Key]
    public long MarginRuleId { get; set; }

    public long? TradeId { get; set; }

    public long? SiteId { get; set; }

    [Range(0, 100, ErrorMessage = "Minimum labor margin percent must be between 0 and 100.")]
    public decimal MinLaborMarginPercent { get; set; }

    [Range(0, 100, ErrorMessage = "Minimum material margin percent must be between 0 and 100.")]
    public decimal MinMaterialMarginPercent { get; set; }

    [Range(0, 100, ErrorMessage = "Subcontractor markup percent must be between 0 and 100.")]
    public decimal SubcontractorMarkupPercent { get; set; }

    public bool IsActive { get; set; } = true;
}
