using System.ComponentModel.DataAnnotations;
using Customer.Entity.Enums;
using Shared.Kernel.Tenancy;

namespace Customer.Entity.TableEntities;

public class Automation : OrgScopedEntity
{
    public long AutomationId { get; set; }

    [Required(ErrorMessage = "Automation name is required.")]
    [MaxLength(150, ErrorMessage = "Automation name cannot exceed 150 characters.")]
    public string Name { get; set; } = null!;

    public AutomationTriggerType TriggerType { get; set; }

    [MaxLength(50, ErrorMessage = "Trigger source cannot exceed 50 characters.")]
    public string? TriggerSource { get; set; }

    [MaxLength(50, ErrorMessage = "Trigger status cannot exceed 50 characters.")]
    public string? TriggerStatus { get; set; }

    public bool IsActive { get; set; } = true;

    public List<AutomationStep> Steps { get; set; } = [];
}
