using System;
using Shared.Kernel.Tenancy;

namespace Inventory.Entity.TableEntities;

public class Recipe : OrgScopedEntity
{
    public long RecipeId { get; set; }
    public long MenuItemId { get; set; } // The finished dish
    public decimal OutputQuantity { get; set; }
}

public class RecipeLine : OrgScopedEntity
{
    public long RecipeLineId { get; set; }
    public long RecipeId { get; set; }
    public long IngredientItemId { get; set; }
    public decimal Quantity { get; set; }
    public long UomId { get; set; }
}

public class WastageEntry : OrgScopedEntity
{
    public long WastageEntryId { get; set; }
    public long ItemId { get; set; }
    public decimal Quantity { get; set; }
    public string Reason { get; set; } = string.Empty;
    public DateOnly EntryDate { get; set; }
}
