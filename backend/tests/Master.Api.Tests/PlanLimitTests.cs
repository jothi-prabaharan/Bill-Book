using Master.Entity.Enums;
using Master.Entity.Models;
using Master.Entity.TableEntities;
using Master.Repository;
using Master.Repository.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Shared.Kernel.Apps;
using Shared.Kernel.Interfaces;
using Shared.Kernel.Licensing;
using Shared.Kernel.Tenancy;
using Xunit;

namespace Master.Api.Tests;

/// <summary>
/// What a plan allows (D-68, TK-209): Professional holds five users in a branch,
/// revoked ones included, Elite holds any number, and a customer can ask for the
/// higher plan without paying inside the app (D-70).
/// </summary>
[Collection(nameof(AdminCollection))]
public sealed class PlanLimitTests
{
    private const string ProfessionalRefusal =
        "Professional plan user limit reached (maximum 5 users including inactive accounts). "
        + "Upgrade to the Elite Plan to add more users.";

    private readonly AdminFixture _admin;

    public PlanLimitTests(AdminFixture admin) => _admin = admin;

    // ---- The plan table, pure ---------------------------------------------

    [Theory]
    [InlineData(LicenseType.Trial, 3, 1000, 1000)]
    [InlineData(LicenseType.Standard, 5, 1000, 1000)]
    [InlineData(LicenseType.Pro, 5, 1000, 1000)]
    [InlineData(LicenseType.Elite, null, null, null)]
    public void PLAN_01_each_plan_is_given_its_own_limits(LicenseType type, int? users, int? sales, int? purchase)
    {
        PlanCatalogRepository.PlanLimits limits = PlanCatalogRepository.DefaultsFor(type);

        Assert.Equal((users, sales, purchase), (limits.MaxUsers, limits.MonthlySalesTransactions, limits.MonthlyPurchaseTransactions));
    }

    [Theory]
    [InlineData(LicenseType.Trial, "Trial")]
    [InlineData(LicenseType.Standard, "Professional")]
    [InlineData(LicenseType.Pro, "Professional")]
    [InlineData(LicenseType.Elite, "Elite")]
    public void PLAN_02_standard_is_read_as_professional(LicenseType type, string name) =>
        Assert.Equal(name, PlanCatalogRepository.Name(type));

    [Theory]
    [InlineData(LicenseType.Trial, LicenseType.Pro, true)]
    [InlineData(LicenseType.Trial, LicenseType.Elite, true)]
    [InlineData(LicenseType.Pro, LicenseType.Elite, true)]
    [InlineData(LicenseType.Standard, LicenseType.Elite, true)]
    [InlineData(LicenseType.Pro, LicenseType.Pro, false)]
    [InlineData(LicenseType.Standard, LicenseType.Pro, false)]
    [InlineData(LicenseType.Elite, LicenseType.Pro, false)]
    [InlineData(LicenseType.Elite, LicenseType.Elite, false)]
    [InlineData(LicenseType.Pro, LicenseType.Trial, false)]
    public void PLAN_03_only_a_higher_paid_plan_is_an_upgrade(LicenseType from, LicenseType to, bool allowed) =>
        Assert.Equal(allowed, PlanCatalogRepository.IsUpgrade(from, to));

    [Fact]
    public void PLAN_04_the_professional_refusal_reads_as_the_owner_worded_it()
    {
        Assert.Equal(ProfessionalRefusal, PlanCatalogRepository.UserLimitMessage(LicenseType.Pro, 5));
        Assert.Equal(ProfessionalRefusal, PlanCatalogRepository.UserLimitMessage(LicenseType.Standard, 5));

        // A trial is pointed at either paid plan, not only the one above Professional.
        Assert.Equal(
            "Trial plan user limit reached (maximum 3 users including inactive accounts). "
            + "Upgrade to the Professional or Elite Plan to add more users.",
            PlanCatalogRepository.UserLimitMessage(LicenseType.Trial, 3));
    }

    [Fact]
    public void PLAN_05_apply_replaces_every_limit_with_the_plans_own()
    {
        var licence = new License
        {
            LicenseType = LicenseType.Elite,
            MaxUsers = 5,
            MonthlySalesTransactionLimit = 1000,
            MonthlyPurchaseTransactionLimit = 1000,
        };

        PlanCatalogRepository.Apply(licence);
        Assert.Equal((null, null, null), (licence.MaxUsers, licence.MonthlySalesTransactionLimit, licence.MonthlyPurchaseTransactionLimit));

        licence.LicenseType = LicenseType.Pro;
        PlanCatalogRepository.Apply(licence);
        Assert.Equal((5, 1000, 1000), (licence.MaxUsers, licence.MonthlySalesTransactionLimit, licence.MonthlyPurchaseTransactionLimit));
    }

