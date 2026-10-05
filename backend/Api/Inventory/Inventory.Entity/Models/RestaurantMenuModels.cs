using System.ComponentModel.DataAnnotations;
using Inventory.Entity.Enums;
using Shared.Kernel.Restaurant;

namespace Inventory.Entity.Models;

/// <summary>A menu heading as saved from the menu page (TK-144).</summary>
public sealed class SaveMenuCategoryRequest
{
    [Required(ErrorMessage = "Enter the category's name.")]
    [MaxLength(100, ErrorMessage = "A category name can be at most 100 characters.")]
    public string Name { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
}

public sealed class MenuCategoryView
{
    public long MenuCategoryId { get; set; }

    public string Name { get; set; } = string.Empty;

    public int DisplayOrder { get; set; }

    public bool IsActive { get; set; }

    public int ItemCount { get; set; }
}

/// <summary>A timed menu as saved from the menu page (TK-144).</summary>
public sealed class SaveMenuScheduleRequest
{
    [Required(ErrorMessage = "Enter the timed menu's name.")]
    [MaxLength(100, ErrorMessage = "A timed menu's name can be at most 100 characters.")]
    public string Name { get; set; } = string.Empty;

    public MenuDays Days { get; set; } = MenuDays.Everyday;

    public TimeOnly StartTime { get; set; }

    public TimeOnly EndTime { get; set; }

    public bool IsActive { get; set; } = true;
}

public sealed class MenuScheduleView
{
    public long MenuScheduleId { get; set; }

    public string Name { get; set; } = string.Empty;

    public MenuDays Days { get; set; }

    public TimeOnly StartTime { get; set; }

    public TimeOnly EndTime { get; set; }

    public bool IsActive { get; set; }
}

/// <summary>
/// A dish with everything about how it is sold (TK-144), saved in one piece.
/// Children carrying an id are updated, those without are added, and those
/// left out are removed.
/// </summary>
public sealed class SaveMenuItemRequest
{
    [Range(1, long.MaxValue, ErrorMessage = "Choose the item this dish is.")]
    public long ItemId { get; set; }

    [Range(1, long.MaxValue, ErrorMessage = "Choose the dish's category.")]
    public long MenuCategoryId { get; set; }

    public long? MenuScheduleId { get; set; }

    public MenuStation Station { get; set; } = MenuStation.Kitchen;

    public bool IsAvailable { get; set; } = true;

    public List<SaveItemVariantRequest> Variants { get; set; } = [];

    public List<SaveMenuItemPriceRequest> Prices { get; set; } = [];

    public List<SaveModifierGroupRequest> ModifierGroups { get; set; } = [];
}

public sealed class SaveItemVariantRequest
{
    public long? ItemVariantId { get; set; }

    [Required(ErrorMessage = "Enter the portion's name.")]
    [MaxLength(50, ErrorMessage = "A portion's name can be at most 50 characters.")]
    public string Name { get; set; } = string.Empty;

    [Range(typeof(decimal), "0", "9999999999999999", ErrorMessage = "A portion's price cannot be negative.")]
    public decimal Price { get; set; }

    [Range(typeof(decimal), "0.0001", "100", ErrorMessage = "A portion factor must be more than 0 and at most 100.")]
    public decimal PortionFactor { get; set; } = 1m;
}

/// <summary>A price for one order type. <see cref="VariantName"/> names a portion in the same request, so a new one can be priced before it has an id.</summary>
public sealed class SaveMenuItemPriceRequest
{
    public OrderType OrderType { get; set; }

    [MaxLength(50, ErrorMessage = "A portion's name can be at most 50 characters.")]
    public string? VariantName { get; set; }

    [Range(typeof(decimal), "0", "9999999999999999", ErrorMessage = "A price cannot be negative.")]
    public decimal Price { get; set; }
}

public sealed class SaveModifierGroupRequest
{
    public long? ModifierGroupId { get; set; }

