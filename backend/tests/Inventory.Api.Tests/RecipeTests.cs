using Inventory.Entity.Enums;
using Inventory.Entity.Models;
using Inventory.Entity.TableEntities;
using Inventory.Repository;
using Inventory.Repository.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Shared.Kernel.Interfaces;
using Shared.Kernel.Numbering;
using Xunit;

namespace Inventory.Api.Tests;

/// <summary>The recipe arithmetic (TK-149), pure.</summary>
public sealed class RecipeRulesTests
{
    private static readonly RecipeRulesRepository.RecipeShape Tikka = new(2m, [(1, 0.3m), (2, 0.02m)]);

    [Fact]
    public void A_portion_uses_its_share_of_the_batch()
    {
        Dictionary<long, decimal> needs = RecipeRulesRepository.IngredientNeeds(
            [new ConsumedDish { ItemId = 10, Quantity = 1m }],
            id => id == 10 ? Tikka : null);

        Assert.Equal(0.15m, needs[1]);
        Assert.Equal(0.01m, needs[2]);
    }

    [Fact]
    public void A_half_uses_half_and_choices_add_their_stock_per_dish()
    {
        Dictionary<long, decimal> needs = RecipeRulesRepository.IngredientNeeds(
            [new ConsumedDish { ItemId = 10, Quantity = 2m, PortionFactor = 0.5m, Choices = [new ConsumedChoice { LinkedItemId = 2, LinkedQuantity = 0.005m }] }],
            id => id == 10 ? Tikka : null);

        Assert.Equal(0.15m, needs[1]);          // two halves are one portion
        Assert.Equal(0.02m, needs[2]);          // 0.01 from the recipe + 2 × 0.005 extra butter
    }

    [Fact]
    public void A_stocked_dish_uses_only_its_choices_and_a_dish_with_no_recipe_nothing()
    {
        Dictionary<long, decimal> needs = RecipeRulesRepository.IngredientNeeds(
            [
                new ConsumedDish { ItemId = 10, Quantity = 1m, UsesRecipe = false, Choices = [new ConsumedChoice { LinkedItemId = 3, LinkedQuantity = 1m }] },
                new ConsumedDish { ItemId = 99, Quantity = 4m },
            ],
            id => id == 10 ? Tikka : null);

        Assert.Equal([3L], needs.Keys);
    }

    [Fact]
    public void Units_convert_through_the_base_and_cost_follows_the_average()
    {
        Assert.Equal(0.3m, RecipeRulesRepository.Convert(300m, 0.001m, 1m));
        // (0.3 × 250 + 0.02 × 500) ÷ 2 portions.
        Assert.Equal(42.5m, RecipeRulesRepository.PortionCost(Tikka, id => id == 1 ? 250m : 500m));
    }

    [Fact]
    public void Food_cost_is_the_cost_over_the_price()
    {
        Assert.Equal(25m, RecipeRulesRepository.FoodCostPercent(50m, 200m));
        Assert.Null(RecipeRulesRepository.FoodCostPercent(50m, null));
        Assert.Null(RecipeRulesRepository.FoodCostPercent(50m, 0m));
    }

    [Theory]
    [InlineData(WastageReason.Expired, StockAdjustmentReason.Expired)]
    [InlineData(WastageReason.Burnt, StockAdjustmentReason.Damage)]
    [InlineData(WastageReason.ReturnedByGuest, StockAdjustmentReason.Damage)]
    [InlineData(WastageReason.Other, StockAdjustmentReason.Other)]
    public void Wastage_is_recorded_under_the_matching_adjustment_reason(WastageReason reason, StockAdjustmentReason expected)
    {
        Assert.Equal(expected, WastageRepository.AdjustmentReason(reason));
    }
}

/// <summary>Recipes and wastage through their repositories (TK-149).</summary>
[Collection(nameof(PostgresCollection))]
public sealed class RecipeTests
{
    private readonly PostgresFixture _postgres;

    public RecipeTests(PostgresFixture postgres) => _postgres = postgres;

    private sealed record Kitchen(InventoryDbContext Db, RecipeRepository Recipes, WastageRepository Wastage, long Kg, long Gram, long Paneer, long Butter, long Tikka);

    private sealed class NoUser : ICurrentUser
    {
        public Guid? UserId => Guid.NewGuid();

        public Guid? CustomerId => null;

        public Guid? OrgId => null;

        public int? RoleId => 1;
    }

    private sealed class FinancialYear : IFinancialYearProvider
    {
        public Task<int> GetStartMonthAsync(CancellationToken ct = default) => Task.FromResult(4);
    }