    // ---- The seat guard, on a real database ---------------------------------

    private static IConfiguration Config(string? upgradeEmail = null) =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["App:BaseUrl"] = "https://app.test",
            [PlanRepository.UpgradeRequestEmailKey] = upgradeEmail,
        }).Build();

    internal static UserRepository Users(AdminDbContext db, RecordingEmailSender mail) =>
        new(db, mail, new OrgContextRepository(db, TimeProvider.System), Config(), TimeProvider.System);

    internal static PlanRepository Plans(AdminDbContext db, RecordingEmailSender mail, string? upgradeEmail = "ops@platform.test") =>
        new(db, mail, Config(upgradeEmail), NullLogger<PlanRepository>.Instance);

    /// <summary>
    /// A customer of the given kind and plan with one branch holding
    /// <paramref name="seated"/> users, the last <paramref name="revoked"/> of
    /// whom have been revoked.
    /// </summary>
    internal static async Task<(Guid CustomerId, Guid OrgId, List<Guid> UserIds)> SeedAsync(
        AdminDbContext db, LicenseType plan, int seated, int revoked = 0, CustomerKind kind = CustomerKind.Standard)
    {
        var customerId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        string suffix = Guid.NewGuid().ToString("N")[..8];
        DateOnly today = DateOnly.FromDateTime(DateTime.UtcNow);

        db.Customers.Add(new Master.Entity.TableEntities.Customer
        {
            CustomerId = customerId,
            CustomerCode = Random.Shared.NextInt64(1_000_000_000, 9_999_999_999).ToString(),
            CountryPrefix = "IN",
            Name = $"Plan Test {suffix}",
            BillingEmail = $"billing-{suffix}@example.com",
            Status = TenantStatus.Active,
            PlanTier = plan == LicenseType.Elite ? PlanTier.Elite : PlanTier.Pro,
            Kind = kind,
            DatabaseName = "IN000001",
        });
        db.Organizations.Add(new Organization
        {
            OrgId = orgId,
            CustomerId = customerId,
            OrgCode = $"P{suffix}",
            Name = "Chennai",
            BaseCurrency = "INR",
            FinancialYearStartMonth = 4,
            Status = TenantStatus.Active,
            BusinessType = BusinessType.Retail,
            Vertical = Vertical.Retail,
        });

        var licence = new License
        {
            CustomerId = customerId,
            LicenseType = plan,
            StartDate = today.AddDays(-1),
            ExpiryDate = today.AddYears(1),
            MaxOrganizations = 3,
            IsActive = true,
        };
        PlanCatalogRepository.Apply(licence);
        db.Licenses.Add(licence);

        int roleId = await db.Roles.Where(r => r.IsSystemRole && r.SystemName == "Viewer").Select(r => r.RoleId).SingleAsync();
        var userIds = new List<Guid>();
        for (int i = 0; i < seated; i++)
        {
            var userId = Guid.NewGuid();
            userIds.Add(userId);
            db.Users.Add(new User
            {
                UserId = userId,
                Email = $"user{i}-{suffix}@example.com",
                DisplayName = $"User {i}",
                PasswordHash = "x",
                IsActive = true,
                EmailConfirmed = true,
            });
            db.UserOrganizationRoles.Add(new UserOrganizationRole
            {
                UserId = userId,
                OrgId = orgId,
                RoleId = roleId,
                IsActive = i < seated - revoked,
            });
        }

        await db.SaveChangesAsync();
        return (customerId, orgId, userIds);
    }

    internal static InviteUserRequest Invitation(string email, int roleId) =>
        new() { Email = email, DisplayName = "Newcomer", RoleId = roleId };

    internal static Task<int> ViewerRoleAsync(AdminDbContext db) =>
        db.Roles.Where(r => r.IsSystemRole && r.SystemName == "Viewer").Select(r => r.RoleId).SingleAsync();

    [SkippableFact]
    public async Task PLAN_06_the_sixth_user_of_a_professional_branch_is_refused_with_the_figures()
    {
        Skip.If(_admin.SkipReason is not null, _admin.SkipReason ?? string.Empty);

        await using AdminDbContext db = _admin.CreateContext();
        (Guid customerId, Guid orgId, _) = await SeedAsync(db, LicenseType.Pro, seated: 5);
        var mail = new RecordingEmailSender();

        InviteUserResult result = await Users(db, mail).InviteAsync(
            orgId, customerId, Invitation($"sixth-{Guid.NewGuid():N}@example.com", await ViewerRoleAsync(db)), default);

        Assert.Equal(InviteOutcome.UserLimitReached, result.Outcome);
        Assert.NotNull(result.Limit);
        Assert.Equal(ProfessionalRefusal, result.Limit.Message);
        Assert.Equal(PlanLimitResponse.ReachedCode, result.Limit.Code);
        Assert.Equal(PlanLimitKind.Users, result.Limit.Kind);
        Assert.Equal(("Professional", 5, 5), (result.Limit.Plan, result.Limit.Limit, result.Limit.Used));
        Assert.Empty(mail.Sent);
    }

    [SkippableFact]
    public async Task PLAN_07_a_revoked_user_still_holds_a_place()
    {
        Skip.If(_admin.SkipReason is not null, _admin.SkipReason ?? string.Empty);

        await using AdminDbContext db = _admin.CreateContext();

        // Five users, two of them revoked: only three are active, and the branch is full.
        (Guid customerId, Guid orgId, _) = await SeedAsync(db, LicenseType.Pro, seated: 5, revoked: 2);

        InviteUserResult result = await Users(db, new RecordingEmailSender()).InviteAsync(
            orgId, customerId, Invitation($"new-{Guid.NewGuid():N}@example.com", await ViewerRoleAsync(db)), default);

        Assert.Equal(InviteOutcome.UserLimitReached, result.Outcome);
        Assert.Equal(5, result.Limit!.Used);
    }

    [SkippableFact]
    public async Task PLAN_08_inviting_a_revoked_user_back_takes_no_new_place_and_reactivates_them()
    {
        Skip.If(_admin.SkipReason is not null, _admin.SkipReason ?? string.Empty);

        await using AdminDbContext db = _admin.CreateContext();
        (Guid customerId, Guid orgId, List<Guid> userIds) = await SeedAsync(db, LicenseType.Pro, seated: 5, revoked: 1);
        Guid revokedId = userIds[^1];
        User revoked = await db.Users.AsNoTracking().SingleAsync(u => u.UserId == revokedId);
        int roleId = await ViewerRoleAsync(db);
        var mail = new RecordingEmailSender();

        // The branch is full at 5 of 5, and the person already counted is welcome back.
        InviteUserResult result = await Users(db, mail).InviteAsync(orgId, customerId, Invitation(revoked.Email, roleId), default);

        Assert.Equal(InviteOutcome.Ok, result.Outcome);
        Assert.Single(mail.Sent);

        db.ChangeTracker.Clear();
        UserOrganizationRole row = await db.UserOrganizationRoles.SingleAsync(a => a.UserId == revokedId && a.OrgId == orgId);
        Assert.True(row.IsActive);
        Assert.Equal(5, await db.UserOrganizationRoles.Where(a => a.OrgId == orgId).Select(a => a.UserId).Distinct().CountAsync());
    }

    [SkippableFact]
    public async Task PLAN_09_a_member_who_is_active_is_told_so_not_that_the_plan_is_full()
    {
        Skip.If(_admin.SkipReason is not null, _admin.SkipReason ?? string.Empty);

        await using AdminDbContext db = _admin.CreateContext();
        (Guid customerId, Guid orgId, List<Guid> userIds) = await SeedAsync(db, LicenseType.Pro, seated: 5);
        User member = await db.Users.AsNoTracking().SingleAsync(u => u.UserId == userIds[0]);

        InviteUserResult result = await Users(db, new RecordingEmailSender()).InviteAsync(
            orgId, customerId, Invitation(member.Email, await ViewerRoleAsync(db)), default);

        Assert.Equal(InviteOutcome.AlreadyMember, result.Outcome);
    }

    [SkippableFact]
    public async Task PLAN_10_elite_holds_any_number_of_users()
    {
        Skip.If(_admin.SkipReason is not null, _admin.SkipReason ?? string.Empty);

        await using AdminDbContext db = _admin.CreateContext();
        (Guid customerId, Guid orgId, _) = await SeedAsync(db, LicenseType.Elite, seated: 8);

        InviteUserResult result = await Users(db, new RecordingEmailSender()).InviteAsync(
            orgId, customerId, Invitation($"ninth-{Guid.NewGuid():N}@example.com", await ViewerRoleAsync(db)), default);

        Assert.Equal(InviteOutcome.Ok, result.Outcome);
    }

    [SkippableFact]
    public async Task PLAN_11_the_limit_is_counted_per_branch()
    {
        Skip.If(_admin.SkipReason is not null, _admin.SkipReason ?? string.Empty);

        await using AdminDbContext db = _admin.CreateContext();
        (Guid customerId, Guid fullBranch, _) = await SeedAsync(db, LicenseType.Pro, seated: 5);

        var secondBranch = Guid.NewGuid();
        db.Organizations.Add(new Organization
        {
            OrgId = secondBranch,
            CustomerId = customerId,
            OrgCode = $"S{Guid.NewGuid():N}"[..10],
            Name = "Madurai",
            BaseCurrency = "INR",
            FinancialYearStartMonth = 4,
            Status = TenantStatus.Active,
            BusinessType = BusinessType.Retail,
            Vertical = Vertical.Retail,
        });
        await db.SaveChangesAsync();
        int roleId = await ViewerRoleAsync(db);
        UserRepository users = Users(db, new RecordingEmailSender());

        InviteUserResult full = await users.InviteAsync(fullBranch, customerId, Invitation($"a-{Guid.NewGuid():N}@example.com", roleId), default);
        InviteUserResult empty = await users.InviteAsync(secondBranch, customerId, Invitation($"b-{Guid.NewGuid():N}@example.com", roleId), default);

        Assert.Equal(InviteOutcome.UserLimitReached, full.Outcome);
        Assert.Equal(InviteOutcome.Ok, empty.Outcome);
    }

    [SkippableFact]
    public async Task PLAN_12_the_summary_counts_revoked_users_and_offers_the_upgrade_only_below_elite()
    {
        Skip.If(_admin.SkipReason is not null, _admin.SkipReason ?? string.Empty);

        await using AdminDbContext db = _admin.CreateContext();
        (Guid proCustomer, Guid proOrg, _) = await SeedAsync(db, LicenseType.Pro, seated: 4, revoked: 1);
        (Guid eliteCustomer, Guid eliteOrg, _) = await SeedAsync(db, LicenseType.Elite, seated: 2);
        (Guid demoCustomer, Guid demoOrg, _) = await SeedAsync(db, LicenseType.Elite, seated: 1, kind: CustomerKind.Demo);
        PlanRepository plans = Plans(db, new RecordingEmailSender());

        PlanSummaryResponse pro = (await plans.GetSummaryAsync(proCustomer, proOrg, default))!;
        PlanSummaryResponse elite = (await plans.GetSummaryAsync(eliteCustomer, eliteOrg, default))!;
        PlanSummaryResponse demo = (await plans.GetSummaryAsync(demoCustomer, demoOrg, default))!;

        Assert.Equal(("Professional", 5, 4, 1000, 1000, true), (pro.Plan, pro.MaxUsers, pro.UsersUsed, pro.MonthlySalesTransactionLimit, pro.MonthlyPurchaseTransactionLimit, pro.CanUpgrade));
        Assert.Equal(("Elite", null, 2, false), (elite.Plan, elite.MaxUsers, elite.UsersUsed, elite.CanUpgrade));
        Assert.False(demo.CanUpgrade);

        // One place left: no refusal to show. All taken, revoked ones among them: the invitation's own sentence.
        Assert.Null(pro.UserLimitMessage);
        Assert.Null(elite.UserLimitMessage);
        (Guid fullCustomer, Guid fullOrg, _) = await SeedAsync(db, LicenseType.Pro, seated: 5, revoked: 2);
        Assert.Equal(ProfessionalRefusal, (await plans.GetSummaryAsync(fullCustomer, fullOrg, default))!.UserLimitMessage);
        Assert.Null(await plans.GetSummaryAsync(Guid.NewGuid(), Guid.NewGuid(), default));
    }

    [SkippableFact]
    public async Task PLAN_13_the_org_context_carries_the_limits_and_none_for_an_unbilled_customer()
    {
        Skip.If(_admin.SkipReason is not null, _admin.SkipReason ?? string.Empty);

        await using AdminDbContext db = _admin.CreateContext();
        (_, Guid proOrg, _) = await SeedAsync(db, LicenseType.Pro, seated: 1);
        (_, Guid internalOrg, _) = await SeedAsync(db, LicenseType.Elite, seated: 1, kind: CustomerKind.Internal);
        var context = new OrgContextRepository(db, TimeProvider.System);

        OrgContextResponse pro = (await context.ResolveAsync(proOrg, default))!;
        OrgContextResponse unbilled = (await context.ResolveAsync(internalOrg, default))!;

        Assert.Equal((5, 1000, 1000, LicenseType.Pro), (pro.MaxUsers, pro.MonthlySalesTransactionLimit, pro.MonthlyPurchaseTransactionLimit, pro.LicenseType));
        Assert.Equal((null, null, null), (unbilled.MaxUsers, unbilled.MonthlySalesTransactionLimit, unbilled.MonthlyPurchaseTransactionLimit));

        // Sales and Purchase name the plan in a refusal from here, and keep no copy of the mapping (TK-210).
        Assert.Equal(("Professional", "Elite"), (pro.PlanName, unbilled.PlanName));
    }

    // ---- Asking for the higher plan ---------------------------------------------

    [SkippableFact]
    public async Task PLAN_14_a_request_is_recorded_and_mailed_and_a_second_is_answered_with_the_first()
    {
        Skip.If(_admin.SkipReason is not null, _admin.SkipReason ?? string.Empty);

        await using AdminDbContext db = _admin.CreateContext();
        (Guid customerId, Guid orgId, List<Guid> userIds) = await SeedAsync(db, LicenseType.Pro, seated: 5);
        var mail = new RecordingEmailSender();
        PlanRepository plans = Plans(db, mail);
        var input = new UpgradeRequestInput { Reason = PlanLimitKind.Users, Note = "We opened a second counter" };

        UpgradeRequestResult first = await plans.RequestUpgradeAsync(customerId, orgId, userIds[0], input, default);
        UpgradeRequestResult second = await plans.RequestUpgradeAsync(customerId, orgId, userIds[1], input, default);

        Assert.Equal(UpgradeRequestOutcome.Recorded, first.Outcome);
        Assert.Equal(UpgradeRequestOutcome.AlreadyRequested, second.Outcome);

        db.ChangeTracker.Clear();
        UpgradeRequest saved = await db.UpgradeRequests.SingleAsync(r => r.CustomerId == customerId);
        Assert.Equal((LicenseType.Pro, LicenseType.Elite, PlanLimitKind.Users, UpgradeRequestStatus.Pending),
            (saved.FromPlan, saved.ToPlan, saved.Reason, saved.Status));
        Assert.Equal(userIds[0], saved.RequestedByUserId);
        Assert.Equal(orgId, saved.OrgId);

        EmailMessage sent = Assert.Single(mail.Sent);
        Assert.Equal("ops@platform.test", sent.ToEmail);
        Assert.Contains("Professional to Elite", sent.Subject);
        Assert.Contains("We opened a second counter", sent.TextBody);

        Assert.True((await plans.GetSummaryAsync(customerId, orgId, default))!.UpgradeRequested);
    }

    [SkippableFact]
    public async Task PLAN_15_a_request_is_kept_when_no_address_is_set_to_mail_it_to()
    {
        Skip.If(_admin.SkipReason is not null, _admin.SkipReason ?? string.Empty);

        await using AdminDbContext db = _admin.CreateContext();
        (Guid customerId, Guid orgId, List<Guid> userIds) = await SeedAsync(db, LicenseType.Pro, seated: 1);
        var mail = new RecordingEmailSender();

        UpgradeRequestResult result = await Plans(db, mail, upgradeEmail: null)
            .RequestUpgradeAsync(customerId, orgId, userIds[0], new UpgradeRequestInput(), default);

        Assert.Equal(UpgradeRequestOutcome.Recorded, result.Outcome);
        Assert.Empty(mail.Sent);
        Assert.True(await db.UpgradeRequests.AnyAsync(r => r.CustomerId == customerId));
    }

    [SkippableFact]
    public async Task PLAN_16_elite_an_unbilled_customer_and_a_lateral_move_are_not_upgrades()
    {
        Skip.If(_admin.SkipReason is not null, _admin.SkipReason ?? string.Empty);

        await using AdminDbContext db = _admin.CreateContext();
        (Guid eliteCustomer, Guid eliteOrg, List<Guid> eliteUsers) = await SeedAsync(db, LicenseType.Elite, seated: 1);
        (Guid demoCustomer, Guid demoOrg, List<Guid> demoUsers) = await SeedAsync(db, LicenseType.Elite, seated: 1, kind: CustomerKind.Demo);
        (Guid proCustomer, Guid proOrg, List<Guid> proUsers) = await SeedAsync(db, LicenseType.Pro, seated: 1);
        PlanRepository plans = Plans(db, new RecordingEmailSender());

        Assert.Equal(UpgradeRequestOutcome.NotAnUpgrade,
            (await plans.RequestUpgradeAsync(eliteCustomer, eliteOrg, eliteUsers[0], new UpgradeRequestInput(), default)).Outcome);
        Assert.Equal(UpgradeRequestOutcome.NotAnUpgrade,
            (await plans.RequestUpgradeAsync(demoCustomer, demoOrg, demoUsers[0], new UpgradeRequestInput(), default)).Outcome);
        Assert.Equal(UpgradeRequestOutcome.NotAnUpgrade,
            (await plans.RequestUpgradeAsync(proCustomer, proOrg, proUsers[0], new UpgradeRequestInput { ToPlan = LicenseType.Pro }, default)).Outcome);
        Assert.Equal(UpgradeRequestOutcome.NoLicence,
            (await plans.RequestUpgradeAsync(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), new UpgradeRequestInput(), default)).Outcome);
        Assert.False(await db.UpgradeRequests.AnyAsync(r => r.CustomerId == eliteCustomer || r.CustomerId == demoCustomer || r.CustomerId == proCustomer));
    }

    // ---- Licences stored before the plans --------------------------------------------

    [SkippableFact]
    public async Task PLAN_17_stored_licences_are_brought_in_line_and_an_operators_figure_is_left_alone()
    {
        Skip.If(_admin.SkipReason is not null, _admin.SkipReason ?? string.Empty);

        await using AdminDbContext db = _admin.CreateContext();
        (Guid elite, _, _) = await SeedAsync(db, LicenseType.Elite, seated: 0);
        (Guid missing, _, _) = await SeedAsync(db, LicenseType.Pro, seated: 0);
        (Guid trial, _, _) = await SeedAsync(db, LicenseType.Trial, seated: 0);
        (Guid custom, _, _) = await SeedAsync(db, LicenseType.Pro, seated: 0);

        // How the rows look after the migration made the columns empty-able.
        (await db.Licenses.SingleAsync(l => l.CustomerId == elite)).MaxUsers = 10_000;
        (await db.Licenses.SingleAsync(l => l.CustomerId == elite)).MonthlySalesTransactionLimit = 1000;
        License gaps = await db.Licenses.SingleAsync(l => l.CustomerId == missing);
        gaps.MaxUsers = null;
        gaps.MonthlySalesTransactionLimit = null;
        gaps.MonthlyPurchaseTransactionLimit = null;
        License trialGaps = await db.Licenses.SingleAsync(l => l.CustomerId == trial);
        trialGaps.MaxUsers = null;
        trialGaps.MonthlySalesTransactionLimit = null;
        License deal = await db.Licenses.SingleAsync(l => l.CustomerId == custom);
        deal.MaxUsers = 25;
        deal.MonthlySalesTransactionLimit = 5000;
        await db.SaveChangesAsync();

        await PlanRepository.ApplyDefaultsAsync(db, default);
        await PlanRepository.ApplyDefaultsAsync(db, default);

        db.ChangeTracker.Clear();
        static (int?, int?, int?) Row(License l) => (l.MaxUsers, l.MonthlySalesTransactionLimit, l.MonthlyPurchaseTransactionLimit);
        Assert.Equal(((int?)null, (int?)null, (int?)null), Row(await db.Licenses.SingleAsync(l => l.CustomerId == elite)));
        Assert.Equal(((int?)5, (int?)1000, (int?)1000), Row(await db.Licenses.SingleAsync(l => l.CustomerId == missing)));
        Assert.Equal(((int?)3, (int?)1000, (int?)1000), Row(await db.Licenses.SingleAsync(l => l.CustomerId == trial)));
        Assert.Equal(((int?)25, (int?)5000, (int?)1000), Row(await db.Licenses.SingleAsync(l => l.CustomerId == custom)));
    }
}
