using Facility.Api.Services;
using Facility.Entity.Enums;
using Facility.Entity.Models;
using Facility.Repository;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Facility.Api.Tests;

/// <summary>
/// Buildings, spaces and assets against a real database (S5, TK-65). The
/// card's Done-when: they can be created, listed and deactivated.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class FacilityServiceTests
{
    private readonly PostgresFixture _postgres;

    public FacilityServiceTests(PostgresFixture postgres) => _postgres = postgres;

    private static FacilityService Service(FacilityDbContext db) => new(db, TimeProvider.System);

    [SkippableFact]
    public async Task A_building_space_and_asset_are_created_listed_and_deactivated()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);
        await using FacilityDbContext db = _postgres.CreateContext(Guid.NewGuid(), Guid.NewGuid());
        FacilityService service = Service(db);

        long building = (await service.SaveBuildingAsync(null, new SaveBuildingRequest { Code = "b1", Name = "Main block", Floors = 3 }, default)).Id!.Value;
        long space = (await service.SaveSpaceAsync(null, new SaveSpaceRequest { BuildingId = building, Code = "b1-204", Name = "Class VI A", Floor = 2 }, default)).Id!.Value;
        long asset = (await service.SaveAssetAsync(null, new SaveAssetRequest
        {
            AssetTag = "ac-0001", Name = "Split AC 1.5T", AssetCategory = AssetCategory.Hvac, SpaceId = space,
            PurchaseDate = new DateOnly(2025, 4, 1), WarrantyUntil = new DateOnly(2099, 3, 31),
        }, default)).Id!.Value;

        Assert.Equal("B1", (await service.BuildingsAsync(default)).Single().Code);
        Assert.Equal("Main block", (await service.SpacesAsync(building, default)).Single().BuildingName);
        AssetView listed = (await service.AssetsAsync(null, null, null, default)).Single();
        Assert.Equal("AC-0001", listed.AssetTag);
        Assert.Equal("Class VI A", listed.SpaceName);
        Assert.True(listed.IsUnderWarranty);

        // The building cannot go while its space is active; the space can, then the building.
        Assert.Equal(FacilityOutcome.Invalid,
            (await service.SaveBuildingAsync(building, new SaveBuildingRequest { Code = "B1", Name = "Main block", Floors = 3, IsActive = false }, default)).Outcome);
        Assert.Equal(FacilityOutcome.Ok,
            (await service.SaveSpaceAsync(space, new SaveSpaceRequest { BuildingId = building, Code = "B1-204", Name = "Class VI A", Floor = 2, IsActive = false }, default)).Outcome);
        Assert.Equal(FacilityOutcome.Ok,
            (await service.SaveBuildingAsync(building, new SaveBuildingRequest { Code = "B1", Name = "Main block", Floors = 3, IsActive = false }, default)).Outcome);

        Assert.Equal(FacilityOutcome.Ok, (await service.SaveAssetAsync(asset, new SaveAssetRequest
        {
            AssetTag = "AC-0001", Name = "Split AC 1.5T", AssetStatus = AssetStatus.Disposed,
        }, default)).Outcome);
        Assert.Single(await service.AssetsAsync(null, AssetStatus.Disposed, null, default));
    }

    [SkippableFact]
    public async Task Codes_and_tags_are_unique_in_a_branch_but_not_across_branches()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);
        await using FacilityDbContext mine = _postgres.CreateContext(Guid.NewGuid(), Guid.NewGuid());
        await using FacilityDbContext theirs = _postgres.CreateContext(Guid.NewGuid(), Guid.NewGuid());

        Assert.Equal(FacilityOutcome.Ok, (await Service(mine).SaveBuildingAsync(null, new SaveBuildingRequest { Code = "B1", Name = "Main" }, default)).Outcome);
        Assert.Equal(FacilityOutcome.Duplicate, (await Service(mine).SaveBuildingAsync(null, new SaveBuildingRequest { Code = "b1", Name = "Other" }, default)).Outcome);
        Assert.Equal(FacilityOutcome.Ok, (await Service(theirs).SaveBuildingAsync(null, new SaveBuildingRequest { Code = "B1", Name = "Main" }, default)).Outcome);
        Assert.Single(await Service(theirs).BuildingsAsync(default));
    }

    [SkippableFact]
    public async Task A_space_is_in_an_active_building_and_an_asset_in_an_active_space()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);
        await using FacilityDbContext db = _postgres.CreateContext(Guid.NewGuid(), Guid.NewGuid());
        FacilityService service = Service(db);

        long building = (await service.SaveBuildingAsync(null, new SaveBuildingRequest { Code = "OLD", Name = "Old block", IsActive = false }, default)).Id!.Value;

        Assert.Equal(FacilityOutcome.Invalid, (await service.SaveSpaceAsync(null, new SaveSpaceRequest { BuildingId = building, Code = "S1", Name = "Store" }, default)).Outcome);
        Assert.Equal(FacilityOutcome.Invalid, (await service.SaveAssetAsync(null, new SaveAssetRequest { AssetTag = "X1", Name = "Pump", SpaceId = 999 }, default)).Outcome);
        Assert.Equal(FacilityOutcome.Ok, (await service.SaveAssetAsync(null, new SaveAssetRequest { AssetTag = "X1", Name = "Pump" }, default)).Outcome);
    }

    [SkippableFact]
    public async Task A_disposed_asset_stays_disposed()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);
        await using FacilityDbContext db = _postgres.CreateContext(Guid.NewGuid(), Guid.NewGuid());
        FacilityService service = Service(db);

        long asset = (await service.SaveAssetAsync(null, new SaveAssetRequest { AssetTag = "P1", Name = "Projector", AssetStatus = AssetStatus.Disposed }, default)).Id!.Value;

        Assert.Equal(FacilityOutcome.Invalid,
            (await service.SaveAssetAsync(asset, new SaveAssetRequest { AssetTag = "P1", Name = "Projector", AssetStatus = AssetStatus.InUse }, default)).Outcome);
    }
}

/// <summary>The facility rules, pure (S5, TK-65).</summary>
public sealed class FacilityRuleTests
{
    [Theory]
    [InlineData(0, 3, true)]
    [InlineData(2, 3, true)]
    [InlineData(3, 3, false)]
    [InlineData(-1, 3, true)]
    public void A_floor_is_below_the_buildings_top(int floor, int floors, bool fine) =>
        Assert.Equal(fine, FacilityService.FloorProblem(floor, floors) is null);

    [Fact]
    public void A_warranty_runs_through_its_last_day()
    {
        var today = new DateOnly(2026, 9, 25);
        Assert.True(FacilityService.UnderWarranty(today, today));
        Assert.False(FacilityService.UnderWarranty(today.AddDays(-1), today));
        Assert.False(FacilityService.UnderWarranty(null, today));
    }
}
