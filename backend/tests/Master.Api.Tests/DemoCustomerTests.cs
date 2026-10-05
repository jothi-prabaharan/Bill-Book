using Master.Entity.Enums;
using Master.Entity.Models;
using Master.Entity.TableEntities;
using Master.Repository;
using Master.Repository.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Master.Api.Services;
using Shared.Kernel.Apps;
using Shared.Kernel.Tenancy;
using Xunit;

namespace Master.Api.Tests;

/// <summary>
/// Demo customers (D-44, D-45, TK-154): one per business type, each with one
/// branch of that type and every add-on it allows, seeded with master data
/// only, owners with no password, and a second run that adds nothing.
/// </summary>
[Collection(nameof(AdminCollection))]
public sealed class DemoCustomerTests
{
    private readonly AdminFixture _admin;

    public DemoCustomerTests(AdminFixture admin) => _admin = admin;

    // ---- The rules, pure --------------------------------------------------

    [Fact]
    public void There_is_one_demo_customer_per_business_type_with_its_own_code_and_ids()
    {
        Assert.Equal(Enum.GetValues<BusinessType>().Order(), DemoCustomerRepository.BusinessTypes.Order());

        List<string> codes = [.. DemoCustomerRepository.BusinessTypes.Select(DemoCustomerRepository.CustomerCode)];
        List<Guid> ids = [.. DemoCustomerRepository.BusinessTypes.SelectMany(t => new[] { DemoCustomerRepository.CustomerId(t), DemoCustomerRepository.OrgId(t) })];

        Assert.Equal(codes.Count, codes.Distinct().Count());
        Assert.Equal(ids.Count, ids.Distinct().Count());
        Assert.DoesNotContain(InternalCustomerRepository.CustomerId, ids);

        // Not numbers, so the next signup is never numbered after a demo, and
        // within the ten characters a customer code may have.
        Assert.All(codes, c =>
        {
            Assert.False(long.TryParse(c, out _));
            Assert.InRange(c.Length, 1, 10);
        });
    }

    [Theory]
    [InlineData("owner@example.com", BusinessType.Retail, "owner+demo-retail@example.com")]
    [InlineData(" owner@example.com ", BusinessType.School, "owner+demo-school@example.com")]
    [InlineData("owner+ops@example.com", BusinessType.Pharma, "owner+demo-pharma@example.com")]
    [InlineData("not-an-address", BusinessType.Retail, null)]
    [InlineData("@example.com", BusinessType.Retail, null)]
    [InlineData(null, BusinessType.Retail, null)]
    public void Each_demo_owner_is_a_plus_address_of_one_mailbox(string? mailbox, BusinessType type, string? expected) =>
        Assert.Equal(expected, DemoCustomerRepository.OwnerEmail(mailbox, type));

    [Fact]
    public void Each_demo_branch_has_every_add_on_its_type_allows()
    {
        foreach (BusinessType type in DemoCustomerRepository.BusinessTypes)
        {
            Assert.Equal(AddOnRules.Allowed(type), DemoCustomerRepository.AddOnsFor(type));
            Assert.True(AddOnRules.IsAllowed(type, DemoCustomerRepository.AddOnsFor(type)));
        }
    }

