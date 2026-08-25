using Microsoft.EntityFrameworkCore;
using Master.Entity.Enums;
using Master.Entity.Models;
using Master.Repository;

namespace Master.Api.Services;

/// <summary>Resolves what Identity needs at login: customer, database readiness, licence.</summary>
public sealed class OrgContextService
{
    private readonly AdminDbContext _db;
    private readonly TimeProvider _clock;

    public OrgContextService(AdminDbContext db, TimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<OrgContextResponse?> ResolveAsync(Guid orgId, CancellationToken ct)
    {
        var row = await (
            from o in _db.Organizations
            join c in _db.Customers on o.CustomerId equals c.CustomerId
            join l in _db.Licenses on c.CustomerId equals l.CustomerId
            join s in _db.States on o.StateId equals s.StateId into sGroup
            from s in sGroup.DefaultIfEmpty()
            where o.OrgId == orgId
            select new
            {
                o.OrgId,
                o.CustomerId,
                OrgName = o.Name,
                o.FinancialYearStartMonth,
                o.BaseCurrency,
                o.DiscountBeforeTax,
                o.Vertical,
                StateCode = s != null ? s.StateCode : null,
                OrgExpiryDate = o.ExpiryDate,
                CustomerStatus = c.Status,
                l.ExpiryDate,
                l.GraceDays,
                l.LicenseType,
                LicenseActive = l.IsActive,
                l.MaxUsers,
            }).FirstOrDefaultAsync(ct);

        if (row is null)
        {
            return null;
        }

        DateOnly today = DateOnly.FromDateTime(_clock.GetUtcNow().UtcDateTime);

        // The branch's own date can only bring access forward, never push it
        // out — otherwise a branch would be a way to outlive the licence paying
        // for it. So the effective date is whichever ends first.
        DateOnly effectiveExpiry =
            row.OrgExpiryDate is DateOnly branchExpiry && branchExpiry < row.ExpiryDate
                ? branchExpiry
                : row.ExpiryDate;

        // Grace is applied once, to whichever date ended access. Applying it to
        // the licence alone would let a wound-down branch keep working past the
        // date it was wound down on.
        bool expired = today > effectiveExpiry.AddDays(row.GraceDays);

        // Expiry blocks the app, never the login — the caller gates on this.
        // That is deliberate for a branch too: a user whose Chennai branch has
        // ended still has to sign in to see why, and to switch to one that
        // has not.
        LicenseStatus licenseStatus = !row.LicenseActive
            ? LicenseStatus.Suspended
            : expired
                ? LicenseStatus.Expired
                : row.LicenseType == LicenseType.Trial
                    ? LicenseStatus.Trial
                    : LicenseStatus.Active;

        // Only the licence's own expiry marks the account expired. A branch that
        // has been wound down says nothing about the account paying for it —
        // stamping the customer here would expire a head office because one
        // seasonal counter reached its end date.
        bool licenceItselfExpired = today > row.ExpiryDate.AddDays(row.GraceDays);

        if (licenceItselfExpired && row.CustomerStatus != TenantStatus.Expired)
        {
            // Stamp lazily at first observation; no nightly job required.
            Entity.TableEntities.Customer customer =
                await _db.Customers.FirstAsync(c => c.CustomerId == row.CustomerId, ct);
            customer.Status = TenantStatus.Expired;
            await _db.SaveChangesAsync(ct);
        }

        return new OrgContextResponse
        {
            OrgId = row.OrgId,
            CustomerId = row.CustomerId,
            OrgName = row.OrgName,
            // No per-customer database to be ready any more — "ready" now means
            // this customer's own seeding finished (Trial/Active) rather than
            // never having started (Provisioning) or having failed (Failed).
            // Suspended and Expired both still count as ready: expiry blocks the
            // app below, never the login itself, per the comment there.
            DatabaseReady = row.CustomerStatus is not (TenantStatus.Provisioning or TenantStatus.Failed),
            LicenseStatus = licenseStatus.ToString(),
            // The date that actually governs, which is the one the user needs to
            // be shown. Reporting the licence date while refusing on the branch
            // date would put a future date on the screen that says access ended.
            LicenseExpiry = effectiveExpiry,
            // Which of the two dates it is. Without this the caller cannot tell
            // "your licence has lapsed, renew it" from "this branch was wound
            // down, talk to your head office" — and telling a paying customer
            // to renew a licence that is perfectly valid is the worse message.
            ExpiryIsBranchLevel = row.OrgExpiryDate is DateOnly branchDate && branchDate < row.ExpiryDate,
            MaxUsers = row.MaxUsers,
            FinancialYearStartMonth = row.FinancialYearStartMonth,
            BaseCurrency = row.BaseCurrency,
            StateCode = row.StateCode,
            DiscountBeforeTax = row.DiscountBeforeTax,
            Vertical = row.Vertical.ToString(),
        };
    }
}