    private async Task<Kitchen> ArrangeAsync()
    {
        Guid customer = Guid.NewGuid();
        Guid org = Guid.NewGuid();
        InventoryDbContext db = _postgres.CreateContext(customer, org);
        db.NumberingSeries.AddRange(Repository.SeedData.NumberingSeriesSeed.Build(org));

        UomType weight = new() { OrgId = org, UomTypeName = "Weight", UomTypeSystemName = "WEIGHT", IsActive = true };
        db.UomTypes.Add(weight);
        await db.SaveChangesAsync();
        UnitOfMeasure kg = new() { OrgId = org, UomTypeId = weight.UomTypeId, UomCode = "KG", UomName = "Kilogram", ConversionToBase = 1m, IsBaseUnit = true, IsActive = true };
        UnitOfMeasure gram = new() { OrgId = org, UomTypeId = weight.UomTypeId, UomCode = "G", UomName = "Gram", ConversionToBase = 0.001m, IsActive = true };
        db.UnitOfMeasures.AddRange(kg, gram);
        await db.SaveChangesAsync();

        Item Make(string code, string name, bool tracked) => new()
        {
            OrgId = org,
            ItemCode = code,
            ItemName = name,
            ItemProfile = tracked ? ItemProfile.Standard : ItemProfile.Restaurant,
            TrackInventory = tracked,
            UomTypeId = weight.UomTypeId,
            InventoryUomId = kg.UomId,
            SalesUomId = kg.UomId,
            PurchaseUomId = kg.UomId,
            ReportUomId = kg.UomId,
            SalesPrice = tracked ? null : 200m,
            IsActive = true,
        };

        Item paneer = Make("PNR", "Paneer", true), butter = Make("BTR", "Butter", true), tikka = Make("TKA", "Paneer tikka", false);
        db.Items.AddRange(paneer, butter, tikka);
        await db.SaveChangesAsync();

        MenuCategory starters = new() { OrgId = org, Name = "Starters", DisplayOrder = 10, IsActive = true };
        db.MenuCategories.Add(starters);
        await db.SaveChangesAsync();
        db.MenuItems.Add(new MenuItem { OrgId = org, MenuItemId = tikka.ItemId, MenuCategoryId = starters.MenuCategoryId, IsAvailable = true });
        await db.SaveChangesAsync();

        TimeProvider clock = TimeProvider.System;
        StockRepository stock = new(db, new CostingRepository(db), clock);
        foreach ((Item item, decimal quantity, decimal cost) in new[] { (paneer, 2m, 250m), (butter, 1m, 500m) })
        {
            RecordStockMovementResult received = await stock.RecordAsync(new RecordStockMovementRequest
            {
                ItemId = item.ItemId,
                MovementType = nameof(StockMovementType.Receipt),
                MovementDate = DateOnly.FromDateTime(DateTime.Today),
                Quantity = quantity,
                UnitCost = cost,
            }, default);
            Assert.Equal(StockOutcome.Ok, received.Outcome);
        }

        NumberGenerator numbers = new(db, Options.Create(new NumberingOptions()), new FinancialYear());
        StockAdjustmentRepository adjustments = new(db, stock, numbers, new NoUser(), clock, NullLogger<StockAdjustmentRepository>.Instance);
        return new Kitchen(db, new RecipeRepository(db, stock), new WastageRepository(db, adjustments, clock), kg.UomId, gram.UomId, paneer.ItemId, butter.ItemId, tikka.ItemId);
    }

    private static SaveRecipeRequest TikkaRecipe(Kitchen k) => new()
    {
        Yield = 2m,
        Lines =
        [
            new() { IngredientItemId = k.Paneer, Quantity = 300m, UomId = k.Gram },
            new() { IngredientItemId = k.Butter, Quantity = 20m, UomId = k.Gram },
        ],
    };

    private static async Task<decimal> OnHandAsync(Kitchen k, long itemId) =>
        await k.Db.ItemStock.Where(s => s.ItemId == itemId).Select(s => s.QuantityOnHand).SingleAsync();

