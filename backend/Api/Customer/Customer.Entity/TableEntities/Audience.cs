using System.ComponentModel.DataAnnotations;
using Shared.Kernel.Tenancy;

namespace Customer.Entity.TableEntities;

public class Audience : OrgScopedEntity
{
    public long AudienceId { get; set; }

    [Required(ErrorMessage = "Audience name is required.")]
    [MaxLength(100, ErrorMessage = "Audience name cannot exceed 100 characters.")]
    public string Name { get; set; } = null!;

    [Required(ErrorMessage = "Audience rules are required.")]
    public string Rules { get; set; } = "{}";

    public bool IsActive { get; set; } = true;
}