    [Fact]
    public void The_mailbox_falls_back_from_the_request_to_configuration()
    {
        IConfiguration config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Bootstrap:OwnerEmail"] = "owner@example.com",
        }).Build();

        Assert.Equal("asked@example.com", DemoCustomerBootstrapWorker.Mailbox("asked@example.com", config));
        Assert.Equal("owner@example.com", DemoCustomerBootstrapWorker.Mailbox(null, config));
        Assert.Null(DemoCustomerBootstrapWorker.Mailbox(null, new ConfigurationBuilder().Build()));
    }

    // ---- Through the database --------------------------------------------

    /// <summary>Records what it was asked to seed; seeding itself is each service's own test.</summary>
    private sealed class RecordingSeeder : ITenantSeeder
    {
        public List<(Guid OrgId, BusinessType? Type, AddOn AddOns)> Calls { get; } = [];

        public Task<IReadOnlyList<string>> SeedAsync(Guid customerId, Guid orgId, CancellationToken ct) =>
            throw new NotSupportedException("A demo branch is seeded with its scope named.");

        public Task<IReadOnlyList<string>> SeedAsync(Guid customerId, Guid orgId, BusinessType? businessType, AddOn addOns, CancellationToken ct)
        {
            Calls.Add((orgId, businessType, addOns));
            return Task.FromResult<IReadOnlyList<string>>([]);
        }
    }

    private const string Mailbox = "demo-tests@example.com";

    private static DemoCustomerRepository Create(AdminDbContext db, RecordingSeeder seeder) =>
        new(db, seeder,
            new TenantDatabaseAllocatorRepository(db, NullLogger<TenantDatabaseAllocatorRepository>.Instance),
            TimeProvider.System);

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

    [SkippableFact]
    public async Task Running_twice_leaves_six_seeded_demo_customers_and_creates_nothing_the_second_time()
    {
        Skip.If(_admin.SkipReason is not null, _admin.SkipReason ?? string.Empty);

        await using AdminDbContext db = _admin.CreateContext();
        await EnsureShardAsync(db);

        var seeder = new RecordingSeeder();
        await Create(db, seeder).EnsureAllAsync(Mailbox, default);
        IReadOnlyList<DemoCustomerResult> second = await Create(db, new RecordingSeeder()).EnsureAllAsync(Mailbox, default);

        Assert.All(second, r => Assert.False(r.Created));
        Assert.All(second, r => Assert.Empty(r.Unseeded));

        db.ChangeTracker.Clear();
        List<Guid> demoIds = [.. DemoCustomerRepository.BusinessTypes.Select(DemoCustomerRepository.CustomerId)];
        List<Master.Entity.TableEntities.Customer> demos = await db.Customers.Where(c => demoIds.Contains(c.CustomerId)).ToListAsync();

        Assert.Equal(6, demos.Count);
        Assert.All(demos, c =>
        {
            Assert.Equal(CustomerKind.Demo, c.Kind);
            Assert.Equal(TenantStatus.Active, c.Status);
            Assert.Equal(PlanTier.Elite, c.PlanTier);
        });

        foreach (BusinessType type in DemoCustomerRepository.BusinessTypes)
        {
            Guid orgId = DemoCustomerRepository.OrgId(type);
            Organization branch = await db.Organizations.SingleAsync(o => o.OrgId == orgId);

            Assert.Equal(type, branch.BusinessType);
            Assert.Equal(AddOnRules.Allowed(type), branch.AddOns);
            Assert.Equal(1, await db.Organizations.CountAsync(o => o.CustomerId == DemoCustomerRepository.CustomerId(type)));
            Assert.Equal(1, await db.Licenses.CountAsync(l => l.CustomerId == DemoCustomerRepository.CustomerId(type)));

            // Seeded with its own scope.
            Assert.Contains(seeder.Calls, c => c.OrgId == orgId && c.Type == type && c.AddOns == AddOnRules.Allowed(type));

            // An owner with no password, holding the Owner role there, once.
            string email = DemoCustomerRepository.OwnerEmail(Mailbox, type)!;
            User owner = await db.Users.SingleAsync(u => u.Email == email);
            Assert.Null(owner.PasswordHash);
            Assert.Equal(1, await db.UserOrganizationRoles.CountAsync(a => a.UserId == owner.UserId && a.OrgId == orgId && a.RoleId == AdminDbContext.OwnerRoleId));
        }
    }

    [SkippableFact]
    public async Task A_demo_customer_never_lapses()
    {
        Skip.If(_admin.SkipReason is not null, _admin.SkipReason ?? string.Empty);

        await using AdminDbContext db = _admin.CreateContext();
        await EnsureShardAsync(db);
        await Create(db, new RecordingSeeder()).EnsureAllAsync(Mailbox, default);

        OrgContextResponse? ctx = await new OrgContextRepository(db, TimeProvider.System)
            .ResolveAsync(DemoCustomerRepository.OrgId(BusinessType.School), default);

        Assert.Equal("Active", ctx!.LicenseStatus);
        Assert.Null(ctx.LicenseExpiry);
    }
}
