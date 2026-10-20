using Master.Api.Services;
using Master.Repository.Repositories;
using Master.Repository.Clients;
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
/// Signup by business type and add-ons (D-50, TK-136), which replaced signup
/// per app: the first branch takes what was signed up for, and a pair the
/// add-on rules refuse is refused before anything is written.
/// </summary>
public sealed class SignupScopeRuleTests
{
    [Theory]
    [InlineData(BusinessType.Retail, AddOn.None)]
    [InlineData(BusinessType.Service, AddOn.Crm | AddOn.RecurringBilling)]
    [InlineData(BusinessType.Jewellery, AddOn.SavingsScheme)]
    [InlineData(BusinessType.School, AddOn.Hrms)]
    [InlineData(null, AddOn.Payroll)]
    [InlineData(null, AddOn.Hrms | AddOn.Payroll)]
    public void A_pair_the_add_on_rules_allow_is_accepted(BusinessType? businessType, AddOn addOns) =>
        Assert.Null(SignupRepository.ScopeRefusal(businessType, addOns));

    [Theory]
    [InlineData(null, AddOn.None, "Without a business type, choose HRMS or Payroll.")]
    [InlineData(null, AddOn.Crm, "Without a business type, choose HRMS or Payroll.")]
    [InlineData(BusinessType.School, AddOn.Pos, "One of the chosen add-ons is not available for that business type.")]
    [InlineData(BusinessType.Retail, AddOn.SavingsScheme, "One of the chosen add-ons is not available for that business type.")]
    [InlineData(BusinessType.Service, AddOn.Pos, "One of the chosen add-ons is not available for that business type.")]
    public void A_pair_the_add_on_rules_refuse_says_why(BusinessType? businessType, AddOn addOns, string expected) =>
        Assert.Equal(expected, SignupRepository.ScopeRefusal(businessType, addOns));

    [Fact]
    public void An_undefined_business_type_is_refused() =>
        Assert.Equal(
            "Choose a business type: Retail, Pharma, Jewellery, School, Service or Restaurant.",
            SignupRepository.ScopeRefusal((BusinessType)99, AddOn.None));
}

[Collection(nameof(AdminCollection))]
public sealed class SignupScopeTests
{
    private readonly AdminFixture _admin;

    public SignupScopeTests(AdminFixture admin) => _admin = admin;

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

    private sealed class StubCurrencies : IMasterCurrenciesRepository
    {
        public Task<IReadOnlyList<MasterCurrency>> GetAllAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<MasterCurrency>>([]);

        public Task<int?> FindCurrencyIdAsync(string code, CancellationToken ct = default) =>
            Task.FromResult<int?>(1);
    }

    private sealed class NoSeeding : ITenantSeeder
    {
        public Task<IReadOnlyList<string>> SeedAsync(Guid customerId, Guid orgId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<string>>([]);

        public Task<IReadOnlyList<string>> SeedAsync(Guid customerId, Guid orgId, BusinessType? businessType, AddOn addOns, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<string>>([]);
    }

    private sealed class Hasher : IPasswordHasherRepository
    {
        public string Hash(string password) => "hashed";

        public bool Verify(string password, string hash) => true;
    }

    private async Task<(Guid CustomerId, Guid OrgId, Guid UserId)> SignUpAsync(
        AdminDbContext db, BusinessType? businessType, AddOn addOns)
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
        SignupRepository signup = new(
            db, queue, new StubCurrencies(), new NoSeeding(),
            new TenantDatabaseAllocatorRepository(db, NullLogger<TenantDatabaseAllocatorRepository>.Instance), TimeProvider.System,
            new OtpService(), new RecordingMobileMessages(), new RecordingEmailSender());

        SignupResponse response = await signup.SignupAsync(new SignupRequest
        {
            BusinessType = businessType,
            AddOns = addOns,
            CompanyName = $"Scope {suffix}",
            Email = $"owner-{suffix}@example.com",
            Password = "a-long-enough-password",
            DisplayName = "Meena",
            OrganizationName = $"Head Office {suffix}",
        }, default);

        // The worker's first step: the owner, with the one Owner role.
        ProvisioningJob job = Assert.Single(queue.Jobs);
        await new InProcessIdentityAdmin(db, new Hasher()).CreateOwnerUserAsync(new CreateOwnerUser(
            job.OrgId, job.OwnerEmail, job.OwnerDisplayName, job.OwnerMobileNumber, job.OwnerPassword), default);

        Guid userId = await db.Users.Where(u => u.Email == job.OwnerEmail).Select(u => u.UserId).SingleAsync();
        return (response.CustomerId, job.OrgId, userId);
    }

    [SkippableFact]
    public async Task A_payroll_only_signup_gives_a_trial_a_branch_with_payroll_and_the_owner_role()
    {
        Skip.If(_admin.SkipReason is not null, _admin.SkipReason ?? string.Empty);

        await using AdminDbContext db = _admin.CreateContext();
        (Guid customerId, Guid orgId, Guid userId) = await SignUpAsync(db, null, AddOn.Payroll);

        License licence = await db.Licenses.AsNoTracking().SingleAsync(l => l.CustomerId == customerId);
        Assert.Equal(LicenseType.Trial, licence.LicenseType);

        Organization org = await db.Organizations.AsNoTracking().SingleAsync(o => o.OrgId == orgId);
        Assert.Null(org.BusinessType);
        Assert.Equal(AddOn.Payroll, org.AddOns);

        int role = await db.UserOrganizationRoles.Where(a => a.UserId == userId && a.OrgId == orgId).Select(a => a.RoleId).SingleAsync();
        Assert.Equal(AdminDbContext.OwnerRoleId, role);
    }

    [SkippableFact]
    public async Task A_service_signup_gives_a_service_branch_with_its_add_ons()
    {
        Skip.If(_admin.SkipReason is not null, _admin.SkipReason ?? string.Empty);

        await using AdminDbContext db = _admin.CreateContext();
        (_, Guid orgId, _) = await SignUpAsync(db, BusinessType.Service, AddOn.Crm);

        Organization org = await db.Organizations.AsNoTracking().SingleAsync(o => o.OrgId == orgId);
        Assert.Equal(BusinessType.Service, org.BusinessType);
        Assert.Equal(AddOn.Crm, org.AddOns);
    }
}
