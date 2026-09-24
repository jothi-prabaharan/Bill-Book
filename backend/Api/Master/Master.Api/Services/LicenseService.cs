using Microsoft.EntityFrameworkCore;
using Master.Entity.Enums;
using Master.Entity.Models;
using Master.Entity.TableEntities;
using Master.Repository;
using Shared.Kernel.Apps;
using Shared.Kernel.Interfaces;

namespace Master.Api.Services;

public sealed class LicenseService
{
    private readonly AdminDbContext _db;
    private readonly ILogger<LicenseService> _log;

    public LicenseService(AdminDbContext db, ILogger<LicenseService> log)
    {
        _db = db;
        _log = log;
    }

    /// <summary>The customer's licence for one app (TK-43), or null when it holds none.</summary>
    public async Task<LicenseDto?> GetAsync(Guid customerId, CancellationToken ct, App app = App.RetailErp)
    {
        var license = await _db.Licenses
            .Where(l => l.CustomerId == customerId && l.App == app)
            .FirstOrDefaultAsync(ct);

        return license is null ? null : ToDto(license);
    }

    /// <summary>Every licence the customer holds, one per app (TK-43).</summary>
    public async Task<IReadOnlyList<LicenseDto>> ListAsync(Guid customerId, CancellationToken ct)
    {
        List<License> licences = await _db.Licenses
            .Where(l => l.CustomerId == customerId)
            .OrderBy(l => l.App)
            .ToListAsync(ct);

        return licences.Select(ToDto).ToList();
    }

    private static LicenseDto ToDto(License license)
    {
        return new LicenseDto
        {
            App = license.App.ToString(),
            LicenseId = license.LicenseId,
            CustomerId = license.CustomerId,
            LicenseType = license.LicenseType.ToString(),
            StartDate = license.StartDate,
            ExpiryDate = license.ExpiryDate,
            MaxUsers = license.MaxUsers,
            MaxOrganizations = license.MaxOrganizations,
            IsActive = license.IsActive,
            GraceDays = license.GraceDays,
        };
    }

    /// <summary>
    /// Renews the customer's license, extending the ExpiryDate.
    /// Also updates the ExpiryDate for any branches that were tracking the license's previous ExpiryDate.
    /// See Master.md section 5.16.
    /// </summary>
    public async Task<bool> RenewAsync(Guid customerId, DateOnly newExpiryDate, CancellationToken ct, App app = App.RetailErp)
    {
        var license = await _db.Licenses
            .FirstOrDefaultAsync(l => l.CustomerId == customerId && l.App == app, ct);

        if (license is null)
        {
            return false;
        }

        DateOnly oldExpiry = license.ExpiryDate;
        license.ExpiryDate = newExpiryDate;

        // Find all branches that were tracking the old expiry date and move them forward.
        var organizations = await _db.Organizations
            .Where(o => o.CustomerId == customerId)
            .ToListAsync(ct);

        foreach (var org in organizations)
        {
            if (org.ExpiryDate == oldExpiry)
            {
                org.ExpiryDate = newExpiryDate;
            }
        }

        await _db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>
    /// Upgrades the customer's license by increasing the organization entitlement.
    /// Clears the IsTrial flag on a specified trial branch and aligns its ExpiryDate with the license.
    /// See Master.md section 5.19.
    /// </summary>
    public async Task<bool> ClearBranchTrialAsync(Guid customerId, Guid orgId, CancellationToken ct, App app = App.RetailErp)
    {
        var license = await _db.Licenses
            .FirstOrDefaultAsync(l => l.CustomerId == customerId && l.App == app, ct);

        if (license is null)
        {
            return false;
        }

        var org = await _db.Organizations
            .FirstOrDefaultAsync(o => o.CustomerId == customerId && o.OrgId == orgId, ct);

        if (org is null || !org.IsTrial)
        {
            return false;
        }

        org.IsTrial = false;
        org.ExpiryDate = license.ExpiryDate;
        license.MaxOrganizations += 1;

        await _db.SaveChangesAsync(ct);
        return true;
    }
}
