using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Sales.Entity.Enums;
using Sales.Entity.Models;
using Sales.Repository;
using Sales.Repository.Clients;
using Sales.Repository.Repositories;
using Shared.Kernel.Documents;
using Shared.Kernel.Numbering;
using Shared.Kernel.Restaurant;
using Shared.Kernel.Tenancy;
using Xunit;

namespace Sales.Api.Tests;

/// <summary>Loyalty points on restaurant bills (TK-166): earned after the bill posts, taken back on a void, retried when Customer was away.</summary>
[Collection(nameof(PostgresCollection))]
public sealed class RestaurantLoyaltyTests
{
    private readonly PostgresFixture _postgres;

    public RestaurantLoyaltyTests(PostgresFixture postgres) => _postgres = postgres;

    private sealed class RecordingLoyalty : ILoyaltyClient
    {
        public List<(long ContactId, long InvoiceId, int Points)> Earned { get; } = [];

        public List<long> Reversed { get; } = [];

        public bool Down { get; set; }

        public Task<LoyaltyPosting?> EarnAsync(long contactId, long invoiceId, string? documentNo, int points, CancellationToken ct)
        {
            if (Down) return Task.FromResult<LoyaltyPosting?>(null);
            bool again = Earned.Any(e => e.InvoiceId == invoiceId);
            if (!again) Earned.Add((contactId, invoiceId, points));
            return Task.FromResult<LoyaltyPosting?>(new LoyaltyPosting(points, Earned.Where(e => e.ContactId == contactId).Sum(e => e.Points), again));
        }

        public Task<LoyaltyPosting?> ReverseAsync(long invoiceId, CancellationToken ct)
        {
            if (Down) return Task.FromResult<LoyaltyPosting?>(null);
            Reversed.Add(invoiceId);
            int points = Earned.Where(e => e.InvoiceId == invoiceId).Sum(e => e.Points);
            return Task.FromResult<LoyaltyPosting?>(new LoyaltyPosting(-points, 0, false));
        }
    }

    private sealed record Branch(SalesDbContext Db, RestaurantOrderRepository Orders, RestaurantBillingRepository Billing, InvoiceRepository Invoices, RecordingLoyalty Loyalty);

    private async Task<Branch> ArrangeAsync(decimal rate, long? walkIn = 900)
    {
        Guid customer = Guid.NewGuid();
        Guid org = Guid.NewGuid();
        TenantContext tenant = new() { CustomerId = customer, OrgId = org, CustomerCode = "0000000042" };
        SalesDbContext db = _postgres.CreateContext(customer, org);
        db.NumberingSeries.AddRange(Repository.SeedData.NumberingSeriesSeed.Build(org));
        await db.SaveChangesAsync();

        NumberGenerator numbering = new(db, Options.Create(new NumberingOptions()), new StubFinancialYear());
        StubNameLookup names = new();
        RecordingLedger ledger = new();
        StubTaxRates rates = new();
        StubBranchSettings branch = new();
        RecordingLoyalty loyalty = new();
        InvoiceRepository invoices = new(
            db, tenant, numbering, new StubBaseCurrency(), branch, rates,
            names, names, new StubCurrentUser(), TimeProvider.System, new RecordingInventory(), ledger,
            new StubCreditCheck(), new StubDocumentStorage(), new StubInvoicePdf(), new StubOrgIdentity(), new StubUqcLookup(), new StubEInvoicing(),
            loyalty: loyalty);

        StubRestaurantSettings settings = new(new RestaurantSettingsView { LoyaltyEarnRate = rate });
        RestaurantOrderRepository orders = new(db, numbering, settings, new StubRestaurantMenu(), new StubUserNames(), names, TimeProvider.System);
        RestaurantBillingRepository billing = new(
            db, invoices, settings, new StubWalkIn(walkIn), new StubAccounts(), ledger, rates, branch, tenant, TimeProvider.System, loyalty: loyalty);
        return new Branch(db, orders, billing, invoices, loyalty);
    }

    private static PosTenderRequest Cash(decimal amount) => new() { Mode = PosTenderMode.Cash, Amount = amount, BankAccountId = 1 };

    private static async Task<OrderResult<RestaurantBillView>> BillAsync(Branch b, long? contactId, bool pay = false)
    {
        long orderId = (await b.Orders.OpenAsync(new OpenRestaurantOrderRequest
        {
            OrderType = OrderType.Takeaway,
            ContactId = contactId,
            Lines = [new AddOrderLineRequest { ItemId = 20, Quantity = 2 }],
        }, default)).Value;

        BillRestaurantOrdersRequest request = new() { OrderIds = [orderId] };
        if (pay)
        {
            BillPreview preview = (await b.Billing.PreviewAsync(request, default)).Value!;
            request.Tenders = [Cash(Math.Ceiling(preview.Total))];
        }

        return await b.Billing.BillAsync(request, default);
    }

