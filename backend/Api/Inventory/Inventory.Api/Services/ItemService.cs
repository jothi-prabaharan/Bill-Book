using Inventory.Entity.Enums;
using Inventory.Entity.Models;
using Inventory.Entity.TableEntities;
using Inventory.Repository;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Numbering;
using Shared.Kernel.Ordering;

namespace Inventory.Api.Services;

/// <summary>
/// The item master. Saved as an aggregate — item, its vertical extension and its
/// barcodes together — because the extension is only valid in the context of the
/// profile that names it.
///
/// Two families of rule run here. The unit rules: all four unit slots must
/// belong to the item's unit type, since every conversion derives from
/// <c>ConversionToBase</c> within a type. And the lock: once stock has moved,
/// the unit type, inventory unit, costing type and tracking flags are frozen,
/// because every recorded quantity and cost was written under them.
/// </summary>
public sealed class ItemService
{
    private readonly InventoryDbContext _db;
    private readonly INumberGenerator _numbers;
    private readonly TimeProvider _clock;

    public ItemService(InventoryDbContext db, INumberGenerator numbers, TimeProvider clock)
    {
        _db = db;
        _numbers = numbers;
        _clock = clock;
    }

    public async Task<IReadOnlyList<ItemListItem>> ListAsync(
        string? search, string? profile, long? categoryId, bool includeInactive, CancellationToken ct)
    {
        IQueryable<Item> query = _db.Items;

        if (!includeInactive)
        {
            query = query.Where(i => i.IsActive);
        }

        if (Enum.TryParse(profile, ignoreCase: true, out ItemProfile itemProfile))
        {
            query = query.Where(i => i.ItemProfile == itemProfile);
        }

        if (categoryId is long category)
        {
            query = query.Where(i => i.ItemCategoryId == category);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            string term = search.Trim();
            query = query.Where(i =>
                EF.Functions.ILike(i.ItemName, $"%{term}%")
                || EF.Functions.ILike(i.ItemCode, $"%{term}%"));
        }

        List<Item> items = await query
            .OrderBy(i => i.DisplayOrder)
            .ThenBy(i => i.ItemName)
            .Take(500)
            .ToListAsync(ct);

        // Two dictionaries rather than a join per row — this is the screen where
        // N+1 shows up first.
        Dictionary<long, string> unitCodes = await _db.UnitOfMeasures
            .ToDictionaryAsync(u => u.UomId, u => u.UomCode, ct);

        Dictionary<long, string> categoryNames = await _db.ItemCategories
            .ToDictionaryAsync(c => c.ItemCategoryId, c => c.CategoryName, ct);

        return items.Select(i => MapSummary(i, unitCodes, categoryNames)).ToList();
    }

    public async Task<ItemDetail?> GetAsync(long itemId, CancellationToken ct)
    {
        Item? item = await _db.Items.FirstOrDefaultAsync(i => i.ItemId == itemId, ct);
        if (item is null)
        {
            return null;
        }

        Dictionary<long, string> unitCodes = await _db.UnitOfMeasures
            .ToDictionaryAsync(u => u.UomId, u => u.UomCode, ct);

        Dictionary<long, string> categoryNames = await _db.ItemCategories
            .ToDictionaryAsync(c => c.ItemCategoryId, c => c.CategoryName, ct);

        ItemDetail detail = MapDetail(item, unitCodes, categoryNames);

        if (item.ItemProfile == ItemProfile.Jewellery)
        {
            ItemJewelleryDetails? row = await _db.ItemJewelleryDetails
                .FirstOrDefaultAsync(j => j.ItemId == itemId, ct);

            detail.Jewellery = row is null ? null : MapJewellery(row);
        }

        if (item.ItemProfile == ItemProfile.Pharma)
        {
            ItemPharmaDetails? row = await _db.ItemPharmaDetails
                .FirstOrDefaultAsync(p => p.ItemId == itemId, ct);

            detail.Pharma = row is null ? null : MapPharma(row);
        }

        detail.Barcodes = await _db.ItemBarcodes
            .Where(b => b.ItemId == itemId)
            .OrderByDescending(b => b.IsPrimary)
            .ThenBy(b => b.Barcode)
            .Select(b => new ItemBarcodeModel
            {
                ItemBarcodeId = b.ItemBarcodeId,
                Barcode = b.Barcode,
                BarcodeType = b.BarcodeType.ToString(),
                UomId = b.UomId,
                IsPrimary = b.IsPrimary,
                IsActive = b.IsActive,
            })
            .ToListAsync(ct);

        return detail;
    }

