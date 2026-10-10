using Master.Entity.Enums;
using Master.Entity.TableEntities;
using Master.Repository;
using Master.Repository.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Shared.Kernel.Apps;
using Shared.Kernel.Tenancy;
using Xunit;

namespace Master.Api.Tests;

/// <summary>
/// The sample data (TK-185): one sample customer with one branch for every
/// business type, each with every add-on its type allows and seeded with
/// master data only, and one sample user who is Owner in all of them. The
/// user's details come from configuration, a second run adds nothing, and an
/// existing account keeps its own password.
/// </summary>
[Collection(nameof(AdminCollection))]
public sealed class SampleDataTests
{
    private readonly AdminFixture _admin;

    public SampleDataTests(AdminFixture admin) => _admin = admin;

    // ---- The rules, pure --------------------------------------------------

    [Fact]
    public void There_is_one_sample_branch_per_business_type_with_its_own_code_and_id()
    {
        Assert.Equal(Enum.GetValues<BusinessType>().Order(), SampleDataRepository.BusinessTypes.Order());

        List<string> codes = [.. SampleDataRepository.BusinessTypes.Select(SampleDataRepository.OrgCode)];
        List<Guid> ids = [.. SampleDataRepository.BusinessTypes.Select(SampleDataRepository.OrgId)];

        Assert.Equal(codes.Count, codes.Distinct().Count());
        Assert.Equal(ids.Count, ids.Distinct().Count());
        Assert.All(codes, c => Assert.InRange(c.Length, 1, 10));

        // Nothing collides with the Internal customer's or the demo customers' ids.
        List<Guid> others =
        [
            InternalCustomerRepository.CustomerId,
            InternalCustomerRepository.OrgId,
            .. DemoCustomerRepository.BusinessTypes.SelectMany(t => new[] { DemoCustomerRepository.CustomerId(t), DemoCustomerRepository.OrgId(t) }),
        ];
        Assert.DoesNotContain(SampleDataRepository.CustomerId, others);
        Assert.All(ids, id => Assert.DoesNotContain(id, others));

        // Not a number, so the next signup is never numbered after it.
        Assert.False(long.TryParse(SampleDataRepository.CustomerCode, out _));
        Assert.InRange(SampleDataRepository.CustomerCode.Length, 1, 10);
    }

    [Fact]
    public void Each_sample_branch_has_every_add_on_its_type_allows()
    {
        foreach (BusinessType type in SampleDataRepository.BusinessTypes)
        {
            Assert.Equal(AddOnRules.Allowed(type), SampleDataRepository.AddOnsFor(type));
            Assert.True(AddOnRules.IsAllowed(type, SampleDataRepository.AddOnsFor(type)));
        }
    }

    [Theory]
    [InlineData("sample@example.com", "Asha", "secret", "sample@example.com", "Asha", "secret")]
    [InlineData(" sample@example.com ", null, null, "sample@example.com", "Sample User", null)]
    [InlineData("sample@example.com", "  ", "", "sample@example.com", "Sample User", null)]
    public void The_sample_user_comes_from_configuration(
        string email, string? name, string? password, string expectedEmail, string expectedName, string? expectedPassword)
    {
        SampleUser? user = SampleDataRepository.UserFrom(email, name, password);

        Assert.NotNull(user);
        Assert.Equal(expectedEmail, user.Email);
        Assert.Equal(expectedName, user.DisplayName);
        Assert.Equal(expectedPassword, user.Password);
    }

