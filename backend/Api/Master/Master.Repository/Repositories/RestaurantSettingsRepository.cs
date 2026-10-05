using Master.Entity.Models;
using Shared.Kernel.Restaurant;
using Shared.Kernel.Tenancy;

namespace Master.Repository.Repositories;

/// <summary>
/// A Restaurant branch's settings (D-39, TK-143), kept as typed keys in
/// <c>mst.Configuration</c>.
///
/// Reads go through <see cref="ConfigurationRepository.ListAsync"/>, the same
/// branch-override-else-system-default fallback every other setting uses, and
/// come back as one effective <see cref="RestaurantSettingsView"/> built by
/// <see cref="RestaurantSettingsRules.Effective"/>. Writes check each value
/// against its key's type before anything is stored.
/// </summary>
public sealed class RestaurantSettingsRepository
{
    private readonly ITenantContext _tenant;
    private readonly ConfigurationRepository _config;

    public RestaurantSettingsRepository(ITenantContext tenant, ConfigurationRepository config)
    {
        _tenant = tenant;
        _config = config;
    }

    public async Task<RestaurantSettingsView> GetSettingsAsync(CancellationToken ct = default)
    {
        Guid orgId = _tenant.OrgId ?? throw new InvalidOperationException("No branch is set for the restaurant settings.");

        IReadOnlyList<ConfigurationDto> all = await _config.ListAsync(orgId, ct);
        Dictionary<RestaurantSettingKey, string?> stored = [];
        foreach (var row in all)
        {
            if (RestaurantSettingKeys.TryParse(row.Code, out RestaurantSettingKey key))
            {
                stored[key] = row.Value;
            }
        }

        return RestaurantSettingsRules.Effective(stored);
    }

    /// <summary>
    /// Stores every key the request carries, after checking all of them: one bad
    /// value refuses the lot, so a page never saves half a form. Returns the
    /// refusal, or null and the new effective view.
    /// </summary>
    public async Task<(string? Refusal, RestaurantSettingsView? Settings)> UpdateSettingsAsync(
        SaveRestaurantSettingsRequest request, CancellationToken ct = default)
    {
        Guid orgId = _tenant.OrgId ?? throw new InvalidOperationException("No branch is set for the restaurant settings.");

        Dictionary<RestaurantSettingKey, string> values = new()
        {
            [RestaurantSettingKey.TablesEnabled] = Flag(request.TablesEnabled),
            [RestaurantSettingKey.KitchenEnabled] = Flag(request.KitchenEnabled),
            [RestaurantSettingKey.RecipesEnabled] = Flag(request.RecipesEnabled),
            [RestaurantSettingKey.TableRequired] = request.TableRequired.ToString(),
            [RestaurantSettingKey.KotRequired] = Flag(request.KotRequired),
            [RestaurantSettingKey.CustomerRequired] = request.CustomerRequired.ToString(),
            [RestaurantSettingKey.WaiterRequired] = Flag(request.WaiterRequired),
            [RestaurantSettingKey.ServiceChargePercent] = request.ServiceChargePercent.ToString(System.Globalization.CultureInfo.InvariantCulture),
            [RestaurantSettingKey.TipsEnabled] = Flag(request.TipsEnabled),
            [RestaurantSettingKey.LoyaltyEarnRate] = request.LoyaltyEarnRate.ToString(System.Globalization.CultureInfo.InvariantCulture),
        };

        Dictionary<RestaurantSettingKey, string> normalised = [];
        foreach ((RestaurantSettingKey key, string value) in values)
        {
            (string? ok, string? refusal) = RestaurantSettingKeys.Normalise(key, value);
            if (refusal is not null)
            {
                return (refusal, null);
            }

            normalised[key] = ok!;
        }

        foreach ((RestaurantSettingKey key, string value) in normalised)
        {
            if (!await _config.SetAsync(orgId, key.Code(), value, ct))
            {
                // The system default row is seeded by migration; its absence is a
                // deployment fault, not something the person can fix.
                throw new InvalidOperationException($"The system default for {key.Code()} is missing.");
            }
        }

        return (null, await GetSettingsAsync(ct));
    }

    private static string Flag(bool value) => value ? "true" : "false";
}
