using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Sales.Entity.Enums;
using Sales.Entity.Models;
using Sales.Entity.TableEntities;
using Sales.Repository;
using Sales.Repository.Clients;
using Sales.Repository.Repositories;
using Shared.Kernel.Documents;
using Shared.Kernel.Numbering;
using Shared.Kernel.Restaurant;
using Shared.Kernel.Tenancy;
using Xunit;

namespace Sales.Api.Tests;

/// <summary>The billing rules (TK-148), pure.</summary>
public sealed class RestaurantBillingRulesTests
{
    private static RestaurantOrderLine Line(long id, decimal qty, decimal price, long? taxGroup = 1, bool inclusive = false) => new()
    {
        RestaurantOrderLineId = id,
        ItemId = id,
        ItemName = $"Dish {id}",
        Quantity = qty,
        UnitPrice = price,
        TaxGroupId = taxGroup,
        IsPriceInclusive = inclusive,
        TaxTreatment = TaxTreatment.Taxable,
    };

    [Fact]
    public void The_service_charge_is_split_by_the_tax_its_dishes_carry()
    {
        IReadOnlyList<RestaurantBillingRulesRepository.ServiceChargePart> parts = RestaurantBillingRulesRepository.ServiceCharge(
            [Line(1, 2, 150m, taxGroup: 1), Line(2, 1, 99.99m, taxGroup: 2), Line(3, 1, 100m, taxGroup: 1)], 10m);

        Assert.Equal(2, parts.Count);
        Assert.Equal(40m, parts.Single(p => p.TaxGroupId == 1).Amount);
        Assert.Equal(10m, parts.Single(p => p.TaxGroupId == 2).Amount);
    }

    [Fact]
    public void No_service_charge_at_zero_and_one_part_per_pricing_basis()
    {
        Assert.Empty(RestaurantBillingRulesRepository.ServiceCharge([Line(1, 1, 100m)], 0m));
        Assert.Equal(2, RestaurantBillingRulesRepository.ServiceCharge([Line(1, 1, 100m), Line(2, 1, 100m, inclusive: true)], 5m).Count);
    }

    [Fact]
    public void A_dish_is_billed_once_and_only_from_its_orders()
    {
        RestaurantOrderLine[] available = [Line(1, 1, 10m), Line(2, 1, 10m)];

        Assert.Null(RestaurantBillingRulesRepository.SelectionRefusal(available, []));
        Assert.Null(RestaurantBillingRulesRepository.SelectionRefusal(available, [2]));
        Assert.NotNull(RestaurantBillingRulesRepository.SelectionRefusal(available, [3]));
        Assert.NotNull(RestaurantBillingRulesRepository.SelectionRefusal(available, [1, 1]));
        Assert.NotNull(RestaurantBillingRulesRepository.SelectionRefusal([], []));
    }

    [Fact]
    public void A_tip_needs_tips_on_and_an_account()
    {
        Assert.Null(RestaurantBillingRulesRepository.TipRefusal(false, 0m, null));
        Assert.NotNull(RestaurantBillingRulesRepository.TipRefusal(false, 20m, 1));
        Assert.NotNull(RestaurantBillingRulesRepository.TipRefusal(true, 20m, null));
        Assert.Null(RestaurantBillingRulesRepository.TipRefusal(true, 20m, 1));
    }

    [Fact]
    public void A_stocked_dish_issues_stock_and_a_made_dish_does_not()
    {
        RestaurantOrderLine water = Line(1, 2, 20m);
        water.IsStocked = true;
        water.Modifiers = [new RestaurantOrderLineModifier { Name = "Chilled" }];

        SaveInvoiceLineRequest line = RestaurantBillingRulesRepository.InvoiceLine(water);
        Assert.Equal(DocumentLineType.Stock, line.LineType);
        Assert.Equal("Dish 1: Chilled", line.Description);
        Assert.Equal(DocumentLineType.Expense, RestaurantBillingRulesRepository.InvoiceLine(Line(2, 1, 100m)).LineType);
    }
}

