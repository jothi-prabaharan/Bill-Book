using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Site.Entity.Enums;
using Shared.Kernel.Tenancy;

namespace Site.Entity.TableEntities;

/// <summary>
/// Tiered margin / markup hierarchy (Assignment -> Site -> Trade or Item -> Branch default) (TK-170).
/// </summary>
[Table("MarginRules", Schema = "sit")]
public class MarginRule : OrgScopedEntity
{
    [Key]
    public long MarginRuleId { get; set; }

    [Required(ErrorMessage = "Margin scope is required.")]
    public MarginScope Scope { get; set; } = MarginScope.Branch;

    /// <summary>Target entity ID depending on scope: AssignmentId, SiteId, TradeId, or ItemId.</summary>
    public long? ScopeId { get; set; }

    [Required(ErrorMessage = "AppliesTo is required.")]
    public MarginAppliesTo AppliesTo { get; set; } = MarginAppliesTo.All;

    [Required(ErrorMessage = "Margin type is required.")]
    public MarginType MarginType { get; set; } = MarginType.Percent;

    [Range(0, 1000000, ErrorMessage = "Margin value must be between 0 and 1,000,000.")]
    public decimal MarginValue { get; set; }

    [Required(ErrorMessage = "Effective from date is required.")]
    public DateOnly EffectiveFrom { get; set; }

    public DateOnly? EffectiveTo { get; set; }

    public bool IsActive { get; set; } = true;
}
