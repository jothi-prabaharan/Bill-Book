using Master.Api.Services;
using Master.Entity.Enums;
using Master.Entity.Models;
using Master.Entity.TableEntities;
using Master.Repository;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Shared.Kernel.Apps;
using Shared.Kernel.Interfaces;
using Xunit;

namespace Master.Api.Tests;

/// <summary>
/// Signup and seeding per app (H0.4, TK-45): signing up for one app, then
/// starting another, gives one customer, one set of branches and a licence per app.
/// </summary>
[Collection(nameof(AdminCollection))]
public sealed class PerAppSignupTests
{
    private readonly AdminFixture _admin;

    public PerAppSignupTests(AdminFixture admin) => _admin = admin;

    private sealed class RecordingQueue : IProvisioningQueue
    {
        public List<ProvisioningJob> Jobs { get; } = [];

        public ValueTask EnqueueAsync(ProvisioningJob job, CancellationToken ct = default)
        {
            Jobs.Add(job);
            return ValueTask.CompletedTask;
        }

        public ValueTask<ProvisioningJob> DequeueAsync(CancellationToken ct) =>
            throw new NotSupportedException();
    }

    private sealed class StubCurrencies : IMasterCurrencies
    {
        public Task<IReadOnlyList<MasterCurrency>> GetAllAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<MasterCurrency>>([]);

        public Task<int?> FindCurrencyIdAsync(string code, CancellationToken ct = default) =>
            Task.FromResult<int?>(1);
    }

    /// <summary>Records the apps each branch was seeded for; fails when told to.</summary>
    private sealed class RecordingSeeder(bool fail = false) : ITenantSeeder
    {
        public List<(Guid OrgId, App Apps)> Seeded { get; } = [];

        public Task<IReadOnlyList<string>> SeedAsync(Guid customerId, Guid orgId, CancellationToken ct) =>
            SeedAsync(customerId, orgId, App.RetailErp, ct);

        public Task<IReadOnlyList<string>> SeedAsync(Guid customerId, Guid orgId, App apps, CancellationToken ct)
        {
            Seeded.Add((orgId, apps));
            return Task.FromResult<IReadOnlyList<string>>(fail ? ["Accounting"] : []);
        }
    }

    private sealed class Hasher : IPasswordHasher
    {
        public string Hash(string password) => "hashed";

        public bool Verify(string password, string hash) => true;
    }

    private async Task<(Guid CustomerId, Guid OrgId, Guid UserId)> SignUpForPayrollAsync(AdminDbContext db)
    {
        string suffix = Guid.NewGuid().ToString("N")[..8];
        db.TenantDatabases.Add(new TenantDatabase
        {
            DatabaseName = $"TEST{suffix.ToUpperInvariant()}",
            PlanType = PlanTier.Trial,
            MaxCustomers = 1000,
            CurrentCustomers = 0,
        });
        await db.SaveChangesAsync();

        RecordingQueue queue = new();
        SignupService signup = new(
            db, queue, new StubCurrencies(), new RecordingSeeder(),
            new TenantDatabaseAllocator(db, NullLogger<TenantDatabaseAllocator>.Instance), TimeProvider.System);

        SignupResponse response = await signup.SignupAsync(new SignupRequest
        {
            App = "Payroll",
            CompanyName = $"Two Apps {suffix}",
            Email = $"owner-{suffix}@example.com",
            Password = "a-long-enough-password",
            DisplayName = "Meena",
            OrganizationName = $"Head Office {suffix}",
        }, default);

        // The worker's first step: the owner, with the Owner role of the app signed up for.
        ProvisioningJob job = Assert.Single(queue.Jobs);
        Assert.Equal(App.Payroll, job.App);
        await new InProcessIdentityAdmin(db, new Hasher()).CreateOwnerUserAsync(new CreateOwnerUser(
            job.OrgId, job.OwnerEmail, job.OwnerDisplayName, job.OwnerMobileNumber, job.OwnerPassword, job.App), default);

        Guid userId = await db.Users.Where(u => u.Email == job.OwnerEmail).Select(u => u.UserId).SingleAsync();
        return (response.CustomerId, job.OrgId, userId);
    }

    [SkippableFact]
    public async Task A_payroll_signup_gives_a_payroll_trial_and_the_payroll_owner_role()
    {
        Skip.If(_admin.SkipReason is not null, _admin.SkipReason ?? string.Empty);

        await using AdminDbContext db = _admin.CreateContext();
        (Guid customerId, Guid orgId, Guid userId) = await SignUpForPayrollAsync(db);

        License licence = await db.Licenses.AsNoTracking().SingleAsync(l => l.CustomerId == customerId);
        Assert.Equal(App.Payroll, licence.App);
        Assert.Equal(LicenseType.Trial, licence.LicenseType);

        int role = await db.UserOrganizationRoles.Where(a => a.UserId == userId && a.OrgId == orgId).Select(a => a.RoleId).SingleAsync();
        Assert.Equal(AdminDbContext.OwnerRoleOf(App.Payroll), role);
    }

