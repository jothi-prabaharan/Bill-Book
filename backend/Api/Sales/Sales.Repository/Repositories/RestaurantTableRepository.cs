using Microsoft.EntityFrameworkCore;
using Sales.Entity.Enums;
using Sales.Entity.Models;
using Sales.Entity.TableEntities;
using Shared.Kernel.Persistence;
using Shared.Kernel.Restaurant;

namespace Sales.Repository.Repositories;

/// <summary>
/// Floors and tables (TK-145): the floor plan, a table's status through its
/// turn, and moving, merging and splitting tables. A merged group is led by
/// its head table, and everything a group does is done on the head.
/// </summary>
public sealed class RestaurantTableRepository
{
    private const int Gap = 10;

    private readonly SalesDbContext _db;

    public RestaurantTableRepository(SalesDbContext db) => _db = db;

    // ---- Floor plan ----

    public async Task<FloorPlanView> FloorPlanAsync(bool includeInactive, CancellationToken ct)
    {
        List<Floor> floors = await _db.Floors
            .Where(f => includeInactive || f.IsActive)
            .OrderBy(f => f.DisplayOrder).ThenBy(f => f.Name)
            .ToListAsync(ct);
        List<RestaurantTable> tables = await _db.RestaurantTables
            .Where(t => includeInactive || t.IsActive)
            .OrderBy(t => t.DisplayOrder).ThenBy(t => t.Code)
            .ToListAsync(ct);

        Dictionary<long, string> codes = tables.ToDictionary(t => t.RestaurantTableId, t => t.Code);
        Dictionary<long, long> openOrders = await _db.RestaurantOrders
            .Where(o => o.Status == RestaurantOrderStatus.Open && o.RestaurantTableId != null)
            .ToDictionaryAsync(o => o.RestaurantTableId!.Value, o => o.RestaurantOrderId, ct);
        ILookup<long, string> members = tables
            .Where(t => t.MergedIntoTableId is not null)
            .ToLookup(t => t.MergedIntoTableId!.Value, t => t.Code);

        return new FloorPlanView
        {
            Floors = floors.Select(f => new FloorView
            {
                FloorId = f.FloorId,
                Name = f.Name,
                DisplayOrder = f.DisplayOrder,
                IsActive = f.IsActive,
                Tables = tables.Where(t => t.FloorId == f.FloorId).Select(t => new RestaurantTableView
                {
                    RestaurantTableId = t.RestaurantTableId,
                    FloorId = t.FloorId,
                    Code = t.Code,
                    Seats = t.Seats,
                    Status = t.Status,
                    MergedIntoTableId = t.MergedIntoTableId,
                    MergedIntoCode = t.MergedIntoTableId is long head ? codes.GetValueOrDefault(head) : null,
                    MergedCodes = [.. members[t.RestaurantTableId]],
                    IsActive = t.IsActive,
                    OpenOrderId = openOrders.TryGetValue(t.MergedIntoTableId ?? t.RestaurantTableId, out long orderId) ? orderId : null,
                }).ToList(),
            }).ToList(),
        };
    }

    // ---- Floors ----

    public async Task<TableResult<long>> SaveFloorAsync(long? floorId, SaveFloorRequest request, CancellationToken ct)
    {
        string name = request.Name.Trim();
        if (await _db.Floors.AnyAsync(f => f.Name == name && f.FloorId != (floorId ?? 0), ct))
        {
            return TableResult<long>.Refuse("A floor with that name already exists.");
        }

        Floor? floor;
        if (floorId is null)
        {
            int highest = await _db.Floors.Select(f => (int?)f.DisplayOrder).MaxAsync(ct) ?? 0;
            floor = new Floor { DisplayOrder = highest + Gap };
            _db.Floors.Add(floor);
        }
        else
        {
            floor = await _db.Floors.FirstOrDefaultAsync(f => f.FloorId == floorId, ct);
            if (floor is null)
            {
                return TableResult<long>.Missing();
            }

            if (!request.IsActive && floor.IsActive
                && await _db.RestaurantTables.AnyAsync(t => t.FloorId == floorId && t.IsActive && t.Status != TableStatus.Free, ct))
            {
                return TableResult<long>.Refuse("Guests are seated on this floor. Clear its tables before switching it off.");
            }
        }

        floor.Name = name;
        floor.IsActive = request.IsActive;
        await _db.SaveChangesAsync(ct);
        return TableResult<long>.Done(floor.FloorId);
    }

