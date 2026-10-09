using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Shared.Kernel.Tenancy;

namespace Site.Entity.TableEntities;

[Table("SiteAssignments", Schema = "sit")]
public class SiteAssignment : OrgScopedEntity
{
    [Key]
    public long SiteAssignmentId { get; set; }

    [Required(ErrorMessage = "Site is required.")]
    public long SiteId { get; set; }

    [Required(ErrorMessage = "Laborer is required.")]
    public long LaborerId { get; set; }

    [StringLength(100, ErrorMessage = "Assigned role cannot exceed 100 characters.")]
    public string AssignedRole { get; set; } = string.Empty;

    public DateOnly StartDate { get; set; }

    public DateOnly? EndDate { get; set; }

    public bool IsActive { get; set; } = true;
}
