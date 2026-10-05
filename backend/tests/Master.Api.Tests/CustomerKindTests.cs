using Master.Api.Services;
using Master.Entity.Enums;
using Master.Entity.Models;
using Master.Entity.TableEntities;
using Master.Repository;
using Master.Repository.Clients;
using Master.Repository.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Shared.Kernel.Apps;
using Shared.Kernel.Tenancy;
using Xunit;

namespace Master.Api.Tests;

/// <summary>
/// Customer kinds (D-44, TK-152): a signup is always Standard, only an operator
/// makes Internal or Demo, and those two are never expired, never stamped
/// Expired and never held to the user or branch limits a licence sells.
/// </summary>
[Collection(nameof(AdminCollection))]
public sealed class CustomerKindTests
{
    private readonly AdminFixture _admin;

    public CustomerKindTests(AdminFixture admin) => _admin = admin;

    // ---- The rules, pure --------------------------------------------------

    [Theory]
    [InlineData(CustomerKind.Internal, false, CustomerKind.Standard)]
    [InlineData(CustomerKind.Demo, false, CustomerKind.Standard)]
    [InlineData(null, false, CustomerKind.Standard)]
    [InlineData(CustomerKind.Internal, true, CustomerKind.Internal)]
    [InlineData(CustomerKind.Demo, true, CustomerKind.Demo)]
    [InlineData(null, true, CustomerKind.Standard)]
    [InlineData((CustomerKind)99, true, CustomerKind.Standard)]
    public void Only_an_operator_makes_anything_but_standard(CustomerKind? requested, bool byOperator, CustomerKind made) =>
        Assert.Equal(made, CustomerKindRepository.KindToCreate(requested, byOperator));

    [Theory]
    [InlineData(CustomerKind.Standard, false)]
    [InlineData(CustomerKind.Internal, true)]
    [InlineData(CustomerKind.Demo, true)]
    public void Internal_and_demo_never_lapse(CustomerKind kind, bool neverLapses) =>
        Assert.Equal(neverLapses, CustomerKindRepository.NeverLapses(kind));

    [Fact]
    public void A_standard_customer_starts_on_a_trial_and_the_others_on_elite_with_no_expiry()
    {
        DateOnly today = new(2026, 10, 5);

        Assert.Equal((LicenseType.Trial, today.AddDays(14)), CustomerKindRepository.FirstLicence(CustomerKind.Standard, today, 14));
        Assert.Equal((LicenseType.Elite, CustomerKindRepository.NoExpiry), CustomerKindRepository.FirstLicence(CustomerKind.Demo, today, 14));
        Assert.Equal(PlanTier.Trial, CustomerKindRepository.FirstPlanTier(CustomerKind.Standard));
        Assert.Equal(PlanTier.Elite, CustomerKindRepository.FirstPlanTier(CustomerKind.Internal));

        // Grace days are added to the expiry; the sentinel leaves room for them.
        Assert.True(CustomerKindRepository.NoExpiry.AddDays(3650) > CustomerKindRepository.NoExpiry);
    }

