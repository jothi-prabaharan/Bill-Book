using Master.Api.Services;
using Master.Entity.Enums;
using Master.Entity.TableEntities;
using Master.Repository;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Master.Api.Tests;

/// <summary>Which database a new shard goes in (H0.5, TK-46). No database needed.</summary>
public sealed class ShardNamingTests
{
    [Fact]
    public void A_listed_standby_is_taken_first_and_a_registered_one_never_twice()
    {
        Assert.Equal("IN000003", TenantShardProvisioner.NextDatabaseName(
            ["IN000001", "IN000002"], ["IN000002", "IN000003"], mayCreate: false, "IN"));
    }

    [Fact]
    public void Development_creates_the_next_in_sequence_when_no_standby_is_left()
    {
        Assert.Equal("IN000008", TenantShardProvisioner.NextDatabaseName(
            ["IN000001", "IN000007", "TESTABCD"], [], mayCreate: true, "IN"));
    }

    [Fact]
    public void Outside_development_with_no_standby_there_is_nothing_to_provision()
    {
        Assert.Null(TenantShardProvisioner.NextDatabaseName(["IN000001"], [], mayCreate: false, "IN"));
    }
}

/// <summary>
/// Allocation counts customers and provisions a shard when the pools are full
/// (H0.5, TK-46), against the real registry.
/// </summary>
[Collection(nameof(AdminCollection))]
public sealed class ShardingTests
{
    private readonly AdminFixture _admin;

    public ShardingTests(AdminFixture admin) => _admin = admin;

    /// <summary>Registers a shard the way the real one does, without creating or migrating a database.</summary>
    private sealed class RegisteringProvisioner(AdminFixture admin) : ITenantShardProvisioner
    {
        public List<(PlanTier Plan, int Capacity, string Name)> Provisioned { get; } = [];

        public async Task<string?> ProvisionAsync(PlanTier plan, int capacity, CancellationToken ct)
        {
            string name = $"NEW{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";
            await using AdminDbContext db = admin.CreateContext();
            db.TenantDatabases.Add(new TenantDatabase { DatabaseName = name, PlanType = plan, MaxCustomers = capacity });
            await db.SaveChangesAsync(ct);
            lock (Provisioned)
            {
                Provisioned.Add((plan, capacity, name));
            }
            return name;
        }
    }

    private static Task<int> FillEveryShardAsync(AdminDbContext db) =>
        db.TenantDatabases.ExecuteUpdateAsync(set => set.SetProperty(d => d.CurrentCustomers, d => d.MaxCustomers));

    private static TenantDatabaseAllocator Allocator(AdminDbContext db, ITenantShardProvisioner provisioner) =>
        new(db, NullLogger<TenantDatabaseAllocator>.Instance, provisioner);

    private static async Task<string> PoolAsync(AdminDbContext db, int max, int current)
    {
        string name = $"POOL{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";
        db.TenantDatabases.Add(new TenantDatabase { DatabaseName = name, PlanType = PlanTier.Pro, MaxCustomers = max, CurrentCustomers = current });
        await db.SaveChangesAsync();
        return name;
    }

    private static Task<int> CurrentAsync(AdminDbContext db, string name) =>
        db.TenantDatabases.AsNoTracking().Where(d => d.DatabaseName == name).Select(d => d.CurrentCustomers).SingleAsync();

    /// <summary>The card's first test: the 101st customer lands in a new shard.</summary>
    [SkippableFact]
    public async Task The_101st_customer_lands_in_a_new_shard()
    {
        Skip.If(_admin.SkipReason is not null, _admin.SkipReason ?? string.Empty);

        await using AdminDbContext db = _admin.CreateContext();
        await FillEveryShardAsync(db);
        string full = await PoolAsync(db, max: 100, current: 100);
        RegisteringProvisioner provisioner = new(_admin);

        string? placed = await Allocator(db, provisioner).AllocateAsync(PlanTier.Trial, default);

        (PlanTier plan, int capacity, string name) = Assert.Single(provisioner.Provisioned);
        Assert.Equal(name, placed);
        Assert.Equal(TenantDatabaseAllocator.DefaultCustomersPerPool, capacity);
        Assert.NotEqual(PlanTier.Elite, plan);
        Assert.Equal(1, await CurrentAsync(db, name));
        Assert.Equal(100, await CurrentAsync(db, full));
    }

