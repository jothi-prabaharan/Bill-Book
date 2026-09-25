using Facility.Entity.Enums;
using Facility.Entity.Models;
using Facility.Entity.TableEntities;
using Facility.Repository;
using Microsoft.EntityFrameworkCore;

namespace Facility.Api.Services;

public enum FacilityOutcome
{
    Ok = 1,
    NotFound = 2,
    Duplicate = 3,
    Invalid = 4,
}

public sealed record FacilityResult(FacilityOutcome Outcome, long? Id = null, string? Detail = null)
{
    public static FacilityResult Ok(long id) => new(FacilityOutcome.Ok, id);

    public static FacilityResult Fail(FacilityOutcome outcome, string? detail = null) => new(outcome, null, detail);
}

/// <summary>
/// Buildings, spaces and assets (S5, TK-65), in their hierarchy: a space is in
/// a building, an asset in a space or nowhere yet. Nothing is deleted: a
/// building or space is deactivated and an asset disposed, because work orders,
/// plans and contracts name them. Codes and tags are unique per branch.
/// </summary>
public sealed class FacilityService
{
    private readonly FacilityDbContext _db;
    private readonly TimeProvider _clock;

    public FacilityService(FacilityDbContext db, TimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public Task<List<BuildingView>> BuildingsAsync(CancellationToken ct) =>
        _db.Buildings.AsNoTracking().OrderBy(b => b.Code)
            .Select(b => new BuildingView
            {
                BuildingId = b.BuildingId, Code = b.Code, Name = b.Name, Floors = b.Floors, IsActive = b.IsActive,
                Spaces = _db.Spaces.Count(s => s.BuildingId == b.BuildingId),
            })
            .ToListAsync(ct);

    public async Task<FacilityResult> SaveBuildingAsync(long? id, SaveBuildingRequest request, CancellationToken ct)
    {
        string code = request.Code.Trim().ToUpperInvariant();
        if (await _db.Buildings.AnyAsync(b => b.BuildingId != (id ?? 0) && b.Code == code, ct))
        {
            return FacilityResult.Fail(FacilityOutcome.Duplicate, "Another building already uses that code.");
        }

        Building? row = id is long existing ? await _db.Buildings.FirstOrDefaultAsync(b => b.BuildingId == existing, ct) : new Building();
        if (row is null)
        {
            return FacilityResult.Fail(FacilityOutcome.NotFound);
        }

        if (!request.IsActive && row.IsActive && id is not null
            && await _db.Spaces.AnyAsync(s => s.BuildingId == row.BuildingId && s.IsActive, ct))
        {
            return FacilityResult.Fail(FacilityOutcome.Invalid, "Deactivate the building's spaces first.");
        }

        row.Code = code;
        row.Name = request.Name.Trim();
        row.Floors = request.Floors;
        row.IsActive = request.IsActive;
        if (id is null)
        {
            _db.Buildings.Add(row);
        }

        await _db.SaveChangesAsync(ct);
        return FacilityResult.Ok(row.BuildingId);
    }

    public Task<List<SpaceView>> SpacesAsync(long? buildingId, CancellationToken ct) =>
        (from s in _db.Spaces.AsNoTracking()
         join b in _db.Buildings on s.BuildingId equals b.BuildingId
         where buildingId == null || s.BuildingId == buildingId
         orderby b.Code, s.Floor, s.Code
         select new SpaceView
         {
             SpaceId = s.SpaceId, BuildingId = s.BuildingId, BuildingName = b.Name, Code = s.Code, Name = s.Name,
             SpaceKind = s.SpaceKind, Floor = s.Floor, Capacity = s.Capacity, IsActive = s.IsActive,
         }).ToListAsync(ct);

    public async Task<FacilityResult> SaveSpaceAsync(long? id, SaveSpaceRequest request, CancellationToken ct)
    {
        Building? building = await _db.Buildings.AsNoTracking().FirstOrDefaultAsync(b => b.BuildingId == request.BuildingId, ct);
        if (building is null || !building.IsActive)
        {
            return FacilityResult.Fail(FacilityOutcome.Invalid, "Choose an active building of this branch.");
        }

        if (FloorProblem(request.Floor, building.Floors) is string floor)
        {
            return FacilityResult.Fail(FacilityOutcome.Invalid, floor);
        }

        string code = request.Code.Trim().ToUpperInvariant();
        if (await _db.Spaces.AnyAsync(s => s.SpaceId != (id ?? 0) && s.Code == code, ct))
        {
            return FacilityResult.Fail(FacilityOutcome.Duplicate, "Another space already uses that code.");
        }

        Space? row = id is long existing ? await _db.Spaces.FirstOrDefaultAsync(s => s.SpaceId == existing, ct) : new Space();
        if (row is null)
        {
            return FacilityResult.Fail(FacilityOutcome.NotFound);
        }

        row.BuildingId = request.BuildingId;
        row.Code = code;
        row.Name = request.Name.Trim();
        row.SpaceKind = request.SpaceKind;
        row.Floor = request.Floor;
        row.Capacity = request.Capacity;
        row.IsActive = request.IsActive;
        if (id is null)
        {
            _db.Spaces.Add(row);
        }

        await _db.SaveChangesAsync(ct);
        return FacilityResult.Ok(row.SpaceId);
    }

    public async Task<List<AssetView>> AssetsAsync(long? spaceId, AssetStatus? status, string? search, CancellationToken ct)
    {
        DateOnly today = DateOnly.FromDateTime(_clock.GetUtcNow().UtcDateTime);
        var rows = await (
            from a in _db.FacilityAssets.AsNoTracking()
            join s in _db.Spaces on a.SpaceId equals s.SpaceId into ss
            from s in ss.DefaultIfEmpty()
            join b in _db.Buildings on s.BuildingId equals b.BuildingId into bs
            from b in bs.DefaultIfEmpty()
            where (spaceId == null || a.SpaceId == spaceId)
                && (status == null || a.AssetStatus == status)
                && (search == null || EF.Functions.ILike(a.Name, "%" + search + "%") || EF.Functions.ILike(a.AssetTag, "%" + search + "%"))
            orderby a.AssetTag
            select new { Asset = a, SpaceName = s == null ? null : s.Name, BuildingName = b == null ? null : b.Name })
            .Take(2000)
            .ToListAsync(ct);

        return [.. rows.Select(r => new AssetView
        {
            FacilityAssetId = r.Asset.FacilityAssetId,
            AssetTag = r.Asset.AssetTag,
            Name = r.Asset.Name,
            AssetCategory = r.Asset.AssetCategory,
            SpaceId = r.Asset.SpaceId,
            SpaceName = r.SpaceName,
            BuildingName = r.BuildingName,
            Make = r.Asset.Make,
            Model = r.Asset.Model,
            SerialNo = r.Asset.SerialNo,
            PurchaseDate = r.Asset.PurchaseDate,
            WarrantyUntil = r.Asset.WarrantyUntil,
            PurchaseCost = r.Asset.PurchaseCost,
            AssetStatus = r.Asset.AssetStatus,
            IsUnderWarranty = UnderWarranty(r.Asset.WarrantyUntil, today),
        })];
    }

    public async Task<FacilityResult> SaveAssetAsync(long? id, SaveAssetRequest request, CancellationToken ct)
    {
        if (request.WarrantyUntil is DateOnly until && request.PurchaseDate is DateOnly bought && until < bought)
        {
            return FacilityResult.Fail(FacilityOutcome.Invalid, "The warranty cannot end before the purchase date.");
        }

        if (request.SpaceId is long spaceId && !await _db.Spaces.AnyAsync(s => s.SpaceId == spaceId && s.IsActive, ct))
        {
            return FacilityResult.Fail(FacilityOutcome.Invalid, "Choose an active space of this branch, or none.");
        }

        string tag = request.AssetTag.Trim().ToUpperInvariant();
        if (await _db.FacilityAssets.AnyAsync(a => a.FacilityAssetId != (id ?? 0) && a.AssetTag == tag, ct))
        {
            return FacilityResult.Fail(FacilityOutcome.Duplicate, "Another asset already has that tag.");
        }

        FacilityAsset? row = id is long existing
            ? await _db.FacilityAssets.FirstOrDefaultAsync(a => a.FacilityAssetId == existing, ct)
            : new FacilityAsset();
        if (row is null)
        {
            return FacilityResult.Fail(FacilityOutcome.NotFound);
        }

        if (row.AssetStatus == AssetStatus.Disposed && id is not null && request.AssetStatus != AssetStatus.Disposed)
        {
            return FacilityResult.Fail(FacilityOutcome.Invalid, "A disposed asset stays disposed. Add it again if it came back.");
        }

        row.AssetTag = tag;
        row.Name = request.Name.Trim();
        row.AssetCategory = request.AssetCategory;
        row.SpaceId = request.SpaceId;
        row.Make = Trimmed(request.Make);
        row.Model = Trimmed(request.Model);
        row.SerialNo = Trimmed(request.SerialNo);
        row.PurchaseDate = request.PurchaseDate;
        row.WarrantyUntil = request.WarrantyUntil;
        row.PurchaseCost = request.PurchaseCost;
        row.AssetStatus = request.AssetStatus;
        if (id is null)
        {
            _db.FacilityAssets.Add(row);
        }

        await _db.SaveChangesAsync(ct);
        return FacilityResult.Ok(row.FacilityAssetId);
    }

    // ---- Rules, pure and public for tests -----------------------------------

    /// <summary>A floor from the lowest basement to the building's top floor. 0 is the ground floor.</summary>
    public static string? FloorProblem(int floor, int buildingFloors) =>
        floor >= buildingFloors ? "That floor is above the building's top floor." : null;

    public static bool UnderWarranty(DateOnly? until, DateOnly today) => until is DateOnly u && u >= today;

    private static string? Trimmed(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
