using Master.Entity.TableEntities;
using Master.Repository.Repositories;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Restaurant;
using Shared.Kernel.Tenancy;
using System.Text.Json.Serialization;

namespace Master.Repository.Repositories;

public class RestaurantSettingsRepository
{
    private readonly ITenantContext _tenantContext;
    private readonly ConfigurationRepository _configRepository;

    public RestaurantSettingsRepository(ITenantContext tenantContext, ConfigurationRepository configRepository)
    {
        _tenantContext = tenantContext;
        _configRepository = configRepository;
    }

    public async Task<RestaurantSettingsView> GetSettingsAsync(CancellationToken ct = default)
    {
        if (_tenantContext.OrgId == null) throw new InvalidOperationException("No OrgId");

        var allConfigs = await _configRepository.ListAsync(_tenantContext.OrgId.Value, ct);
        
        string GetVal(string key) => allConfigs.FirstOrDefault(c => c.Code == key)?.Value ?? "false";

        bool GetBool(string key) => bool.TryParse(GetVal(key), out var b) && b;
        RequirementLevel GetLevel(string key) => Enum.TryParse<RequirementLevel>(GetVal(key), out var level) ? level : RequirementLevel.Off;
        decimal GetDecimal(string key) => decimal.TryParse(GetVal(key), out var d) ? d : 0m;

        var view = new RestaurantSettingsView
        {
            TablesEnabled = GetBool(RestaurantSettingKey.TablesEnabled),
            KitchenEnabled = GetBool(RestaurantSettingKey.KitchenEnabled),
            RecipesEnabled = GetBool(RestaurantSettingKey.RecipesEnabled),
            ServiceChargePercent = GetDecimal(RestaurantSettingKey.ServiceChargePercent),
            TipsEnabled = GetBool(RestaurantSettingKey.TipsEnabled)
        };

        // Enforce dependencies (only apply required if the feature is on)
        view.TableRequired = view.TablesEnabled ? GetLevel(RestaurantSettingKey.TableRequired) : RequirementLevel.Off;
        view.KotRequired = view.KitchenEnabled && GetBool(RestaurantSettingKey.KotRequired);
        
        view.CustomerRequired = GetLevel(RestaurantSettingKey.CustomerRequired);
        view.WaiterRequired = GetBool(RestaurantSettingKey.WaiterRequired);

        return view;
    }

    public async Task UpdateSettingsAsync(RestaurantSettingsView settings, CancellationToken ct = default)
    {
        if (_tenantContext.OrgId == null) throw new InvalidOperationException("No OrgId");
        var orgId = _tenantContext.OrgId.Value;

        var updates = new Dictionary<string, string>
        {
            [RestaurantSettingKey.TablesEnabled] = settings.TablesEnabled.ToString().ToLowerInvariant(),
            [RestaurantSettingKey.KitchenEnabled] = settings.KitchenEnabled.ToString().ToLowerInvariant(),
            [RestaurantSettingKey.RecipesEnabled] = settings.RecipesEnabled.ToString().ToLowerInvariant(),
            [RestaurantSettingKey.TableRequired] = ((int)settings.TableRequired).ToString(),
            [RestaurantSettingKey.KotRequired] = settings.KotRequired.ToString().ToLowerInvariant(),
            [RestaurantSettingKey.CustomerRequired] = ((int)settings.CustomerRequired).ToString(),
            [RestaurantSettingKey.WaiterRequired] = settings.WaiterRequired.ToString().ToLowerInvariant(),
            [RestaurantSettingKey.ServiceChargePercent] = settings.ServiceChargePercent.ToString(),
            [RestaurantSettingKey.TipsEnabled] = settings.TipsEnabled.ToString().ToLowerInvariant()
        };

        foreach (var kvp in updates)
        {
            await _configRepository.SetAsync(orgId, kvp.Key, kvp.Value, ct);
        }
    }
}

