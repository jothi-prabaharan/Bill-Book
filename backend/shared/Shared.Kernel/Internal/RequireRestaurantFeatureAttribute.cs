using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using Shared.Kernel.Restaurant;

namespace Shared.Kernel.Internal;

/// <summary>The optional parts of a Restaurant branch (D-39): switches, not paid add-ons.</summary>
[Flags]
public enum RestaurantFeature
{
    Tables = 1,
    Kitchen = 2,
    Recipes = 4,
}

/// <summary>
/// Refuses a route whose restaurant feature is switched off for the branch
/// (TK-143), with 409 and a sentence the page can show. It is <b>not</b> an
/// authorisation guard — the route still carries its module permission — and
/// it fails closed: a host that forgot to register the settings provider throws
/// at the first request rather than letting every switch through, and settings
/// Master cannot supply refuse with 503.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class RequireRestaurantFeatureAttribute : Attribute, IAsyncActionFilter
{
    public RequireRestaurantFeatureAttribute(RestaurantFeature features) => Features = features;

    public RestaurantFeature Features { get; }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        IRestaurantSettingsProvider provider = context.HttpContext.RequestServices.GetRequiredService<IRestaurantSettingsProvider>();
        RestaurantSettingsView? settings = await provider.GetSettingsAsync(context.HttpContext.RequestAborted);

        if (settings is null)
        {
            context.Result = new ObjectResult(new { message = "The restaurant settings could not be read. Try again in a moment." })
            {
                StatusCode = StatusCodes.Status503ServiceUnavailable,
            };
            return;
        }

        if (Refusal(Features, settings) is string refused)
        {
            context.Result = new ConflictObjectResult(new { message = refused });
            return;
        }

        await next();
    }

    /// <summary>Why the switches refuse <paramref name="features"/>, or null when they allow it. Pure.</summary>
    public static string? Refusal(RestaurantFeature features, RestaurantSettingsView settings) =>
        features.HasFlag(RestaurantFeature.Tables) && !settings.TablesEnabled
            ? "Tables are switched off for this branch. Turn them on in Settings › Restaurant."
            : features.HasFlag(RestaurantFeature.Kitchen) && !settings.KitchenEnabled
                ? "The kitchen (KOT) is switched off for this branch. Turn it on in Settings › Restaurant."
                : features.HasFlag(RestaurantFeature.Recipes) && !settings.RecipesEnabled
                    ? "Recipes are switched off for this branch. Turn them on in Settings › Restaurant."
                    : null;
}

/// <summary>A branch's effective restaurant settings, from Master. Null when they could not be read.</summary>
public interface IRestaurantSettingsProvider
{
    Task<RestaurantSettingsView?> GetSettingsAsync(CancellationToken ct = default);
}