    /// <summary>The card's Done-when line.</summary>
    [SkippableFact]
    public async Task Payroll_then_hrms_is_one_customer_one_branch_and_two_licences()
    {
        Skip.If(_admin.SkipReason is not null, _admin.SkipReason ?? string.Empty);

        await using AdminDbContext db = _admin.CreateContext();
        (Guid customerId, Guid orgId, Guid userId) = await SignUpForPayrollAsync(db);
        RecordingSeeder seeder = new();

        StartTrialResult started = await new ApplicationService(db, seeder, TimeProvider.System)
            .StartTrialAsync(customerId, userId, App.Hrms, default);

        Assert.Equal(StartTrialOutcome.Ok, started.Outcome);
        Assert.Equal(1, await db.Customers.CountAsync(c => c.CustomerId == customerId));
        Assert.Equal([orgId], await db.Organizations.Where(o => o.CustomerId == customerId).Select(o => o.OrgId).ToListAsync());
        Assert.Equal(
            [App.Hrms, App.Payroll],
            await db.Licenses.Where(l => l.CustomerId == customerId).Select(l => l.App).OrderBy(a => a).ToListAsync());

        // The starter is HRMS's Owner in the branch too, beside Payroll's.
        List<int> roles = await db.UserOrganizationRoles.Where(a => a.UserId == userId && a.OrgId == orgId).Select(a => a.RoleId).ToListAsync();
        Assert.Contains(AdminDbContext.OwnerRoleOf(App.Hrms), roles);
        Assert.Contains(AdminDbContext.OwnerRoleOf(App.Payroll), roles);

        // HRMS's master data went into the existing branch, alongside Payroll's.
        Assert.Equal((orgId, App.Hrms | App.Payroll), Assert.Single(seeder.Seeded));
    }

    [SkippableFact]
    public async Task Starting_an_app_already_held_changes_nothing()
    {
        Skip.If(_admin.SkipReason is not null, _admin.SkipReason ?? string.Empty);

        await using AdminDbContext db = _admin.CreateContext();
        (Guid customerId, _, Guid userId) = await SignUpForPayrollAsync(db);

        StartTrialResult again = await new ApplicationService(db, new RecordingSeeder(), TimeProvider.System)
            .StartTrialAsync(customerId, userId, App.Payroll, default);

        Assert.Equal(StartTrialOutcome.AlreadyLicensed, again.Outcome);
        Assert.Equal(1, await db.Licenses.CountAsync(l => l.CustomerId == customerId));
    }

    [SkippableFact]
    public async Task A_branch_that_cannot_be_seeded_fails_the_start_so_the_request_rolls_back()
    {
        Skip.If(_admin.SkipReason is not null, _admin.SkipReason ?? string.Empty);

        await using AdminDbContext db = _admin.CreateContext();
        (Guid customerId, _, Guid userId) = await SignUpForPayrollAsync(db);

        StartTrialResult started = await new ApplicationService(db, new RecordingSeeder(fail: true), TimeProvider.System)
            .StartTrialAsync(customerId, userId, App.RetailErp, default);

        // The controller answers 503 on this, and the reliability filter
        // commits only on success, so the licence written above is rolled back.
        Assert.Equal(StartTrialOutcome.SeedFailed, started.Outcome);
        Assert.Equal(["Accounting"], started.UnseededServices);
    }
}

/// <summary>Which services each app seeds (TK-45). No database needed.</summary>
public sealed class SeedingPerAppTests
{
    [Fact]
    public void Accounting_and_printing_are_seeded_for_every_app()
    {
        foreach (App app in new[] { App.RetailErp, App.School, App.Hrms, App.Payroll })
        {
            IReadOnlyList<string> services = HttpTenantSeeder.ServicesFor(app);
            Assert.Equal("Accounting", services[0]);
            Assert.Contains("Printing", services);
        }
    }

    [Fact]
    public void The_trading_services_and_contacts_are_retail_only()
    {
        // Employee (TK-48) for any app that employs people, right after Accounting.
        Assert.Equal(["Accounting", "Employee", "Payroll", "Printing"], HttpTenantSeeder.ServicesFor(App.Payroll));
        Assert.Equal(["Accounting", "Printing"], HttpTenantSeeder.ServicesFor(App.None));
        Assert.Equal(
            ["Accounting", "Inventory", "Sales", "Purchase", "Reporting", "Printing", "Customer"],
            HttpTenantSeeder.ServicesFor(App.RetailErp));
        Assert.Equal(
            ["Accounting", "Employee", "Payroll", "Inventory", "Sales", "Purchase", "Reporting", "Printing", "Customer"],
            HttpTenantSeeder.ServicesFor(App.RetailErp | App.Payroll));
        Assert.False(HttpTenantSeeder.SeedsContacts(App.Hrms | App.Payroll));
        Assert.True(HttpTenantSeeder.SeedsContacts(App.School));
    }

    /// <summary>School seeds the employee master and its own services, never the trading ones (TK-61).</summary>
    [Fact]
    public void School_seeds_its_own_services_and_not_the_trading_ones()
    {
        IReadOnlyList<string> services = HttpTenantSeeder.ServicesFor(App.School);

        Assert.Contains("Employee", services);
        Assert.All(HttpTenantSeeder.SchoolServices, s => Assert.Contains(s, services));
        Assert.DoesNotContain("Sales", services);
        Assert.DoesNotContain("Student", HttpTenantSeeder.ServicesFor(App.RetailErp | App.Hrms | App.Payroll));
    }
}
