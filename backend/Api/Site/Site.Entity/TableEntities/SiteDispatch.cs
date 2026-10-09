using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Site.Entity.Enums;
using Shared.Kernel.Tenancy;

namespace Site.Entity.TableEntities;

[Table("SiteDispatches", Schema = "sit")]
public class SiteDispatch : OrgScopedEntity
{
    [Key]
    public long SiteDispatchId { get; set; }

    [Required(ErrorMessage = "Dispatch number is required.")]
    [StringLength(30, ErrorMessage = "Dispatch number cannot exceed 30 characters.")]
    public string DispatchNumber { get; set; } = string.Empty;

    [Required(ErrorMessage = "Site is required.")]
    public long SiteId { get; set; }

    [Required(ErrorMessage = "Source warehouse is required.")]
    public long FromWarehouseId { get; set; }

    [Required(ErrorMessage = "Destination site warehouse is required.")]
    public long ToWarehouseId { get; set; }

    public DateOnly DispatchDate { get; set; }

    [Required(ErrorMessage = "Status is required.")]
    public DispatchStatus Status { get; set; } = DispatchStatus.Draft;

    [StringLength(500, ErrorMessage = "Notes cannot exceed 500 characters.")]
    public string? Notes { get; set; }
}
