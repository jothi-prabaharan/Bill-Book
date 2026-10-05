using System.ComponentModel.DataAnnotations;
using Shared.Kernel.Restaurant;

namespace Master.Entity.Models;

/// <summary>What Settings › Restaurant saves: every key, as the branch wants it stored.</summary>
public sealed class SaveRestaurantSettingsRequest
{
    public bool TablesEnabled { get; set; }
    public bool KitchenEnabled { get; set; }
    public bool RecipesEnabled { get; set; }
    public RequirementLevel TableRequired { get; set; }
    public bool KotRequired { get; set; }
    public RequirementLevel CustomerRequired { get; set; }
    public bool WaiterRequired { get; set; }

    [Range(typeof(decimal), "0", "25", ErrorMessage = "The service charge must be between 0 and 25%.")]
    public decimal ServiceChargePercent { get; set; }

    public bool TipsEnabled { get; set; }
}

/// <summary>A service-to-service call naming the branch it is for.</summary>
public sealed class InternalBranchRequest
{
    public Guid CustomerId { get; set; }

    public Guid OrgId { get; set; }
}