/// <summary>Billing through the invoice (TK-148).</summary>
[Collection(nameof(PostgresCollection))]
public sealed class RestaurantBillingTests
{
    private readonly PostgresFixture _postgres;

    public RestaurantBillingTests(PostgresFixture postgres) => _postgres = postgres;

    private sealed record Branch(
        SalesDbContext Db, RestaurantOrderRepository Orders, RestaurantBillingRepository Billing, StubRestaurantSettings Settings, RecordingLedger Ledger, long TableId);

    private sealed class RecordingRecipes : IRecipeConsumptionClient
    {
        public List<(long BillId, IReadOnlyList<DishToConsume> Dishes)> Calls { get; } = [];

        public string? Refuse { get; set; }

        public Task<ConsumptionResult> ConsumeAsync(long billId, DateOnly date, IReadOnlyList<DishToConsume> dishes, CancellationToken ct)
        {
            Calls.Add((billId, dishes));
            return Task.FromResult(Refuse is null ? new ConsumptionResult(true, null) : new ConsumptionResult(false, Refuse));
        }
    }

    private async Task<Branch> ArrangeAsync(RestaurantSettingsView settings, long? walkIn = 900, IRecipeConsumptionClient? recipes = null)
    {
        Guid customer = Guid.NewGuid();
        Guid org = Guid.NewGuid();
        TenantContext tenant = new() { CustomerId = customer, OrgId = org, CustomerCode = "0000000042" };
        SalesDbContext db = _postgres.CreateContext(customer, org);
        db.NumberingSeries.AddRange(Repository.SeedData.NumberingSeriesSeed.Build(org));
        await db.SaveChangesAsync();

        RestaurantTableRepository tables = new(db);
        await tables.SeedFloorAsync(org, "Restaurant", default);
        long floorId = (await tables.FloorPlanAsync(false, default)).Floors.Single().FloorId;
        long tableId = (await tables.SaveTableAsync(null, new SaveRestaurantTableRequest { FloorId = floorId, Code = "T1" }, default)).Value;

        NumberGenerator numbering = new(db, Options.Create(new NumberingOptions()), new StubFinancialYear());
        StubNameLookup names = new();
        RecordingLedger ledger = new();
        StubTaxRates rates = new();
        StubBranchSettings branch = new();
        InvoiceRepository invoices = new(
            db, tenant, numbering, new StubBaseCurrency(), branch, rates,
            names, names, new StubCurrentUser(), TimeProvider.System, new RecordingInventory(), ledger,
            new StubCreditCheck(), new StubDocumentStorage(), new StubInvoicePdf(), new StubOrgIdentity(), new StubUqcLookup(), new StubEInvoicing());

        StubRestaurantSettings stubSettings = new(settings);
        RestaurantOrderRepository orders = new(db, numbering, stubSettings, new StubRestaurantMenu(), new StubUserNames(), names, TimeProvider.System);
        RestaurantBillingRepository billing = new(db, invoices, stubSettings, new StubWalkIn(walkIn), new StubAccounts(), ledger, rates, branch, tenant, TimeProvider.System, recipes);
        return new Branch(db, orders, billing, stubSettings, ledger, tableId);
    }

    private static AddOrderLineRequest Dish(long itemId, decimal quantity = 1) => new() { ItemId = itemId, Quantity = quantity };

    private static PosTenderRequest Cash(decimal amount) => new() { Mode = PosTenderMode.Cash, Amount = amount, BankAccountId = 1 };