    [SkippableFact]
    public async Task A_pool_with_room_is_used_before_anything_is_provisioned()
    {
        Skip.If(_admin.SkipReason is not null, _admin.SkipReason ?? string.Empty);

        await using AdminDbContext db = _admin.CreateContext();
        await FillEveryShardAsync(db);
        string pool = await PoolAsync(db, max: 100, current: 42);
        RegisteringProvisioner provisioner = new(_admin);

        Assert.Equal(pool, await Allocator(db, provisioner).AllocateAsync(PlanTier.Trial, default));
        Assert.Empty(provisioner.Provisioned);
        Assert.Equal(43, await CurrentAsync(db, pool));
    }

    [SkippableFact]
    public async Task An_elite_customer_gets_a_shard_of_capacity_one()
    {
        Skip.If(_admin.SkipReason is not null, _admin.SkipReason ?? string.Empty);

        await using AdminDbContext db = _admin.CreateContext();
        await PoolAsync(db, max: 100, current: 0);
        RegisteringProvisioner provisioner = new(_admin);

        string? own = await Allocator(db, provisioner).AllocateAsync(PlanTier.Elite, default);

        (PlanTier plan, int capacity, string name) = Assert.Single(provisioner.Provisioned);
        Assert.Equal(name, own);
        Assert.Equal((PlanTier.Elite, 1), (plan, capacity));
        Assert.Equal(1, await CurrentAsync(db, name));
    }

    /// <summary>The card's second test: two concurrent signups cannot both take the last slot.</summary>
    [SkippableFact]
    public async Task Two_signups_racing_for_the_last_slot_do_not_overfill_it()
    {
        Skip.If(_admin.SkipReason is not null, _admin.SkipReason ?? string.Empty);

        await using AdminDbContext seed = _admin.CreateContext();
        await FillEveryShardAsync(seed);
        string last = await PoolAsync(seed, max: 100, current: 99);
        RegisteringProvisioner provisioner = new(_admin);

        await using AdminDbContext one = _admin.CreateContext();
        await using AdminDbContext two = _admin.CreateContext();

        string?[] placed = await Task.WhenAll(
            Allocator(one, provisioner).AllocateAsync(PlanTier.Trial, default),
            Allocator(two, provisioner).AllocateAsync(PlanTier.Trial, default));

        Assert.Equal(1, placed.Count(p => p == last));
        Assert.All(placed, p => Assert.NotNull(p));
        Assert.Equal(100, await CurrentAsync(seed, last));
    }

    [SkippableFact]
    public async Task The_start_up_recount_sets_each_shard_to_its_customers()
    {
        Skip.If(_admin.SkipReason is not null, _admin.SkipReason ?? string.Empty);

        await using AdminDbContext db = _admin.CreateContext();
        string pool = await PoolAsync(db, max: 100, current: 57);
        db.Customers.Add(new Master.Entity.TableEntities.Customer
        {
            CustomerId = Guid.NewGuid(),
            CustomerCode = Random.Shared.NextInt64(1_000_000_000, 9_999_999_999).ToString(),
            Name = "Counted",
            BillingEmail = "counted@example.com",
            DatabaseName = pool,
            PlanTier = PlanTier.Trial,
        });
        await db.SaveChangesAsync();

        await DatabaseMigrationService.RecountShardCustomersAsync(db, default);

        Assert.Equal(1, await CurrentAsync(db, pool));
    }
}
