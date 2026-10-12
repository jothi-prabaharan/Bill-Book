using Accounting.Api.Controllers;
using Accounting.Entity.Enums;
using Accounting.Entity.Models;
using Accounting.Entity.TableEntities;
using Accounting.Repository;
using Accounting.Repository.Repositories;
using Accounting.Repository.SeedData;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Numbering;
using Xunit;

namespace Accounting.Api.Tests;

/// <summary>
/// The option lists a create page reads (TK-192). A document is offered only
/// what its side may use and what posting would accept: a rate in force on its
/// date, an account that takes postings, and the number the generator would
/// give it.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class DocumentChoicesTests
{
    private static readonly DateOnly From = new(2026, 4, 1);

    private readonly PostgresFixture _postgres;

    public DocumentChoicesTests(PostgresFixture postgres) => _postgres = postgres;

    /// <summary>Records what it was asked, and previews a number from it.</summary>
    private sealed class PeekingNumberGenerator : INumberGenerator
    {
        public string? Code { get; private set; }

        public DateOnly? On { get; private set; }

        public Task<NumberAllocation> NextAsync(string seriesCode, DateOnly onDate, CancellationToken ct) =>
            throw new InvalidOperationException("A choice list must never allocate a number.");

        public Task<string?> PeekAsync(string seriesCode, DateOnly onDate, CancellationToken ct)
        {
            Code = seriesCode;
            On = onDate;
            return Task.FromResult<string?>($"{seriesCode}/{onDate:yyyyMMdd}/00042");
        }
    }

    private sealed class FixedClock : TimeProvider
    {
        private readonly DateTimeOffset _now;

        public FixedClock(DateTimeOffset now) => _now = now;

        public override DateTimeOffset GetUtcNow() => _now;
    }

    private static readonly FixedClock Clock = new(new DateTimeOffset(2026, 6, 15, 10, 0, 0, TimeSpan.Zero));

    private static DocumentChoicesRepository Repository(AccountingDbContext db, INumberGenerator? numbers = null) =>
        new(db, numbers ?? new PeekingNumberGenerator(), Clock);

    private async Task<(Guid CustomerId, Guid OrgId)> SeedRatesAsync()
    {
        Guid customerId = Guid.NewGuid();
        Guid orgId = Guid.NewGuid();

        await using AccountingDbContext db = _postgres.CreateContext(customerId, orgId);
        List<TaxMaster> rates = [.. TaxMasterSeed.Build(orgId, From)];
        db.TaxMasters.AddRange(rates);
        await db.SaveChangesAsync();

        foreach (TaxMaster rate in rates)
        {
            rate.TaxGroupId = rate.TaxMasterId;
        }

        await db.SaveChangesAsync();
        return (customerId, orgId);
    }

    [SkippableFact]
    public async Task A_purchase_document_is_offered_only_purchase_rates_in_force_on_its_date()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        (Guid customerId, Guid orgId) = await SeedRatesAsync();
        await using AccountingDbContext db = _postgres.CreateContext(customerId, orgId);

        (await db.TaxMasters.SingleAsync(t => t.TaxSystemName == "GST18")).IsPurchase = false;
        (await db.TaxMasters.SingleAsync(t => t.TaxSystemName == "GST12")).IsActive = false;
        (await db.TaxMasters.SingleAsync(t => t.TaxSystemName == "GST5")).EffectiveTo = From.AddDays(5);
        await db.SaveChangesAsync();

        IReadOnlyList<TaxRateChoice> purchase = await Repository(db).TaxRatesAsync(ChoiceUsage.Purchase, From.AddDays(10), default);
        IReadOnlyList<TaxRateChoice> sales = await Repository(db).TaxRatesAsync(ChoiceUsage.Sales, From.AddDays(10), default);

        Assert.Equal(["GST 0%", "Bullion 3%", "GST 28%"], purchase.Select(r => r.TaxName));
        Assert.Contains(sales, r => r.TaxName == "GST 18%" && r.CgstRate == 9m && r.IgstRate == 18m);
        Assert.DoesNotContain(sales, r => r.TaxName is "GST 12%" or "GST 5%");

        // Before any rate takes effect there is nothing to offer.
        Assert.Empty(await Repository(db).TaxRatesAsync(ChoiceUsage.Sales, From.AddDays(-1), default));
    }

    [SkippableFact]
    public async Task A_revision_starting_the_day_the_old_rate_ends_is_offered_once()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        (Guid customerId, Guid orgId) = await SeedRatesAsync();
        await using AccountingDbContext db = _postgres.CreateContext(customerId, orgId);

        DateOnly revisedOn = From.AddDays(30);
        TaxMaster old = await db.TaxMasters.SingleAsync(t => t.TaxSystemName == "GST18");
        old.EffectiveTo = revisedOn;
        db.TaxMasters.Add(new TaxMaster
        {
            OrgId = orgId,
            TaxGroupId = old.TaxGroupId,
            TaxSystemName = old.TaxSystemName,
            TaxName = "GST 18% (revised)",
            TotalRate = 18m,
            CgstRate = 9m,
            SgstRate = 9m,
            IgstRate = 18m,
            CessRate = 1m,
            EffectiveFrom = revisedOn,
            IsSales = true,
            IsPurchase = true,
            IsActive = true,
        });
        await db.SaveChangesAsync();

        IReadOnlyList<TaxRateChoice> onTheDay = await Repository(db).TaxRatesAsync(ChoiceUsage.Any, revisedOn, default);

        TaxRateChoice eighteen = Assert.Single(onTheDay, r => r.TaxGroupId == old.TaxGroupId);
        Assert.Equal("GST 18% (revised)", eighteen.TaxName);
        Assert.Equal(1m, eighteen.CessRate);
    }

    [SkippableFact]
    public async Task Locked_and_inactive_accounts_are_not_offered_and_each_side_sees_its_own()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        Guid customerId = Guid.NewGuid();
        Guid orgId = Guid.NewGuid();
        await using AccountingDbContext db = _postgres.CreateContext(customerId, orgId);
        db.Accounts.AddRange(ChartOfAccountsSeed.Build(orgId));
        await db.SaveChangesAsync();

        List<Account> salesAccounts = await db.Accounts.Where(a => a.IsSales && a.IsActive).OrderBy(a => a.AccountCode).ToListAsync();
        Assert.True(salesAccounts.Count >= 3, "The seeded chart has four accounts usable on sales.");
        Account locked = salesAccounts[0];
        Account inactive = salesAccounts[1];
        locked.IsLock = true;
        inactive.IsActive = false;
        await db.SaveChangesAsync();

        IReadOnlyList<AccountChoice> sales = await Repository(db).AccountsAsync(ChoiceUsage.Sales, default);
        IReadOnlyList<AccountChoice> purchase = await Repository(db).AccountsAsync(ChoiceUsage.Purchase, default);

        Assert.DoesNotContain(sales, a => a.AccountId == locked.AccountId || a.AccountId == inactive.AccountId);
        Assert.Contains(sales, a => a.AccountId == salesAccounts[2].AccountId);

        HashSet<long> purchaseIds = [.. await db.Accounts.Where(a => a.IsPurchase && a.IsActive && !a.IsLock).Select(a => a.AccountId).ToListAsync()];
        Assert.True(purchaseIds.SetEquals(purchase.Select(a => a.AccountId)));
    }

    [SkippableFact]
    public async Task Payment_terms_follow_the_side_and_the_branch_order()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        Guid customerId = Guid.NewGuid();
        Guid orgId = Guid.NewGuid();
        await using AccountingDbContext db = _postgres.CreateContext(customerId, orgId);
        db.PaymentTerms.AddRange(PaymentTermsSeed.Build(orgId));
        await db.SaveChangesAsync();

        PaymentTerm salesOnly = await db.PaymentTerms.OrderBy(t => t.DisplayOrder).FirstAsync();
        salesOnly.IsPurchase = false;
        await db.SaveChangesAsync();

        IReadOnlyList<PaymentTermChoice> sales = await Repository(db).PaymentTermsAsync(ChoiceUsage.Sales, default);
        IReadOnlyList<PaymentTermChoice> purchase = await Repository(db).PaymentTermsAsync(ChoiceUsage.Purchase, default);

        Assert.Equal(salesOnly.PaymentTermId, sales[0].PaymentTermId);
        Assert.DoesNotContain(purchase, t => t.PaymentTermId == salesOnly.PaymentTermId);
        Assert.Equal(sales.Count - 1, purchase.Count);
    }

    [Fact]
    public async Task The_next_number_is_the_generators_preview_for_the_document_date_or_today()
    {
        var numbers = new PeekingNumberGenerator();

        // The preview reads nothing of its own, so no database stands behind it.
        DocumentChoicesRepository repository = Repository(null!, numbers);

        NextNumberChoice dated = await repository.NextNumberAsync("INV", new DateOnly(2026, 7, 1), default);
        Assert.Equal("INV/20260701/00042", dated.Number);
        Assert.Equal(("INV", new DateOnly(2026, 7, 1)), (numbers.Code, numbers.On));

        NextNumberChoice today = await repository.NextNumberAsync("JRN", null, default);
        Assert.Equal("JRN", today.SeriesCode);
        Assert.Equal(new DateOnly(2026, 6, 15), numbers.On);
    }

    [Fact]
    public async Task A_module_previews_its_own_series_and_no_other()
    {
        DocumentChoicesRepository repository = Repository(null!);

        var sales = new SalesChoicesController(repository);
        var purchase = new PurchaseChoicesController(repository);
        var banking = new BankingChoicesController(repository);

        OkObjectResult invoice = Assert.IsType<OkObjectResult>(await sales.NextNumber(" inv ", null, default));
        Assert.Equal("INV", Assert.IsType<NextNumberChoice>(invoice.Value).SeriesCode);

        Assert.IsType<NotFoundResult>(await sales.NextNumber("BIL", null, default));
        Assert.IsType<OkObjectResult>(await purchase.NextNumber("BIL", null, default));
        Assert.IsType<NotFoundResult>(await purchase.NextNumber("JRN", null, default));
        Assert.IsType<OkObjectResult>(await banking.NextNumber("RCM", null, default));
    }
}
