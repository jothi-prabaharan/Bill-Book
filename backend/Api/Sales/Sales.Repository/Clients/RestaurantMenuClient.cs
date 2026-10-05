using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Shared.Kernel.Restaurant;
using Shared.Kernel.Tenancy;

namespace Sales.Repository.Clients;

/// <summary>A dish line to price, as Inventory's menu takes it (TK-144).</summary>
public sealed record MenuLineToPrice(long ItemId, long? ItemVariantId, IReadOnlyList<long> ModifierIds);

public sealed class MenuPricedModifier
{
    public long ModifierId { get; set; }

    public string Name { get; set; } = string.Empty;

    public decimal PriceChange { get; set; }

    public long? LinkedItemId { get; set; }

    public decimal? LinkedQuantity { get; set; }
}

public sealed class MenuPricedLine
{
    public long ItemId { get; set; }

    public string ItemName { get; set; } = string.Empty;

    public long? ItemVariantId { get; set; }

    public string? VariantName { get; set; }

    public decimal PortionFactor { get; set; } = 1m;

    public MenuStation Station { get; set; }

    public decimal UnitPrice { get; set; }

    public List<MenuPricedModifier> Modifiers { get; set; } = [];

    public long? TaxGroupId { get; set; }

    public long SalesUomId { get; set; }

    public bool IsPriceInclusive { get; set; }

    public string TaxPreference { get; set; } = "Taxable";

    public bool TrackInventory { get; set; }
}

/// <summary>
/// The menu's answer: every line priced, or the menu's refusal and the line it
/// is for, or neither when Inventory could not be reached.
/// </summary>
public sealed record MenuPricing(IReadOnlyList<MenuPricedLine>? Lines, int? RefusedLine, string? Refusal)
{
    public bool Unavailable => Lines is null && Refusal is null;
}

/// <summary>Prices order lines on Inventory's menu (TK-146): the price, portion, station and choices of each.</summary>
public interface IRestaurantMenuClient
{
    Task<MenuPricing> PriceAsync(OrderType orderType, DateTime at, IReadOnlyList<MenuLineToPrice> lines, CancellationToken ct);
}

public sealed class RestaurantMenuClient : IRestaurantMenuClient
{
    private readonly HttpClient _http;
    private readonly ITenantContext _tenant;
    private readonly ILogger<RestaurantMenuClient> _log;

    public RestaurantMenuClient(HttpClient http, ITenantContext tenant, ILogger<RestaurantMenuClient> log)
    {
        _http = http;
        _tenant = tenant;
        _log = log;
    }

    public async Task<MenuPricing> PriceAsync(OrderType orderType, DateTime at, IReadOnlyList<MenuLineToPrice> lines, CancellationToken ct)
    {
        (Guid customerId, Guid orgId) = _tenant.Require();
        try
        {
            HttpResponseMessage response = await _http.PostAsJsonAsync("internal/restaurant/menu/price", new
            {
                customerId,
                orgId,
                orderType,
                at,
                lines = lines.Select(l => new { l.ItemId, l.ItemVariantId, l.ModifierIds }),
            }, ct);

            if (response.StatusCode == HttpStatusCode.UnprocessableEntity)
            {
                Refusal? refusal = await response.Content.ReadFromJsonAsync<Refusal>(ct);
                return new MenuPricing(null, refusal?.LineIndex, refusal?.Message ?? "A dish could not be priced.");
            }

            response.EnsureSuccessStatusCode();
            Priced? priced = await response.Content.ReadFromJsonAsync<Priced>(ct);
            return new MenuPricing(priced?.Lines ?? [], null, null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _log.LogWarning(ex, "Pricing {Count} order line(s) on the menu failed for organization {OrgId}", lines.Count, orgId);
            return new MenuPricing(null, null, null);
        }
    }

    private sealed class Priced
    {
        public List<MenuPricedLine> Lines { get; set; } = [];
    }

    private sealed class Refusal
    {
        public int LineIndex { get; set; }

        public string Message { get; set; } = string.Empty;
    }
}
