using Inventory.Entity.Models;
using Inventory.Entity.TableEntities;
using Inventory.Repository;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Ordering;

namespace Inventory.Api.Services;

/// <summary>
/// Unit types and their units, served together because the screen edits them
/// together.
///
/// The rule that shapes this service: every conversion in the system derives
/// from <c>ConversionToBase</c>, so moving which unit is the base rescales the
/// whole type. That is its own action, and it is refused once any item uses a
/// unit of the type — the rescale would restate quantities already recorded.
/// </summary>
public sealed class UomService
{
    private readonly InventoryDbContext _db;

    public UomService(InventoryDbContext db) => _db = db;

    public async Task<IReadOnlyList<UomTypeListItem>> ListAsync(
        bool includeInactive, CancellationToken ct)
    {
        List<UomType> types = await Types(includeInactive)
            .OrderBy(t => t.DisplayOrder)
            .ThenBy(t => t.UomTypeName)
            .ToListAsync(ct);

        List<UnitOfMeasure> units = await Units(includeInactive)
            .OrderBy(u => u.UomTypeId)
            .ThenBy(u => u.DisplayOrder)
            .ThenBy(u => u.UomCode)
            .ToListAsync(ct);

        // One grouped query rather than a lookup per unit — this list is small
        // but the pattern is what makes the item list survive.
        HashSet<long> usedUnitIds = await UsedUnitIdsAsync(ct);

        return types.Select(t => new UomTypeListItem
        {
            UomTypeId = t.UomTypeId,
            UomTypeSystemName = t.UomTypeSystemName,
            UomTypeName = t.UomTypeName,
            DisplayOrder = t.DisplayOrder,
            IsSystem = t.IsSystem,
            IsActive = t.IsActive,
            Units = units
                .Where(u => u.UomTypeId == t.UomTypeId)
                .Select(u => Map(u, usedUnitIds.Contains(u.UomId)))
                .ToList(),
        }).ToList();
    }

    public async Task<SaveUomOutcome> CreateTypeAsync(SaveUomTypeRequest request, CancellationToken ct)
    {
        if (await _db.UomTypes.AnyAsync(t => t.UomTypeName == request.UomTypeName.Trim(), ct))
        {
            return SaveUomOutcome.DuplicateName;
        }

        int highest = await _db.UomTypes.Select(t => (int?)t.DisplayOrder).MaxAsync(ct) ?? 0;

        _db.UomTypes.Add(new UomType
        {
            UomTypeSystemName = null,
            UomTypeName = request.UomTypeName.Trim(),
            DisplayOrder = highest + Reordering.Gap,
            IsSystem = false,
            IsActive = request.IsActive,
        });

        await _db.SaveChangesAsync(ct);
        return SaveUomOutcome.Ok;
    }

    public async Task<SaveUomOutcome> UpdateTypeAsync(
        long uomTypeId, SaveUomTypeRequest request, CancellationToken ct)
    {
        UomType? type = await _db.UomTypes.FirstOrDefaultAsync(t => t.UomTypeId == uomTypeId, ct);
        if (type is null)
        {
            return SaveUomOutcome.NotFound;
        }

        string name = request.UomTypeName.Trim();
        if (await _db.UomTypes.AnyAsync(t => t.UomTypeName == name && t.UomTypeId != uomTypeId, ct))
        {
            return SaveUomOutcome.DuplicateName;
        }

        // Deactivating a type whose units are on live items would make those
        // units vanish from every picker while the items keep trading in them.
        if (type.IsActive && !request.IsActive && await TypeInUseAsync(uomTypeId, ct))
        {
            return SaveUomOutcome.TypeInUse;
        }

        type.UomTypeName = name;
        type.IsActive = request.IsActive;
        await _db.SaveChangesAsync(ct);
        return SaveUomOutcome.Ok;
    }

    public async Task<SaveUomOutcome> DeleteTypeAsync(long uomTypeId, CancellationToken ct)
    {
        UomType? type = await _db.UomTypes.FirstOrDefaultAsync(t => t.UomTypeId == uomTypeId, ct);
        if (type is null)
        {
            return SaveUomOutcome.NotFound;
        }

        if (type.IsSystem)
        {
            return SaveUomOutcome.SystemRowUndeletable;
        }

        // Deleting would orphan every unit under it, and those units are on items.
        if (await _db.UnitOfMeasures.AnyAsync(u => u.UomTypeId == uomTypeId, ct))
        {
            return SaveUomOutcome.TypeInUse;
        }

        _db.UomTypes.Remove(type);
        await _db.SaveChangesAsync(ct);
        return SaveUomOutcome.Ok;
    }

