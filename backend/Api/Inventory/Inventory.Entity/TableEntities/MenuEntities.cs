using System.ComponentModel.DataAnnotations;
using Shared.Kernel.Tenancy;
using Shared.Kernel.Restaurant;

namespace Inventory.Entity.TableEntities;

public class MenuCategory : OrgScopedEntity
{
    public long MenuCategoryId { get; set; }

    [Required(ErrorMessage = "Category name is required.")]
    [MaxLength(100, ErrorMessage = "Category name cannot exceed 100 characters.")]
    public string Name { get; set; } = null!;

    public int DisplayOrder { get; set; }
    
    [MaxLength(50)]
    public string? ColorCode { get; set; }
    
    public bool IsActive { get; set; } = true;
}

public class MenuSchedule : OrgScopedEntity
{
    public long MenuScheduleId { get; set; }

    [Required(ErrorMessage = "Schedule name is required.")]
    [MaxLength(100, ErrorMessage = "Schedule name cannot exceed 100 characters.")]
    public string Name { get; set; } = null!;

    /// <summary>Bitmask of days (e.g. 1=Sun, 2=Mon, 64=Sat)</summary>
    public int DaysOfWeek { get; set; }

    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
}

public class MenuItem : OrgScopedEntity
{
    /// <summary>Same as ItemId.</summary>
    public long MenuItemId { get; set; }

    public long MenuCategoryId { get; set; }
    
    public long? MenuScheduleId { get; set; }

    public MenuStation Station { get; set; } = MenuStation.Kitchen;

    public bool IsAvailable { get; set; } = true;
    
    public int DisplayOrder { get; set; }
}

public class ItemVariant : OrgScopedEntity
{
    public long ItemVariantId { get; set; }
    public long MenuItemId { get; set; }

    [Required(ErrorMessage = "Variant name is required.")]
    [MaxLength(100, ErrorMessage = "Variant name cannot exceed 100 characters.")]
    public string Name { get; set; } = null!;

    [Range(0, 999999999999.99, ErrorMessage = "Price cannot be negative.")]
    public decimal Price { get; set; }

    /// <summary>Factor used to calculate food cost or stock consumption.</summary>
    public decimal PortionFactor { get; set; } = 1.0m;
    
    public int DisplayOrder { get; set; }
}

public class ModifierGroup : OrgScopedEntity
{
    public long ModifierGroupId { get; set; }
    public long MenuItemId { get; set; }

    [Required(ErrorMessage = "Group name is required.")]
    [MaxLength(100, ErrorMessage = "Group name cannot exceed 100 characters.")]
    public string Name { get; set; } = null!;

    public int MinChoices { get; set; } = 0;
    public int MaxChoices { get; set; } = 1;
    
    public int DisplayOrder { get; set; }
}

public class Modifier : OrgScopedEntity
{
    public long ModifierId { get; set; }
    public long ModifierGroupId { get; set; }

    [Required(ErrorMessage = "Modifier name is required.")]
    [MaxLength(100, ErrorMessage = "Modifier name cannot exceed 100 characters.")]
    public string Name { get; set; } = null!;

    [Range(-999999999999.99, 999999999999.99, ErrorMessage = "Price change out of range.")]
    public decimal PriceChange { get; set; }

    /// <summary>Optional real item to deduct stock from.</summary>
    public long? LinkedItemId { get; set; }
    
    public int DisplayOrder { get; set; }
}

public class MenuItemPrice : OrgScopedEntity
{
    public long MenuItemPriceId { get; set; }
    public long MenuItemId { get; set; }

    public OrderType OrderType { get; set; }

    [Range(0, 999999999999.99, ErrorMessage = "Price cannot be negative.")]
    public decimal Price { get; set; }
}