    public async Task<TableResult<bool>> DeleteFloorAsync(long floorId, CancellationToken ct)
    {
        Floor? floor = await _db.Floors.FirstOrDefaultAsync(f => f.FloorId == floorId, ct);
        if (floor is null)
        {
            return TableResult<bool>.Missing();
        }

        if (await _db.RestaurantTables.AnyAsync(t => t.FloorId == floorId, ct))
        {
            return TableResult<bool>.Refuse("Move or delete this floor's tables first, or switch the floor off.");
        }

        _db.Floors.Remove(floor);
        await _db.SaveChangesAsync(ct);
        return TableResult<bool>.Done(true);
    }

    /// <summary>A new Restaurant branch starts with one floor to put tables on. Re-runnable; other trades get none.</summary>
    public async Task<int> SeedFloorAsync(Guid orgId, string vertical, CancellationToken ct)
    {
        if (!string.Equals(vertical, "Restaurant", StringComparison.OrdinalIgnoreCase)
            || await _db.Floors.IgnoreQueryFilters().AnyAsync(f => f.OrgId == orgId, ct))
        {
            return 0;
        }

        _db.Floors.Add(new Floor { OrgId = orgId, Name = "Main floor", DisplayOrder = Gap, IsActive = true });
        await _db.SaveChangesAsync(ct);
        return 1;
    }

    // ---- Tables ----

    public async Task<TableResult<long>> SaveTableAsync(long? tableId, SaveRestaurantTableRequest request, CancellationToken ct)
    {
        string code = request.Code.Trim().ToUpperInvariant();
        if (!await _db.Floors.AnyAsync(f => f.FloorId == request.FloorId, ct))
        {
            return TableResult<long>.Refuse("Choose a floor that exists.");
        }

        if (await _db.RestaurantTables.AnyAsync(t => t.Code == code && t.RestaurantTableId != (tableId ?? 0), ct))
        {
            return TableResult<long>.Refuse($"Table {code} already exists.");
        }

        RestaurantTable? table;
        if (tableId is null)
        {
            int highest = await _db.RestaurantTables.Where(t => t.FloorId == request.FloorId).Select(t => (int?)t.DisplayOrder).MaxAsync(ct) ?? 0;
            table = new RestaurantTable { DisplayOrder = highest + Gap, Status = TableStatus.Free };
            _db.RestaurantTables.Add(table);
        }
        else
        {
            table = await _db.RestaurantTables.FirstOrDefaultAsync(t => t.RestaurantTableId == tableId, ct);
            if (table is null)
            {
                return TableResult<long>.Missing();
            }

            if (!request.IsActive && (table.Status != TableStatus.Free || await InGroupAsync(table, ct)))
            {
                return TableResult<long>.Refuse($"Table {table.Code} is in use. Free it, and split it from any group, before switching it off.");
            }
        }

        table.FloorId = request.FloorId;
        table.Code = code;
        table.Seats = request.Seats;
        table.IsActive = request.IsActive;
        await _db.SaveChangesAsync(ct);
        return TableResult<long>.Done(table.RestaurantTableId);
    }