    public async Task<SaveItemResult> CreateAsync(SaveItemRequest request, CancellationToken ct)
    {
        if (!TryParse(request, out ParsedItem parsed))
        {
            return new SaveItemResult(SaveItemOutcome.InvalidValue, null, null);
        }

        SaveItemOutcome invalid = await ValidateAsync(request, parsed, null, ct);
        if (invalid != SaveItemOutcome.Ok)
        {
            return new SaveItemResult(invalid, null, null);
        }

        string code = await ResolveCodeAsync(request, ct);
        if (await _db.Items.AnyAsync(i => i.ItemCode == code, ct))
        {
            return new SaveItemResult(SaveItemOutcome.DuplicateCode, null, null);
        }

        int highest = await _db.Items.Select(i => (int?)i.DisplayOrder).MaxAsync(ct) ?? 0;

        var item = new Item { ItemCode = code, DisplayOrder = highest + Reordering.Gap };
        Apply(item, request, parsed);

        _db.Items.Add(item);
        await _db.SaveChangesAsync(ct);

        await SaveExtensionAsync(item, request, parsed, ct);
        await ReplaceBarcodesAsync(item.ItemId, request, ct);
        await _db.SaveChangesAsync(ct);

        return new SaveItemResult(SaveItemOutcome.Ok, item.ItemId, item.ItemCode);
    }

    public async Task<SaveItemResult> UpdateAsync(
        long itemId, SaveItemRequest request, CancellationToken ct)
    {
        Item? item = await _db.Items.FirstOrDefaultAsync(i => i.ItemId == itemId, ct);
        if (item is null)
        {
            return new SaveItemResult(SaveItemOutcome.NotFound, null, null);
        }

        if (!TryParse(request, out ParsedItem parsed))
        {
            return new SaveItemResult(SaveItemOutcome.InvalidValue, null, null);
        }

        SaveItemOutcome invalid = await ValidateAsync(request, parsed, itemId, ct);
        if (invalid != SaveItemOutcome.Ok)
        {
            return new SaveItemResult(invalid, null, null);
        }

        // The config lock. Everything else on the item is free to change; these
        // five decide how existing quantities and costs are to be read.
        if (await HasStockMovementsAsync(itemId, ct)
            && (item.UomTypeId != request.UomTypeId
                || item.InventoryUomId != request.InventoryUomId
                || item.CostingType != parsed.CostingType
                || item.ItemProfile != parsed.Profile
                || item.IsBatchTracked != request.IsBatchTracked
                || item.IsExpiryTracked != request.IsExpiryTracked
                || item.IsSerialTracked != request.IsSerialTracked))
        {
            return new SaveItemResult(SaveItemOutcome.LockedAfterMovement, null, null);
        }

        ItemProfile previousProfile = item.ItemProfile;
        Apply(item, request, parsed);

        if (previousProfile != parsed.Profile)
        {
            await RemoveExtensionAsync(itemId, previousProfile, ct);
        }

        await SaveExtensionAsync(item, request, parsed, ct);
        await ReplaceBarcodesAsync(itemId, request, ct);
        await _db.SaveChangesAsync(ct);

        return new SaveItemResult(SaveItemOutcome.Ok, item.ItemId, item.ItemCode);
    }

    /// <summary>
    /// Deactivates. Items are never deleted — documents, movements and ledger
    /// sub-accounts all point at them.
    /// </summary>
    public async Task<SaveItemOutcome> DeactivateAsync(long itemId, CancellationToken ct)
    {
        Item? item = await _db.Items.FirstOrDefaultAsync(i => i.ItemId == itemId, ct);
        if (item is null)
        {
            return SaveItemOutcome.NotFound;
        }

        item.IsActive = false;
        await _db.SaveChangesAsync(ct);
        return SaveItemOutcome.Ok;
    }

