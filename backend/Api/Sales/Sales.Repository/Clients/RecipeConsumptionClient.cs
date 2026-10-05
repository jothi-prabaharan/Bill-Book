using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Shared.Kernel.Tenancy;

namespace Sales.Repository.Clients;

/// <summary>A billed dish as Inventory's recipes take it (TK-149).</summary>
public sealed record DishToConsume(long ItemId, decimal Quantity, decimal PortionFactor, bool UsesRecipe, IReadOnlyList<(long LinkedItemId, decimal LinkedQuantity)> Choices);

/// <summary>Inventory's answer: done, refused with a sentence, or not reached.</summary>
public sealed record ConsumptionResult(bool Done, string? Refusal)
{
    public bool Unavailable => !Done && Refusal is null;
}

/// <summary>Takes a bill's ingredients out of stock through Inventory's recipes (TK-149). Once per bill.</summary>
public interface IRecipeConsumptionClient
{
    Task<ConsumptionResult> ConsumeAsync(long billId, DateOnly date, IReadOnlyList<DishToConsume> dishes, CancellationToken ct);
}

public sealed class RecipeConsumptionClient : IRecipeConsumptionClient
{
    private readonly HttpClient _http;
    private readonly ITenantContext _tenant;
    private readonly ILogger<RecipeConsumptionClient> _log;

    public RecipeConsumptionClient(HttpClient http, ITenantContext tenant, ILogger<RecipeConsumptionClient> log)
    {
        _http = http;
        _tenant = tenant;
        _log = log;
    }

    public async Task<ConsumptionResult> ConsumeAsync(long billId, DateOnly date, IReadOnlyList<DishToConsume> dishes, CancellationToken ct)
    {
        (Guid customerId, Guid orgId) = _tenant.Require();
        try
        {
            HttpResponseMessage response = await _http.PostAsJsonAsync("internal/restaurant/recipes/consume", new
            {
                customerId,
                orgId,
                billId,
                date,
                dishes = dishes.Select(d => new
                {
                    d.ItemId,
                    d.Quantity,
                    d.PortionFactor,
                    d.UsesRecipe,
                    choices = d.Choices.Select(c => new { c.LinkedItemId, c.LinkedQuantity }),
                }),
            }, ct);

            if (response.StatusCode == HttpStatusCode.Conflict)
            {
                Refused? refused = await response.Content.ReadFromJsonAsync<Refused>(ct);
                return new ConsumptionResult(false, refused?.Message ?? "The bill's ingredients could not be taken out of stock.");
            }

            response.EnsureSuccessStatusCode();
            return new ConsumptionResult(true, null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _log.LogWarning(ex, "Taking bill {BillId}'s ingredients out of stock failed for organization {OrgId}", billId, orgId);
            return new ConsumptionResult(false, null);
        }
    }

    private sealed class Refused
    {
        public string Message { get; set; } = string.Empty;
    }
}
