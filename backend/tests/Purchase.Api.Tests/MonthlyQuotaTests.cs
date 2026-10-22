using BillBook.Tests.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Purchase.Entity.Models;
using Purchase.Entity.TableEntities;
using Purchase.Repository;
using Purchase.Repository.Repositories;
using Shared.Kernel.Documents;
using Shared.Kernel.Licensing;
using Shared.Kernel.Numbering;
using Shared.Kernel.Recurring;
using Shared.Kernel.Tenancy;
using Xunit;

namespace Purchase.Api.Tests;

/// <summary>
/// A Professional or trial branch may create 1,000 Purchase documents a month,
/// counted apart from its Sales documents (D-69, TK-210). The same mechanism as
/// Sales, here for purchase orders, goods receipts, bills and debit notes; the
/// mechanism's own cases (the races, the rollback, the month turning at midnight
/// in India) are in Sales' suite, so these are what is Purchase's.
///
/// Written for TK-210 and not run by the AI that wrote it (docs/Task_Details.md 0.5).
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class MonthlyQuotaTests
{
    private static readonly DateTimeOffset MidOctober = new(2026, 10, 15, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly October = new(2026, 10, 1);
    private static readonly DateOnly Today = new(2026, 10, 15);

    private readonly PostgresFixture _pg;

    public MonthlyQuotaTests(PostgresFixture pg) => _pg = pg;

    private QuotaHarness<PurchaseDbContext> Harness(int? purchase, int? sales = null) =>
        new(_pg.ConnectionString, PlanLimitKind.PurchaseTransactions, UsageRepository.CountedDocuments,
            new PlanLimits("Professional", sales, purchase), MidOctober);

    private static string Number() => $"P/{Guid.NewGuid():N}"[..20];

    private static Bill NewBill() => new()
    {
        TransactionTypeCode = "BIL",
        DocumentNo = Number(),
        DocumentDate = Today,
        VendorBillNo = $"V-{Guid.NewGuid():N}"[..20],
        VendorBillDate = Today,
        ContactId = 1,
        CurrencyCode = "INR",
        ExchangeRate = 1m,
        Status = DocumentStatus.Draft,
    };

    private static Task Bills(PurchaseDbContext db, int count)
    {
        for (int i = 0; i < count; i++)
        {
            db.Bills.Add(NewBill());
        }

        return Task.CompletedTask;
    }

    [SkippableFact]
    public async Task QUOTA_P01_every_counted_purchase_document_takes_a_place()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason ?? string.Empty);

        (Guid customerId, Guid orgId) = (Guid.NewGuid(), Guid.NewGuid());
        await using QuotaHarness<PurchaseDbContext> harness = Harness(purchase: 100);

        Assert.Equal("ok", await harness.TryAsync(customerId, orgId, db =>
        {
            db.PurchaseOrders.Add(new PurchaseOrder
            {
                TransactionTypeCode = "POR", DocumentNo = Number(), DocumentDate = Today, ContactId = 1,
                CurrencyCode = "INR", ExchangeRate = 1m, Status = DocumentStatus.Draft,
            });
            return Task.CompletedTask;
        }));
        Assert.Equal("ok", await harness.TryAsync(customerId, orgId, db =>
        {
            db.GoodsReceipts.Add(new GoodsReceipt
            {
                TransactionTypeCode = "GRN", DocumentNo = Number(), DocumentDate = Today, ContactId = 1,
                CurrencyCode = "INR", ExchangeRate = 1m, Status = DocumentStatus.Draft,
            });
            return Task.CompletedTask;
        }));
        Assert.Equal("ok", await harness.TryAsync(customerId, orgId, db => Bills(db, 1)));
        Assert.Equal(3, await harness.UsedAsync(customerId, orgId, October));

        // A debit note is against a bill, which is already in the books.
        long billId = await harness.RequestAsync(customerId, orgId, db => db.Bills.Select(b => b.BillId).FirstAsync(), inTransaction: false);
        Assert.Equal("ok", await harness.TryAsync(customerId, orgId, db =>
        {
            db.DebitNotes.Add(new DebitNote
            {
                TransactionTypeCode = "DBN", DocumentNo = Number(), DocumentDate = Today, BillId = billId, ContactId = 1,
                CurrencyCode = "INR", ExchangeRate = 1m, Status = DocumentStatus.Draft,
            });
            return Task.CompletedTask;
        }));
        Assert.Equal(4, await harness.UsedAsync(customerId, orgId, October));

        // An edit is not a new document.
        await harness.RequestAsync(customerId, orgId, async db =>
        {
            (await db.Bills.FirstAsync()).Notes = "edited";
            await db.SaveChangesAsync();
            return 0;
        });
        Assert.Equal(4, await harness.UsedAsync(customerId, orgId, October));
    }

    [SkippableFact]
    public async Task QUOTA_P02_the_thousand_and_first_is_refused_with_the_purchase_wording()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason ?? string.Empty);

        (Guid customerId, Guid orgId) = (Guid.NewGuid(), Guid.NewGuid());
        await using QuotaHarness<PurchaseDbContext> harness = Harness(purchase: 1000);

        Assert.Equal("ok", await harness.TryAsync(customerId, orgId, db => Bills(db, 1000)));

        PlanLimitReachedException refusal = await Assert.ThrowsAsync<PlanLimitReachedException>(() =>
            harness.RequestAsync(customerId, orgId, async db =>
            {
                db.Bills.Add(NewBill());
                await db.SaveChangesAsync();
                return 0;
            }));

        Assert.Equal(
            "Professional plan Purchase transaction limit reached (maximum 1,000 Purchase transactions this month). "
            + "Upgrade to the Elite Plan for unlimited transactions.",
            refusal.Message);
        Assert.Equal((PlanLimitKind.PurchaseTransactions, 1000, 1000),
            (refusal.Limit.Kind, refusal.Limit.Limit, refusal.Limit.Used));
        Assert.Equal(1000, await harness.RequestAsync(customerId, orgId, db => db.Bills.CountAsync(), inTransaction: false));
    }

    [SkippableFact]
    public async Task QUOTA_P03_a_full_sales_month_stops_nothing_in_purchase()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason ?? string.Empty);

        (Guid customerId, Guid orgId) = (Guid.NewGuid(), Guid.NewGuid());

        // The plan's Sales limit is zero, which is as full as a month can be.
        await using QuotaHarness<PurchaseDbContext> harness = Harness(purchase: 3, sales: 0);

        for (int i = 0; i < 3; i++)
        {
            Assert.Equal("ok", await harness.TryAsync(customerId, orgId, db => Bills(db, 1)));
        }

        Assert.Equal("refused:3/3", await harness.TryAsync(customerId, orgId, db => Bills(db, 1)));
    }

    [SkippableFact]
    public async Task QUOTA_P04_the_usage_read_says_what_is_used_what_is_allowed_and_what_is_left()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason ?? string.Empty);

        (Guid customerId, Guid orgId) = (Guid.NewGuid(), Guid.NewGuid());
        await using QuotaHarness<PurchaseDbContext> harness = Harness(purchase: 1000);
        await harness.TryAsync(customerId, orgId, db => Bills(db, 40));

        MonthlyUsageResponse usage = await harness.RequestAsync(customerId, orgId,
            db => new UsageRepository(db, harness.Limits, harness.Clock).ReadAsync(default), inTransaction: false);

        Assert.Equal((PlanLimitKind.PurchaseTransactions, "Professional", October, 40, 1000, 960),
            (usage.Kind, usage.Plan, usage.PeriodStart, usage.Used, usage.Limit, usage.Remaining));
    }

    [SkippableFact]
    public async Task QUOTA_P05_a_due_recurring_bill_is_skipped_at_the_limit_and_entered_once_a_place_is_free()
    {
        Skip.If(_pg.SkipReason is not null, _pg.SkipReason ?? string.Empty);

        (Guid customerId, Guid orgId) = (Guid.NewGuid(), Guid.NewGuid());
        await using QuotaHarness<PurchaseDbContext> harness = Harness(purchase: 2);

        await harness.RequestAsync(customerId, orgId, async db =>
        {
            db.NumberingSeries.AddRange(Repository.SeedData.NumberingSeriesSeed.Build(orgId));
            await db.SaveChangesAsync();
            return 0;
        }, inTransaction: false);

        RunRecurringResponse skipped = new();
        RunRecurringResponse entered = new();
        string? lastError = null;
        DateOnly? nextRun = null;

        await harness.RequestAsync(customerId, orgId, async db =>
        {
            TenantContext tenant = new() { CustomerId = customerId, OrgId = orgId };
            StubNameLookup names = new();
            StubBaseCurrency baseCurrency = new();
            BillRepository bills = new(
                db, tenant, new NumberGenerator(db, Options.Create(new NumberingOptions()), new StubFinancialYear()),
                baseCurrency, new StubBranchSettings(), new StubTaxRates(), names, names,
                new RecordingInventory(), new RecordingLedger(), new StubPaymentTerms(), new RecordingFixedAssets(),
                new StubCurrentUser(), TimeProvider.System);
            RecurringBillingRepository schedules = new(db, bills, baseCurrency, TimeProvider.System);

            // The template is the month's first document; the February bill is the second.
            BillResult template = await bills.CreateAsync(new SaveBillRequest
            {
                DocumentDate = new DateOnly(2026, 1, 1),
                ContactId = 42,
                VendorBillNo = "RENT-JAN",
                VendorBillDate = new DateOnly(2026, 1, 1),
                DueDate = new DateOnly(2026, 1, 11),
                PlaceOfSupplyStateCode = "33",
                Lines =
                [
                    new SaveBillLineRequest
                    {
                        ItemId = 7, Description = "Shop rent", Quantity = 1m, ConversionFactor = 1m,
                        UnitPrice = 50000m, TaxGroupId = 1, LineType = DocumentLineType.Stock,
                    },
                ],
            }, default);
            Assert.Equal(BillOutcome.Ok, template.Outcome);

            RecurringScheduleResult schedule = await schedules.CreateAsync(new SaveRecurringBillScheduleRequest
            {
                Name = "Rent",
                TemplateBillId = template.BillId,
                Frequency = RecurrenceFrequency.Monthly,
                StartDate = new DateOnly(2026, 2, 1),
                VendorBillNoPrefix = "RENT-",
                PlaceOfSupplyStateCode = "33",
            }, default);
            Assert.Equal(RecurringScheduleOutcome.Ok, schedule.Outcome);

            // February takes the second place; March is refused and stays due.
            skipped = await schedules.RunDueAsync(new DateOnly(2026, 3, 1), default);
            db.ChangeTracker.Clear();
            RecurringBillSchedule row = await db.RecurringBillSchedules.AsNoTracking().SingleAsync();
            lastError = row.LastError;
            nextRun = row.NextRunDate;

            harness.SetLimits(new PlanLimits("Professional", null, 5));
            entered = await schedules.RunDueAsync(new DateOnly(2026, 3, 1), default);
            return 0;
        }, inTransaction: false);

        Assert.Equal((1, 0, 1), (skipped.Created, skipped.Failed, skipped.Skipped));
        Assert.Contains("Purchase transaction limit reached", lastError);
        Assert.Equal(new DateOnly(2026, 3, 1), nextRun);
        Assert.Equal(1, entered.Created);
    }
}
