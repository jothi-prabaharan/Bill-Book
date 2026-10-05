namespace Sales.Entity.Enums;

/// <summary>Where a restaurant order is (TK-146).</summary>
public enum RestaurantOrderStatus
{
    Open = 1,
    Billed = 2,
    Cancelled = 3,

    /// <summary>Its lines were moved onto another order when its table joined a group.</summary>
    Merged = 4,
}

/// <summary>Where a line is with the kitchen (TK-146, TK-147). Lines start NotSent; the kitchen ticket moves them on.</summary>
public enum KitchenStatus
{
    NotSent = 0,
    Sent = 1,
    Preparing = 2,
    Ready = 3,
    Served = 4,
    Cancelled = 5,
}

/// <summary>The delivery aggregator an order arrived from (TK-167).</summary>
public enum OrderAggregator
{
    Zomato = 1,
    Swiggy = 2,
}