    [Fact]
    public async Task A_split_bill_posts_two_invoices_whose_totals_equal_the_order()
    {
        Branch b = await ArrangeAsync(new RestaurantSettingsView());
        await using (b.Db)
        {
            long orderId = (await b.Orders.OpenAsync(new OpenRestaurantOrderRequest { OrderType = OrderType.DineIn, ContactId = 42, Lines = [Dish(10), Dish(20), Dish(30, 2)] }, default)).Value;
            List<long> lineIds = await b.Db.RestaurantOrderLines.OrderBy(l => l.LineNo).Select(l => l.RestaurantOrderLineId).ToListAsync();

            OrderResult<RestaurantBillView> first = await b.Billing.BillAsync(new BillRestaurantOrdersRequest { OrderIds = [orderId], LineIds = [lineIds[0], lineIds[1]] }, default);
            Assert.Equal(OrderOutcome.Ok, first.Outcome);
            Assert.Empty(first.Value!.ClosedOrderIds);
            Assert.Equal(RestaurantOrderStatus.Open, (await b.Orders.GetAsync(orderId, default))!.Status);

            OrderResult<RestaurantBillView> second = await b.Billing.BillAsync(new BillRestaurantOrdersRequest { OrderIds = [orderId] }, default);
            Assert.Equal(OrderOutcome.Ok, second.Outcome);
            Assert.Equal([orderId], second.Value!.ClosedOrderIds);

            List<Invoice> invoices = await b.Db.Invoices.Where(i => i.InvoiceId == first.Value.InvoiceId || i.InvoiceId == second.Value.InvoiceId).ToListAsync();
            Assert.Equal(2, invoices.Count);
            Assert.All(invoices, i => Assert.Equal(DocumentStatus.Posted, i.Status));

            // The order was worth 100 + 200 + 2 × 300 before tax (dine-in prices); the two bills' values add up to it.
            RestaurantOrderView order = (await b.Orders.GetAsync(orderId, default))!;
            Assert.Equal(900m, order.Total);
            Assert.Equal(order.Total, invoices.Sum(i => i.SubTotal));
            Assert.Equal(RestaurantOrderStatus.Billed, order.Status);

            // Nothing is left to bill twice.
            Assert.Equal(OrderOutcome.Refused, (await b.Billing.BillAsync(new BillRestaurantOrdersRequest { OrderIds = [orderId] }, default)).Outcome);
        }
    }

    [Fact]
    public async Task A_missing_required_table_is_refused()
    {
        Branch b = await ArrangeAsync(new RestaurantSettingsView { TablesEnabled = true, TableRequired = RequirementLevel.Off });
        await using (b.Db)
        {
            long orderId = (await b.Orders.OpenAsync(new OpenRestaurantOrderRequest { OrderType = OrderType.DineIn, ContactId = 42, Lines = [Dish(10)] }, default)).Value;

            b.Settings.Settings = new RestaurantSettingsView { TablesEnabled = true, TableRequired = RequirementLevel.DineInOnly };
            OrderResult<RestaurantBillView> refused = await b.Billing.BillAsync(new BillRestaurantOrdersRequest { OrderIds = [orderId] }, default);

            Assert.Equal(OrderOutcome.Refused, refused.Outcome);
            Assert.Contains("Choose a table", refused.Message);
            Assert.False(await b.Db.Invoices.AnyAsync());
        }
    }

    [Fact]
    public async Task The_service_charge_is_an_invoice_line_taxed_like_the_food()
    {
        Branch b = await ArrangeAsync(new RestaurantSettingsView { ServiceChargePercent = 10m });
        await using (b.Db)
        {
            long orderId = (await b.Orders.OpenAsync(new OpenRestaurantOrderRequest { OrderType = OrderType.DineIn, ContactId = 42, Lines = [Dish(10, 2), Dish(30)] }, default)).Value;

            BillPreview preview = (await b.Billing.PreviewAsync(new BillRestaurantOrdersRequest { OrderIds = [orderId] }, default)).Value!;
            OrderResult<RestaurantBillView> billed = await b.Billing.BillAsync(new BillRestaurantOrdersRequest { OrderIds = [orderId] }, default);

            InvoiceDetail charge = await b.Db.InvoiceDetails.SingleAsync(d => d.InvoiceId == billed.Value!.InvoiceId && d.ItemId == null);
            Assert.Equal("Service charge 10%", charge.Description);
            Assert.Equal(50m, charge.TaxableAmount);       // 10% of 200 + 300
            Assert.Equal(9m, charge.TaxAmount);            // at the food's 18% (the stub's rate)
            Assert.Equal(50m, billed.Value!.ServiceChargeAmount);
            Assert.Equal(preview.Total, billed.Value.Total);

            // Waived, there is no line.
            long another = (await b.Orders.OpenAsync(new OpenRestaurantOrderRequest { OrderType = OrderType.DineIn, ContactId = 42, Lines = [Dish(10)] }, default)).Value;
            OrderResult<RestaurantBillView> waived = await b.Billing.BillAsync(new BillRestaurantOrdersRequest { OrderIds = [another], ApplyServiceCharge = false }, default);
            Assert.False(await b.Db.InvoiceDetails.AnyAsync(d => d.InvoiceId == waived.Value!.InvoiceId && d.ItemId == null));
        }
    }

