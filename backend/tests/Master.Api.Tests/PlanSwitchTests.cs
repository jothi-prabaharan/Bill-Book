using Master.Api.Controllers;
using Master.Entity.Enums;
using Master.Entity.Models;
using Master.Entity.TableEntities;
using Master.Repository;
using Master.Repository.Repositories;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Apps;
using Shared.Kernel.Licensing;
using Shared.Kernel.Tenancy;
using Xunit;

namespace Master.Api.Tests;

/// <summary>
/// A platform operator moving a customer between the paid plans (D-70, TK-211),
/// and the customer list the operator does it from: the licence takes the new
/// plan's own limits at once, a request for it is closed, an internal or demo
/// customer is left alone, and the expiry stays with the renewal.
/// </summary>
[Collection(nameof(AdminCollection))]
public sealed class PlanSwitchTests
{
    private readonly AdminFixture _admin;

    public PlanSwitchTests(AdminFixture admin) => _admin = admin;

    private static Task<License> LicenceAsync(AdminDbContext db, Guid customerId) =>
        db.Licenses.AsNoTracking().SingleAsync(l => l.CustomerId == customerId);

    private static Task<Master.Entity.TableEntities.Customer> CustomerAsync(AdminDbContext db, Guid customerId) =>
        db.Customers.AsNoTracking().SingleAsync(c => c.CustomerId == customerId);

    [SkippableFact]
    public async Task SWITCH_01_moving_to_elite_lifts_every_limit_sets_the_tier_and_closes_the_request()
    {
        Skip.If(_admin.SkipReason is not null, _admin.SkipReason ?? string.Empty);

        await using AdminDbContext db = _admin.CreateContext();
        (Guid customerId, Guid orgId, List<Guid> userIds) = await PlanLimitTests.SeedAsync(db, LicenseType.Pro, seated: 1);
        var mail = new RecordingEmailSender();
        await PlanLimitTests.Plans(db, mail).RequestUpgradeAsync(
            customerId, orgId, userIds[0], new UpgradeRequestInput { Reason = PlanLimitKind.Users }, default);

        SwitchPlanResult result = await PlanLimitTests.Plans(db, mail).SwitchPlanAsync(customerId, LicenseType.Elite, default);

        db.ChangeTracker.Clear();
        License licence = await LicenceAsync(db, customerId);
        UpgradeRequest request = await db.UpgradeRequests.AsNoTracking().SingleAsync(r => r.CustomerId == customerId);

        Assert.Equal((SwitchPlanOutcome.Switched, "Elite"), (result.Outcome, result.Plan));
        Assert.Equal(
            (LicenseType.Elite, null, null, null),
            (licence.LicenseType, licence.MaxUsers, licence.MonthlySalesTransactionLimit, licence.MonthlyPurchaseTransactionLimit));
        Assert.Equal(PlanTier.Elite, (await CustomerAsync(db, customerId)).PlanTier);
        Assert.Equal(UpgradeRequestStatus.Done, request.Status);
    }

    [SkippableFact]
    public async Task SWITCH_02_moving_back_to_professional_gives_the_limits_back()
    {
        Skip.If(_admin.SkipReason is not null, _admin.SkipReason ?? string.Empty);

        await using AdminDbContext db = _admin.CreateContext();
        (Guid customerId, _, _) = await PlanLimitTests.SeedAsync(db, LicenseType.Elite, seated: 0);

        SwitchPlanResult result = await PlanLimitTests.Plans(db, new RecordingEmailSender()).SwitchPlanAsync(customerId, LicenseType.Pro, default);

        db.ChangeTracker.Clear();
        License licence = await LicenceAsync(db, customerId);
        Assert.Equal((SwitchPlanOutcome.Switched, "Professional"), (result.Outcome, result.Plan));
        Assert.Equal(
            (LicenseType.Pro, 5, 1000, 1000),
            (licence.LicenseType, licence.MaxUsers, licence.MonthlySalesTransactionLimit, licence.MonthlyPurchaseTransactionLimit));
        Assert.Equal(PlanTier.Pro, (await CustomerAsync(db, customerId)).PlanTier);
    }

