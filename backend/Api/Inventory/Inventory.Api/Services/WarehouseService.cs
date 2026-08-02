using Inventory.Entity.Enums;
using Inventory.Entity.Models;
using Inventory.Entity.TableEntities;
using Inventory.Repository;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Numbering;
using Shared.Kernel.Ordering;

namespace Inventory.Api.Services;

/// <summary>
/// Stock locations. A reporting and movement dimension only — stock is one
/// shared pool and weighted average cost is company-wide, so nothing here
/// partitions inventory.
/// </summary>
public sealed class WarehouseService
{
    private readonly InventoryDbContext _db;
    private readonly INumberGenerator _numbers;
    private readonly TimeProvider _clock;

    public WarehouseService(InventoryDbContext db, INumberGenerator numbers, TimeProvider clock)
    {
        _db = db;
        _numbers = numbers;
        _clock = clock;
    }

    public async Task<IReadOnlyList<WarehouseListItem>> ListAsync(
        bool includeInactive, CancellationToken ct)
    {
        IQueryable<Warehouse> query = includeInactive
            ? _db.Warehouses
            : _db.Warehouses.Where(w => w.IsActive);

        List<Warehouse> rows = await query
            .OrderByDescending(w => w.IsDefault)
            .ThenBy(w => w.DisplayOrder)
            .ThenBy(w => w.WarehouseName)
            .ToListAsync(ct);

        return rows.Select(Map).ToList();
    }

    public async Task<WarehouseListItem?> GetAsync(long warehouseId, CancellationToken ct)
    {
        Warehouse? row = await _db.Warehouses.FirstOrDefaultAsync(w => w.WarehouseId == warehouseId, ct);
        return row is null ? null : Map(row);
    }

    public async Task<SaveMasterOutcome> CreateAsync(SaveWarehouseRequest request, CancellationToken ct)
    {
        if (!TryParse(request, out WarehouseType type, out StorageCondition storage))
        {
            return SaveMasterOutcome.InvalidValue;
        }

        string code = await ResolveCodeAsync(request, ct);
        if (await _db.Warehouses.AnyAsync(w => w.WarehouseCode == code, ct))
        {
            return SaveMasterOutcome.DuplicateCode;
        }

        int highest = await _db.Warehouses.Select(w => (int?)w.DisplayOrder).MaxAsync(ct) ?? 0;

        var warehouse = new Warehouse
        {
            WarehouseCode = code,
            DisplayOrder = highest + Reordering.Gap,
            // The first warehouse must be the default, or items have nowhere to
            // suggest as a location.
            IsDefault = !await _db.Warehouses.AnyAsync(w => w.IsDefault, ct),
        };
        Apply(warehouse, request, type, storage);

        _db.Warehouses.Add(warehouse);
        await _db.SaveChangesAsync(ct);
        return SaveMasterOutcome.Ok;
    }

    public async Task<SaveMasterOutcome> UpdateAsync(
        long warehouseId, SaveWarehouseRequest request, CancellationToken ct)
    {
        Warehouse? warehouse = await _db.Warehouses
            .FirstOrDefaultAsync(w => w.WarehouseId == warehouseId, ct);

        if (warehouse is null)
        {
            return SaveMasterOutcome.NotFound;
        }

        if (!TryParse(request, out WarehouseType type, out StorageCondition storage))
        {
            return SaveMasterOutcome.InvalidValue;
        }

        if (warehouse.IsDefault && !request.IsActive)
        {
            return SaveMasterOutcome.DefaultWarehouseLocked;
        }

        Apply(warehouse, request, type, storage);
        await _db.SaveChangesAsync(ct);
        return SaveMasterOutcome.Ok;
    }

    public async Task<SaveMasterOutcome> SetDefaultAsync(long warehouseId, CancellationToken ct)
    {
        Warehouse? warehouse = await _db.Warehouses
            .FirstOrDefaultAsync(w => w.WarehouseId == warehouseId, ct);

        if (warehouse is null)
        {
            return SaveMasterOutcome.NotFound;
        }

        List<Warehouse> previous = await _db.Warehouses
            .Where(w => w.IsDefault && w.WarehouseId != warehouseId)
            .ToListAsync(ct);

        foreach (Warehouse row in previous)
        {
            row.IsDefault = false;
        }

        warehouse.IsDefault = true;
        warehouse.IsActive = true;
        await _db.SaveChangesAsync(ct);
        return SaveMasterOutcome.Ok;
    }