    [Fact]
    public async Task A_walk_in_bill_must_be_paid_and_its_tenders_replace_the_receivable()
    {
        Branch b = await ArrangeAsync(new RestaurantSettingsView());
        await using (b.Db)
        {
            long orderId = (await b.Orders.OpenAsync(new OpenRestaurantOrderRequest { OrderType = OrderType.DineIn, Lines = [Dish(10)] }, default)).Value;

            Assert.Contains("must be paid", (await b.Billing.BillAsync(new BillRestaurantOrdersRequest { OrderIds = [orderId] }, default)).Message);

            // 100 + 18% = 118; paid 200 in cash, 82 back.
            OrderResult<RestaurantBillView> billed = await b.Billing.BillAsync(new BillRestaurantOrdersRequest { OrderIds = [orderId], Tenders = [Cash(200m)] }, default);
            Assert.Equal(OrderOutcome.Ok, billed.Outcome);
            Assert.Equal(82m, billed.Value!.Change);

            Invoice invoice = await b.Db.Invoices.SingleAsync(i => i.InvoiceId == billed.Value.InvoiceId);
            Assert.Equal(900, invoice.ContactId);
            PostLedgerRequest posting = b.Ledger.Posts.Single(p => p.TransactionId == invoice.InvoiceId && p.TransactionTypeCode == "INV");
            Assert.Contains(posting.Legs, l => l.BankAccountId == 1 && l.DebitAmount == 118m);
            Assert.DoesNotContain(posting.Legs, l => l.AccountSystemName == "Accounts Receivable");
        }
    }

    [Fact]
    public async Task A_tip_posts_to_tips_payable_and_only_while_tips_are_on()
    {
        Branch b = await ArrangeAsync(new RestaurantSettingsView { TipsEnabled = false });
        await using (b.Db)
        {
            long orderId = (await b.Orders.OpenAsync(new OpenRestaurantOrderRequest { OrderType = OrderType.DineIn, ContactId = 42, Lines = [Dish(10)] }, default)).Value;
            BillRestaurantOrdersRequest withTip = new() { OrderIds = [orderId], TipAmount = 20m, TipBankAccountId = 2 };

            Assert.Contains("Tips are switched off", (await b.Billing.BillAsync(withTip, default)).Message);

            b.Settings.Settings = new RestaurantSettingsView { TipsEnabled = true };
            OrderResult<RestaurantBillView> billed = await b.Billing.BillAsync(withTip, default);
            Assert.Equal(OrderOutcome.Ok, billed.Outcome);

            PostLedgerRequest tip = b.Ledger.Posts.Single(p => p.TransactionTypeCode == "TIP");
            Assert.Equal(billed.Value!.RestaurantBillId, tip.TransactionId);
            Assert.Contains(tip.Legs, l => l.BankAccountId == 2 && l.DebitAmount == 20m);
            Assert.Contains(tip.Legs, l => l.AccountSystemName == "Tips Payable" && l.CreditAmount == 20m);

            // The tip is not on the invoice: it is the staff's, not the branch's income.
            Assert.Equal(118m, (await b.Db.Invoices.SingleAsync(i => i.InvoiceId == billed.Value.InvoiceId)).TotalAmount);
        }
    }

