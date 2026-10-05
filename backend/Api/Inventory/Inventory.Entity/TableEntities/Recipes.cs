using System.ComponentModel.DataAnnotations;
using Inventory.Entity.Enums;
using Shared.Kernel.Tenancy;

namespace Inventory.Entity.TableEntities;

/// <summary>
/// What a dish is made from (TK-149). One per dish on the menu; its lines are
/// for <see cref="Yield"/> portions, so a curry cooked by the pot of ten is
/// entered as the pot. A half portion uses its portion factor of it.
/// </summary>
public class Recipe : OrgScopedEntity
{
    public long RecipeId { get; set; }

    /// <summary>The dish: an inv.MenuItems row, which is its item.</summary>
    public long MenuItemId { get; set; }

    /// <summary>How many full portions the lines make.</summary>
    [Range(typeof(decimal), "0.0001", "100000", ErrorMessage = "A recipe must make more than 0 portions.")]
    public decimal Yield { get; set; } = 1m;

    [MaxLength(250, ErrorMessage = "Notes can be at most 250 characters.")]
    public string? Notes { get; set; }

    public bool IsActive { get; set; } = true;
}

/// <summary>One ingredient of a recipe (TK-149): a stocked item, in any unit of its kind.</summary>
public class RecipeLine : OrgScopedEntity
{
    public long RecipeLineId { get; set; }

    public long RecipeId { get; set; }

    public long IngredientItemId { get; set; }

    [Range(typeof(decimal), "0.0001", "100000", ErrorMessage = "An ingredient's quantity must be more than 0.")]
    public decimal Quantity { get; set; }

    public long UomId { get; set; }

    public int DisplayOrder { get; set; }
}

/// <summary>
/// Food thrown away (TK-149). It leaves stock through a stock adjustment, so
/// it posts to cost and goes through the branch's stock adjustment approvals
/// like any write-off; this row keeps why.
/// </summary>
public class WastageEntry : OrgScopedEntity
{
    public long WastageEntryId { get; set; }

    public DateOnly EntryDate { get; set; }

    public long ItemId { get; set; }

    [Range(typeof(decimal), "0.0001", "100000000", ErrorMessage = "A quantity must be more than 0.")]
    public decimal Quantity { get; set; }

    public long UomId { get; set; }

    public WastageReason Reason { get; set; }

    [MaxLength(250, ErrorMessage = "A note can be at most 250 characters.")]
    public string? Note { get; set; }

    /// <summary>The stock adjustment that takes it out of stock.</summary>
    public long StockAdjustmentId { get; set; }
}