    public async Task<SaveItemOutcome> ReorderAsync(ReorderRequest request, CancellationToken ct)
    {
        List<Item> all = await _db.Items
            .OrderBy(i => i.DisplayOrder)
            .ThenBy(i => i.ItemName)
            .ToListAsync(ct);

        if (!Reordering.Apply(all, request, i => i.ItemId, i => i.DisplayOrder,
                (i, order) => i.DisplayOrder = order))
        {
            return SaveItemOutcome.NotFound;
        }

        await _db.SaveChangesAsync(ct);
        return SaveItemOutcome.Ok;
    }

    private async Task<SaveItemOutcome> ValidateAsync(
        SaveItemRequest request, ParsedItem parsed, long? itemId, CancellationToken ct)
    {
        if (parsed.Type == ItemType.Service && request.TrackInventory)
        {
            return SaveItemOutcome.ServiceCannotBeStocked;
        }

        // Mirrors the check constraints, so the user gets a sentence instead of
        // a constraint-violation 500.
        if (request.TrackInventory == (parsed.CostingType == CostingType.None))
        {
            return SaveItemOutcome.CostingTrackingMismatch;
        }

        if (request.IsExpiryTracked && !request.IsBatchTracked)
        {
            return SaveItemOutcome.CostingTrackingMismatch;
        }

        if (parsed.CostingType == CostingType.Fefo
            && (!request.IsBatchTracked || !request.IsExpiryTracked))
        {
            return SaveItemOutcome.CostingTrackingMismatch;
        }

        if (parsed.CostingType == CostingType.SpecificIdentification && !request.IsSerialTracked)
        {
            return SaveItemOutcome.CostingTrackingMismatch;
        }

        // All four unit slots must sit in the item's type: conversion between
        // them is derived from ConversionToBase, which only relates units of one
        // type. A pack size is a unit of its type, not a cross-type mapping.
        long[] unitIds =
        [
            request.InventoryUomId, request.SalesUomId, request.PurchaseUomId, request.ReportUomId,
        ];

        List<UnitOfMeasure> units = await _db.UnitOfMeasures
            .Where(u => unitIds.Contains(u.UomId))
            .ToListAsync(ct);

        if (units.Count != unitIds.Distinct().Count())
        {
            return SaveItemOutcome.UnknownUnit;
        }

        if (units.Any(u => u.UomTypeId != request.UomTypeId))
        {
            return SaveItemOutcome.UnitTypeMismatch;
        }

        if (request.ItemCategoryId is long categoryId
            && !await _db.ItemCategories.AnyAsync(c => c.ItemCategoryId == categoryId, ct))
        {
            return SaveItemOutcome.UnknownCategory;
        }

        // The profile names which extension must be present, and only that one.
        bool jewelleryExpected = parsed.Profile == ItemProfile.Jewellery;
        bool pharmaExpected = parsed.Profile == ItemProfile.Pharma;

        if ((jewelleryExpected && request.Jewellery is null)
            || (pharmaExpected && request.Pharma is null))
        {
            return SaveItemOutcome.ProfileDetailsMissing;
        }

        if ((!jewelleryExpected && request.Jewellery is not null)
            || (!pharmaExpected && request.Pharma is not null))
        {
            return SaveItemOutcome.ProfileDetailsUnexpected;
        }

        List<string> barcodes = request.Barcodes.Select(b => b.Barcode.Trim()).ToList();
        if (barcodes.Count != barcodes.Distinct().Count())
        {
            return SaveItemOutcome.DuplicateBarcode;
        }

        bool clash = await _db.ItemBarcodes.AnyAsync(
            b => barcodes.Contains(b.Barcode) && (itemId == null || b.ItemId != itemId), ct);

        return clash ? SaveItemOutcome.DuplicateBarcode : SaveItemOutcome.Ok;
    }

    private async Task<string> ResolveCodeAsync(SaveItemRequest request, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(request.ItemCode))
        {
            return request.ItemCode.Trim();
        }

