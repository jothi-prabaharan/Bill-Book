using Master.Repository.Clients;
using Microsoft.EntityFrameworkCore;
using Master.Entity.Models;
using Master.Entity.TableEntities;
using Master.Repository;

namespace Master.Repository.Repositories;

/// <summary>
/// Effective configuration for an organization: the org's override when present,
/// otherwise the system default row (OrgId null). Keys themselves are seed data —
/// this service edits values only, it never adds or removes keys.
/// </summary>
public sealed class ConfigurationRepository
{
    private readonly AdminDbContext _db;

    public ConfigurationRepository(AdminDbContext db) => _db = db;

    public async Task<IReadOnlyList<ConfigurationDto>> ListAsync(Guid orgId, CancellationToken ct)
    {
        var org = await _db.Organizations.AsNoTracking()
            .Where(o => o.OrgId == orgId)
            .Select(o => new { o.BusinessType, o.Vertical })
            .FirstOrDefaultAsync(ct);

        bool isRestaurant = org is not null && (
            org.BusinessType == Shared.Kernel.Apps.BusinessType.Restaurant
            || org.Vertical == Master.Entity.Enums.Vertical.Restaurant);

        List<Configuration> defaults = await _db.Configurations
            .Where(c => c.OrgId == null)
            .ToListAsync(ct);
        List<Configuration> overrides = await _db.Configurations
            .Where(c => c.OrgId == orgId)
            .ToListAsync(ct);

        return defaults
            .Where(d =>
            {
                // UPI ID has moved into Branch Master (Settings › Organization / Branches).
                if (d.Code == OrgContextRepository.UpiIdConfigCode)
                {
                    return false;
                }

                bool isRestaurantConfig = string.Equals(d.Category, "Restaurant", StringComparison.OrdinalIgnoreCase)
                    || d.Code.StartsWith("restaurant.", StringComparison.OrdinalIgnoreCase);

                if (isRestaurantConfig && !isRestaurant)
                {
                    return false;
                }

                return true;
            })
            .Select(d =>
            {
                Configuration? o = overrides.FirstOrDefault(x => x.Code == d.Code);
                return new ConfigurationDto
                {
                    Code = d.Code,
                    Name = d.Name,
                    Description = d.Description,
                    DataType = d.DataType.ToString(),
                    Category = d.Category,
                    DefaultValue = d.Value,
                    Value = o?.Value ?? d.Value,
                    IsOverridden = o is not null,
                };
            })
            .OrderBy(c => c.Category)
            .ThenBy(c => c.Name)
            .ToList();
    }

    /// <summary>
    /// Why a value cannot be saved for a key, or null when it can. Pure. Only
    /// the keys whose value the product reads in a fixed shape are checked:
    /// a UPI id that is not one would print a QR no payments app can use (TK-158).
    /// </summary>
    public static string? ValueRefusal(string code, string? value) =>
        code == OrgContextRepository.UpiIdConfigCode
            && !string.IsNullOrWhiteSpace(value)
            && !Shared.Kernel.Payments.UpiPayment.IsValidId(value)
            ? "That is not a UPI id. It looks like name@bank, for example shop@okhdfcbank."
            : null;

    /// <summary>
    /// Sets an org's value for a key, creating the override row on first write.
    /// Unknown keys are rejected — a value nothing reads is dead data.
    /// </summary>
    public async Task<bool> SetAsync(Guid orgId, string code, string value, CancellationToken ct)
    {
        Configuration? systemDefault = await _db.Configurations
            .FirstOrDefaultAsync(c => c.OrgId == null && c.Code == code, ct);
        if (systemDefault is null)
        {
            return false;
        }

        Configuration? existing = await _db.Configurations
            .FirstOrDefaultAsync(c => c.OrgId == orgId && c.Code == code, ct);

        if (existing is null)
        {
            _db.Configurations.Add(new Configuration
            {
                ConfigId = Guid.NewGuid(),
                OrgId = orgId,
                Code = systemDefault.Code,
                Name = systemDefault.Name,
                Description = systemDefault.Description,
                DataType = systemDefault.DataType,
                Category = systemDefault.Category,
                Value = value,
                IsSystem = false,
            });
        }
        else
        {
            existing.Value = value;
        }

        await _db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>Drops the org override so the key falls back to the system default.</summary>
    public async Task<bool> ResetAsync(Guid orgId, string code, CancellationToken ct)
    {
        Configuration? existing = await _db.Configurations
            .FirstOrDefaultAsync(c => c.OrgId == orgId && c.Code == code, ct);
        if (existing is null)
        {
            return false;
        }

        _db.Configurations.Remove(existing);
        await _db.SaveChangesAsync(ct);
        return true;
    }
}