    [Fact]
    public async Task Merged_bills_put_several_orders_on_one_invoice_and_free_nothing_until_cleared()
    {
        Branch b = await ArrangeAsync(new RestaurantSettingsView { TablesEnabled = true });
        await using (b.Db)
        {
            long atTable = (await b.Orders.OpenAsync(new OpenRestaurantOrderRequest { OrderType = OrderType.DineIn, RestaurantTableId = b.TableId, ContactId = 42, Lines = [Dish(10)] }, default)).Value;
            long takeaway = (await b.Orders.OpenAsync(new OpenRestaurantOrderRequest { OrderType = OrderType.Takeaway, ContactId = 42, Lines = [Dish(20)] }, default)).Value;

            OrderResult<RestaurantBillView> billed = await b.Billing.BillAsync(new BillRestaurantOrdersRequest { OrderIds = [atTable, takeaway] }, default);

            Assert.Equal(OrderOutcome.Ok, billed.Outcome);
            Assert.Equal(2, billed.Value!.ClosedOrderIds.Count);
            Assert.Equal(2, await b.Db.InvoiceDetails.CountAsync(d => d.InvoiceId == billed.Value.InvoiceId));
            Assert.Equal(TableStatus.Billed, (await b.Db.RestaurantTables.SingleAsync()).Status);
        }
    }

    [Fact]
    public async Task With_kitchen_tickets_required_an_unsent_dish_stops_the_bill()
    {
        Branch b = await ArrangeAsync(new RestaurantSettingsView { KitchenEnabled = true, KotRequired = true });
        await using (b.Db)
        {
            long orderId = (await b.Orders.OpenAsync(new OpenRestaurantOrderRequest { OrderType = OrderType.DineIn, ContactId = 42, Lines = [Dish(10)] }, default)).Value;

            Assert.Equal("Send every dish to the kitchen before billing.", (await b.Billing.BillAsync(new BillRestaurantOrdersRequest { OrderIds = [orderId] }, default)).Message);
            Assert.Equal("Send every dish to the kitchen before billing.", (await b.Billing.PreviewAsync(new BillRestaurantOrdersRequest { OrderIds = [orderId] }, default)).Value!.Problem);
        }
    }

    [Fact]
    public async Task With_recipes_on_a_bill_uses_its_ingredients_once_and_a_shortage_stops_it()
    {
        RecordingRecipes recipes = new();
        Branch b = await ArrangeAsync(new RestaurantSettingsView { RecipesEnabled = true }, recipes: recipes);
        await using (b.Db)
        {
            long orderId = (await b.Orders.OpenAsync(new OpenRestaurantOrderRequest { OrderType = OrderType.DineIn, ContactId = 42, Lines = [Dish(10, 2)] }, default)).Value;

            recipes.Refuse = "Not enough Paneer in stock for this bill's dishes.";
            OrderResult<RestaurantBillView> refused = await b.Billing.BillAsync(new BillRestaurantOrdersRequest { OrderIds = [orderId] }, default);
            Assert.Equal(recipes.Refuse, refused.Message);

            recipes.Refuse = null;
            OrderResult<RestaurantBillView> billed = await b.Billing.BillAsync(new BillRestaurantOrdersRequest { OrderIds = [orderId] }, default);
            Assert.Equal(OrderOutcome.Ok, billed.Outcome);

            (long billId, IReadOnlyList<DishToConsume> dishes) = recipes.Calls[^1];
            Assert.Equal(billed.Value!.RestaurantBillId, billId);
            DishToConsume dish = Assert.Single(dishes);
            Assert.Equal(10, dish.ItemId);
            Assert.Equal(2m, dish.Quantity);
            Assert.True(dish.UsesRecipe);   // the stub's dishes are made to order, not stocked
        }
    }
}
