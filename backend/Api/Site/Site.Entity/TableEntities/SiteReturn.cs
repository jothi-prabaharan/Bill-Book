using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Site.Entity.Enums;
using Shared.Kernel.Tenancy;

namespace Site.Entity.TableEntities;

[Table("SiteReturns", Schema = "sit")]
public class SiteReturn : OrgScopedEntity
{
    [Key]
    public long SiteReturnId { get; set; }

    [Required(ErrorMessage = "Return number is required.")]
    [StringLength(30, ErrorMessage = "Return number cannot exceed 30 characters.")]
    public string ReturnNumber { get; set; } = string.Empty;

    [Required(ErrorMessage = "Site is required.")]
    public long SiteId { get; set; }

    [Required(ErrorMessage = "Site warehouse is required.")]
    public long FromWarehouseId { get; set; }

    [Required(ErrorMessage = "Restock warehouse is required.")]
    public long ToWarehouseId { get; set; }

    public DateOnly ReturnDate { get; set; }

    [Required(ErrorMessage = "Status is required.")]
    public ReturnStatus Status { get; set; } = ReturnStatus.Draft;

    [StringLength(500, ErrorMessage = "Notes cannot exceed 500 characters.")]
    public string? Notes { get; set; }
}
