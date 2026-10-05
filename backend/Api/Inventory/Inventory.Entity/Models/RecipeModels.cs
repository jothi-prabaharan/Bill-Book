using System.ComponentModel.DataAnnotations;
using Inventory.Entity.Enums;

namespace Inventory.Entity.Models;

public sealed class SaveRecipeRequest
{
    [Range(typeof(decimal), "0.0001", "100000", ErrorMessage = "A recipe must make more than 0 portions.")]
    public decimal Yield { get; set; } = 1m;

    [MaxLength(250, ErrorMessage = "Notes can be at most 250 characters.")]
    public string? Notes { get; set; }

    public bool IsActive { get; set; } = true;

    [MinLength(1, ErrorMessage = "Add at least one ingredient.")]
    public List<SaveRecipeLineRequest> Lines { get; set; } = [];
}

public sealed class SaveRecipeLineRequest
{
    [Range(1, long.MaxValue, ErrorMessage = "Choose the ingredient.")]
    public long IngredientItemId { get; set; }

    [Range(typeof(decimal), "0.0001", "100000", ErrorMessage = "An ingredient's quantity must be more than 0.")]
    public decimal Quantity { get; set; }

    [Range(1, long.MaxValue, ErrorMessage = "Choose the unit.")]
    public long UomId { get; set; }
}

/// <summary>A dish with its recipe and what a portion costs to make (TK-149).</summary>
public sealed class RecipeView
{
    public long MenuItemId { get; set; }

    public string DishName { get; set; } = string.Empty;

    public string CategoryName { get; set; } = string.Empty;

    /// <summary>What the dish sells for with no portion or order-type price.</summary>
    public decimal? Price { get; set; }

    public bool HasRecipe { get; set; }

    public decimal Yield { get; set; } = 1m;

    public string? Notes { get; set; }

    public bool IsActive { get; set; }

    /// <summary>One full portion's ingredients at their current weighted average cost.</summary>
    public decimal PortionCost { get; set; }

    /// <summary>The portion cost as a share of the price; null when the dish has no price.</summary>
    public decimal? FoodCostPercent { get; set; }

    public List<RecipeLineView> Lines { get; set; } = [];
}

public sealed class RecipeLineView
{
    public long IngredientItemId { get; set; }

    public string IngredientName { get; set; } = string.Empty;

    public decimal Quantity { get; set; }

    public long UomId { get; set; }

    public string UomCode { get; set; } = string.Empty;

    /// <summary>This line's share of the batch's cost, at the ingredient's current average.</summary>
    public decimal Cost { get; set; }
}

/// <summary>
/// Sales asking for a bill's ingredients to leave stock (TK-149, TK-148). Once
/// per bill: a second call for the same bill issues nothing.
/// </summary>
public sealed class ConsumeIngredientsRequest
{
    public Guid CustomerId { get; set; }

    public Guid OrgId { get; set; }

    /// <summary>The restaurant bill: the document every ingredient issue is filed under.</summary>
    public long BillId { get; set; }

    public DateOnly Date { get; set; }

    public List<ConsumedDish> Dishes { get; set; } = [];
}

public sealed class ConsumedDish
{
    public long ItemId { get; set; }

    public decimal Quantity { get; set; }

    public decimal PortionFactor { get; set; } = 1m;

    /// <summary>False for a dish that is itself stocked (a bottled drink): the invoice issued it, so only its choices' stock is used here.</summary>
    public bool UsesRecipe { get; set; } = true;

    public List<ConsumedChoice> Choices { get; set; } = [];
}

/// <summary>A choice that uses stock: extra cheese, a quantity of it per dish.</summary>
public sealed class ConsumedChoice
{
    public long LinkedItemId { get; set; }

    public decimal LinkedQuantity { get; set; }
}

public sealed class ConsumeIngredientsResponse
{
    public List<IngredientIssue> Issued { get; set; } = [];
}

public sealed class IngredientIssue
{
    public long ItemId { get; set; }

    public decimal Quantity { get; set; }

    /// <summary>True when this bill had already issued it: nothing moved this time.</summary>
    public bool AlreadyIssued { get; set; }
}

public sealed class RecordWastageRequest
{
    public DateOnly? EntryDate { get; set; }

    [Range(1, long.MaxValue, ErrorMessage = "Choose the item thrown away.")]
    public long ItemId { get; set; }

    [Range(typeof(decimal), "0.0001", "100000000", ErrorMessage = "A quantity must be more than 0.")]
    public decimal Quantity { get; set; }

    public long? UomId { get; set; }

    public WastageReason Reason { get; set; }

    [MaxLength(250, ErrorMessage = "A note can be at most 250 characters.")]
    public string? Note { get; set; }
}

public sealed class WastageView
{
    public long WastageEntryId { get; set; }

    public DateOnly EntryDate { get; set; }

    public long ItemId { get; set; }

    public string ItemName { get; set; } = string.Empty;

    public decimal Quantity { get; set; }

    public string UomCode { get; set; } = string.Empty;

    public WastageReason Reason { get; set; }

    public string? Note { get; set; }

    public long StockAdjustmentId { get; set; }

    public string AdjustmentNo { get; set; } = string.Empty;

    /// <summary>The adjustment's status: Draft while it waits for approval, Posted once it has left stock.</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>What it cost at the item's weighted average when it was recorded.</summary>
    public decimal Cost { get; set; }
}