    // ---- Through the database --------------------------------------------

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class NoQueue : IProvisioningQueue
    {
        public ValueTask EnqueueAsync(ProvisioningJob job, CancellationToken ct = default) => ValueTask.CompletedTask;

        public ValueTask<ProvisioningJob> DequeueAsync(CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed class NoCurrencies : IMasterCurrenciesRepository
    {
        public Task<IReadOnlyList<MasterCurrency>> GetAllAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<MasterCurrency>>([]);

        public Task<int?> FindCurrencyIdAsync(string code, CancellationToken ct = default) => Task.FromResult<int?>(1);
    }

    private sealed class NoSeeding : ITenantSeeder
    {
        public Task<IReadOnlyList<string>> SeedAsync(Guid customerId, Guid orgId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<string>>([]);

        public Task<IReadOnlyList<string>> SeedAsync(Guid customerId, Guid orgId, BusinessType? businessType, AddOn addOns, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<string>>([]);
    }

    private static readonly DateTimeOffset SignedUpOn = new(2026, 10, 5, 10, 0, 0, TimeSpan.Zero);

    private async Task<SignupResponse> SignUpAsync(AdminDbContext db, CustomerKind kind)
    {
        db.TenantDatabases.Add(new TenantDatabase
        {
            DatabaseName = $"TEST{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}",
            PlanType = PlanTier.Trial,
            MaxCustomers = 100,
            CurrentCustomers = 0,
        });
        await db.SaveChangesAsync();

        string suffix = Guid.NewGuid().ToString("N")[..8];
        var signup = new SignupRepository(
            db, new NoQueue(), new NoCurrencies(), new NoSeeding(),
            new TenantDatabaseAllocatorRepository(db, NullLogger<TenantDatabaseAllocatorRepository>.Instance),
            new FixedClock(SignedUpOn));

        return await signup.SignupAsync(new SignupRequest
        {
            CompanyName = $"Kind Test {suffix}",
            Email = $"kind-{suffix}@example.com",
            Password = "a-long-enough-password",
            DisplayName = "Kind Test",
            OrganizationName = $"Head Office {suffix}",
            Kind = kind,
        }, default);
    }

    [SkippableFact]
    public async Task A_demo_customer_is_on_elite_with_no_expiry_and_no_limits()
    {
        Skip.If(_admin.SkipReason is not null, _admin.SkipReason ?? string.Empty);

        await using AdminDbContext db = _admin.CreateContext();
        SignupResponse response = await SignUpAsync(db, CustomerKind.Demo);

        Master.Entity.TableEntities.Customer customer = await db.Customers.AsNoTracking().SingleAsync(c => c.CustomerId == response.CustomerId);
        License licence = await db.Licenses.AsNoTracking().SingleAsync(l => l.CustomerId == response.CustomerId);

        Assert.Equal(CustomerKind.Demo, customer.Kind);
        Assert.Equal(PlanTier.Elite, customer.PlanTier);
        Assert.Equal(LicenseType.Elite, licence.LicenseType);
        Assert.Equal(CustomerKindRepository.NoExpiry, licence.ExpiryDate);
        Assert.Equal(CustomerKindRepository.UnbilledMaxUsers, licence.MaxUsers);
    }

    [SkippableFact]
    public async Task An_internal_customer_is_never_expired_even_on_a_lapsed_licence()
    {
        Skip.If(_admin.SkipReason is not null, _admin.SkipReason ?? string.Empty);

        await using AdminDbContext db = _admin.CreateContext();
        SignupResponse response = await SignUpAsync(db, CustomerKind.Internal);

        // A licence dated in the past, as an older install's bootstrap carried.
        await db.Licenses.Where(l => l.CustomerId == response.CustomerId)
            .ExecuteUpdateAsync(s => s.SetProperty(l => l.ExpiryDate, new DateOnly(2026, 1, 1)));
        Guid orgId = await db.Organizations.Where(o => o.CustomerId == response.CustomerId).Select(o => o.OrgId).SingleAsync();

        OrgContextResponse? ctx = await new OrgContextRepository(db, new FixedClock(SignedUpOn.AddYears(2))).ResolveAsync(orgId, default);

        Assert.NotNull(ctx);
        Assert.Equal("Active", ctx.LicenseStatus);
        Assert.Null(ctx.LicenseExpiry);
        Assert.NotEqual(TenantStatus.Expired, await db.Customers.Where(c => c.CustomerId == response.CustomerId).Select(c => c.Status).SingleAsync());
    }

    [SkippableFact]
    public async Task A_standard_customer_still_expires()
    {
        Skip.If(_admin.SkipReason is not null, _admin.SkipReason ?? string.Empty);

        await using AdminDbContext db = _admin.CreateContext();
        SignupResponse response = await SignUpAsync(db, CustomerKind.Standard);
        Guid orgId = await db.Organizations.Where(o => o.CustomerId == response.CustomerId).Select(o => o.OrgId).SingleAsync();

        OrgContextResponse? ctx = await new OrgContextRepository(db, new FixedClock(SignedUpOn.AddDays(30))).ResolveAsync(orgId, default);

        Assert.Equal("Expired", ctx!.LicenseStatus);
    }

    [SkippableFact]
    public async Task The_admin_list_shows_and_filters_by_kind()
    {
        Skip.If(_admin.SkipReason is not null, _admin.SkipReason ?? string.Empty);

        await using AdminDbContext db = _admin.CreateContext();
        SignupResponse demo = await SignUpAsync(db, CustomerKind.Demo);
        SignupResponse standard = await SignUpAsync(db, CustomerKind.Standard);

        var signup = new SignupRepository(
            db, new NoQueue(), new NoCurrencies(), new NoSeeding(),
            new TenantDatabaseAllocatorRepository(db, NullLogger<TenantDatabaseAllocatorRepository>.Instance),
            new FixedClock(SignedUpOn));

        IReadOnlyList<CustomerListItem> demos = await signup.ListAsync(default, CustomerKind.Demo);

        Assert.Contains(demos, c => c.CustomerId == demo.CustomerId && c.Kind == "Demo");
        Assert.DoesNotContain(demos, c => c.CustomerId == standard.CustomerId);
        Assert.All(demos, c => Assert.Equal("Demo", c.Kind));
    }
}

/// <summary>A branch's UPI id must be one before it is saved (TK-158).</summary>
public sealed class UpiIdSettingTests
{
    [Theory]
    [InlineData("payment.upiId", "shop@okhdfcbank", false)]
    [InlineData("payment.upiId", "", false)]
    [InlineData("payment.upiId", "not a upi id", true)]
    [InlineData("format.date", "not a upi id", false)]
    public void Only_a_real_upi_id_is_saved(string code, string value, bool refused) =>
        Assert.Equal(refused, Master.Repository.Repositories.ConfigurationRepository.ValueRefusal(code, value) is not null);
}
