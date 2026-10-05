using Inventory.Entity.Enums;
using Inventory.Entity.Models;
using Inventory.Entity.TableEntities;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Persistence;

namespace Inventory.Repository.Repositories;

/// <summary>
/// Recipes (TK-149): what each dish is made of, what a portion costs, and the
/// ingredients a bill uses, issued through the guarded stock issue once per
/// bill and ingredient so a retry issues nothing twice.
/// </summary>
public sealed class RecipeRepository
{
    /// <summary>The source type a bill's ingredient issues are filed under (mst.TransactionTypes).</summary>
    public const string ConsumptionSource = "RCN";

    private readonly InventoryDbContext _db;
    private readonly StockRepository _stock;

    public RecipeRepository(InventoryDbContext db, StockRepository stock)
    {
        _db = db;
        _stock = stock;
    }

    // ---- Recipes ----

    /// <summary>Every dish on the menu with its recipe, if it has one, and its food cost.</summary>
    public async Task<List<RecipeView>> ListAsync(CancellationToken ct)
    {
        var dishes = await (
                from m in _db.MenuItems
                join i in _db.Items on m.MenuItemId equals i.ItemId
                join c in _db.MenuCategories on m.MenuCategoryId equals c.MenuCategoryId
                orderby c.DisplayOrder, m.DisplayOrder, i.ItemName
                select new { m.MenuItemId, Name = i.ItemName, Category = c.Name, i.SalesPrice })
            .ToListAsync(ct);

        Dictionary<long, Recipe> recipes = await _db.Recipes.ToDictionaryAsync(r => r.MenuItemId, ct);
        Dictionary<long, List<RecipeLine>> lines = (await _db.RecipeLines.OrderBy(l => l.DisplayOrder).ToListAsync(ct))
            .GroupBy(l => l.RecipeId).ToDictionary(g => g.Key, g => g.ToList());
        Costing costing = await CostingAsync(lines.Values.SelectMany(l => l), ct);

        return dishes.Select(d =>
        {
            Recipe? recipe = recipes.GetValueOrDefault(d.MenuItemId);
            return View(d.MenuItemId, d.Name, d.Category, d.SalesPrice, recipe, recipe is null ? [] : lines.GetValueOrDefault(recipe.RecipeId) ?? [], costing);
        }).ToList();
    }

    public async Task<RecipeView?> GetAsync(long menuItemId, CancellationToken ct) =>
        (await ListAsync(ct)).FirstOrDefault(r => r.MenuItemId == menuItemId);

    /// <summary>
    /// Saves a dish's recipe whole. Each ingredient must be a stocked item, not
    /// the dish itself, in a unit of its own kind; the lines are replaced.
    /// </summary>
    public async Task<MenuResult<long>> SaveAsync(long menuItemId, SaveRecipeRequest request, CancellationToken ct)
    {
        if (!await _db.MenuItems.AnyAsync(m => m.MenuItemId == menuItemId, ct))
        {
            return MenuResult<long>.Missing();
        }

        if (request.Lines.Count == 0)
        {
            return MenuResult<long>.Refuse("Add at least one ingredient.");
        }

        if (request.Lines.Any(l => l.IngredientItemId == menuItemId))
        {
            return MenuResult<long>.Refuse("A dish cannot be an ingredient of itself.");
        }

        if (request.Lines.GroupBy(l => l.IngredientItemId).Any(g => g.Count() > 1))
        {
            return MenuResult<long>.Refuse("An ingredient is listed twice. Add the quantities together.");
        }

        List<long> ingredientIds = request.Lines.Select(l => l.IngredientItemId).ToList();
        var ingredients = await _db.Items
            .Where(i => ingredientIds.Contains(i.ItemId))
            .Select(i => new { i.ItemId, i.ItemName, i.TrackInventory, i.UomTypeId })
            .ToDictionaryAsync(i => i.ItemId, ct);
        List<long> uomIds = request.Lines.Select(l => l.UomId).Distinct().ToList();
        Dictionary<long, long> uomTypes = await _db.UnitOfMeasures.Where(u => uomIds.Contains(u.UomId)).ToDictionaryAsync(u => u.UomId, u => u.UomTypeId, ct);

        foreach (SaveRecipeLineRequest line in request.Lines)
        {
            if (!ingredients.TryGetValue(line.IngredientItemId, out var item))
            {
                return MenuResult<long>.Refuse("An ingredient does not exist.");
            }

            if (!item.TrackInventory)
            {
                return MenuResult<long>.Refuse($"{item.ItemName} is not a stocked item, so the recipe cannot use it up.");
            }

            if (!uomTypes.TryGetValue(line.UomId, out long uomType) || uomType != item.UomTypeId)
            {
                return MenuResult<long>.Refuse($"Choose a unit {item.ItemName} is measured in.");
            }
        }

        await using ITransactionScope tx = await _db.Database.BeginScopeAsync(ct);

        Recipe? recipe = await _db.Recipes.FirstOrDefaultAsync(r => r.MenuItemId == menuItemId, ct);
        if (recipe is null)
        {
            recipe = new Recipe { MenuItemId = menuItemId };
            _db.Recipes.Add(recipe);
        }

        recipe.Yield = request.Yield;
        recipe.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();
        recipe.IsActive = request.IsActive;
        await _db.SaveChangesAsync(ct);

        await _db.RecipeLines.Where(l => l.RecipeId == recipe.RecipeId).ExecuteDeleteAsync(ct);
        int order = 0;
        foreach (SaveRecipeLineRequest line in request.Lines)
        {
            _db.RecipeLines.Add(new RecipeLine
            {
                RecipeId = recipe.RecipeId,
                IngredientItemId = line.IngredientItemId,
                Quantity = line.Quantity,
                UomId = line.UomId,
                DisplayOrder = order += 10,
            });
        }

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return MenuResult<long>.Done(recipe.RecipeId);
    }

