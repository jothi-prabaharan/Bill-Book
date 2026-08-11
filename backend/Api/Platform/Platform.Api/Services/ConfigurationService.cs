using Microsoft.EntityFrameworkCore;
using Platform.Entity.Models;
using Platform.Entity.TableEntities;
using Platform.Repository;

namespace Platform.Api.Services;

/// <summary>
/// Effective configuration for an organization: the org's override when present,
/// otherwise the system default row (OrgId null). Keys themselves are seed data —
/// this service edits values only, it never adds or removes keys.
/// </summary>
public sealed class ConfigurationService
{
    private readonly PlatformDbContext _db;

    public ConfigurationService(PlatformDbContext db) => _db = db;

    public async Task<IReadOnlyList<ConfigurationDto>> ListAsync(Guid orgId, CancellationToken ct)
    {
        List<Configuration> defaults = await _db.Configurations
            .Where(c => c.OrgId == null)
            .ToListAsync(ct);
        List<Configuration> overrides = await _db.Configurations
            .Where(c => c.OrgId == orgId)
            .ToListAsync(ct);

        bool locked = await HasTradedAsync(orgId, ct);

        return defaults
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
                    IsOneTime = d.IsOneTime,
                    IsLocked = d.IsOneTime && locked,
                };
            })
            .OrderBy(c => c.Category)
            .ThenBy(c => c.Name)
            .ToList();
    }

    /// <summary>
    /// Sets an org's value for a key, creating the override row on first write.
    /// Unknown keys are rejected — a value nothing reads is dead data.
    ///
    /// A one-time key is refused once the branch has traded. Returns
    /// <see cref="SetConfigurationOutcome.Locked"/> rather than a bare false so
    /// the caller can say why, since "unknown key" and "too late" want different
    /// answers on screen.
    /// </summary>
    public async Task<SetConfigurationOutcome> SetAsync(
        Guid orgId, string code, string value, CancellationToken ct)
    {
        Configuration? systemDefault = await _db.Configurations
            .FirstOrDefaultAsync(c => c.OrgId == null && c.Code == code, ct);
        if (systemDefault is null)
        {
            return SetConfigurationOutcome.UnknownKey;
        }

        if (systemDefault.IsOneTime && await HasTradedAsync(orgId, ct))
        {
            return SetConfigurationOutcome.Locked;
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
        return SetConfigurationOutcome.Ok;
    }

    /// <summary>
    /// Whether this branch has posted its first sales or purchase document.
    ///
    /// <b>Deliberately false for now.</b> Neither service exists yet, so nothing
    /// can have been posted and no one-time setting can be locked — which is the
    /// correct answer today, not a stub returning a convenient one. Point it at
    /// the real query when <c>sal.*</c> and <c>pur.*</c> land (TRANSACTIONS.md
    /// T2.2 and T4.2); it is deliberately the only line that has to change, the
    /// same way <c>HasStockMovementsAsync</c> was.
    /// </summary>
    private Task<bool> HasTradedAsync(Guid orgId, CancellationToken ct) =>
        Task.FromResult(false);

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
