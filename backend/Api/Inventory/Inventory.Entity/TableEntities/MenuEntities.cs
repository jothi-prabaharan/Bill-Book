using System.ComponentModel.DataAnnotations;
using Inventory.Entity.Enums;
using Shared.Kernel.Restaurant;
using Shared.Kernel.Tenancy;

namespace Inventory.Entity.TableEntities;

/// <summary>A heading on the menu (TK-144): Starters, Main course, Beverages.</summary>
public class MenuCategory : OrgScopedEntity
{
    public long MenuCategoryId { get; set; }

    [Required(ErrorMessage = "Enter the category's name.")]
    [MaxLength(100, ErrorMessage = "A category name can be at most 100 characters.")]
    public string Name { get; set; } = null!;

    public int DisplayOrder { get; set; }

    public bool IsActive { get; set; } = true;
}

/// <summary>
/// When part of the menu is served (TK-144): breakfast 07:00 to 11:00 on
/// weekdays. An end before the start runs past midnight, and the hours after
/// midnight belong to the day it started.
/// </summary>
public class MenuSchedule : OrgScopedEntity
{
    public long MenuScheduleId { get; set; }

    [Required(ErrorMessage = "Enter the timed menu's name.")]
    [MaxLength(100, ErrorMessage = "A timed menu's name can be at most 100 characters.")]
    public string Name { get; set; } = null!;

    public MenuDays Days { get; set; } = MenuDays.Everyday;

    public TimeOnly StartTime { get; set; }

    public TimeOnly EndTime { get; set; }

    public bool IsActive { get; set; } = true;
}

/// <summary>
/// A dish on the menu (TK-144). The dish itself is an <c>inv.Items</c> row,
/// which carries its tax, unit and default price; this row says where it is
/// listed, which kitchen station makes it, and whether it is sold out. The key
/// is the item's id, so an item is on the menu once.
/// </summary>
public class MenuItem : OrgScopedEntity
{
    public long MenuItemId { get; set; }

    public long MenuCategoryId { get; set; }

    /// <summary>Null: served whenever the restaurant is open.</summary>
    public long? MenuScheduleId { get; set; }

    public MenuStation Station { get; set; } = MenuStation.Kitchen;

    /// <summary>False while sold out: listed, but it cannot be ordered.</summary>
    public bool IsAvailable { get; set; } = true;

    public int DisplayOrder { get; set; }
}

/// <summary>A portion of a dish with its own price (TK-144): Half, Full.</summary>
public class ItemVariant : OrgScopedEntity
{
    public long ItemVariantId { get; set; }

    public long MenuItemId { get; set; }

    [Required(ErrorMessage = "Enter the portion's name.")]
    [MaxLength(50, ErrorMessage = "A portion's name can be at most 50 characters.")]
    public string Name { get; set; } = null!;

    [Range(typeof(decimal), "0", "9999999999999999", ErrorMessage = "A portion's price cannot be negative.")]
    public decimal Price { get; set; }

    /// <summary>How much of the full recipe the portion uses: 0.5 for a half. Recipes read it (TK-149).</summary>
    [Range(typeof(decimal), "0.0001", "100", ErrorMessage = "A portion factor must be more than 0 and at most 100.")]
    public decimal PortionFactor { get; set; } = 1m;

    public int DisplayOrder { get; set; }
}

/// <summary>A set of choices on a dish (TK-144): "Spice level", choose one; "Extras", up to three.</summary>
public class ModifierGroup : OrgScopedEntity
{
    public long ModifierGroupId { get; set; }

    public long MenuItemId { get; set; }

    [Required(ErrorMessage = "Enter the choice group's name.")]
    [MaxLength(100, ErrorMessage = "A choice group's name can be at most 100 characters.")]
    public string Name { get; set; } = null!;

    /// <summary>At least this many must be chosen; more than zero makes the group required.</summary>
    [Range(0, 50, ErrorMessage = "The minimum must be between 0 and 50.")]
    public int MinChoices { get; set; }

    [Range(1, 50, ErrorMessage = "The maximum must be between 1 and 50.")]
    public int MaxChoices { get; set; } = 1;

    public int DisplayOrder { get; set; }
}

/// <summary>One choice in a group (TK-144), with what it adds to the price.</summary>
public class Modifier : OrgScopedEntity
{
    public long ModifierId { get; set; }

    public long ModifierGroupId { get; set; }

    [Required(ErrorMessage = "Enter the choice's name.")]
    [MaxLength(100, ErrorMessage = "A choice's name can be at most 100 characters.")]
    public string Name { get; set; } = null!;

    /// <summary>Added to the line's price; negative for "no cheese".</summary>
    [Range(typeof(decimal), "-9999999999999999", "9999999999999999", ErrorMessage = "The price change is out of range.")]
    public decimal PriceChange { get; set; }

    /// <summary>A stocked item the choice uses up, such as extra cheese. Recipes read it (TK-149).</summary>
    public long? LinkedItemId { get; set; }

    [Range(typeof(decimal), "0.0001", "999999", ErrorMessage = "The quantity used must be more than 0.")]
    public decimal? LinkedQuantity { get; set; }

    public bool IsAvailable { get; set; } = true;

    public int DisplayOrder { get; set; }
}

/// <summary>
/// A price for one order type (TK-144): a takeaway or delivery price that
/// differs from dine-in. With a portion it prices that portion; without, the
/// dish. A dish or portion with no row for the order type sells at its own price.
/// </summary>
public class MenuItemPrice : OrgScopedEntity
{
    public long MenuItemPriceId { get; set; }

    public long MenuItemId { get; set; }

    public long? ItemVariantId { get; set; }

    public OrderType OrderType { get; set; }

    [Range(typeof(decimal), "0", "9999999999999999", ErrorMessage = "A price cannot be negative.")]
    public decimal Price { get; set; }
}
