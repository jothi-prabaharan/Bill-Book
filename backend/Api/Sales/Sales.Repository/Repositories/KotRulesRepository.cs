using Sales.Entity.Enums;
using Sales.Entity.TableEntities;
using Shared.Kernel.Restaurant;

namespace Sales.Repository.Repositories;

/// <summary>The kitchen rules (TK-147), pure.</summary>
public static class KotRulesRepository
{
    /// <summary>
    /// A dish moves forward through the kitchen: Sent → Preparing → Ready →
    /// Served. A step may be skipped (a drink goes straight to Ready); nothing
    /// moves back, and a served or cancelled dish moves nowhere.
    /// </summary>
    public static bool CanMove(KitchenStatus from, KitchenStatus to) =>
        to is KitchenStatus.Preparing or KitchenStatus.Ready or KitchenStatus.Served
        && from is KitchenStatus.Sent or KitchenStatus.Preparing or KitchenStatus.Ready
        && to > from;

    /// <summary>
    /// Why a dish cannot be cancelled, or null. A dish the kitchen has not
    /// started can be cancelled by anyone who may void; once it is being made,
    /// only someone who may approve, since the food is already spent.
    /// </summary>
    public static string? CancelRefusal(KitchenStatus status, bool canApprove) => status switch
    {
        KitchenStatus.Sent => null,
        KitchenStatus.Preparing or KitchenStatus.Ready when canApprove => null,
        KitchenStatus.Preparing or KitchenStatus.Ready => "The kitchen has started this dish. Someone who can approve must cancel it.",
        KitchenStatus.Served => "This dish has been served. Take it off the bill instead.",
        KitchenStatus.Cancelled => "This dish is already cancelled.",
        _ => "This dish has not gone to the kitchen. Take it off the order instead.",
    };

    /// <summary>
    /// With kitchen tickets required, an order cannot be billed while any dish
    /// on it has not been sent (TK-147, applied at the bill in TK-148).
    /// </summary>
    public static string? BillingRefusal(RestaurantSettingsView settings, IEnumerable<RestaurantOrderLine> lines) =>
        settings.KotRequired && lines.Any(l => l.KitchenStatus == KitchenStatus.NotSent)
            ? "Send every dish to the kitchen before billing."
            : null;

    public static string StationLabel(MenuStation station) => station.ToString();
}
