using System;
using System.ComponentModel.DataAnnotations;
using Shared.Kernel.Tenancy;

namespace Inventory.Entity.TableEntities;

public class PriceList : OrgScopedEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required(ErrorMessage = "Name is required.")]
    [MaxLength(100, ErrorMessage = "Name cannot exceed 100 characters.")]
    public string Name { get; set; } = null!;

    [MaxLength(300, ErrorMessage = "Description cannot exceed 300 characters.")]
    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;
}
