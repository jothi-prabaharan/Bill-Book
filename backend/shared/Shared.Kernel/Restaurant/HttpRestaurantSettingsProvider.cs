using System.Net.Http.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shared.Kernel.Internal;
using Shared.Kernel.Tenancy;

namespace Shared.Kernel.Restaurant;

/// <summary>
/// Master's <c>internal/restaurant/settings</c>, cached per branch for a minute
/// (TK-143) — the shape of <c>HttpTaxRateProvider</c>. Short, because a person
/// who flips a switch expects the next screen to obey it.
/// </summary>
public sealed class HttpRestaurantSettingsProvider : IRestaurantSettingsProvider
{
    private static readonly TimeSpan CacheFor = TimeSpan.FromMinutes(1);

    private readonly HttpClient _http;
    private readonly IMemoryCache _cache;
    private readonly ITenantContext _tenant;
    private readonly ILogger<HttpRestaurantSettingsProvider> _log;

    public HttpRestaurantSettingsProvider(
        HttpClient http, IMemoryCache cache, ITenantContext tenant, ILogger<HttpRestaurantSettingsProvider> log)
    {
        _http = http;
        _cache = cache;
        _tenant = tenant;
        _log = log;
    }

    public async Task<RestaurantSettingsView?> GetSettingsAsync(CancellationToken ct = default)
    {
        if (_tenant.CustomerId is not Guid customerId || _tenant.OrgId is not Guid orgId)
        {
            return null;
        }

        string key = $"restaurant-settings:{customerId:N}:{orgId:N}";
        if (_cache.TryGetValue(key, out RestaurantSettingsView? cached) && cached is not null)
        {
            return cached;
        }

        try
        {
            using HttpResponseMessage response = await _http.PostAsJsonAsync(
                "internal/restaurant/settings", new { customerId, orgId }, ct);

            if (!response.IsSuccessStatusCode)
            {
                _log.LogWarning("Master answered {Status} for the restaurant settings of {OrgId}", (int)response.StatusCode, orgId);
                return null;
            }

            RestaurantSettingsView? settings = await response.Content.ReadFromJsonAsync<RestaurantSettingsView>(ct);
            if (settings is not null)
            {
                _cache.Set(key, settings, CacheFor);
            }

            return settings;
        }
        catch (HttpRequestException ex)
        {
            _log.LogWarning(ex, "Master could not be asked for the restaurant settings of {OrgId}", orgId);
            return null;
        }
    }
}

public static class RestaurantSettingsRegistration
{
    /// <summary>Registers <see cref="IRestaurantSettingsProvider"/> against Master, carrying the internal key.</summary>
    public static IServiceCollection AddRestaurantSettings(this IServiceCollection services, IConfiguration config)
    {
        services.AddMemoryCache();
        services.AddTransient<InternalKeyHandler>();
        services.AddHttpClient<IRestaurantSettingsProvider, HttpRestaurantSettingsProvider>(client =>
        {
            client.BaseAddress = new Uri(config["Master:BaseUrl"] ?? "http://localhost:4504/");
        })
        .AddHttpMessageHandler<InternalKeyHandler>();

        return services;
    }
}
