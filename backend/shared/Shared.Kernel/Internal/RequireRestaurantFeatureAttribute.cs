using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Shared.Kernel.Internal;

[Flags]
public enum RestaurantFeature
{
    Tables = 1,
    Kitchen = 2,
    Recipes = 4
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public class RequireRestaurantFeatureAttribute : ActionFilterAttribute
{
    private readonly RestaurantFeature _features;

    public RequireRestaurantFeatureAttribute(RestaurantFeature features)
    {
        _features = features;
    }

    public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        // Actually, we could use IRestaurantSettingsProvider (which is like ITaxRateProvider)
        // to check if the feature is on. But since we need to inject it:
        var provider = context.HttpContext.RequestServices.GetService(typeof(IRestaurantSettingsProvider)) as IRestaurantSettingsProvider;
        if (provider != null)
        {
            var settings = await provider.GetSettingsAsync();
            if (_features.HasFlag(RestaurantFeature.Tables) && !settings.TablesEnabled)
            {
                context.Result = new BadRequestObjectResult("Table management is disabled for this branch.");
                return;
            }
            if (_features.HasFlag(RestaurantFeature.Kitchen) && !settings.KitchenEnabled)
            {
                context.Result = new BadRequestObjectResult("Kitchen (KOT) is disabled for this branch.");
                return;
            }
            if (_features.HasFlag(RestaurantFeature.Recipes) && !settings.RecipesEnabled)
            {
                context.Result = new BadRequestObjectResult("Recipes are disabled for this branch.");
                return;
            }
        }

        await next();
    }
}

public interface IRestaurantSettingsProvider
{
    Task<Shared.Kernel.Restaurant.RestaurantSettingsView> GetSettingsAsync();
}