        DateOnly today = DateOnly.FromDateTime(_clock.GetUtcNow().UtcDateTime);
        NumberAllocation allocation = await _numbers.NextAsync("ITEM", today, ct);
        return allocation.Code;
    }

    private async Task SaveExtensionAsync(
        Item item, SaveItemRequest request, ParsedItem parsed, CancellationToken ct)
    {
        if (parsed.Profile == ItemProfile.Jewellery && request.Jewellery is ItemJewelleryModel j)
        {
            ItemJewelleryDetails? row = await _db.ItemJewelleryDetails
                .FirstOrDefaultAsync(x => x.ItemId == item.ItemId, ct);

            if (row is null)
            {
                row = new ItemJewelleryDetails { ItemId = item.ItemId };
                _db.ItemJewelleryDetails.Add(row);
            }

            row.MetalType = Enum.TryParse(j.MetalType, ignoreCase: true, out MetalType metal)
                ? metal
                : MetalType.Gold;
            row.MetalPurityId = j.MetalPurityId;
            row.GrossWeight = j.GrossWeight;
            row.NetWeight = j.NetWeight;
            row.StoneWeight = j.StoneWeight;
            row.StoneCharge = j.StoneCharge;
            row.WastagePercent = j.WastagePercent;
            row.MakingChargeType =
                Enum.TryParse(j.MakingChargeType, ignoreCase: true, out MakingChargeType making)
                    ? making
                    : MakingChargeType.Percentage;
            row.MakingChargeValue = j.MakingChargeValue;
            row.IsHallmarked = j.IsHallmarked;
        }

        if (parsed.Profile == ItemProfile.Pharma && request.Pharma is ItemPharmaModel p)
        {
            ItemPharmaDetails? row = await _db.ItemPharmaDetails
                .FirstOrDefaultAsync(x => x.ItemId == item.ItemId, ct);

            if (row is null)
            {
                row = new ItemPharmaDetails { ItemId = item.ItemId };
                _db.ItemPharmaDetails.Add(row);
            }

            row.GenericName = p.GenericName.Trim();
            row.Strength = p.Strength;
            row.DosageForm = Enum.TryParse(p.DosageForm, ignoreCase: true, out DosageForm form)
                ? form
                : DosageForm.Tablet;
            row.PackSize = p.PackSize.Trim();
            row.ManufacturerName = p.ManufacturerName.Trim();
            row.MarketedBy = p.MarketedBy;
            row.DrugSchedule = Enum.TryParse(p.DrugSchedule, ignoreCase: true, out DrugSchedule schedule)
                ? schedule
                : DrugSchedule.None;

            // Schedules H, H1 and X require a prescription by law, so the flag
            // is derived rather than trusted from the caller.
            row.IsPrescriptionRequired = row.DrugSchedule is DrugSchedule.H
                or DrugSchedule.H1
                or DrugSchedule.X
                || p.IsPrescriptionRequired;

            row.IsNarcotic = p.IsNarcotic;
            row.StorageCondition =
                Enum.TryParse(p.StorageCondition, ignoreCase: true, out StorageCondition storage)
                    ? storage
                    : StorageCondition.Ambient;
            row.ShelfLifeDays = p.ShelfLifeDays;
            row.MinExpiryDaysOnReceipt = p.MinExpiryDaysOnReceipt;
            row.ExpiryAlertDays = p.ExpiryAlertDays;
        }
    }

    private async Task RemoveExtensionAsync(long itemId, ItemProfile profile, CancellationToken ct)
    {
        if (profile == ItemProfile.Jewellery)
        {
            ItemJewelleryDetails? row = await _db.ItemJewelleryDetails
                .FirstOrDefaultAsync(x => x.ItemId == itemId, ct);

            if (row is not null)
            {
                _db.ItemJewelleryDetails.Remove(row);
            }
        }

        if (profile == ItemProfile.Pharma)
        {
            ItemPharmaDetails? row = await _db.ItemPharmaDetails
                .FirstOrDefaultAsync(x => x.ItemId == itemId, ct);

            if (row is not null)
            {
                _db.ItemPharmaDetails.Remove(row);
            }
        }
    }

    private async Task ReplaceBarcodesAsync(
        long itemId, SaveItemRequest request, CancellationToken ct)
    {
        List<ItemBarcode> existing = await _db.ItemBarcodes
            .Where(b => b.ItemId == itemId)
            .ToListAsync(ct);

        long[] kept = request.Barcodes
            .Where(b => b.ItemBarcodeId > 0)
            .Select(b => b.ItemBarcodeId)
            .ToArray();

        _db.ItemBarcodes.RemoveRange(existing.Where(b => !kept.Contains(b.ItemBarcodeId)));

        // Cleared before any is set, so the filtered unique index never sees two
        // primaries mid-save.
        foreach (ItemBarcode row in existing)
        {
            row.IsPrimary = false;
        }

        foreach (ItemBarcodeModel model in request.Barcodes)
        {
            ItemBarcode? row = model.ItemBarcodeId > 0
                ? existing.FirstOrDefault(b => b.ItemBarcodeId == model.ItemBarcodeId)
                : null;

            if (row is null)
            {
                row = new ItemBarcode { ItemId = itemId };
                _db.ItemBarcodes.Add(row);
            }

            row.Barcode = model.Barcode.Trim();
            row.BarcodeType = Enum.TryParse(model.BarcodeType, ignoreCase: true, out BarcodeType type)
                ? type
                : BarcodeType.Ean13;
            row.UomId = model.UomId;
            row.IsPrimary = model.IsPrimary;
            row.IsActive = model.IsActive;
        }
    }

    /// <summary>
    /// Whether stock has ever moved for this item.
    ///
    /// This is what makes the config lock real: the unit type, inventory unit,
    /// costing method, profile and tracking flags are frozen once a single
    /// movement exists, because every quantity and cost recorded so far was
    /// written under them. Movements are never deleted, so this only ever goes
    /// from false to true.
    /// </summary>
    private Task<bool> HasStockMovementsAsync(long itemId, CancellationToken ct) =>
        _db.StockMovements.AnyAsync(m => m.ItemId == itemId, ct);

    private static void Apply(Item item, SaveItemRequest request, ParsedItem parsed)
    {
        item.ItemName = request.ItemName.Trim();
        item.PrintName = request.PrintName;
        item.Description = request.Description;
        item.ItemProfile = parsed.Profile;
        item.ItemType = parsed.Type;
        item.ItemCategoryId = request.ItemCategoryId;
        item.HsnSacCodeId = request.HsnSacCodeId;
        item.TaxGroupId = request.TaxGroupId;
        item.TaxPreference = parsed.TaxPreference;
        item.IsPriceInclusiveOfTax = request.IsPriceInclusiveOfTax;
        item.UomTypeId = request.UomTypeId;
        item.InventoryUomId = request.InventoryUomId;
        item.SalesUomId = request.SalesUomId;
        item.PurchaseUomId = request.PurchaseUomId;
        item.ReportUomId = request.ReportUomId;
        item.TrackInventory = request.TrackInventory;
        item.CostingType = parsed.CostingType;
        item.IsBatchTracked = request.IsBatchTracked;
        item.IsExpiryTracked = request.IsExpiryTracked;
        item.IsSerialTracked = request.IsSerialTracked;
        item.SalesPrice = request.SalesPrice;
        item.PurchasePrice = request.PurchasePrice;
        item.Mrp = request.Mrp;
        item.MinSalePrice = request.MinSalePrice;
        item.StandardCost = request.StandardCost;
        item.ReorderLevel = request.ReorderLevel;
        item.ReorderQuantity = request.ReorderQuantity;
        item.MinStockLevel = request.MinStockLevel;
        item.MaxStockLevel = request.MaxStockLevel;
        item.LeadTimeDays = request.LeadTimeDays;
        item.DefaultWarehouseId = request.DefaultWarehouseId;
        item.IsSales = request.IsSales;
        item.IsPurchase = request.IsPurchase;
        item.IsReturnable = request.IsReturnable;
        item.ImageUrl = request.ImageUrl;
        item.IsActive = request.IsActive;
    }

    private static bool TryParse(SaveItemRequest request, out ParsedItem parsed)
    {
        bool ok = Enum.TryParse(request.ItemProfile, ignoreCase: true, out ItemProfile profile)
            & Enum.TryParse(request.ItemType, ignoreCase: true, out ItemType type)
            & Enum.TryParse(request.TaxPreference, ignoreCase: true, out TaxPreference tax)
            & Enum.TryParse(request.CostingType, ignoreCase: true, out CostingType costing);

        parsed = new ParsedItem(profile, type, tax, costing);
        return ok;
    }

    private static ItemListItem MapSummary(
        Item i, Dictionary<long, string> unitCodes, Dictionary<long, string> categoryNames) => new()
    {
        ItemId = i.ItemId,
        ItemCode = i.ItemCode,
        ItemName = i.ItemName,
        ItemProfile = i.ItemProfile.ToString(),
        ItemType = i.ItemType.ToString(),
        ItemCategoryId = i.ItemCategoryId,
        CategoryName = i.ItemCategoryId is long id ? categoryNames.GetValueOrDefault(id) : null,
        CostingType = i.CostingType.ToString(),
        TrackInventory = i.TrackInventory,
        InventoryUomCode = unitCodes.GetValueOrDefault(i.InventoryUomId, string.Empty),
        SalesPrice = i.SalesPrice,
        Mrp = i.Mrp,
        IsActive = i.IsActive,
        DisplayOrder = i.DisplayOrder,
    };

    private static ItemDetail MapDetail(
        Item i, Dictionary<long, string> unitCodes, Dictionary<long, string> categoryNames)
    {
        ItemListItem summary = MapSummary(i, unitCodes, categoryNames);

        return new ItemDetail
        {
            ItemId = summary.ItemId,
            ItemCode = summary.ItemCode,
            ItemName = summary.ItemName,
            ItemProfile = summary.ItemProfile,
            ItemType = summary.ItemType,
            ItemCategoryId = summary.ItemCategoryId,
            CategoryName = summary.CategoryName,
            CostingType = summary.CostingType,
            TrackInventory = summary.TrackInventory,
            InventoryUomCode = summary.InventoryUomCode,
            SalesPrice = summary.SalesPrice,
            Mrp = summary.Mrp,
            IsActive = summary.IsActive,
            DisplayOrder = summary.DisplayOrder,
            PrintName = i.PrintName,
            Description = i.Description,
            HsnSacCodeId = i.HsnSacCodeId,
            TaxGroupId = i.TaxGroupId,
            TaxPreference = i.TaxPreference.ToString(),
            IsPriceInclusiveOfTax = i.IsPriceInclusiveOfTax,
            UomTypeId = i.UomTypeId,
            InventoryUomId = i.InventoryUomId,
            SalesUomId = i.SalesUomId,
            PurchaseUomId = i.PurchaseUomId,
            ReportUomId = i.ReportUomId,
            IsBatchTracked = i.IsBatchTracked,
            IsExpiryTracked = i.IsExpiryTracked,
            IsSerialTracked = i.IsSerialTracked,
            PurchasePrice = i.PurchasePrice,
            MinSalePrice = i.MinSalePrice,
            StandardCost = i.StandardCost,
            ReorderLevel = i.ReorderLevel,
            ReorderQuantity = i.ReorderQuantity,
            MinStockLevel = i.MinStockLevel,
            MaxStockLevel = i.MaxStockLevel,
            LeadTimeDays = i.LeadTimeDays,
            DefaultWarehouseId = i.DefaultWarehouseId,
            IsSales = i.IsSales,
            IsPurchase = i.IsPurchase,
            IsReturnable = i.IsReturnable,
            ImageUrl = i.ImageUrl,
            HasStockMovements = false,
        };
    }

    private static ItemJewelleryModel MapJewellery(ItemJewelleryDetails j) => new()
    {
        MetalType = j.MetalType.ToString(),
        MetalPurityId = j.MetalPurityId,
        GrossWeight = j.GrossWeight,
        NetWeight = j.NetWeight,
        StoneWeight = j.StoneWeight,
        StoneCharge = j.StoneCharge,
        WastagePercent = j.WastagePercent,
        MakingChargeType = j.MakingChargeType.ToString(),
        MakingChargeValue = j.MakingChargeValue,
        IsHallmarked = j.IsHallmarked,
    };

    private static ItemPharmaModel MapPharma(ItemPharmaDetails p) => new()
    {
        GenericName = p.GenericName,
        Strength = p.Strength,
        DosageForm = p.DosageForm.ToString(),
        PackSize = p.PackSize,
        ManufacturerName = p.ManufacturerName,
        MarketedBy = p.MarketedBy,
        DrugSchedule = p.DrugSchedule.ToString(),
        IsPrescriptionRequired = p.IsPrescriptionRequired,
        IsNarcotic = p.IsNarcotic,
        StorageCondition = p.StorageCondition.ToString(),
        ShelfLifeDays = p.ShelfLifeDays,
        MinExpiryDaysOnReceipt = p.MinExpiryDaysOnReceipt,
        ExpiryAlertDays = p.ExpiryAlertDays,
    };

    private sealed record ParsedItem(
        ItemProfile Profile, ItemType Type, TaxPreference TaxPreference, CostingType CostingType);
}
