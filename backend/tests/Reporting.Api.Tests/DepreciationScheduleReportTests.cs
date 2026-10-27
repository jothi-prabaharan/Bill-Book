using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Reporting.Api.Controllers;
using Reporting.Entity.Models;
using Reporting.Repository.Repositories;
using Shared.Kernel.Apps;
using Shared.Kernel.Internal;
using Xunit;

namespace Reporting.Api.Tests;

public sealed class DepreciationScheduleReportTests
{
    [Fact]
    public void Controller_HasExpectedSecurityAttributes()
    {
        var type = typeof(DepreciationScheduleReportController);

        Assert.NotNull(type.GetCustomAttribute<ApiControllerAttribute>());
        Assert.NotNull(type.GetCustomAttribute<AuthorizeAttribute>());

        // Guarded with AddOn.FixedAssets alone (no RequireBusinessType beside it)
        var addOnAttr = type.GetCustomAttribute<RequireAddOnAttribute>();
        Assert.NotNull(addOnAttr);
        Assert.Equal(AddOn.FixedAssets, addOnAttr.AddOn);

        Assert.Null(type.GetCustomAttribute<RequireBusinessTypeAttribute>());

        var moduleAttr = type.GetCustomAttribute<RequireModulePermissionAttribute>();
        Assert.NotNull(moduleAttr);
        Assert.Equal("reports", moduleAttr.Module);

        var runMethod = type.GetMethod(nameof(DepreciationScheduleReportController.Run));
        Assert.NotNull(runMethod);
        var permAttr = runMethod.GetCustomAttribute<RequirePermissionAttribute>();
        Assert.NotNull(permAttr);
        Assert.Equal("accounting.view", permAttr.Permission);

        var catMethod = type.GetMethod(nameof(DepreciationScheduleReportController.Categories));
        Assert.NotNull(catMethod);
        var catPerm = catMethod.GetCustomAttribute<RequirePermissionAttribute>();
        Assert.NotNull(catPerm);
        Assert.Equal("accounting.view", catPerm.Permission);

        var lookupsMethod = type.GetMethod(nameof(DepreciationScheduleReportController.Lookups));
        Assert.NotNull(lookupsMethod);
        var lookupsPerm = lookupsMethod.GetCustomAttribute<RequirePermissionAttribute>();
        Assert.NotNull(lookupsPerm);
        Assert.Equal("accounting.view", lookupsPerm.Permission);
    }

    [Fact]
    public void Refusals_FailsWhenFromDateMissing()
    {
        var req = new DepreciationScheduleReportRequest
        {
            From = null,
            To = new DateOnly(2026, 3, 31)
        };
        var refusals = DepreciationScheduleReportRepository.Refusals(req);

        Assert.Contains(refusals, r => r.Field == nameof(DepreciationScheduleReportRequest.From));
    }

    [Fact]
    public void Refusals_FailsWhenToDateMissing()
    {
        var req = new DepreciationScheduleReportRequest
        {
            From = new DateOnly(2025, 4, 1),
            To = null
        };
        var refusals = DepreciationScheduleReportRepository.Refusals(req);

        Assert.Contains(refusals, r => r.Field == nameof(DepreciationScheduleReportRequest.To));
    }

    [Fact]
    public void Refusals_FailsWhenToDateEarlierThanFromDate()
    {
        var req = new DepreciationScheduleReportRequest
        {
            From = new DateOnly(2026, 3, 31),
            To = new DateOnly(2025, 4, 1)
        };
        var refusals = DepreciationScheduleReportRepository.Refusals(req);

        Assert.Contains(refusals, r => r.Field == nameof(DepreciationScheduleReportRequest.To));
    }

    [Fact]
    public void Refusals_PassesWhenValidDatesProvided()
    {
        var req = new DepreciationScheduleReportRequest
        {
            From = new DateOnly(2025, 4, 1),
            To = new DateOnly(2026, 3, 31)
        };
        var refusals = DepreciationScheduleReportRepository.Refusals(req);

        Assert.Empty(refusals);
    }