    /// <summary>
    /// Deactivates. Never deleted — movements name the warehouse they happened
    /// at, and that history has to stay readable.
    /// </summary>
    public async Task<SaveMasterOutcome> DeactivateAsync(long warehouseId, CancellationToken ct)
    {
        Warehouse? warehouse = await _db.Warehouses
            .FirstOrDefaultAsync(w => w.WarehouseId == warehouseId, ct);

        if (warehouse is null)
        {
            return SaveMasterOutcome.NotFound;
        }

        if (warehouse.IsDefault)
        {
            return SaveMasterOutcome.DefaultWarehouseLocked;
        }

        warehouse.IsActive = false;
        await _db.SaveChangesAsync(ct);
        return SaveMasterOutcome.Ok;
    }

    public async Task<SaveMasterOutcome> ReorderAsync(ReorderRequest request, CancellationToken ct)
    {
        List<Warehouse> all = await _db.Warehouses
            .OrderBy(w => w.DisplayOrder)
            .ThenBy(w => w.WarehouseName)
            .ToListAsync(ct);

        if (!Reordering.Apply(all, request, w => w.WarehouseId, w => w.DisplayOrder,
                (w, order) => w.DisplayOrder = order))
        {
            return SaveMasterOutcome.NotFound;
        }

        await _db.SaveChangesAsync(ct);
        return SaveMasterOutcome.Ok;
    }

    private async Task<string> ResolveCodeAsync(SaveWarehouseRequest request, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(request.WarehouseCode))
        {
            return request.WarehouseCode.Trim().ToUpperInvariant();
        }

        DateOnly today = DateOnly.FromDateTime(_clock.GetUtcNow().UtcDateTime);
        NumberAllocation allocation = await _numbers.NextAsync("WAREHOUSE", today, ct);
        return allocation.Code;
    }

    private static void Apply(
        Warehouse warehouse,
        SaveWarehouseRequest request,
        WarehouseType type,
        StorageCondition storage)
    {
        warehouse.WarehouseName = request.WarehouseName.Trim();
        warehouse.WarehouseType = type;
        warehouse.StorageType = storage;
        warehouse.AddressLine1 = request.AddressLine1;
        warehouse.AddressLine2 = request.AddressLine2;
        warehouse.City = request.City;
        warehouse.StateId = request.StateId;
        warehouse.CountryId = request.CountryId;
        warehouse.PostalCode = request.PostalCode;
        warehouse.Gstin = string.IsNullOrWhiteSpace(request.Gstin)
            ? null
            : request.Gstin.Trim().ToUpperInvariant();
        warehouse.ContactPersonName = request.ContactPersonName;
        warehouse.PhoneNumber = request.PhoneNumber;
        warehouse.MobileNumber = request.MobileNumber;
        warehouse.IsActive = request.IsActive;
    }

    private static bool TryParse(
        SaveWarehouseRequest request, out WarehouseType type, out StorageCondition storage) =>
        Enum.TryParse(request.WarehouseType, ignoreCase: true, out type)
        & Enum.TryParse(request.StorageType, ignoreCase: true, out storage);

    private static WarehouseListItem Map(Warehouse w) => new()
    {
        WarehouseId = w.WarehouseId,
        WarehouseCode = w.WarehouseCode,
        WarehouseName = w.WarehouseName,
        WarehouseType = w.WarehouseType.ToString(),
        StorageType = w.StorageType.ToString(),
        AddressLine1 = w.AddressLine1,
        AddressLine2 = w.AddressLine2,
        City = w.City,
        StateId = w.StateId,
        CountryId = w.CountryId,
        PostalCode = w.PostalCode,
        Gstin = w.Gstin,
        ContactPersonName = w.ContactPersonName,
        PhoneNumber = w.PhoneNumber,
        MobileNumber = w.MobileNumber,
        IsDefault = w.IsDefault,
        DisplayOrder = w.DisplayOrder,
        IsActive = w.IsActive,
    };
}