    public async Task<SaveUomOutcome> CreateUnitAsync(
        SaveUnitOfMeasureRequest request, CancellationToken ct)
    {
        if (!await _db.UomTypes.AnyAsync(t => t.UomTypeId == request.UomTypeId, ct))
        {
            return SaveUomOutcome.NotFound;
        }

        string code = request.UomCode.Trim().ToUpperInvariant();
        if (await _db.UnitOfMeasures.AnyAsync(u => u.UomCode == code, ct))
        {
            return SaveUomOutcome.DuplicateCode;
        }

        bool typeHasUnits = await _db.UnitOfMeasures.AnyAsync(u => u.UomTypeId == request.UomTypeId, ct);

        int highest = await _db.UnitOfMeasures
            .Where(u => u.UomTypeId == request.UomTypeId)
            .Select(u => (int?)u.DisplayOrder)
            .MaxAsync(ct) ?? 0;

        // The first unit of a type has to be its base, or the type has no scale
        // and nothing under it can convert.
        bool isBase = !typeHasUnits;

        _db.UnitOfMeasures.Add(new UnitOfMeasure
        {
            UomTypeId = request.UomTypeId,
            UomSystemName = null,
            UomCode = code,
            UqcCode = request.UqcCode.Trim().ToUpperInvariant(),
            UomName = request.UomName.Trim(),
            IsBaseUnit = isBase,
            ConversionToBase = isBase ? 1m : request.ConversionToBase,
            DecimalPlaces = request.DecimalPlaces,
            DisplayOrder = highest + Reordering.Gap,
            IsSystem = false,
            IsActive = request.IsActive,
        });

        await _db.SaveChangesAsync(ct);
        return SaveUomOutcome.Ok;
    }

    public async Task<SaveUomOutcome> UpdateUnitAsync(
        long uomId, SaveUnitOfMeasureRequest request, CancellationToken ct)
    {
        UnitOfMeasure? unit = await _db.UnitOfMeasures.FirstOrDefaultAsync(u => u.UomId == uomId, ct);
        if (unit is null)
        {
            return SaveUomOutcome.NotFound;
        }

        string code = request.UomCode.Trim().ToUpperInvariant();
        if (await _db.UnitOfMeasures.AnyAsync(u => u.UomCode == code && u.UomId != uomId, ct))
        {
            return SaveUomOutcome.DuplicateCode;
        }

        // The base unit defines the scale; a factor other than 1 on it is
        // meaningless rather than merely wrong.
        if (unit.IsBaseUnit && request.ConversionToBase != 1m)
        {
            return SaveUomOutcome.BaseUnitFactorFixed;
        }

        bool used = (await UsedUnitIdsAsync(ct)).Contains(uomId);

        // Rescaling a unit that items already hold stock in would restate every
        // quantity recorded under it.
        if (used && unit.ConversionToBase != request.ConversionToBase)
        {
            return SaveUomOutcome.UnitInUse;
        }

        if (used && unit.UomTypeId != request.UomTypeId)
        {
            return SaveUomOutcome.UnitInUse;
        }

        unit.UomTypeId = request.UomTypeId;
        unit.UomCode = code;
        unit.UqcCode = request.UqcCode.Trim().ToUpperInvariant();
        unit.UomName = request.UomName.Trim();
        unit.ConversionToBase = unit.IsBaseUnit ? 1m : request.ConversionToBase;
        unit.DecimalPlaces = request.DecimalPlaces;
        unit.IsActive = request.IsActive;

        await _db.SaveChangesAsync(ct);
        return SaveUomOutcome.Ok;
    }

    /// <summary>
    /// Moves the base unit of a type, rescaling every factor in it so the
    /// relationships between units are preserved. Refused once any item uses a
    /// unit of the type.
    /// </summary>
    public async Task<SaveUomOutcome> SetBaseUnitAsync(long uomId, CancellationToken ct)
    {
        UnitOfMeasure? target = await _db.UnitOfMeasures.FirstOrDefaultAsync(u => u.UomId == uomId, ct);
        if (target is null)
        {
            return SaveUomOutcome.NotFound;
        }

        if (target.IsBaseUnit)
        {
            return SaveUomOutcome.Ok;
        }

        List<UnitOfMeasure> siblings = await _db.UnitOfMeasures
            .Where(u => u.UomTypeId == target.UomTypeId)
            .ToListAsync(ct);

        HashSet<long> used = await UsedUnitIdsAsync(ct);
        if (siblings.Any(u => used.Contains(u.UomId)))
        {
            return SaveUomOutcome.BaseChangeBlocked;
        }

        // Every factor is divided by the new base's factor, which leaves each
        // unit's ratio to every other unchanged and puts the new base at 1.
        decimal divisor = target.ConversionToBase;
        foreach (UnitOfMeasure unit in siblings)
        {
            unit.IsBaseUnit = false;
            unit.ConversionToBase = unit.ConversionToBase / divisor;
        }

        target.IsBaseUnit = true;
        target.ConversionToBase = 1m;

        await _db.SaveChangesAsync(ct);
        return SaveUomOutcome.Ok;
    }