    [Fact]
    public async Task A_posted_bill_earns_its_contact_points_once()
    {
        Branch b = await ArrangeAsync(rate: 1m);
        await using (b.Db)
        {
            OrderResult<RestaurantBillView> billed = await BillAsync(b, contactId: 42);

            Assert.Equal(OrderOutcome.Ok, billed.Outcome);
            var invoice = await b.Db.Invoices.SingleAsync(i => i.InvoiceId == billed.Value!.InvoiceId);
            int due = LoyaltyRules.PointsFor(invoice.TotalAmount, 1m);
            Assert.True(due > 0);
            Assert.Equal(due, billed.Value!.LoyaltyPoints);
            Assert.Equal(due, billed.Value.LoyaltyBalance);
            Assert.Null(billed.Value.LoyaltyNote);
            Assert.Equal([(42L, invoice.InvoiceId, due)], b.Loyalty.Earned);

            // The retry records nothing twice.
            OrderResult<BillLoyaltyView> retry = await b.Billing.SyncLoyaltyAsync(billed.Value.RestaurantBillId, default);
            Assert.True(retry.Value!.AlreadyRecorded);
            Assert.Single(b.Loyalty.Earned);
        }
    }

    [Fact]
    public async Task Voiding_the_bill_takes_its_points_back()
    {
        Branch b = await ArrangeAsync(rate: 1m);
        await using (b.Db)
        {
            OrderResult<RestaurantBillView> billed = await BillAsync(b, contactId: 42);

            InvoiceResult voided = await b.Invoices.VoidAsync(billed.Value!.InvoiceId, new VoidInvoiceRequest { Reason = "Wrong table" }, default);

            Assert.Equal(InvoiceOutcome.Ok, voided.Outcome);
            Assert.Equal([billed.Value.InvoiceId], b.Loyalty.Reversed);
        }
    }

    [Fact]
    public async Task The_walk_in_customer_and_a_branch_with_no_rate_earn_nothing()
    {
        Branch walkIn = await ArrangeAsync(rate: 1m);
        await using (walkIn.Db)
        {
            OrderResult<RestaurantBillView> billed = await BillAsync(walkIn, contactId: null, pay: true);
            Assert.Equal(OrderOutcome.Ok, billed.Outcome);
            Assert.Equal(0, billed.Value!.LoyaltyPoints);
            Assert.Empty(walkIn.Loyalty.Earned);
        }

        Branch noRate = await ArrangeAsync(rate: 0m);
        await using (noRate.Db)
        {
            OrderResult<RestaurantBillView> billed = await BillAsync(noRate, contactId: 42);
            Assert.Equal(0, billed.Value!.LoyaltyPoints);
            Assert.Empty(noRate.Loyalty.Earned);
            Assert.Equal(OrderOutcome.Refused, (await noRate.Billing.SyncLoyaltyAsync(billed.Value.RestaurantBillId, default)).Outcome);
        }
    }

    [Fact]
    public async Task When_Customer_is_away_the_bill_stands_and_the_retry_earns_the_points_due_then()
    {
        Branch b = await ArrangeAsync(rate: 1m);
        await using (b.Db)
        {
            b.Loyalty.Down = true;
            OrderResult<RestaurantBillView> billed = await BillAsync(b, contactId: 42);

            Assert.Equal(OrderOutcome.Ok, billed.Outcome);
            Assert.NotNull(billed.Value!.LoyaltyNote);
            Assert.Null(billed.Value.LoyaltyBalance);
            Assert.Equal(DocumentStatus.Posted, await b.Db.Invoices.Where(i => i.InvoiceId == billed.Value.InvoiceId).Select(i => i.Status).SingleAsync());
            Assert.Equal(OrderOutcome.Unavailable, (await b.Billing.SyncLoyaltyAsync(billed.Value.RestaurantBillId, default)).Outcome);

            b.Loyalty.Down = false;
            OrderResult<BillLoyaltyView> retry = await b.Billing.SyncLoyaltyAsync(billed.Value.RestaurantBillId, default);
            Assert.Equal(billed.Value.LoyaltyPoints, retry.Value!.Points);
            Assert.Single(b.Loyalty.Earned);
        }
    }
}