    [Fact]
    public void The_sample_user_falls_back_to_the_bootstrap_owner_settings()
    {
        IConfiguration ownerOnly = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Bootstrap:OwnerEmail"] = "owner@example.com",
            ["Bootstrap:OwnerName"] = "Owner",
            ["Bootstrap:OwnerPassword"] = "owner-secret",
        }).Build();

        SampleUser? fromOwner = SampleDataRepository.UserFrom(ownerOnly);
        Assert.Equal(new SampleUser("owner@example.com", "Owner", "owner-secret"), fromOwner);

        IConfiguration both = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Bootstrap:OwnerEmail"] = "owner@example.com",
            ["Bootstrap:OwnerPassword"] = "owner-secret",
            ["Bootstrap:SampleUserEmail"] = "sample@example.com",
            ["Bootstrap:SampleUserName"] = "Sample",
        }).Build();

        // A sample setting wins; each one falls back on its own.
        Assert.Equal(new SampleUser("sample@example.com", "Sample", "owner-secret"), SampleDataRepository.UserFrom(both));

        Assert.Null(SampleDataRepository.UserFrom(new ConfigurationBuilder().Build()));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-an-address")]
    [InlineData("@example.com")]
    [InlineData("sample@")]
    [InlineData("sam ple@example.com")]
    public void Without_an_address_there_is_no_sample_user(string? email) =>
        Assert.Null(SampleDataRepository.UserFrom(email, "Name", "secret"));

    // ---- Through the database --------------------------------------------

    /// <summary>Records what it was asked to seed; seeding itself is each service's own test.</summary>
    private sealed class RecordingSeeder : ITenantSeeder
    {
        private readonly IReadOnlyList<string> _unseeded;

        public RecordingSeeder(params string[] unseeded) => _unseeded = unseeded;

        public List<(Guid OrgId, BusinessType? Type, AddOn AddOns)> Calls { get; } = [];

        public Task<IReadOnlyList<string>> SeedAsync(Guid customerId, Guid orgId, CancellationToken ct) =>
            throw new NotSupportedException("A sample branch is seeded with its scope named.");

        public Task<IReadOnlyList<string>> SeedAsync(Guid customerId, Guid orgId, BusinessType? businessType, AddOn addOns, CancellationToken ct)
        {
            Assert.Equal(SampleDataRepository.CustomerId, customerId);
            Calls.Add((orgId, businessType, addOns));
            return Task.FromResult(_unseeded);
        }
    }

    private static readonly BcryptPasswordHasher Hasher = new();

    private static SampleDataRepository Create(AdminDbContext db, ITenantSeeder seeder) =>
        new(db, seeder, Hasher, TimeProvider.System);

    private static async Task EnsureShardAsync(AdminDbContext db)
    {
        db.TenantDatabases.Add(new TenantDatabase
        {
            DatabaseName = $"TEST{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}",
            PlanType = PlanTier.Trial,
            MaxCustomers = 100,
            CurrentCustomers = 0,
        });
        await db.SaveChangesAsync();
    }

    private static string UniqueEmail() => $"sample-{Guid.NewGuid():N}@example.com";

    [SkippableFact]
    public async Task Running_twice_leaves_one_customer_with_a_seeded_branch_per_business_type_and_creates_nothing_the_second_time()
    {
        Skip.If(_admin.SkipReason is not null, _admin.SkipReason ?? string.Empty);

        await using AdminDbContext db = _admin.CreateContext();
        await EnsureShardAsync(db);

        var sample = new SampleUser(UniqueEmail(), "Sample User", "sample-password");
        var seeder = new RecordingSeeder();
        SampleDataResult first = await Create(db, seeder).EnsureAsync(sample, default);
        SampleDataResult second = await Create(db, new RecordingSeeder()).EnsureAsync(sample, default);

        Assert.True(first.UserCreated);
        Assert.False(second.CustomerCreated);
        Assert.False(second.UserCreated);
        Assert.Equal(SampleDataRepository.BusinessTypes.Count, second.Branches.Count);
        Assert.All(second.Branches, b => Assert.Empty(b.Unseeded));

        db.ChangeTracker.Clear();
        Master.Entity.TableEntities.Customer customer = await db.Customers.SingleAsync(c => c.CustomerId == SampleDataRepository.CustomerId);
        Assert.Equal(SampleDataRepository.CustomerCode, customer.CustomerCode);
        Assert.Equal(CustomerKind.Demo, customer.Kind);
        Assert.Equal(TenantStatus.Active, customer.Status);
        Assert.Equal(PlanTier.Elite, customer.PlanTier);

        // The first shard, never a pool: the services' seed endpoints write to
        // the configured tenant database whatever shard the customer is in.
        Assert.Equal(
            await db.TenantDatabases.OrderBy(d => d.DatabaseName).Select(d => d.DatabaseName).FirstAsync(),
            customer.DatabaseName);
        Assert.Equal(1, await db.Licenses.CountAsync(l => l.CustomerId == SampleDataRepository.CustomerId));
        Assert.Equal(
            SampleDataRepository.BusinessTypes.Count,
            await db.Organizations.CountAsync(o => o.CustomerId == SampleDataRepository.CustomerId));

        User user = await db.Users.SingleAsync(u => u.Email == sample.Email);
        Assert.Equal("Sample User", user.DisplayName);
        Assert.True(Hasher.Verify("sample-password", user.PasswordHash!));
        Assert.False(user.IsPlatformOperator);

        foreach (BusinessType type in SampleDataRepository.BusinessTypes)
        {
            Guid orgId = SampleDataRepository.OrgId(type);
            Organization branch = await db.Organizations.SingleAsync(o => o.OrgId == orgId);

            Assert.Equal(SampleDataRepository.CustomerId, branch.CustomerId);
            Assert.Equal(SampleDataRepository.OrgCode(type), branch.OrgCode);
            Assert.Equal(type, branch.BusinessType);
            Assert.Equal(AddOnRules.Allowed(type), branch.AddOns);
            Assert.Equal(TenantStatus.Active, branch.Status);
            Assert.Equal(1, await db.OrgCurrencies.CountAsync(c => c.OrgId == orgId && c.IsBaseCurrency));

            // Seeded with its own scope.
            Assert.Contains(seeder.Calls, c => c.OrgId == orgId && c.Type == type && c.AddOns == AddOnRules.Allowed(type));

            // The sample user is Owner there, once.
            Assert.Equal(1, await db.UserOrganizationRoles.CountAsync(
                a => a.UserId == user.UserId && a.OrgId == orgId && a.RoleId == AdminDbContext.OwnerRoleId));
        }
    }

    [SkippableFact]
    public async Task The_rows_alone_make_the_customer_branches_and_owner_without_calling_any_service()
    {
        Skip.If(_admin.SkipReason is not null, _admin.SkipReason ?? string.Empty);

        await using AdminDbContext db = _admin.CreateContext();
        await EnsureShardAsync(db);

        // What Master's startup calls before it listens (TK-186): the seed
        // fan-out calls every other service, which are still starting then.
        var sample = new SampleUser(UniqueEmail(), "Sample User", null);
        var seeder = new RecordingSeeder();
        (bool _, bool userCreated) = await Create(db, seeder).EnsureRowsAsync(sample, default);

        Assert.True(userCreated);
        Assert.Empty(seeder.Calls);

        db.ChangeTracker.Clear();
        Assert.True(await db.Customers.AnyAsync(c => c.CustomerId == SampleDataRepository.CustomerId));
        User user = await db.Users.SingleAsync(u => u.Email == sample.Email);
        foreach (BusinessType type in SampleDataRepository.BusinessTypes)
        {
            Guid orgId = SampleDataRepository.OrgId(type);
            Assert.True(await db.Organizations.AnyAsync(o => o.OrgId == orgId));
            Assert.Equal(1, await db.UserOrganizationRoles.CountAsync(
                a => a.UserId == user.UserId && a.OrgId == orgId && a.RoleId == AdminDbContext.OwnerRoleId));
        }

        // A second call adds nothing.
        (bool customerCreated, bool userCreatedAgain) = await Create(db, seeder).EnsureRowsAsync(sample, default);
        Assert.False(customerCreated);
        Assert.False(userCreatedAgain);
        Assert.Empty(seeder.Calls);
    }

    [SkippableFact]
    public async Task Without_a_password_the_sample_user_has_none()
    {
        Skip.If(_admin.SkipReason is not null, _admin.SkipReason ?? string.Empty);

        await using AdminDbContext db = _admin.CreateContext();
        await EnsureShardAsync(db);

        var sample = new SampleUser(UniqueEmail(), "Sample User", null);
        await Create(db, new RecordingSeeder()).EnsureAsync(sample, default);

        db.ChangeTracker.Clear();
        User user = await db.Users.SingleAsync(u => u.Email == sample.Email);
        Assert.Null(user.PasswordHash);
    }

    [SkippableFact]
    public async Task An_existing_account_keeps_its_password_and_name_and_gains_the_sample_branches()
    {
        Skip.If(_admin.SkipReason is not null, _admin.SkipReason ?? string.Empty);

        await using AdminDbContext db = _admin.CreateContext();
        await EnsureShardAsync(db);

        string email = UniqueEmail();
        string existingHash = Hasher.Hash("their-own-password");
        db.Users.Add(new User
        {
            UserId = Guid.NewGuid(),
            Email = email,
            DisplayName = "Existing Person",
            PasswordHash = existingHash,
            IsActive = true,
        });
        await db.SaveChangesAsync();

        SampleDataResult result = await Create(db, new RecordingSeeder())
            .EnsureAsync(new SampleUser(email, "Sample User", "a-different-password"), default);

        Assert.False(result.UserCreated);

        db.ChangeTracker.Clear();
        User user = await db.Users.SingleAsync(u => u.Email == email);
        Assert.Equal(existingHash, user.PasswordHash);
        Assert.Equal("Existing Person", user.DisplayName);
        Assert.Equal(
            SampleDataRepository.BusinessTypes.Count,
            await db.UserOrganizationRoles.CountAsync(a => a.UserId == user.UserId && a.RoleId == AdminDbContext.OwnerRoleId));
    }

    [SkippableFact]
    public async Task A_branch_a_service_could_not_seed_is_reported_and_not_made_active()
    {
        Skip.If(_admin.SkipReason is not null, _admin.SkipReason ?? string.Empty);

        await using AdminDbContext db = _admin.CreateContext();
        await EnsureShardAsync(db);

        // The branches may already exist and be active from another test in
        // this collection; what matters is that a failed run changes nothing.
        Dictionary<Guid, TenantStatus> before = await db.Organizations
            .Where(o => o.CustomerId == SampleDataRepository.CustomerId)
            .ToDictionaryAsync(o => o.OrgId, o => o.Status);

        SampleDataResult result = await Create(db, new RecordingSeeder("Accounting"))
            .EnsureAsync(new SampleUser(UniqueEmail(), "Sample User", null), default);

        Assert.All(result.Branches, b => Assert.Equal(["Accounting"], b.Unseeded));

        db.ChangeTracker.Clear();
        foreach (BusinessType type in SampleDataRepository.BusinessTypes)
        {
            Guid orgId = SampleDataRepository.OrgId(type);
            TenantStatus now = await db.Organizations.Where(o => o.OrgId == orgId).Select(o => o.Status).SingleAsync();
            Assert.Equal(before.GetValueOrDefault(orgId, TenantStatus.Provisioning), now);
        }
    }
}