    public async Task<SaveUomOutcome> DeleteUnitAsync(long uomId, CancellationToken ct)
    {
        UnitOfMeasure? unit = await _db.UnitOfMeasures.FirstOrDefaultAsync(u => u.UomId == uomId, ct);
        if (unit is null)
        {
            return SaveUomOutcome.NotFound;
        }

        if (unit.IsSystem)
        {
            return SaveUomOutcome.SystemRowUndeletable;
        }

        if ((await UsedUnitIdsAsync(ct)).Contains(uomId))
        {
            return SaveUomOutcome.UnitInUse;
        }

        // A type with units must keep exactly one base. Removing the base would
        // leave the rest with factors relative to nothing.
        if (unit.IsBaseUnit
            && await _db.UnitOfMeasures.AnyAsync(u => u.UomTypeId == unit.UomTypeId && u.UomId != uomId, ct))
        {
            return SaveUomOutcome.BaseUnitRequired;
        }

        _db.UnitOfMeasures.Remove(unit);
        await _db.SaveChangesAsync(ct);
        return SaveUomOutcome.Ok;
    }

    public async Task<SaveUomOutcome> ReorderTypesAsync(ReorderRequest request, CancellationToken ct)
    {
        List<UomType> all = await _db.UomTypes
            .OrderBy(t => t.DisplayOrder)
            .ThenBy(t => t.UomTypeName)
            .ToListAsync(ct);

        if (!Reordering.Apply(all, request, t => t.UomTypeId, t => t.DisplayOrder,
                (t, order) => t.DisplayOrder = order))
        {
            return SaveUomOutcome.NotFound;
        }

        await _db.SaveChangesAsync(ct);
        return SaveUomOutcome.Ok;
    }

    /// <summary>
    /// Writes the unit types and units for an organization, adding only what is
    /// missing. Safe to re-run: a unit added to the seed list later reaches
    /// organizations created before it existed.
    ///
    /// Still two passes, for the original reason — a unit needs its type's
    /// generated id — but the second pass keys off the types the organization
    /// actually has, not only the ones just inserted. A unit missing from a type
    /// that was already there is the common case on a re-run, and looking only
    /// at new types would never find it.
    /// </summary>
    public async Task<int> SeedForOrganizationAsync(Guid orgId, CancellationToken ct)
    {
        int added = await SeedTypesAsync(orgId, ct);
        added += await SeedUnitsAsync(orgId, ct);
        return added;
    }

    private async Task<int> SeedTypesAsync(Guid orgId, CancellationToken ct)
    {
        List<UomType> existing = await _db.UomTypes
            .IgnoreQueryFilters()
            .Where(t => t.OrgId == orgId)
            .ToListAsync(ct);

        HashSet<string> present = [.. existing
            .Where(t => t.UomTypeSystemName is not null)
            .Select(t => t.UomTypeSystemName!)];

        // UomTypeName is unique per organization; a customer-created "Weight"
        // would otherwise fail the insert for every type in the batch.
        HashSet<string> taken = [.. existing.Select(t => t.UomTypeName)];

        List<UomType> missing = Repository.SeedData.UomSeed.BuildTypes(orgId)
            .Where(t => t.UomTypeSystemName is not null
                && !present.Contains(t.UomTypeSystemName)
                && !taken.Contains(t.UomTypeName))
            .ToList();

        if (missing.Count == 0)
        {
            return 0;
        }

        _db.UomTypes.AddRange(missing);
        await _db.SaveChangesAsync(ct);
        return missing.Count;
    }

