using System;
using System.ComponentModel.DataAnnotations;
using Shared.Kernel.Tenancy;

namespace Inventory.Entity.TableEntities;

public class PriceListItem : OrgScopedEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid PriceListId { get; set; }

    public Guid ItemId { get; set; }

    [Required(ErrorMessage = "Price is required.")]
    public decimal Price { get; set; }
}