    [Fact]
    public async Task Selling_one_portion_reduces_each_ingredient_by_its_recipe_quantity_and_a_rerun_deducts_nothing_twice()
    {
        Kitchen k = await ArrangeAsync();
        await using (k.Db)
        {
            Assert.Equal(MenuOutcome.Ok, (await k.Recipes.SaveAsync(k.Tikka, TikkaRecipe(k), default)).Outcome);

            ConsumeIngredientsRequest bill = new()
            {
                BillId = 501,
                Date = DateOnly.FromDateTime(DateTime.Today),
                Dishes = [new ConsumedDish { ItemId = k.Tikka, Quantity = 1m }],
            };

            (string? refusal, ConsumeIngredientsResponse? issued) = await k.Recipes.ConsumeAsync(bill, default);
            Assert.Null(refusal);
            Assert.Equal(2, issued!.Issued.Count);
            Assert.Equal(1.85m, await OnHandAsync(k, k.Paneer));   // 2 kg less 300 g ÷ 2 portions
            Assert.Equal(0.99m, await OnHandAsync(k, k.Butter));   // 1 kg less 20 g ÷ 2

            (string? again, ConsumeIngredientsResponse? second) = await k.Recipes.ConsumeAsync(bill, default);
            Assert.Null(again);
            Assert.All(second!.Issued, i => Assert.True(i.AlreadyIssued));
            Assert.Equal(1.85m, await OnHandAsync(k, k.Paneer));
            Assert.Equal(2, await k.Db.StockMovements.CountAsync(m => m.SourceType == RecipeRepository.ConsumptionSource && m.SourceId == 501));
        }
    }

    [Fact]
    public async Task A_shortage_refuses_the_bill_and_names_the_ingredient()
    {
        Kitchen k = await ArrangeAsync();
        await using (k.Db)
        {
            await k.Recipes.SaveAsync(k.Tikka, TikkaRecipe(k), default);

            (string? refusal, _) = await k.Recipes.ConsumeAsync(new ConsumeIngredientsRequest
            {
                BillId = 502,
                Date = DateOnly.FromDateTime(DateTime.Today),
                Dishes = [new ConsumedDish { ItemId = k.Tikka, Quantity = 20m }],
            }, default);

            Assert.Contains("Not enough Paneer", refusal);
        }
    }

    [Fact]
    public async Task A_recipe_takes_only_stocked_ingredients_in_their_own_units_and_shows_its_food_cost()
    {
        Kitchen k = await ArrangeAsync();
        await using (k.Db)
        {
            SaveRecipeRequest itself = TikkaRecipe(k);
            itself.Lines.Add(new() { IngredientItemId = k.Tikka, Quantity = 1m, UomId = k.Kg });
            Assert.Equal("A dish cannot be an ingredient of itself.", (await k.Recipes.SaveAsync(k.Tikka, itself, default)).Message);

            Assert.Equal(MenuOutcome.Ok, (await k.Recipes.SaveAsync(k.Tikka, TikkaRecipe(k), default)).Outcome);
            RecipeView view = (await k.Recipes.GetAsync(k.Tikka, default))!;

            // (0.3 kg × 250 + 0.02 kg × 500) ÷ 2 portions = 42.50 a portion, 21.3% of 200.
            Assert.Equal(42.5m, view.PortionCost);
            Assert.Equal(21.3m, view.FoodCostPercent);
            Assert.Equal(["G", "G"], view.Lines.Select(l => l.UomCode));
        }
    }

    [Fact]
    public async Task Wastage_leaves_stock_through_a_posted_stock_adjustment()
    {
        Kitchen k = await ArrangeAsync();
        await using (k.Db)
        {
            MenuResult<long> recorded = await k.Wastage.RecordAsync(new RecordWastageRequest { ItemId = k.Paneer, Quantity = 250m, UomId = k.Gram, Reason = WastageReason.Spoiled, Note = "Left out overnight" }, default);
            Assert.Equal(MenuOutcome.Ok, recorded.Outcome);

            Assert.Equal(1.75m, await OnHandAsync(k, k.Paneer));
            WastageView entry = Assert.Single(await k.Wastage.ListAsync(null, null, default));
            Assert.Equal(nameof(StockAdjustmentStatus.Posted), entry.Status);
            Assert.Equal(62.5m, entry.Cost);    // 0.25 kg × 250

            StockAdjustment adjustment = await k.Db.StockAdjustments.SingleAsync(a => a.StockAdjustmentId == entry.StockAdjustmentId);
            Assert.Equal(StockAdjustmentReason.Damage, adjustment.Reason);
            Assert.Contains("Left out overnight", adjustment.Notes);

            Assert.Contains("not a stocked item", (await k.Wastage.RecordAsync(new RecordWastageRequest { ItemId = k.Tikka, Quantity = 1m, Reason = WastageReason.Burnt }, default)).Message);
        }
    }
}
