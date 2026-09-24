using Master.Entity.Enums;
using Master.Entity.TableEntities;
using Master.Repository;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Apps;

namespace Master.Api.Services;

/// <summary>
/// Starting another app for a customer that already has one (H0.4, TK-45).
///
/// One customer, one set of branches, one set of users. Starting Payroll does
/// not create a second customer or re-enter anybody: it adds Payroll's trial
/// licence, gives the person starting it Payroll's Owner role in every branch,
/// and seeds what Payroll needs into every existing branch.
///
/// <b>The seeding runs in the request</b>, and a branch that could not be
/// seeded fails the whole request, so the reliability filter rolls the licence
/// and the roles back. Every seed is idempotent, so pressing Start trial again
/// finishes the job rather than doubling anything.
/// </summary>
public sealed class ApplicationService
{
    public const int TrialDays = 14;
    public const int TrialMaxUsers = 3;

    private readonly AdminDbContext _db;
    private readonly ITenantSeeder _seeder;
    private readonly TimeProvider _clock;

    public ApplicationService(AdminDbContext db, ITenantSeeder seeder, TimeProvider clock)
    {
        _db = db;
        _seeder = seeder;
        _clock = clock;
    }

    public async Task<StartTrialResult> StartTrialAsync(
        Guid customerId, Guid userId, App app, CancellationToken ct)
    {
        if (!AppRules.IsSingle(app))
        {
            return new StartTrialResult(StartTrialOutcome.InvalidApp, []);
        }

        List<App> held = await _db.Licenses
            .Where(l => l.CustomerId == customerId)
            .Select(l => l.App)
            .ToListAsync(ct);

        if (held.Contains(app))
        {
            return new StartTrialResult(StartTrialOutcome.AlreadyLicensed, []);
        }

        CustomerEntity? customer = await _db.Customers.FirstOrDefaultAsync(c => c.CustomerId == customerId, ct);
        List<Guid> branches = await _db.Organizations
            .Where(o => o.CustomerId == customerId)
            .Select(o => o.OrgId)
            .ToListAsync(ct);

        if (customer is null || branches.Count == 0)
        {
            return new StartTrialResult(StartTrialOutcome.NotFound, []);
        }

        DateOnly today = DateOnly.FromDateTime(_clock.GetUtcNow().UtcDateTime);

        _db.Licenses.Add(new License
        {
            LicenseId = Guid.NewGuid(),
            CustomerId = customerId,
            App = app,
            LicenseType = LicenseType.Trial,
            StartDate = today,
            ExpiryDate = today.AddDays(TrialDays),
            MaxUsers = TrialMaxUsers,
            // Every branch the customer already has may use the new app: a
            // branch is shared by every app, and the trial is for trying it
            // where the business already trades.
            MaxOrganizations = branches.Count,
            IsActive = true,
        });

        // The starter becomes the new app's Owner in every branch.
        int ownerRoleId = AdminDbContext.OwnerRoleOf(app);
        List<Guid> alreadyOwner = await _db.UserOrganizationRoles
            .Where(a => a.UserId == userId && a.RoleId == ownerRoleId && branches.Contains(a.OrgId))
            .Select(a => a.OrgId)
            .ToListAsync(ct);

        foreach (Guid orgId in branches.Except(alreadyOwner))
        {
            _db.UserOrganizationRoles.Add(new UserOrganizationRole
            {
                UserId = userId,
                OrgId = orgId,
                RoleId = ownerRoleId,
                IsActive = true,
            });
        }

        // An account whose every licence had lapsed is live again with a trial.
        if (customer.Status == TenantStatus.Expired)
        {
            customer.Status = TenantStatus.Trial;
        }

        await _db.SaveChangesAsync(ct);

        // The licence above is not committed yet, so the seeder is told the
        // apps rather than left to read them.
        App apps = held.Aggregate(app, (all, one) => all | one);
        List<string> unseeded = [];
        foreach (Guid orgId in branches)
        {
            unseeded.AddRange(await _seeder.SeedAsync(customerId, orgId, apps, ct));
        }

        return unseeded.Count > 0
            ? new StartTrialResult(StartTrialOutcome.SeedFailed, unseeded.Distinct().ToList())
            : new StartTrialResult(StartTrialOutcome.Ok, []);
    }
}

public enum StartTrialOutcome
{
    Ok = 1,
    AlreadyLicensed = 2,
    InvalidApp = 3,
    NotFound = 4,

    /// <summary>A service could not seed a branch. The request is rolled back and can be retried.</summary>
    SeedFailed = 5,
}

public sealed record StartTrialResult(StartTrialOutcome Outcome, IReadOnlyList<string> UnseededServices);