    public async Task<MenuResult<bool>> DeleteAsync(long menuItemId, CancellationToken ct)
    {
        Recipe? recipe = await _db.Recipes.FirstOrDefaultAsync(r => r.MenuItemId == menuItemId, ct);
        if (recipe is null)
        {
            return MenuResult<bool>.Missing();
        }

        _db.Recipes.Remove(recipe);
        await _db.SaveChangesAsync(ct);
        return MenuResult<bool>.Done(true);
    }

    // ---- Using ingredients ----

    /// <summary>
    /// Issues a bill's ingredients: one stock issue per ingredient, filed under
    /// the bill, through the same guarded decrement every sale uses. An
    /// ingredient this bill already issued is skipped, so a retry is harmless.
    /// A shortage refuses the lot and names the ingredient.
    /// </summary>
    public async Task<(string? Refusal, ConsumeIngredientsResponse? Issued)> ConsumeAsync(ConsumeIngredientsRequest request, CancellationToken ct)
    {
        List<long> dishIds = request.Dishes.Where(d => d.UsesRecipe).Select(d => d.ItemId).Distinct().ToList();
        Dictionary<long, RecipeRulesRepository.RecipeShape> shapes = await ShapesAsync(dishIds, ct);
        Dictionary<long, decimal> needs = RecipeRulesRepository.IngredientNeeds(request.Dishes, id => shapes.GetValueOrDefault(id));

        ConsumeIngredientsResponse response = new();
        if (needs.Count == 0)
        {
            return (null, response);
        }

        List<long> itemIds = needs.Keys.ToList();
        Dictionary<long, (string Name, long Uom, bool Tracked)> items = await _db.Items
            .Where(i => itemIds.Contains(i.ItemId))
            .ToDictionaryAsync(i => i.ItemId, i => (i.ItemName, i.InventoryUomId, i.TrackInventory), ct);

        await using ITransactionScope tx = await _db.Database.BeginScopeAsync(ct);

        foreach ((long itemId, decimal quantity) in needs.OrderBy(n => n.Key))
        {
            if (!items.TryGetValue(itemId, out var item) || !item.Tracked)
            {
                // A choice's linked item that stopped being stocked uses nothing.
                continue;
            }

            RecordStockMovementResult result = await _stock.RecordAsync(new RecordStockMovementRequest
            {
                ItemId = itemId,
                MovementType = nameof(StockMovementType.Issue),
                MovementDate = request.Date,
                Quantity = quantity,
                UomId = item.Uom,
                SourceType = ConsumptionSource,
                SourceId = request.BillId,
                SourceLineId = itemId,
            }, ct);

            switch (result.Outcome)
            {
                case StockOutcome.Ok:
                    response.Issued.Add(new IngredientIssue { ItemId = itemId, Quantity = quantity });
                    break;
                case StockOutcome.DuplicateSource:
                    response.Issued.Add(new IngredientIssue { ItemId = itemId, Quantity = quantity, AlreadyIssued = true });
                    break;
                case StockOutcome.InsufficientStock:
                    return ($"Not enough {item.Name} in stock for this bill's dishes. Receive it, or correct the count with a stock adjustment, then bill again.", null);
                default:
                    return ($"{item.Name} could not be taken out of stock ({result.Outcome}).", null);
            }
        }

        await tx.CommitAsync(ct);
        return (null, response);
    }