    [Fact]
    public void Compose_ComputesDepreciationAndNbvCorrectly()
    {
        var from = new DateOnly(2025, 4, 1);
        var to = new DateOnly(2026, 3, 31);

        var rows = new List<FixedAssetPeriodRow>
        {
            // Asset 1: Active Straight Line
            new()
            {
                FixedAssetId = 1,
                FixedAssetCategoryId = 10,
                AssetType = "Office Equipment",
                AssetNumber = "FA-001",
                AssetName = "MacBook Pro",
                AssetAccount = "Office Equipment",
                AccumDepAccount = "Accum Dep - Equipment",
                DepExpenseAccount = "Depreciation Expense",
                PurchaseDate = new DateOnly(2024, 1, 15),
                Cost = 100000m,
                DepMethod = "Straight Line",
                DepStartDate = new DateOnly(2024, 2, 1),
                Rate = 20m,
                EffectiveLife = 5,
                ResidualValue = 5000m,
                OpeningAccumDep = 20000m,
                Depreciation = 19000m,
                DisposalAccumDep = 0m,
                ClosingAccumDep = 39000m,
                OpeningNbv = 80000m,
                ClosingNbv = 61000m,
                DisposalDate = null
            },
            // Asset 2: Active Written Down Value
            new()
            {
                FixedAssetId = 2,
                FixedAssetCategoryId = 10,
                AssetType = "Office Equipment",
                AssetNumber = "FA-002",
                AssetName = "Photocopier",
                AssetAccount = "Office Equipment",
                AccumDepAccount = "Accum Dep - Equipment",
                DepExpenseAccount = "Depreciation Expense",
                PurchaseDate = new DateOnly(2024, 6, 1),
                Cost = 80000m,
                DepMethod = "Written Down Value",
                DepStartDate = new DateOnly(2024, 7, 1),
                Rate = 15m,
                EffectiveLife = 10,
                ResidualValue = 4000m,
                OpeningAccumDep = 9000m,
                Depreciation = 10650m,
                DisposalAccumDep = 0m,
                ClosingAccumDep = 19650m,
                OpeningNbv = 71000m,
                ClosingNbv = 60350m,
                DisposalDate = null
            },
            // Asset 3: Disposed asset (accum dep written off)
            new()
            {
                FixedAssetId = 3,
                FixedAssetCategoryId = 20,
                AssetType = "Vehicles",
                AssetNumber = "FA-003",
                AssetName = "Delivery Scooter",
                AssetAccount = "Motor Vehicles",
                AccumDepAccount = "Accum Dep - Vehicles",
                DepExpenseAccount = "Depreciation Expense",
                PurchaseDate = new DateOnly(2023, 3, 1),
                Cost = 60000m,
                DepMethod = "Straight Line",
                DepStartDate = new DateOnly(2023, 4, 1),
                Rate = 15m,
                EffectiveLife = 6,
                ResidualValue = 3000m,
                OpeningAccumDep = 18000m,
                Depreciation = 4500m,
                DisposalAccumDep = 22500m,
                ClosingAccumDep = 0m,
                OpeningNbv = 42000m,
                ClosingNbv = 0m,
                DisposalDate = new DateOnly(2025, 9, 30)
            }
        };

        var req = new DepreciationScheduleReportRequest { From = from, To = to };
        var response = DepreciationScheduleReportRepository.Compose(rows, req, from, to);

        Assert.Equal(3, response.Summary.TotalAssets);
        Assert.Equal(2, response.Summary.ActiveAssets);
        Assert.Equal(1, response.Summary.DisposedAssets);

        // Period depreciation charged equals 19000 + 10650 + 4500 = 34150
        Assert.Equal(34150m, response.Summary.TotalDepreciation);

        // Opening accum dep: 20000 + 9000 + 18000 = 47000
        Assert.Equal(47000m, response.Summary.TotalOpeningAccumDep);

        // Disposal accum dep: 22500
        Assert.Equal(22500m, response.Summary.TotalDisposalAccumDep);

        // Closing accum dep: 39000 + 19650 + 0 = 58650
        Assert.Equal(58650m, response.Summary.TotalClosingAccumDep);

        // Closing NBV: 61000 + 60350 + 0 = 121350
        Assert.Equal(121350m, response.Summary.TotalClosingNbv);

        // 2 categories
        Assert.Equal(2, response.Categories.Count);
        var officeCat = response.Categories.First(c => c.CategoryId == 10);
        Assert.Equal(2, officeCat.Assets.Count);
        Assert.Equal(180000m, officeCat.TotalCost);
        Assert.Equal(29650m, officeCat.Depreciation);
        Assert.Equal(58650m, officeCat.ClosingAccumDep);
        Assert.Equal(121350m, officeCat.ClosingNbv);
    }

    [Fact]
    public void Compose_FiltersByMethodCorrectly()
    {
        var from = new DateOnly(2025, 4, 1);
        var to = new DateOnly(2026, 3, 31);
        var rows = new List<FixedAssetPeriodRow>
        {
            new() { FixedAssetId = 1, AssetType = "Equipment", AssetNumber = "FA-001", AssetName = "Item 1", DepMethod = "Straight Line" },
            new() { FixedAssetId = 2, AssetType = "Equipment", AssetNumber = "FA-002", AssetName = "Item 2", DepMethod = "Written Down Value" }
        };

        var slReq = new DepreciationScheduleReportRequest { From = from, To = to, Method = "Straight Line" };
        var slRes = DepreciationScheduleReportRepository.Compose(rows, slReq, from, to);
        Assert.Equal(1, slRes.Summary.TotalAssets);
        Assert.Equal("Item 1", slRes.Categories[0].Assets[0].AssetName);

        var wdvReq = new DepreciationScheduleReportRequest { From = from, To = to, Method = "Written Down Value" };
        var wdvRes = DepreciationScheduleReportRepository.Compose(rows, wdvReq, from, to);
        Assert.Equal(1, wdvRes.Summary.TotalAssets);
        Assert.Equal("Item 2", wdvRes.Categories[0].Assets[0].AssetName);
    }
}