    public async Task<TableResult<bool>> DeleteTableAsync(long tableId, CancellationToken ct)
    {
        RestaurantTable? table = await _db.RestaurantTables.FirstOrDefaultAsync(t => t.RestaurantTableId == tableId, ct);
        if (table is null)
        {
            return TableResult<bool>.Missing();
        }

        if (table.Status != TableStatus.Free || await InGroupAsync(table, ct))
        {
            return TableResult<bool>.Refuse($"Table {table.Code} is in use. Free it, and split it from any group, first.");
        }

        // Past orders name the table; deleting it would leave them pointing at nothing.
        if (await _db.RestaurantOrders.AnyAsync(o => o.RestaurantTableId == tableId, ct))
        {
            return TableResult<bool>.Refuse($"Table {table.Code} has orders. Switch it off instead.");
        }

        _db.RestaurantTables.Remove(table);
        await _db.SaveChangesAsync(ct);
        return TableResult<bool>.Done(true);
    }

    /// <summary>
    /// Moves a table to the next stage of its turn. A merged table follows its
    /// head, so the change is made on the head and the whole group moves with it.
    /// </summary>
    public async Task<TableResult<TableStatus>> SetStatusAsync(long tableId, TableStatus status, CancellationToken ct)
    {
        await using ITransactionScope tx = await _db.Database.BeginScopeAsync(ct);

        RestaurantTable? table = await _db.RestaurantTables.FirstOrDefaultAsync(t => t.RestaurantTableId == tableId, ct);
        if (table is null)
        {
            return TableResult<TableStatus>.Missing();
        }

        if (table.MergedIntoTableId is not null)
        {
            string head = await CodeOfAsync(table.MergedIntoTableId.Value, ct);
            return TableResult<TableStatus>.Refuse($"Table {table.Code} is part of {head}'s group. Change it on table {head}.");
        }

        if (!RestaurantTableRulesRepository.CanChange(table.Status, status))
        {
            return TableResult<TableStatus>.Refuse(
                $"Table {table.Code} is {RestaurantTableRulesRepository.StatusLabel(table.Status)} and cannot become {RestaurantTableRulesRepository.StatusLabel(status)}.");
        }

        if (status is TableStatus.Free or TableStatus.Cleaning && await OpenOrderNoAsync(table.RestaurantTableId, ct) is string open)
        {
            return TableResult<TableStatus>.Refuse($"Table {table.Code} has order {open} open. Bill or cancel it first.");
        }

        List<RestaurantTable> group = await GroupOfAsync(table.RestaurantTableId, ct);
        foreach (RestaurantTable t in group)
        {
            t.Status = status;
        }

        // A group that is free again comes apart: the next party may not need every table.
        if (status == TableStatus.Free)
        {
            foreach (RestaurantTable member in group.Where(t => t.RestaurantTableId != table.RestaurantTableId))
            {
                member.MergedIntoTableId = null;
            }
        }

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return TableResult<TableStatus>.Done(status);
    }

    /// <summary>
    /// Guests move from one table to another: the target takes the source's
    /// status and any tables merged with it, and the source is free.
    /// </summary>
    public async Task<TableResult<long>> MoveAsync(long fromTableId, long toTableId, CancellationToken ct)
    {
        if (fromTableId == toTableId)
        {
            return TableResult<long>.Refuse("Choose a different table to move to.");
        }

        await using ITransactionScope tx = await _db.Database.BeginScopeAsync(ct);

        RestaurantTable? from = await _db.RestaurantTables.FirstOrDefaultAsync(t => t.RestaurantTableId == fromTableId, ct);
        RestaurantTable? to = await _db.RestaurantTables.FirstOrDefaultAsync(t => t.RestaurantTableId == toTableId, ct);
        if (from is null || to is null)
        {
            return TableResult<long>.Missing();
        }

        if (from.MergedIntoTableId is not null)
        {
            string head = await CodeOfAsync(from.MergedIntoTableId.Value, ct);
            return TableResult<long>.Refuse($"Table {from.Code} is part of {head}'s group. Move the group from table {head}, or split it first.");
        }

        if (from.Status is not (TableStatus.Occupied or TableStatus.Billed))
        {
            return TableResult<long>.Refuse($"No one is seated at table {from.Code}.");
        }

        if (!to.IsActive || to.Status != TableStatus.Free || to.MergedIntoTableId is not null || await HeadsGroupAsync(to.RestaurantTableId, ct))
        {
            return TableResult<long>.Refuse($"Table {to.Code} is not free.");
        }

        to.Status = from.Status;
        from.Status = TableStatus.Free;
        foreach (RestaurantTable member in await _db.RestaurantTables.Where(t => t.MergedIntoTableId == from.RestaurantTableId).ToListAsync(ct))
        {
            member.MergedIntoTableId = to.RestaurantTableId;
        }

        // The party's open order goes with them.
        foreach (RestaurantOrder order in await _db.RestaurantOrders.Where(o => o.RestaurantTableId == from.RestaurantTableId && o.Status == RestaurantOrderStatus.Open).ToListAsync(ct))
        {
            order.RestaurantTableId = to.RestaurantTableId;
        }

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return TableResult<long>.Done(to.RestaurantTableId);
    }