    // ---- Helpers ----

    private sealed record Costing(Dictionary<long, (string Name, long InventoryUom, decimal Average)> Items, Dictionary<long, (string Code, decimal ToBase)> Units);

    private async Task<Costing> CostingAsync(IEnumerable<RecipeLine> lines, CancellationToken ct)
    {
        List<long> itemIds = lines.Select(l => l.IngredientItemId).Distinct().ToList();
        var items = await (
                from i in _db.Items
                where itemIds.Contains(i.ItemId)
                join s in _db.ItemStock on i.ItemId equals s.ItemId into stock
                from s in stock.DefaultIfEmpty()
                select new { i.ItemId, i.ItemName, i.InventoryUomId, Average = s == null ? 0m : s.WeightedAverageCost })
            .ToListAsync(ct);
        Dictionary<long, (string, decimal)> units = await _db.UnitOfMeasures.ToDictionaryAsync(u => u.UomId, u => (u.UomCode, u.ConversionToBase), ct);
        return new Costing(items.ToDictionary(i => i.ItemId, i => (i.ItemName, i.InventoryUomId, i.Average)), units);
    }

    /// <summary>Each dish's active recipe with its ingredients in their inventory units.</summary>
    private async Task<Dictionary<long, RecipeRulesRepository.RecipeShape>> ShapesAsync(List<long> dishIds, CancellationToken ct)
    {
        List<Recipe> recipes = await _db.Recipes.Where(r => dishIds.Contains(r.MenuItemId) && r.IsActive).ToListAsync(ct);
        List<long> recipeIds = recipes.Select(r => r.RecipeId).ToList();
        List<RecipeLine> lines = await _db.RecipeLines.Where(l => recipeIds.Contains(l.RecipeId)).ToListAsync(ct);
        Costing costing = await CostingAsync(lines, ct);

        return recipes.ToDictionary(r => r.MenuItemId, r => Shape(r, lines.Where(l => l.RecipeId == r.RecipeId), costing));
    }

    private static RecipeRulesRepository.RecipeShape Shape(Recipe recipe, IEnumerable<RecipeLine> lines, Costing costing) =>
        new(recipe.Yield, lines.Select(l =>
        {
            decimal from = costing.Units.GetValueOrDefault(l.UomId).ToBase;
            decimal to = costing.Items.TryGetValue(l.IngredientItemId, out var item) ? costing.Units.GetValueOrDefault(item.InventoryUom).ToBase : from;
            return (l.IngredientItemId, RecipeRulesRepository.Convert(l.Quantity, from, to));
        }).ToList());

    private static RecipeView View(long menuItemId, string name, string category, decimal? price, Recipe? recipe, List<RecipeLine> lines, Costing costing)
    {
        RecipeView view = new() { MenuItemId = menuItemId, DishName = name, CategoryName = category, Price = price };
        if (recipe is null)
        {
            return view;
        }

        RecipeRulesRepository.RecipeShape shape = Shape(recipe, lines, costing);
        decimal Average(long id) => costing.Items.TryGetValue(id, out var item) ? item.Average : 0m;

        view.HasRecipe = true;
        view.Yield = recipe.Yield;
        view.Notes = recipe.Notes;
        view.IsActive = recipe.IsActive;
        view.PortionCost = RecipeRulesRepository.PortionCost(shape, Average);
        view.FoodCostPercent = RecipeRulesRepository.FoodCostPercent(view.PortionCost, price);
        view.Lines = lines.Select((l, index) => new RecipeLineView
        {
            IngredientItemId = l.IngredientItemId,
            IngredientName = costing.Items.TryGetValue(l.IngredientItemId, out var item) ? item.Name : $"Item {l.IngredientItemId}",
            Quantity = l.Quantity,
            UomId = l.UomId,
            UomCode = costing.Units.GetValueOrDefault(l.UomId).Code ?? string.Empty,
            Cost = Math.Round(shape.Ingredients[index].Quantity * Average(l.IngredientItemId), 2, MidpointRounding.AwayFromZero),
        }).ToList();
        return view;
    }
}
