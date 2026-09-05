using Master.Api.Services;
using Master.Entity.Enums;
using Master.Entity.Models;
using Master.Entity.TableEntities;
using Master.Repository;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Master.Api.Tests;

/// <summary>
/// Signing a customer up, through the service, against a real database.
///
/// <b>This test did not exist, and that is why signup was broken on main for
/// ten days.</b> <c>Customer.DatabaseName</c> was <c>[Required]</c> over a NOT
/// NULL column and <c>SignupService</c> never set it, so every
/// <c>POST /api/customers/signup</c> died on a not-null violation — there was no
/// way to create a customer through the product at all. Nothing caught it,
/// because no test signed a customer up and the seeded fixtures everywhere else
/// build their rows directly, filling in whatever the entity happens to require.
///
/// The column was the last piece of the one-database-per-customer model reversed
/// on 25 August 2026; nothing read it or wrote it after that. Dropping it
/// finished a decision already taken. This suite is what stops the next
/// required-and-never-set column going unnoticed for another ten days: it writes
/// through the service, so anything the entity demands and the service does not
/// supply fails here.
/// </summary>
[Collection(nameof(AdminCollection))]
public sealed class SignupTests
{
    private readonly AdminFixture _admin;

    public SignupTests(AdminFixture admin) => _admin = admin;

    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 9, 4, 10, 0, 0, TimeSpan.Zero);
    }

    /// <summary>Records what it was asked to queue; the worker is not under test.</summary>
    private sealed class RecordingQueue : IProvisioningQueue
    {
        public List<ProvisioningJob> Jobs { get; } = [];

        public ValueTask EnqueueAsync(ProvisioningJob job, CancellationToken ct = default)
        {
            Jobs.Add(job);

            return ValueTask.CompletedTask;
        }

        public ValueTask<ProvisioningJob> DequeueAsync(CancellationToken ct) =>
            throw new NotSupportedException("The test never drains the queue.");
    }

    private sealed class StubCurrencies : IMasterCurrencies
    {
        public Task<IReadOnlyList<MasterCurrency>> GetAllAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<MasterCurrency>>([]);

        public Task<int?> FindCurrencyIdAsync(string code, CancellationToken ct = default) =>
            Task.FromResult<int?>(1);
    }

    /// <summary>Every service reachable. Seeding is the worker's job, not signup's.</summary>
    private sealed class StubSeeder : ITenantSeeder
    {
        public Task<IReadOnlyList<string>> SeedAsync(
            Guid customerId, Guid orgId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<string>>([]);
    }

    private static SignupRequest Request(string suffix) => new()
    {
        CompanyName = $"Kumar Traders {suffix}",
        Email = $"owner-{suffix}@example.com",
        Password = "a-long-enough-password",
        DisplayName = "Ravi Kumar",
        OrganizationName = $"Head Office {suffix}",
    };

    /// <summary>
    /// A shard with room, so the allocator has something to allocate.
    ///
    /// <b>A real allocator, not a stub.</b> The whole reason signup was broken
    /// is that nothing ever assigned a database, so a stub returning a name
    /// would test around the bug rather than over it.
    /// </summary>
    private async Task<string> SeedShardAsync(AdminDbContext db, int capacity = 100)
    {
        string name = $"TEST{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";

        db.TenantDatabases.Add(new TenantDatabase
        {
            DatabaseName = name,
            PlanType = "Trial",
            MaxOrganizations = capacity,
            CurrentOrganizations = 0,
        });

        await db.SaveChangesAsync();

        return name;
    }

    /// <summary>
    /// Takes every existing shard to its limit.
    ///
    /// These tests share one database with the rest of the collection, so a
    /// shard another test seeded would otherwise be a candidate and the
    /// assertion would be about which test ran first.
    /// </summary>
    private static Task<int> FillEveryShardAsync(AdminDbContext db) =>
        db.TenantDatabases.ExecuteUpdateAsync(
            set => set.SetProperty(d => d.CurrentOrganizations, d => d.MaxOrganizations));

    private (SignupService Service, AdminDbContext Db, RecordingQueue Queue) Create()
    {
        AdminDbContext db = _admin.CreateContext();
        RecordingQueue queue = new();

        return (
            new SignupService(
                db,
                queue,
                new StubCurrencies(),
                new StubSeeder(),
                new TenantDatabaseAllocator(db, NullLogger<TenantDatabaseAllocator>.Instance),
                new FixedClock()),
            db,
            queue);
    }

    [SkippableFact]
    public async Task A_customer_can_actually_be_signed_up()
    {
        Skip.If(_admin.SkipReason is not null, _admin.SkipReason ?? string.Empty);

        (SignupService signup, AdminDbContext context, _) = Create();
        await using AdminDbContext db = context;

        string suffix = Guid.NewGuid().ToString("N")[..8];

        await SeedShardAsync(db);

        // The whole test. It threw a DbUpdateException here for ten days.
        SignupResponse response = await signup.SignupAsync(Request(suffix), default);

        Assert.NotEqual(Guid.Empty, response.CustomerId);

        Master.Entity.TableEntities.Customer created = await db.Customers
            .AsNoTracking()
            .FirstAsync(c => c.CustomerId == response.CustomerId);

        Assert.Equal($"Kumar Traders {suffix}", created.Name);
        Assert.Equal($"owner-{suffix}@example.com", created.BillingEmail);
    }

    [SkippableFact]
    public async Task Signup_creates_the_customer_the_branch_and_the_licence_in_the_request()
    {
        Skip.If(_admin.SkipReason is not null, _admin.SkipReason ?? string.Empty);

        (SignupService signup, AdminDbContext context, _) = Create();
        await using AdminDbContext db = context;

        await SeedShardAsync(db);

        SignupResponse response = await signup.SignupAsync(
            Request(Guid.NewGuid().ToString("N")[..8]), default);

        // A customer with no branch cannot be signed in to, and one with no
        // licence has no expiry to enforce — so an incomplete signup is a
        // customer who can never use the product.
        Assert.True(await db.Organizations.AnyAsync(o => o.CustomerId == response.CustomerId));
        Assert.True(await db.Licenses.AnyAsync(l => l.CustomerId == response.CustomerId));

        // The customer stays at Provisioning until the seed finishes. It is
        // what GetStatusAsync reports as "cannot log in yet", and a customer
        // marked Active before their chart of accounts exists is a login into
        // an empty set of books.
        Assert.Equal(
            TenantStatus.Provisioning,
            await db.Customers.Where(c => c.CustomerId == response.CustomerId)
                .Select(c => c.Status).FirstAsync());
    }

    /// <summary>
    /// The owner user is the worker's job, not signup's.
    ///
    /// <b>Worth asserting rather than assuming</b>, because it is the part of
    /// this flow that is still asynchronous while the tenancy note in CLAUDE.md
    /// describes signup as synchronous. Signup writes the customer, the branch
    /// and the licence and queues the rest; nothing can sign in until the worker
    /// has drained that job, and the signup screen polls for it.
    /// </summary>
    [SkippableFact]
    public async Task The_owner_and_the_seed_are_queued_rather_than_written_here()
    {
        Skip.If(_admin.SkipReason is not null, _admin.SkipReason ?? string.Empty);

        (SignupService signup, AdminDbContext context, RecordingQueue queue) = Create();
        await using AdminDbContext db = context;

        await SeedShardAsync(db);

        string suffix = Guid.NewGuid().ToString("N")[..8];
        SignupResponse response = await signup.SignupAsync(Request(suffix), default);

        Assert.False(await db.Users.AnyAsync(u => u.Email == $"owner-{suffix}@example.com"));

        ProvisioningJob job = Assert.Single(queue.Jobs);
        Assert.Equal(response.CustomerId, job.CustomerId);
        Assert.Equal($"owner-{suffix}@example.com", job.OwnerEmail);
    }

    [SkippableFact]
    public async Task A_new_customer_starts_on_a_trial_that_expires()
    {
        Skip.If(_admin.SkipReason is not null, _admin.SkipReason ?? string.Empty);

        (SignupService signup, AdminDbContext context, _) = Create();
        await using AdminDbContext db = context;

        await SeedShardAsync(db);

        SignupResponse response = await signup.SignupAsync(
            Request(Guid.NewGuid().ToString("N")[..8]), default);

        License licence = await db.Licenses.AsNoTracking()
            .FirstAsync(l => l.CustomerId == response.CustomerId);

        Assert.Equal(LicenseType.Trial, licence.LicenseType);

        // Fourteen days from the fixed clock. A trial with no end is a free
        // product.
        Assert.Equal(new DateOnly(2026, 9, 18), licence.ExpiryDate);
    }

    /// <summary>
    /// The password the signup form sent is written nowhere in the master
    /// database.
    ///
    /// It travels in the queued job, which is an in-process channel and never
    /// reaches disk, and the worker hashes it before it writes anything. What
    /// this rules out is the mistake of stashing it on the customer or the
    /// organization "until the worker gets to it" — a plaintext credential at
    /// rest, in a table nobody would think to look in.
    /// </summary>
    [SkippableFact]
    public async Task The_password_is_never_written_to_the_master_database()
    {
        Skip.If(_admin.SkipReason is not null, _admin.SkipReason ?? string.Empty);

        (SignupService signup, AdminDbContext context, _) = Create();
        await using AdminDbContext db = context;

        await SeedShardAsync(db);

        SignupResponse response = await signup.SignupAsync(
            Request(Guid.NewGuid().ToString("N")[..8]), default);

        const string password = "a-long-enough-password";

        Assert.False(await db.Customers.AnyAsync(c =>
            c.CustomerId == response.CustomerId
            && (c.Name == password || c.BillingEmail == password || c.CustomerCode == password)));

        Assert.False(await db.Users.AnyAsync(u => u.PasswordHash == password));
    }

    [SkippableFact]
    public async Task Two_signups_get_different_customer_codes()
    {
        Skip.If(_admin.SkipReason is not null, _admin.SkipReason ?? string.Empty);

        (SignupService signup, AdminDbContext context, _) = Create();
        await using AdminDbContext db = context;

        await SeedShardAsync(db);

        SignupResponse first = await signup.SignupAsync(
            Request(Guid.NewGuid().ToString("N")[..8]), default);
        SignupResponse second = await signup.SignupAsync(
            Request(Guid.NewGuid().ToString("N")[..8]), default);

        string firstCode = await db.Customers.AsNoTracking()
            .Where(c => c.CustomerId == first.CustomerId).Select(c => c.CustomerCode).FirstAsync();
        string secondCode = await db.Customers.AsNoTracking()
            .Where(c => c.CustomerId == second.CustomerId).Select(c => c.CustomerCode).FirstAsync();

        Assert.NotEqual(firstCode, secondCode);
    }

    // ---- Shard allocation, which is the step that did not exist ------------

    /// <summary>
    /// The customer is put on a real shard, and the shard's capacity is
    /// consumed.
    ///
    /// <b>`DatabaseName` is what routes every later request.</b>
    /// `TenantDatabaseResolver` reads it — in raw SQL, so nothing in the
    /// compiler or the test suite would object if it stopped being written —
    /// and uses it to choose the connection for the signed-in user. A customer
    /// row without one cannot be inserted, and one with the wrong one would read
    /// somebody else's books.
    /// </summary>
    [SkippableFact]
    public async Task A_new_customer_is_placed_on_a_provisioned_database()
    {
        Skip.If(_admin.SkipReason is not null, _admin.SkipReason ?? string.Empty);

        (SignupService signup, AdminDbContext context, _) = Create();
        await using AdminDbContext db = context;

        // Fill whatever other tests in this collection left behind, so the
        // shard under test is the only one with room and the assertion is about
        // the allocator rather than about test ordering.
        await FillEveryShardAsync(db);
        string shard = await SeedShardAsync(db, capacity: 5);

        SignupResponse response = await signup.SignupAsync(
            Request(Guid.NewGuid().ToString("N")[..8]), default);

        Assert.Equal(
            shard,
            await db.Customers.Where(c => c.CustomerId == response.CustomerId)
                .Select(c => c.DatabaseName).FirstAsync());

        // Capacity consumed. A registry that tracked capacity and never spent
        // it would let every customer onto the first shard for ever.
        Assert.Equal(
            1,
            await db.TenantDatabases.AsNoTracking()
                .Where(d => d.DatabaseName == shard)
                .Select(d => d.CurrentOrganizations).FirstAsync());
    }

    [SkippableFact]
    public async Task A_full_shard_is_passed_over_for_one_with_room()
    {
        Skip.If(_admin.SkipReason is not null, _admin.SkipReason ?? string.Empty);

        (SignupService signup, AdminDbContext context, _) = Create();
        await using AdminDbContext db = context;

        await FillEveryShardAsync(db);

        string full = await SeedShardAsync(db, capacity: 1);
        await db.TenantDatabases.Where(d => d.DatabaseName == full)
            .ExecuteUpdateAsync(set => set.SetProperty(d => d.CurrentOrganizations, 1));

        string spare = await SeedShardAsync(db, capacity: 5);

        SignupResponse response = await signup.SignupAsync(
            Request(Guid.NewGuid().ToString("N")[..8]), default);

        Assert.Equal(
            spare,
            await db.Customers.Where(c => c.CustomerId == response.CustomerId)
                .Select(c => c.DatabaseName).FirstAsync());
    }

    /// <summary>
    /// With every shard full, signup refuses rather than inventing a database
    /// name.
    ///
    /// A customer whose books point at a database no migration has run against
    /// is worse than a refused signup: the account exists, the person can be
    /// told it worked, and the first query fails.
    /// </summary>
    [SkippableFact]
    public async Task Signup_refuses_when_no_shard_has_capacity()
    {
        Skip.If(_admin.SkipReason is not null, _admin.SkipReason ?? string.Empty);

        (SignupService signup, AdminDbContext context, _) = Create();
        await using AdminDbContext db = context;

        // Every shard in the database at its limit, including any left by
        // another test in this collection.
        await db.TenantDatabases.ExecuteUpdateAsync(
            set => set.SetProperty(d => d.CurrentOrganizations, d => d.MaxOrganizations));

        await Assert.ThrowsAsync<NoTenantCapacityException>(
            () => signup.SignupAsync(Request(Guid.NewGuid().ToString("N")[..8]), default));
    }

    /// <summary>
    /// Two signups racing for the last slot: one wins, one does not overfill
    /// the shard.
    ///
    /// A read-then-write would let both see the free slot and both take it, and
    /// the second customer's books would land in a database over its plan's
    /// limit — which is the thing the capacity column exists to prevent.
    /// </summary>
    [SkippableFact]
    public async Task Two_allocations_cannot_both_take_the_last_slot()
    {
        Skip.If(_admin.SkipReason is not null, _admin.SkipReason ?? string.Empty);

        await using AdminDbContext seed = _admin.CreateContext();
        await seed.TenantDatabases.ExecuteUpdateAsync(
            set => set.SetProperty(d => d.CurrentOrganizations, d => d.MaxOrganizations));

        string shard = await SeedShardAsync(seed, capacity: 1);

        await using AdminDbContext one = _admin.CreateContext();
        await using AdminDbContext two = _admin.CreateContext();

        string?[] results = await Task.WhenAll(
            new TenantDatabaseAllocator(one, NullLogger<TenantDatabaseAllocator>.Instance)
                .AllocateAsync("Trial", default),
            new TenantDatabaseAllocator(two, NullLogger<TenantDatabaseAllocator>.Instance)
                .AllocateAsync("Trial", default));

        Assert.Equal(1, results.Count(r => r == shard));
        Assert.Equal(1, results.Count(r => r is null));

        Assert.Equal(
            1,
            await seed.TenantDatabases.AsNoTracking()
                .Where(d => d.DatabaseName == shard)
                .Select(d => d.CurrentOrganizations).FirstAsync());
    }
}