    /// <summary>
    /// Pushes tables together under a head table. Every table joining must be
    /// active, free or occupied, and in no other group; the group is occupied
    /// if anyone is seated at any of them.
    /// </summary>
    public async Task<TableResult<long>> MergeAsync(long headTableId, IReadOnlyCollection<long> tableIds, CancellationToken ct)
    {
        List<long> joining = tableIds.Where(id => id != headTableId).Distinct().ToList();
        if (joining.Count == 0)
        {
            return TableResult<long>.Refuse("Choose at least one other table to join.");
        }

        await using ITransactionScope tx = await _db.Database.BeginScopeAsync(ct);

        RestaurantTable? head = await _db.RestaurantTables.FirstOrDefaultAsync(t => t.RestaurantTableId == headTableId, ct);
        if (head is null)
        {
            return TableResult<long>.Missing();
        }

        if (head.MergedIntoTableId is not null)
        {
            string headOfHead = await CodeOfAsync(head.MergedIntoTableId.Value, ct);
            return TableResult<long>.Refuse($"Table {head.Code} is already part of {headOfHead}'s group. Join tables to {headOfHead} instead.");
        }

        if (!head.IsActive || head.Status is not (TableStatus.Free or TableStatus.Occupied))
        {
            return TableResult<long>.Refuse($"Table {head.Code} is {RestaurantTableRulesRepository.StatusLabel(head.Status)}; only a free or occupied table can take more.");
        }

        List<RestaurantTable> others = await _db.RestaurantTables.Where(t => joining.Contains(t.RestaurantTableId)).ToListAsync(ct);
        if (others.Count != joining.Count)
        {
            return TableResult<long>.Missing();
        }

        foreach (RestaurantTable t in others)
        {
            if (!t.IsActive || t.Status is not (TableStatus.Free or TableStatus.Occupied))
            {
                return TableResult<long>.Refuse($"Table {t.Code} is {RestaurantTableRulesRepository.StatusLabel(t.Status)} and cannot be joined.");
            }

            if (t.MergedIntoTableId is not null && t.MergedIntoTableId != head.RestaurantTableId || await HeadsGroupAsync(t.RestaurantTableId, ct))
            {
                return TableResult<long>.Refuse($"Table {t.Code} is in another group. Split it first.");
            }
        }

        List<RestaurantTable> group = [head, .. others, .. await _db.RestaurantTables.Where(t => t.MergedIntoTableId == head.RestaurantTableId && !joining.Contains(t.RestaurantTableId)).ToListAsync(ct)];
        TableStatus status = RestaurantTableRulesRepository.MergedStatus(group.Select(t => t.Status));
        foreach (RestaurantTable t in group)
        {
            t.Status = status;
        }

        foreach (RestaurantTable t in others)
        {
            t.MergedIntoTableId = head.RestaurantTableId;
        }

        await CombineOrdersAsync(head.RestaurantTableId, joining, ct);

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return TableResult<long>.Done(head.RestaurantTableId);
    }