    [Required(ErrorMessage = "Enter the choice group's name.")]
    [MaxLength(100, ErrorMessage = "A choice group's name can be at most 100 characters.")]
    public string Name { get; set; } = string.Empty;

    [Range(0, 50, ErrorMessage = "The minimum must be between 0 and 50.")]
    public int MinChoices { get; set; }

    [Range(1, 50, ErrorMessage = "The maximum must be between 1 and 50.")]
    public int MaxChoices { get; set; } = 1;

    public List<SaveModifierRequest> Modifiers { get; set; } = [];
}

public sealed class SaveModifierRequest
{
    public long? ModifierId { get; set; }

    [Required(ErrorMessage = "Enter the choice's name.")]
    [MaxLength(100, ErrorMessage = "A choice's name can be at most 100 characters.")]
    public string Name { get; set; } = string.Empty;

    [Range(typeof(decimal), "-9999999999999999", "9999999999999999", ErrorMessage = "The price change is out of range.")]
    public decimal PriceChange { get; set; }

    public long? LinkedItemId { get; set; }

    [Range(typeof(decimal), "0.0001", "999999", ErrorMessage = "The quantity used must be more than 0.")]
    public decimal? LinkedQuantity { get; set; }

    public bool IsAvailable { get; set; } = true;
}

/// <summary>Sold out or back on, from a phone at the pass.</summary>
public sealed class SetMenuAvailabilityRequest
{
    public bool IsAvailable { get; set; }
}

/// <summary>A dish as the menu page edits it (TK-144).</summary>
public sealed class MenuItemView
{
    public long ItemId { get; set; }

    public string ItemCode { get; set; } = string.Empty;

    public string ItemName { get; set; } = string.Empty;

    /// <summary>The item's own sales price: what the dish sells at with no portion and no order-type price.</summary>
    public decimal? BasePrice { get; set; }

    public long MenuCategoryId { get; set; }

    public string CategoryName { get; set; } = string.Empty;

    public long? MenuScheduleId { get; set; }

    public string? ScheduleName { get; set; }

    public MenuStation Station { get; set; }

    public bool IsAvailable { get; set; }

    public int DisplayOrder { get; set; }

    public List<ItemVariantView> Variants { get; set; } = [];

    public List<MenuItemPriceView> Prices { get; set; } = [];

    public List<ModifierGroupView> ModifierGroups { get; set; } = [];
}

public sealed class ItemVariantView
{
    public long ItemVariantId { get; set; }

    public string Name { get; set; } = string.Empty;

    public decimal Price { get; set; }

    public decimal PortionFactor { get; set; }
}

public sealed class MenuItemPriceView
{
    public OrderType OrderType { get; set; }

    public long? ItemVariantId { get; set; }

    public string? VariantName { get; set; }

    public decimal Price { get; set; }
}

public sealed class ModifierGroupView
{
    public long ModifierGroupId { get; set; }

    public string Name { get; set; } = string.Empty;

    public int MinChoices { get; set; }

    public int MaxChoices { get; set; }

    public bool IsRequired { get; set; }

    public List<ModifierView> Modifiers { get; set; } = [];
}

public sealed class ModifierView
{
    public long ModifierId { get; set; }

    public string Name { get; set; } = string.Empty;

    public decimal PriceChange { get; set; }

    public long? LinkedItemId { get; set; }

    public decimal? LinkedQuantity { get; set; }

    public bool IsAvailable { get; set; }
}

/// <summary>The menu as a waiter orders from it (TK-144): what is served at that moment, priced for the order type.</summary>
public sealed class OrderingMenuView
{
    public OrderType OrderType { get; set; }

    public List<OrderingCategoryView> Categories { get; set; } = [];
}

public sealed class OrderingCategoryView
{
    public long MenuCategoryId { get; set; }

    public string Name { get; set; } = string.Empty;

