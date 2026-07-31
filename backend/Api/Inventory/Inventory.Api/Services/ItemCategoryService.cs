using Inventory.Entity.Enums;
using Inventory.Entity.Models;
using Inventory.Entity.TableEntities;
using Inventory.Repository;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Api.Services;

/// <summary>
/// The category tree. Two rules make this more than CRUD: a category cannot be
/// moved inside its own subtree, and the tree is capped at three levels —
/// deeper than that and every report grouping has to decide which level it
/// means.
/// </summary>
public sealed class ItemCategoryService
{
    private const int MaxDepth = 3;

    private readonly InventoryDbContext _db;

    public ItemCategoryService(InventoryDbContext db) => _db = db;

    public async Task<IReadOnlyList<ItemCategoryListItem>> ListAsync(
        bool includeInactive, CancellationToken ct)
    {
        List<ItemCategory> all = await _db.ItemCategories.ToListAsync(ct);

        Dictionary<long, int> itemCounts = await _db.Items
            .Where(i => i.ItemCategoryId != null)
            .GroupBy(i => i.ItemCategoryId!.Value)
            .Select(g => new { CategoryId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.CategoryId, x => x.Count, ct);

        IEnumerable<ItemCategory> visible = includeInactive ? all : all.Where(c => c.IsActive);

        return visible
            .OrderBy(c => c.ParentCategoryId ?? 0)
            .ThenBy(c => c.DisplayOrder)
            .ThenBy(c => c.CategoryName)
            .Select(c => new ItemCategoryListItem
            {
                ItemCategoryId = c.ItemCategoryId,
                CategoryCode = c.CategoryCode,
                CategoryName = c.CategoryName,
                ParentCategoryId = c.ParentCategoryId,
                DefaultItemProfile = c.DefaultItemProfile?.ToString(),
                DefaultCostingType = c.DefaultCostingType?.ToString(),
                DefaultUomTypeId = c.DefaultUomTypeId,
                DisplayOrder = c.DisplayOrder,
                IsActive = c.IsActive,
                Depth = DepthOf(all, c),
                ItemCount = itemCounts.GetValueOrDefault(c.ItemCategoryId),
            })
            .ToList();
    }

    public async Task<SaveMasterOutcome> CreateAsync(
        SaveItemCategoryRequest request, CancellationToken ct)
    {
        SaveMasterOutcome invalid = await ValidateAsync(request, null, ct);
        if (invalid != SaveMasterOutcome.Ok)
        {
            return invalid;
        }

        int highest = await _db.ItemCategories
            .Where(c => c.ParentCategoryId == request.ParentCategoryId)
            .Select(c => (int?)c.DisplayOrder)
            .MaxAsync(ct) ?? 0;

        var category = new ItemCategory
        {
            CategoryCode = request.CategoryCode.Trim().ToUpperInvariant(),
            CategoryName = request.CategoryName.Trim(),
            ParentCategoryId = request.ParentCategoryId,
            DefaultUomTypeId = request.DefaultUomTypeId,
            DisplayOrder = highest + Reordering.Gap,
            IsActive = request.IsActive,
        };
        ApplyDefaults(category, request);

        _db.ItemCategories.Add(category);
        await _db.SaveChangesAsync(ct);
        return SaveMasterOutcome.Ok;
    }

    public async Task<SaveMasterOutcome> UpdateAsync(
        long categoryId, SaveItemCategoryRequest request, CancellationToken ct)
    {
        ItemCategory? category = await _db.ItemCategories
            .FirstOrDefaultAsync(c => c.ItemCategoryId == categoryId, ct);

        if (category is null)
        {
            return SaveMasterOutcome.NotFound;
        }

        SaveMasterOutcome invalid = await ValidateAsync(request, categoryId, ct);
        if (invalid != SaveMasterOutcome.Ok)
        {
            return invalid;
        }

        category.CategoryCode = request.CategoryCode.Trim().ToUpperInvariant();
        category.CategoryName = request.CategoryName.Trim();
        category.ParentCategoryId = request.ParentCategoryId;
        category.DefaultUomTypeId = request.DefaultUomTypeId;
        category.IsActive = request.IsActive;
        ApplyDefaults(category, request);

        await _db.SaveChangesAsync(ct);
        return SaveMasterOutcome.Ok;
    }

    public async Task<SaveMasterOutcome> DeleteAsync(long categoryId, CancellationToken ct)
    {
        ItemCategory? category = await _db.ItemCategories
            .FirstOrDefaultAsync(c => c.ItemCategoryId == categoryId, ct);

        if (category is null)
        {
            return SaveMasterOutcome.NotFound;
        }

        if (await _db.ItemCategories.AnyAsync(c => c.ParentCategoryId == categoryId, ct))
        {
            return SaveMasterOutcome.HasChildren;
        }

        if (await _db.Items.AnyAsync(i => i.ItemCategoryId == categoryId, ct))
        {
            return SaveMasterOutcome.InUse;
        }

        _db.ItemCategories.Remove(category);
        await _db.SaveChangesAsync(ct);
        return SaveMasterOutcome.Ok;
    }

    public async Task<SaveMasterOutcome> ReorderAsync(ReorderRequest request, CancellationToken ct)
    {
        ItemCategory? moved = await _db.ItemCategories
            .FirstOrDefaultAsync(c => c.ItemCategoryId == request.MovedId, ct);

        if (moved is null)
        {
            return SaveMasterOutcome.NotFound;
        }

        // Reordering happens within one parent. A drop onto a different parent
        // is a reparent, which goes through Update so the cycle and depth checks
        // run.
        List<ItemCategory> siblings = await _db.ItemCategories
            .Where(c => c.ParentCategoryId == moved.ParentCategoryId)
            .OrderBy(c => c.DisplayOrder)
            .ThenBy(c => c.CategoryName)
            .ToListAsync(ct);

        Reordering.Apply(siblings, request, c => c.ItemCategoryId, c => c.DisplayOrder,
            (c, order) => c.DisplayOrder = order);

        await _db.SaveChangesAsync(ct);
        return SaveMasterOutcome.Ok;
    }

    private async Task<SaveMasterOutcome> ValidateAsync(
        SaveItemCategoryRequest request, long? categoryId, CancellationToken ct)
    {
        string code = request.CategoryCode.Trim().ToUpperInvariant();
        bool duplicate = await _db.ItemCategories.AnyAsync(
            c => c.CategoryCode == code && (categoryId == null || c.ItemCategoryId != categoryId), ct);

        if (duplicate)
        {
            return SaveMasterOutcome.DuplicateCode;
        }

        if (request.DefaultItemProfile is { Length: > 0 } profile
            && !Enum.TryParse<ItemProfile>(profile, ignoreCase: true, out _))
        {
            return SaveMasterOutcome.InvalidValue;
        }

        if (request.DefaultCostingType is { Length: > 0 } costing
            && !Enum.TryParse<CostingType>(costing, ignoreCase: true, out _))
        {
            return SaveMasterOutcome.InvalidValue;
        }

        if (request.ParentCategoryId is not long parentId)
        {
            return SaveMasterOutcome.Ok;
        }

        List<ItemCategory> all = await _db.ItemCategories.ToListAsync(ct);

        ItemCategory? parent = all.FirstOrDefault(c => c.ItemCategoryId == parentId);
        if (parent is null)
        {
            return SaveMasterOutcome.NotFound;
        }

        // Walking up from the proposed parent: meeting ourselves means the move
        // would detach the subtree from the tree entirely.
        if (categoryId is long id && IsDescendantOf(all, parentId, id))
        {
            return SaveMasterOutcome.CategoryCycle;
        }

        return DepthOf(all, parent) + 1 > MaxDepth
            ? SaveMasterOutcome.CategoryTooDeep
            : SaveMasterOutcome.Ok;
    }

    private static void ApplyDefaults(ItemCategory category, SaveItemCategoryRequest request)
    {
        category.DefaultItemProfile =
            Enum.TryParse(request.DefaultItemProfile, ignoreCase: true, out ItemProfile profile)
                ? profile
                : null;

        category.DefaultCostingType =
            Enum.TryParse(request.DefaultCostingType, ignoreCase: true, out CostingType costing)
                ? costing
                : null;
    }

    /// <summary>Whether <paramref name="candidateId"/> sits anywhere under <paramref name="ancestorId"/>.</summary>
    private static bool IsDescendantOf(List<ItemCategory> all, long candidateId, long ancestorId)
    {
        long? current = candidateId;
        int guard = 0;

        while (current is long id && guard++ < 100)
        {
            if (id == ancestorId)
            {
                return true;
            }

            current = all.FirstOrDefault(c => c.ItemCategoryId == id)?.ParentCategoryId;
        }

        return false;
    }

    private static int DepthOf(List<ItemCategory> all, ItemCategory category)
    {
        int depth = 1;
        long? parentId = category.ParentCategoryId;
        int guard = 0;

        while (parentId is long id && guard++ < 100)
        {
            depth++;
            parentId = all.FirstOrDefault(c => c.ItemCategoryId == id)?.ParentCategoryId;
        }

        return depth;
    }
}
