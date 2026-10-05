using Inventory.Entity.Models;

namespace Inventory.Repository.Repositories;

/// <summary>The recipe arithmetic (TK-149), pure.</summary>
public static class RecipeRulesRepository
{
    /// <summary>A recipe reduced to what the arithmetic needs: its yield, and each ingredient in its item's inventory unit.</summary>
    public sealed record RecipeShape(decimal Yield, IReadOnlyList<(long ItemId, decimal Quantity)> Ingredients);

    /// <summary>A quantity in one unit, in another of the same kind: through each unit's factor to the base unit.</summary>
    public static decimal Convert(decimal quantity, decimal fromToBase, decimal toToBase) =>
        toToBase == 0m ? quantity : quantity * fromToBase / toToBase;

    /// <summary>
    /// What a bill's dishes use, by ingredient, in each ingredient's inventory
    /// unit: every dish's recipe for its quantity and portion (a half uses half
    /// of one portion, a batch recipe is divided by its yield), and every
    /// choice that names stock, once per dish. Rounded to four places, the
    /// stock ledger's precision.
    /// </summary>
    public static Dictionary<long, decimal> IngredientNeeds(IEnumerable<ConsumedDish> dishes, Func<long, RecipeShape?> recipeOf)
    {
        Dictionary<long, decimal> needs = [];
        void Add(long item, decimal quantity)
        {
            if (quantity > 0m)
            {
                needs[item] = needs.GetValueOrDefault(item) + quantity;
            }
        }

        foreach (ConsumedDish dish in dishes)
        {
            if (dish.UsesRecipe && recipeOf(dish.ItemId) is { Yield: > 0m } recipe)
            {
                decimal portions = dish.Quantity * dish.PortionFactor / recipe.Yield;
                foreach ((long item, decimal quantity) in recipe.Ingredients)
                {
                    Add(item, quantity * portions);
                }
            }

            foreach (ConsumedChoice choice in dish.Choices)
            {
                Add(choice.LinkedItemId, choice.LinkedQuantity * dish.Quantity);
            }
        }

        return needs.ToDictionary(n => n.Key, n => Math.Round(n.Value, 4, MidpointRounding.AwayFromZero));
    }

    /// <summary>One full portion's cost: each ingredient at its weighted average, divided by the yield.</summary>
    public static decimal PortionCost(RecipeShape recipe, Func<long, decimal> averageCost) =>
        recipe.Yield <= 0m
            ? 0m
            : Math.Round(recipe.Ingredients.Sum(i => i.Quantity * averageCost(i.ItemId)) / recipe.Yield, 2, MidpointRounding.AwayFromZero);

    /// <summary>The cost as a share of the price, to one place; null with no price to measure against.</summary>
    public static decimal? FoodCostPercent(decimal cost, decimal? price) =>
        price is > 0m ? Math.Round(cost / price.Value * 100m, 1, MidpointRounding.AwayFromZero) : null;
}
