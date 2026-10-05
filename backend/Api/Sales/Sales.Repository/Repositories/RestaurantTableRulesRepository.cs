using Shared.Kernel.Restaurant;

namespace Sales.Repository.Repositories;

/// <summary>The table rules (TK-145), pure: which status may follow which.</summary>
public static class RestaurantTableRulesRepository
{
    /// <summary>
    /// A table's turn runs Free → Occupied → Billed → Cleaning → Free. A
    /// billed table can reopen for one more round, guests can leave before
    /// ordering, and a cleared or cleaned table can go straight back to Free.
    /// </summary>
    public static bool CanChange(TableStatus from, TableStatus to) => (from, to) switch
    {
        _ when from == to => false,
        (TableStatus.Free, TableStatus.Occupied or TableStatus.Cleaning) => true,
        (TableStatus.Occupied, TableStatus.Billed or TableStatus.Free) => true,
        (TableStatus.Billed, TableStatus.Occupied or TableStatus.Cleaning or TableStatus.Free) => true,
        (TableStatus.Cleaning, TableStatus.Free) => true,
        _ => false,
    };

    public static string StatusLabel(TableStatus status) => status switch
    {
        TableStatus.Free => "free",
        TableStatus.Occupied => "occupied",
        TableStatus.Billed => "billed",
        _ => "being cleaned",
    };

    /// <summary>The status a group takes when tables are merged: occupied if anyone is seated at any of them.</summary>
    public static TableStatus MergedStatus(IEnumerable<TableStatus> statuses) =>
        statuses.Any(s => s == TableStatus.Occupied) ? TableStatus.Occupied : TableStatus.Free;
}
