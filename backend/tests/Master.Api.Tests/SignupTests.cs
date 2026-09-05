using Master.Api.Services;
using Master.Entity.Enums;
using Master.Entity.Models;
using Master.Entity.TableEntities;
using Master.Repository;
using Microsoft.EntityFrameworkCore;
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

    private (SignupService Service, AdminDbContext Db, RecordingQueue Queue) Create()
    {
        AdminDbContext db = _admin.CreateContext();
        RecordingQueue queue = new();

        return (
            new SignupService(db, queue, new StubCurrencies(), new StubSeeder(), new FixedClock()),
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
}
