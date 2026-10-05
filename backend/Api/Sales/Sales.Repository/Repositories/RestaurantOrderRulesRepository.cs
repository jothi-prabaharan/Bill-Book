using Sales.Entity.Enums;
using Sales.Entity.TableEntities;
using Shared.Kernel.Restaurant;

namespace Sales.Repository.Repositories;

/// <summary>
/// The order rules (TK-146), pure: what the branch's effective restaurant
/// settings demand of an order, and what an order is worth before the bill.
/// </summary>
public static class RestaurantOrderRulesRepository
{
    /// <summary>The table an order keeps: none while tables are switched off, whatever was sent.</summary>
    public static long? EffectiveTable(RestaurantSettingsView settings, long? tableId) =>
        settings.TablesEnabled ? tableId : null;

    /// <summary>
    /// The first thing the settings demand that the order lacks, or null. A
    /// table is asked for only while tables are on and the level covers the
    /// order type; a customer by its own level; a waiter by its switch.
    /// </summary>
    public static string? RequirementRefusal(RestaurantSettingsView settings, OrderType orderType, long? tableId, long? contactId, Guid? waiterUserId)
    {
        if (settings.TablesEnabled && settings.IsTableRequired(orderType) && tableId is null)
        {
            return orderType == OrderType.DineIn
                ? "Choose a table for this dine-in order."
                : "This branch takes every order against a table. Choose one, or take it as dine-in.";
        }

        if (settings.IsCustomerRequired(orderType) && contactId is null)
        {
            return "Choose the customer for this order.";
        }

        return settings.WaiterRequired && waiterUserId is null
            ? "Choose the waiter taking this order."
            : null;
    }

    /// <summary>An order's worth before tax and service charge: every line still to be served or billed.</summary>
    public static decimal Total(IEnumerable<RestaurantOrderLine> lines) =>
        lines.Where(l => l.KitchenStatus != KitchenStatus.Cancelled).Sum(l => Math.Round(l.Quantity * l.UnitPrice, 2));

    /// <summary>Why a line cannot be changed or removed any more, or null.</summary>
    public static string? LineLockedRefusal(RestaurantOrderLine line) => line.KitchenStatus switch
    {
        KitchenStatus.NotSent => null,
        KitchenStatus.Cancelled => "This dish was cancelled.",
        _ => "This dish has gone to the kitchen. Cancel it on the kitchen ticket instead.",
    };
}
