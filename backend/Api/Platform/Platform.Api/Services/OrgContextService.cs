using Microsoft.EntityFrameworkCore;
using Platform.Entity.Enums;
using Platform.Entity.Models;
using Platform.Repository;

namespace Platform.Api.Services;

/// <summary>Resolves what Identity needs at login: customer, database readiness, licence.</summary>
public sealed class OrgContextService
{
    private readonly PlatformDbContext _db;
    private readonly TimeProvider _clock;

    public OrgContextService(PlatformDbContext db, TimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<OrgContextResponse?> ResolveAsync(Guid orgId, CancellationToken ct)
    {
        var row = await (
            from o in _db.Organizations
            join c in _db.Customers on o.CustomerId equals c.CustomerId
            join d in _db.CustomerDatabases on c.CustomerId equals d.CustomerId
            join l in _db.Licenses on c.CustomerId equals l.CustomerId
            where o.OrgId == orgId
            select new
            {
                o.OrgId,
                o.CustomerId,
                OrgName = o.Name,
                o.FinancialYearStartMonth,
                CustomerStatus = c.Status,
                DbStatus = d.Status,
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
        bool expired = today > row.ExpiryDate.AddDays(row.GraceDays);

        // Expiry blocks the app, never the login — the caller gates on this.
        LicenseStatus licenseStatus = !row.LicenseActive
            ? LicenseStatus.Suspended
            : expired
                ? LicenseStatus.Expired
                : row.LicenseType == LicenseType.Trial
                    ? LicenseStatus.Trial
                    : LicenseStatus.Active;

        if (licenseStatus == LicenseStatus.Expired && row.CustomerStatus != TenantStatus.Expired)
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
            DatabaseReady = row.DbStatus == ProvisioningStatus.Ready,
            LicenseStatus = licenseStatus.ToString(),
            LicenseExpiry = row.ExpiryDate,
            MaxUsers = row.MaxUsers,
            FinancialYearStartMonth = row.FinancialYearStartMonth,
        };
    }
}