    public List<OrderingItemView> Items { get; set; } = [];
}

public sealed class OrderingItemView
{
    public long ItemId { get; set; }

    public string Name { get; set; } = string.Empty;

    public MenuStation Station { get; set; }

    /// <summary>False while sold out: shown so the waiter can say so, but it cannot be ordered.</summary>
    public bool IsAvailable { get; set; }

    /// <summary>The price with no portion; null when the dish must be ordered by portion.</summary>
    public decimal? Price { get; set; }

    public List<OrderingVariantView> Variants { get; set; } = [];

    public List<ModifierGroupView> ModifierGroups { get; set; } = [];
}

public sealed class OrderingVariantView
{
    public long ItemVariantId { get; set; }

    public string Name { get; set; } = string.Empty;

    public decimal Price { get; set; }
}

/// <summary>
/// Sales asking for the price of order lines (TK-144, TK-146). <see cref="At"/>
/// is the branch's local time: the day and hour decide which timed menus are on.
/// </summary>
public sealed class PriceMenuLinesRequest
{
    public Guid CustomerId { get; set; }

    public Guid OrgId { get; set; }

    public OrderType OrderType { get; set; }

    public DateTime At { get; set; }

    [MinLength(1, ErrorMessage = "Send at least one line to price.")]
    public List<PriceMenuLineRequest> Lines { get; set; } = [];
}

public sealed class PriceMenuLineRequest
{
    public long ItemId { get; set; }

    public long? ItemVariantId { get; set; }

    public List<long> ModifierIds { get; set; } = [];
}

/// <summary>Every line priced, or the first refusal and the line it is for.</summary>
public sealed class PriceMenuLinesResponse
{
    public List<PricedMenuLine> Lines { get; set; } = [];
}

public sealed class PricedMenuLine
{
    public long ItemId { get; set; }

    public string ItemName { get; set; } = string.Empty;

    public long? ItemVariantId { get; set; }

    public string? VariantName { get; set; }

    public decimal PortionFactor { get; set; } = 1m;

    public MenuStation Station { get; set; }

    /// <summary>The portion's price for the order type plus every choice's price change.</summary>
    public decimal UnitPrice { get; set; }

    public List<PricedModifier> Modifiers { get; set; } = [];

    // What the bill needs from the item master (TK-148), copied onto the order line.

    public long? TaxGroupId { get; set; }

    public long SalesUomId { get; set; }

    public bool IsPriceInclusive { get; set; }

    /// <summary>The item's tax preference by name: Taxable, Exempt, NilRated, NonGst or ZeroRated.</summary>
    public string TaxPreference { get; set; } = "Taxable";

    /// <summary>Whether selling it issues stock; a dish made to order does not, its recipe does (TK-149).</summary>
    public bool TrackInventory { get; set; }
}

public sealed class PricedModifier
{
    public long ModifierId { get; set; }

    public string Name { get; set; } = string.Empty;

    public decimal PriceChange { get; set; }

    public long? LinkedItemId { get; set; }

    public decimal? LinkedQuantity { get; set; }
}

/// <summary>Why a line cannot be priced, and which one.</summary>
public sealed class MenuLineRefusal
{
    public int LineIndex { get; set; }

    public string Message { get; set; } = string.Empty;
}

/// <summary>How a menu write ended (TK-144).</summary>
public enum MenuOutcome
{
    Ok = 0,
    NotFound = 1,
    Refused = 2,
}

/// <summary>A menu write's outcome, its value when it worked, and the sentence when it was refused.</summary>
public sealed record MenuResult<T>(MenuOutcome Outcome, T? Value = default, string? Message = null)
{
    public static MenuResult<T> Done(T value) => new(MenuOutcome.Ok, value);

    public static MenuResult<T> Missing() => new(MenuOutcome.NotFound);

    public static MenuResult<T> Refuse(string message) => new(MenuOutcome.Refused, default, message);
}
