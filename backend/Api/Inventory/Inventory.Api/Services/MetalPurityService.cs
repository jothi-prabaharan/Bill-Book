using Inventory.Entity.Enums;
using Inventory.Entity.Models;
using Inventory.Entity.TableEntities;
using Inventory.Repository;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Ordering;

namespace Inventory.Api.Services;

/// <summary>
/// Karatage and fineness. Small, but load-bearing: the purity factor scales the
/// metal rate, so a wrong figure here misprices every piece of that purity.
/// </summary>
public sealed class MetalPurityService
{
    private readonly InventoryDbContext _db;

    public MetalPurityService(InventoryDbContext db) => _db = db;

    public async Task<IReadOnlyList<MetalPurityListItem>> ListAsync(
        string? metalType, bool includeInactive, CancellationToken ct)
    {
        IQueryable<MetalPurity> query = _db.MetalPurities;

        if (!includeInactive)
        {
            query = query.Where(p => p.IsActive);
        }

        if (Enum.TryParse(metalType, ignoreCase: true, out MetalType metal))
        {
            query = query.Where(p => p.MetalType == metal);
        }

        List<MetalPurity> rows = await query
            .OrderBy(p => p.MetalType)
            .ThenBy(p => p.DisplayOrder)
            .ToListAsync(ct);

        return rows.Select(p => new MetalPurityListItem
        {
            MetalPurityId = p.MetalPurityId,
            MetalType = p.MetalType.ToString(),
            PuritySystemName = p.PuritySystemName,
            PurityName = p.PurityName,
            PurityFactor = p.PurityFactor,
            DisplayOrder = p.DisplayOrder,
            IsSystem = p.IsSystem,
            IsActive = p.IsActive,
        }).ToList();
    }

    public async Task<SaveMasterOutcome> CreateAsync(
        SaveMetalPurityRequest request, CancellationToken ct)
    {
        if (!Enum.TryParse(request.MetalType, ignoreCase: true, out MetalType metal))
        {
            return SaveMasterOutcome.InvalidValue;
        }

        string name = request.PurityName.Trim();
        if (await _db.MetalPurities.AnyAsync(p => p.MetalType == metal && p.PurityName == name, ct))
        {
            return SaveMasterOutcome.DuplicateName;
        }

        int highest = await _db.MetalPurities
            .Where(p => p.MetalType == metal)
            .Select(p => (int?)p.DisplayOrder)
            .MaxAsync(ct) ?? 0;

        _db.MetalPurities.Add(new MetalPurity
        {
            MetalType = metal,
            PuritySystemName = null,
            PurityName = name,
            PurityFactor = request.PurityFactor,
            DisplayOrder = highest + Reordering.Gap,
            IsSystem = false,
            IsActive = request.IsActive,
        });

        await _db.SaveChangesAsync(ct);
        return SaveMasterOutcome.Ok;
    }

    public async Task<SaveMasterOutcome> UpdateAsync(
        long purityId, SaveMetalPurityRequest request, CancellationToken ct)
    {
        MetalPurity? purity = await _db.MetalPurities
            .FirstOrDefaultAsync(p => p.MetalPurityId == purityId, ct);

        if (purity is null)
        {
            return SaveMasterOutcome.NotFound;
        }

        string name = request.PurityName.Trim();
        bool duplicate = await _db.MetalPurities.AnyAsync(
            p => p.MetalType == purity.MetalType
                && p.PurityName == name
                && p.MetalPurityId != purityId,
            ct);

        if (duplicate)
        {
            return SaveMasterOutcome.DuplicateName;
        }

        // Repricing a purity that items already carry would restate what those
        // pieces are worth, so the factor is frozen once one is in use.
        bool inUse = await _db.ItemJewelleryDetails.AnyAsync(j => j.MetalPurityId == purityId, ct);
        if (inUse && purity.PurityFactor != request.PurityFactor)
        {
            return SaveMasterOutcome.InUse;
        }

        purity.PurityName = name;
        purity.PurityFactor = request.PurityFactor;
        purity.IsActive = request.IsActive;

        await _db.SaveChangesAsync(ct);
        return SaveMasterOutcome.Ok;
    }

    public async Task<SaveMasterOutcome> DeleteAsync(long purityId, CancellationToken ct)
    {
        MetalPurity? purity = await _db.MetalPurities
            .FirstOrDefaultAsync(p => p.MetalPurityId == purityId, ct);

        if (purity is null)
        {
            return SaveMasterOutcome.NotFound;
        }

        if (purity.IsSystem)
        {
            return SaveMasterOutcome.SystemRowUndeletable;
        }

        if (await _db.ItemJewelleryDetails.AnyAsync(j => j.MetalPurityId == purityId, ct))
        {
            return SaveMasterOutcome.InUse;
        }

        _db.MetalPurities.Remove(purity);
        await _db.SaveChangesAsync(ct);
        return SaveMasterOutcome.Ok;
    }

    public async Task<SaveMasterOutcome> ReorderAsync(ReorderRequest request, CancellationToken ct)
    {
        MetalPurity? moved = await _db.MetalPurities
            .FirstOrDefaultAsync(p => p.MetalPurityId == request.MovedId, ct);

        if (moved is null)
        {
            return SaveMasterOutcome.NotFound;
        }

        // Ordering is within a metal — 22K sits above 18K, and gold has nothing
        // to say about where silver goes.
        List<MetalPurity> siblings = await _db.MetalPurities
            .Where(p => p.MetalType == moved.MetalType)
            .OrderBy(p => p.DisplayOrder)
            .ToListAsync(ct);

        Reordering.Apply(siblings, request, p => p.MetalPurityId, p => p.DisplayOrder,
            (p, order) => p.DisplayOrder = order);

        await _db.SaveChangesAsync(ct);
        return SaveMasterOutcome.Ok;
    }

    /// <summary>
    /// Writes the standard purities for an organization, adding only what is
    /// missing. Safe to re-run: a purity added to the seed list later reaches
    /// organizations created before it existed.
    ///
    /// Matched on <c>PuritySystemName</c> rather than the display name, which is
    /// the whole reason that column exists — a jeweller who relabels
    /// "916 (22K)" as "22 Karat" must not be handed a second copy of it under
    /// the original label.
    /// </summary>
    public async Task<int> SeedForOrganizationAsync(Guid orgId, CancellationToken ct)
    {
        List<MetalPurity> existing = await _db.MetalPurities
            .IgnoreQueryFilters()
            .Where(p => p.OrgId == orgId)
            .ToListAsync(ct);

        HashSet<string> present = [.. existing
            .Where(p => p.PuritySystemName is not null)
            .Select(p => p.PuritySystemName!)];

        // (MetalType, PurityName) is unique. A hand-typed purity holding a seed
        // row's name would otherwise fail the insert for every other row too.
        HashSet<(MetalType, string)> taken = [.. existing.Select(p => (p.MetalType, p.PurityName))];

        List<MetalPurity> missing = Repository.SeedData.MetalPuritiesSeed.Build(orgId)
            .Where(p => p.PuritySystemName is not null
                && !present.Contains(p.PuritySystemName)
                && !taken.Contains((p.MetalType, p.PurityName)))
            .ToList();

        if (missing.Count == 0)
        {
            return 0;
        }

        _db.MetalPurities.AddRange(missing);
        await _db.SaveChangesAsync(ct);
        return missing.Count;
    }
}