    /// <summary>
    /// Takes a table out of its group, or breaks up the group it heads. A table
    /// leaving keeps nothing of the group's order; it is free.
    /// </summary>
    public async Task<TableResult<int>> SplitAsync(long tableId, CancellationToken ct)
    {
        await using ITransactionScope tx = await _db.Database.BeginScopeAsync(ct);

        RestaurantTable? table = await _db.RestaurantTables.FirstOrDefaultAsync(t => t.RestaurantTableId == tableId, ct);
        if (table is null)
        {
            return TableResult<int>.Missing();
        }

        List<RestaurantTable> leaving = table.MergedIntoTableId is not null
            ? [table]
            : await _db.RestaurantTables.Where(t => t.MergedIntoTableId == table.RestaurantTableId).ToListAsync(ct);
        if (leaving.Count == 0)
        {
            return TableResult<int>.Refuse($"Table {table.Code} is not part of a group.");
        }

        foreach (RestaurantTable t in leaving)
        {
            t.MergedIntoTableId = null;
            t.Status = TableStatus.Free;
        }

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return TableResult<int>.Done(leaving.Count);
    }

    // ---- Helpers ----

    /// <summary>
    /// A merged group is ordered and billed as its head table, so the joining
    /// tables' open orders become one: their lines move onto the head's open
    /// order, or the first of them becomes the head's when it has none. An
    /// order emptied this way is marked Merged, naming where its lines went.
    /// </summary>
    private async Task CombineOrdersAsync(long headId, IReadOnlyCollection<long> joiningIds, CancellationToken ct)
    {
        List<RestaurantOrder> orders = await _db.RestaurantOrders
            .Include(o => o.Lines)
            .Where(o => o.Status == RestaurantOrderStatus.Open && o.RestaurantTableId != null
                && (o.RestaurantTableId == headId || joiningIds.Contains(o.RestaurantTableId.Value)))
            .OrderBy(o => o.RestaurantTableId == headId ? 0 : 1).ThenBy(o => o.OpenedAt)
            .ToListAsync(ct);
        if (orders.Count == 0)
        {
            return;
        }

        RestaurantOrder target = orders[0];
        int next = target.Lines.Count == 0 ? 1 : target.Lines.Max(l => l.LineNo) + 1;
        foreach (RestaurantOrder other in orders.Skip(1))
        {
            foreach (RestaurantOrderLine line in other.Lines.OrderBy(l => l.LineNo).ToList())
            {
                line.RestaurantOrderId = target.RestaurantOrderId;
                line.LineNo = next++;
            }

            other.Status = RestaurantOrderStatus.Merged;
            other.MergedIntoOrderId = target.RestaurantOrderId;
            other.RestaurantTableId = null;
        }

        // Free the partial unique index on (table, open) before the head takes the order.
        await _db.SaveChangesAsync(ct);
        target.RestaurantTableId = headId;
    }

    private Task<string?> OpenOrderNoAsync(long headId, CancellationToken ct) =>
        _db.RestaurantOrders
            .Where(o => o.RestaurantTableId == headId && o.Status == RestaurantOrderStatus.Open)
            .Select(o => o.OrderNo)
            .FirstOrDefaultAsync(ct);

    private async Task<List<RestaurantTable>> GroupOfAsync(long headId, CancellationToken ct) =>
        await _db.RestaurantTables.Where(t => t.RestaurantTableId == headId || t.MergedIntoTableId == headId).ToListAsync(ct);

    private Task<bool> HeadsGroupAsync(long tableId, CancellationToken ct) =>
        _db.RestaurantTables.AnyAsync(t => t.MergedIntoTableId == tableId, ct);

    private async Task<bool> InGroupAsync(RestaurantTable table, CancellationToken ct) =>
        table.MergedIntoTableId is not null || await HeadsGroupAsync(table.RestaurantTableId, ct);

    private async Task<string> CodeOfAsync(long tableId, CancellationToken ct) =>
        await _db.RestaurantTables.Where(t => t.RestaurantTableId == tableId).Select(t => t.Code).FirstOrDefaultAsync(ct) ?? "?";
}
