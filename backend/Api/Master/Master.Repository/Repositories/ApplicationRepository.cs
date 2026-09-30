using Master.Repository.Clients;
using Master.Entity.Enums;
using Master.Entity.TableEntities;
using Master.Repository;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Apps;

namespace Master.Repository.Repositories;

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
public sealed class ApplicationRepository
{
    public const int TrialDays = 14;
    public const int TrialMaxUsers = 3;

    private readonly AdminDbContext _db;
    private readonly ITenantSeeder _seeder;
    private readonly TimeProvider _clock;

    public ApplicationRepository(AdminDbContext db, ITenantSeeder seeder, TimeProvider clock)
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

        CustomerEntity? customer = await _db.Customers.FirstOrDefaultAsync(c => c.CustomerId == customerId, ct);
        List<Organization> branches = await _db.Organizations
            .Where(o => o.CustomerId == customerId)
            .ToListAsync(ct);

        if (customer is null || branches.Count == 0)
        {
            return new StartTrialResult(StartTrialOutcome.NotFound, []);
        }

        AddOn newAddOn = app switch
        {
            App.Hrms => AddOn.Hrms,
            App.Payroll => AddOn.Payroll,
            _ => AddOn.None,
        };
        BusinessType? newBusinessType = app switch
        {
            App.School => BusinessType.School,
            App.RetailErp => BusinessType.General,
            _ => null,
        };

        bool alreadyHas = branches.All(b =>
            (newAddOn != AddOn.None && (b.AddOns & newAddOn) == newAddOn) ||
            (newBusinessType != null && b.BusinessType == newBusinessType));

        if (alreadyHas)
        {
            return new StartTrialResult(StartTrialOutcome.AlreadyLicensed, []);
        }

        foreach (var b in branches)
        {
            if (newAddOn != AddOn.None)
            {
                b.AddOns |= newAddOn;
            }
            if (newBusinessType != null)
            {
                b.BusinessType = newBusinessType;
            }
        }

        DateOnly today = DateOnly.FromDateTime(_clock.GetUtcNow().UtcDateTime);

        License? license = await _db.Licenses.FirstOrDefaultAsync(l => l.CustomerId == customerId, ct);
        if (license is null)
        {
            license = new License
            {
                LicenseId = Guid.NewGuid(),
                CustomerId = customerId,
                LicenseType = LicenseType.Trial,
                StartDate = today,
                ExpiryDate = today.AddDays(TrialDays),
                MaxUsers = TrialMaxUsers,
                MaxOrganizations = branches.Count,
                IsActive = true,
            };
            _db.Licenses.Add(license);
        }
        else if (license.ExpiryDate < today)
        {
            license.ExpiryDate = today.AddDays(TrialDays);
            license.IsActive = true;
        }

        // The starter becomes Owner in every branch.
        const int ownerRoleId = 1;
        List<Guid> branchIds = branches.Select(b => b.OrgId).ToList();
        List<Guid> alreadyOwner = await _db.UserOrganizationRoles
            .Where(a => a.UserId == userId && a.RoleId == ownerRoleId && branchIds.Contains(a.OrgId))
            .Select(a => a.OrgId)
            .ToListAsync(ct);

        foreach (Guid orgId in branchIds.Except(alreadyOwner))
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

        List<string> unseeded = [];
        foreach (var b in branches)
        {
            unseeded.AddRange(await _seeder.SeedAsync(customerId, b.OrgId, b.BusinessType, b.AddOns, ct));
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
