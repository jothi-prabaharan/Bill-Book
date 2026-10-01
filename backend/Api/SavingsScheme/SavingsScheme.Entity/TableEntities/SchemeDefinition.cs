using System.ComponentModel.DataAnnotations;
using Shared.Kernel.Tenancy;

namespace SavingsScheme.Entity.TableEntities;

public class SchemeDefinition : OrgScopedEntity
{
    public long SchemeDefinitionId { get; set; }

    [Required]
    [MaxLength(100, ErrorMessage = "Name cannot exceed 100 characters.")]
    public string Name { get; set; } = null!;

    [Range(1, 120, ErrorMessage = "Duration must be between 1 and 120 months.")]
    public int DurationMonths { get; set; }

    /// <summary>
    /// The bonus amount given at maturity, expressed as a multiple of the monthly instalment.
    /// E.g. 1.0 means one full instalment is given as a bonus.
    /// </summary>
    public decimal BonusMultiple { get; set; }

    public bool IsActive { get; set; } = true;
}