    private async Task<int> SeedUnitsAsync(Guid orgId, CancellationToken ct)
    {
        // Every type the organization has, not just the new ones — units hang
        // off whichever type row is actually there.
        Dictionary<string, long> typeIds = await _db.UomTypes
            .IgnoreQueryFilters()
            .Where(t => t.OrgId == orgId && t.UomTypeSystemName != null)
            .ToDictionaryAsync(t => t.UomTypeSystemName!, t => t.UomTypeId, ct);

        if (typeIds.Count == 0)
        {
            return 0;
        }

        List<UnitOfMeasure> existing = await _db.UnitOfMeasures
            .IgnoreQueryFilters()
            .Where(u => u.OrgId == orgId)
            .ToListAsync(ct);

        HashSet<string> present = [.. existing
            .Where(u => u.UomSystemName is not null)
            .Select(u => u.UomSystemName!)];

        // UomCode is unique per organization.
        HashSet<string> taken = [.. existing.Select(u => u.UomCode)];

        // The base unit each type currently has, by system name where it has
        // one. Needed for the rescaling check below, not just for the "at most
        // one base unit" index.
        Dictionary<long, string?> currentBase = existing
            .Where(u => u.IsBaseUnit)
            .ToDictionary(u => u.UomTypeId, u => u.UomSystemName);

        IReadOnlyList<UnitOfMeasure> seeded = Repository.SeedData.UomSeed.BuildUnits(orgId, typeIds);

        // Which unit the seed treats as base for each type, read out of the seed
        // rather than restated here so the two cannot drift.
        Dictionary<long, string?> seedBase = seeded
            .Where(u => u.IsBaseUnit)
            .ToDictionary(u => u.UomTypeId, u => u.UomSystemName);

        List<UnitOfMeasure> candidates = [.. seeded
            .Where(u => u.UomSystemName is not null
                && !present.Contains(u.UomSystemName)
                && !taken.Contains(u.UomCode))];

        var missing = new List<UnitOfMeasure>();

        foreach (IGrouping<long, UnitOfMeasure> group in candidates.GroupBy(u => u.UomTypeId))
        {
            if (!currentBase.TryGetValue(group.Key, out string? existingBase))
            {
                // No base unit yet, so nothing to be inconsistent with — the
                // type is empty and takes the seed as written.
                missing.AddRange(group);
                continue;
            }

            if (existingBase is null
                || !seedBase.TryGetValue(group.Key, out string? expected)
                || existingBase != expected)
            {
                // The organization has moved this type onto a different base,
                // and SetBaseUnitAsync rescales every sibling factor when it
                // does. The seed's factors are still expressed against the
                // original base, so inserting them here would put a unit into
                // the type at the wrong scale — a thousandfold error in stock
                // and cost that nothing would flag. Adding nothing is the only
                // honest option; a customer who rebased a type can add the unit
                // themselves with the factor they meant.
                continue;
            }

            // Same base, so the factors are on the same scale. The seed's own
            // base row is already present and filtered out above, which is why
            // nothing here can collide with the one-base-per-type index.
            missing.AddRange(group);
        }

        if (missing.Count == 0)
        {
            return 0;
        }

        _db.UnitOfMeasures.AddRange(missing);
        await _db.SaveChangesAsync(ct);
        return missing.Count;
    }

    /// <summary>
    /// Units referenced by any item, in one query. A unit is "used" the moment
    /// an item points at it in any of its four slots.
    /// </summary>
    private async Task<HashSet<long>> UsedUnitIdsAsync(CancellationToken ct)
    {
        List<long> ids = await _db.Items.Select(i => i.InventoryUomId)
            .Union(_db.Items.Select(i => i.SalesUomId))
            .Union(_db.Items.Select(i => i.PurchaseUomId))
            .Union(_db.Items.Select(i => i.ReportUomId))
            .ToListAsync(ct);

        return [.. ids];
    }

    private Task<bool> TypeInUseAsync(long uomTypeId, CancellationToken ct) =>
        _db.Items.AnyAsync(i => i.UomTypeId == uomTypeId, ct);

    private IQueryable<UomType> Types(bool includeInactive) =>
        includeInactive ? _db.UomTypes : _db.UomTypes.Where(t => t.IsActive);

    private IQueryable<UnitOfMeasure> Units(bool includeInactive) =>
        includeInactive ? _db.UnitOfMeasures : _db.UnitOfMeasures.Where(u => u.IsActive);

    private static UnitOfMeasureListItem Map(UnitOfMeasure u, bool isUsed) => new()
    {
        UomId = u.UomId,
        UomTypeId = u.UomTypeId,
        UomSystemName = u.UomSystemName,
        UomCode = u.UomCode,
        UqcCode = u.UqcCode,
        UomName = u.UomName,
        IsBaseUnit = u.IsBaseUnit,
        ConversionToBase = u.ConversionToBase,
        DecimalPlaces = u.DecimalPlaces,
        DisplayOrder = u.DisplayOrder,
        IsSystem = u.IsSystem,
        IsActive = u.IsActive,
        IsUsed = isUsed,
    };
}