    [SkippableFact]
    public async Task SWITCH_03_a_trial_that_pays_becomes_active_on_professional_and_keeps_a_request_for_elite()
    {
        Skip.If(_admin.SkipReason is not null, _admin.SkipReason ?? string.Empty);

        await using AdminDbContext db = _admin.CreateContext();
        (Guid customerId, Guid orgId, List<Guid> userIds) = await PlanLimitTests.SeedAsync(db, LicenseType.Trial, seated: 1);
        await db.Customers.Where(c => c.CustomerId == customerId)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.Status, TenantStatus.Trial));

        // The update bypassed the tracker, which still holds the customer as Active.
        db.ChangeTracker.Clear();
        var mail = new RecordingEmailSender();
        await PlanLimitTests.Plans(db, mail).RequestUpgradeAsync(
            customerId, orgId, userIds[0], new UpgradeRequestInput { ToPlan = LicenseType.Elite }, default);

        SwitchPlanResult result = await PlanLimitTests.Plans(db, mail).SwitchPlanAsync(customerId, LicenseType.Pro, default);

        db.ChangeTracker.Clear();
        Master.Entity.TableEntities.Customer customer = await CustomerAsync(db, customerId);
        License licence = await LicenceAsync(db, customerId);
        UpgradeRequest request = await db.UpgradeRequests.AsNoTracking().SingleAsync(r => r.CustomerId == customerId);

        Assert.Equal(SwitchPlanOutcome.Switched, result.Outcome);
        Assert.Equal((TenantStatus.Active, PlanTier.Pro), (customer.Status, customer.PlanTier));
        Assert.Equal((5, 1000, 1000), (licence.MaxUsers, licence.MonthlySalesTransactionLimit, licence.MonthlyPurchaseTransactionLimit));

        // They asked for Elite and have been given Professional: the request is still open.
        Assert.Equal(UpgradeRequestStatus.Pending, request.Status);
    }

    [SkippableTheory]
    [InlineData(LicenseType.Standard, LicenseType.Pro, SwitchPlanOutcome.AlreadyOnPlan)]
    [InlineData(LicenseType.Pro, LicenseType.Pro, SwitchPlanOutcome.AlreadyOnPlan)]
    [InlineData(LicenseType.Elite, LicenseType.Elite, SwitchPlanOutcome.AlreadyOnPlan)]
    [InlineData(LicenseType.Pro, LicenseType.Trial, SwitchPlanOutcome.NotSold)]
    [InlineData(LicenseType.Pro, LicenseType.Standard, SwitchPlanOutcome.NotSold)]
    public async Task SWITCH_04_a_plan_the_customer_is_already_on_or_one_that_is_not_for_sale_changes_nothing(
        LicenseType current, LicenseType requested, SwitchPlanOutcome expected)
    {
        Skip.If(_admin.SkipReason is not null, _admin.SkipReason ?? string.Empty);

        await using AdminDbContext db = _admin.CreateContext();
        (Guid customerId, _, _) = await PlanLimitTests.SeedAsync(db, current, seated: 0);
        License before = await LicenceAsync(db, customerId);

        SwitchPlanResult result = await PlanLimitTests.Plans(db, new RecordingEmailSender()).SwitchPlanAsync(customerId, requested, default);

        db.ChangeTracker.Clear();
        License after = await LicenceAsync(db, customerId);
        Assert.Equal(expected, result.Outcome);
        Assert.Equal(
            (before.LicenseType, before.MaxUsers, before.MonthlySalesTransactionLimit, before.MonthlyPurchaseTransactionLimit),
            (after.LicenseType, after.MaxUsers, after.MonthlySalesTransactionLimit, after.MonthlyPurchaseTransactionLimit));
    }

    [SkippableTheory]
    [InlineData(CustomerKind.Internal)]
    [InlineData(CustomerKind.Demo)]
    public async Task SWITCH_05_an_internal_or_demo_customer_is_never_billed_so_never_switched(CustomerKind kind)
    {
        Skip.If(_admin.SkipReason is not null, _admin.SkipReason ?? string.Empty);

        await using AdminDbContext db = _admin.CreateContext();
        (Guid customerId, _, _) = await PlanLimitTests.SeedAsync(db, LicenseType.Elite, seated: 0, kind: kind);

        SwitchPlanResult result = await PlanLimitTests.Plans(db, new RecordingEmailSender()).SwitchPlanAsync(customerId, LicenseType.Pro, default);

        db.ChangeTracker.Clear();
        Assert.Equal(SwitchPlanOutcome.Unbilled, result.Outcome);
        Assert.Equal(LicenseType.Elite, (await LicenceAsync(db, customerId)).LicenseType);
    }

    [SkippableFact]
    public async Task SWITCH_06_a_customer_that_does_not_exist_is_not_found()
    {
        Skip.If(_admin.SkipReason is not null, _admin.SkipReason ?? string.Empty);

        await using AdminDbContext db = _admin.CreateContext();

        SwitchPlanResult result = await PlanLimitTests.Plans(db, new RecordingEmailSender()).SwitchPlanAsync(Guid.NewGuid(), LicenseType.Elite, default);

        Assert.Equal(SwitchPlanOutcome.NotFound, result.Outcome);
    }

    [SkippableFact]
    public async Task SWITCH_07_the_expiry_and_the_branch_cap_are_the_renewals_to_move_not_the_switchs()
    {
        Skip.If(_admin.SkipReason is not null, _admin.SkipReason ?? string.Empty);

        await using AdminDbContext db = _admin.CreateContext();
        (Guid customerId, _, _) = await PlanLimitTests.SeedAsync(db, LicenseType.Pro, seated: 0);
        License before = await LicenceAsync(db, customerId);

        await PlanLimitTests.Plans(db, new RecordingEmailSender()).SwitchPlanAsync(customerId, LicenseType.Elite, default);

        db.ChangeTracker.Clear();
        License after = await LicenceAsync(db, customerId);
        Assert.Equal((before.StartDate, before.ExpiryDate, before.MaxOrganizations), (after.StartDate, after.ExpiryDate, after.MaxOrganizations));
    }

    [SkippableFact]
    public async Task SWITCH_08_the_branch_that_was_full_can_invite_the_moment_the_customer_is_on_elite()
    {
        Skip.If(_admin.SkipReason is not null, _admin.SkipReason ?? string.Empty);

        await using AdminDbContext db = _admin.CreateContext();
        (Guid customerId, Guid orgId, _) = await PlanLimitTests.SeedAsync(db, LicenseType.Pro, seated: 5);
        int roleId = await PlanLimitTests.ViewerRoleAsync(db);
        var mail = new RecordingEmailSender();

        InviteUserResult refused = await PlanLimitTests.Users(db, mail).InviteAsync(
            orgId, customerId, PlanLimitTests.Invitation($"sixth-{Guid.NewGuid():N}@example.com", roleId), default);
        await PlanLimitTests.Plans(db, mail).SwitchPlanAsync(customerId, LicenseType.Elite, default);
        InviteUserResult welcomed = await PlanLimitTests.Users(db, mail).InviteAsync(
            orgId, customerId, PlanLimitTests.Invitation($"sixth-{Guid.NewGuid():N}@example.com", roleId), default);

        Assert.Equal(InviteOutcome.UserLimitReached, refused.Outcome);
        Assert.Equal(InviteOutcome.Ok, welcomed.Outcome);
    }

    [SkippableFact]
    public async Task SWITCH_09_the_route_answers_each_outcome_with_its_own_status()
    {
        Skip.If(_admin.SkipReason is not null, _admin.SkipReason ?? string.Empty);

        await using AdminDbContext db = _admin.CreateContext();
        (Guid customerId, _, _) = await PlanLimitTests.SeedAsync(db, LicenseType.Pro, seated: 0);
        (Guid internalId, _, _) = await PlanLimitTests.SeedAsync(db, LicenseType.Elite, seated: 0, kind: CustomerKind.Internal);

        // SwitchPlan reads only the plan repository, so the licence repository is not needed.
        var controller = new LicensesController(null!, PlanLimitTests.Plans(db, new RecordingEmailSender()));
        static SwitchPlanRequest To(LicenseType plan) => new() { Plan = plan };

        IActionResult switched = await controller.SwitchPlan(customerId, To(LicenseType.Elite), default);
        IActionResult repeated = await controller.SwitchPlan(customerId, To(LicenseType.Elite), default);
        IActionResult notSold = await controller.SwitchPlan(customerId, To(LicenseType.Trial), default);
        IActionResult unbilled = await controller.SwitchPlan(internalId, To(LicenseType.Pro), default);
        IActionResult unknown = await controller.SwitchPlan(Guid.NewGuid(), To(LicenseType.Pro), default);

        Assert.Equal("The customer is now on the Elite plan.", Assert.IsType<MessageResponse>(Assert.IsType<OkObjectResult>(switched).Value).Message);
        Assert.IsType<ConflictObjectResult>(repeated);
        Assert.IsType<BadRequestObjectResult>(notSold);
        Assert.IsType<ConflictObjectResult>(unbilled);
        Assert.IsType<NotFoundResult>(unknown);
    }

    // ---- The list the operator switches from ------------------------------

    /// <summary><c>ListAsync</c> reads only the database, so nothing else is needed to build the repository.</summary>
    private static SignupRepository Listing(AdminDbContext db) =>
        new(db, null!, null!, null!, null!, TimeProvider.System, null!, null!, null!);

    [SkippableFact]
    public async Task SWITCH_10_the_list_names_each_customers_plan_and_the_upgrade_waiting_for_them()
    {
        Skip.If(_admin.SkipReason is not null, _admin.SkipReason ?? string.Empty);

        await using AdminDbContext db = _admin.CreateContext();
        (Guid waitingId, Guid waitingOrg, List<Guid> waitingUsers) = await PlanLimitTests.SeedAsync(db, LicenseType.Pro, seated: 1);
        (Guid calmId, _, _) = await PlanLimitTests.SeedAsync(db, LicenseType.Elite, seated: 0);
        (Guid trialId, _, _) = await PlanLimitTests.SeedAsync(db, LicenseType.Trial, seated: 0);
        (Guid standardId, _, _) = await PlanLimitTests.SeedAsync(db, LicenseType.Standard, seated: 0);
        await PlanLimitTests.Plans(db, new RecordingEmailSender()).RequestUpgradeAsync(
            waitingId, waitingOrg, waitingUsers[0], new UpgradeRequestInput(), default);

        // A customer whose licence was never made has no plan to name.
        var unlicensed = new Master.Entity.TableEntities.Customer
        {
            CustomerId = Guid.NewGuid(),
            CustomerCode = Random.Shared.NextInt64(1_000_000_000, 9_999_999_999).ToString(),
            CountryPrefix = "IN",
            Name = "No licence yet",
            BillingEmail = $"nolicence-{Guid.NewGuid():N}@example.com",
            Status = TenantStatus.Provisioning,
            PlanTier = PlanTier.Pro,
            DatabaseName = "IN000001",
        };
        db.Customers.Add(unlicensed);
        await db.SaveChangesAsync();

        IReadOnlyList<CustomerListItem> list = await Listing(db).ListAsync(default);

        CustomerListItem waiting = list.Single(c => c.CustomerId == waitingId);
        Assert.Equal(("Professional", "Elite"), (waiting.PlanName, waiting.UpgradeRequestedTo));
        Assert.NotNull(waiting.UpgradeRequestedAt);

        CustomerListItem calm = list.Single(c => c.CustomerId == calmId);
        Assert.Equal("Elite", calm.PlanName);
        Assert.Null(calm.UpgradeRequestedTo);
        Assert.Null(calm.UpgradeRequestedAt);

        Assert.Equal("Trial", list.Single(c => c.CustomerId == trialId).PlanName);
        Assert.Equal("Professional", list.Single(c => c.CustomerId == standardId).PlanName);

        CustomerListItem none = list.Single(c => c.CustomerId == unlicensed.CustomerId);
        Assert.Null(none.PlanName);
        Assert.Null(none.UpgradeRequestedTo);
    }

    [SkippableFact]
    public async Task SWITCH_11_a_closed_request_stops_showing_on_the_list()
    {
        Skip.If(_admin.SkipReason is not null, _admin.SkipReason ?? string.Empty);

        await using AdminDbContext db = _admin.CreateContext();
        (Guid customerId, Guid orgId, List<Guid> userIds) = await PlanLimitTests.SeedAsync(db, LicenseType.Pro, seated: 1);
        PlanRepository plans = PlanLimitTests.Plans(db, new RecordingEmailSender());
        await plans.RequestUpgradeAsync(customerId, orgId, userIds[0], new UpgradeRequestInput(), default);

        Assert.Equal("Elite", (await Listing(db).ListAsync(default)).Single(c => c.CustomerId == customerId).UpgradeRequestedTo);

        await plans.SwitchPlanAsync(customerId, LicenseType.Elite, default);

        CustomerListItem after = (await Listing(db).ListAsync(default)).Single(c => c.CustomerId == customerId);
        Assert.Equal("Elite", after.PlanName);
        Assert.Null(after.UpgradeRequestedTo);
    }
}
