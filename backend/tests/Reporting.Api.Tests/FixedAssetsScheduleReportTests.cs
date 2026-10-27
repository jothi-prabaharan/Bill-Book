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

public sealed class FixedAssetsScheduleReportTests
{
    [Fact]
    public void Controller_HasExpectedSecurityAttributes()
    {
        var type = typeof(FixedAssetsScheduleReportController);

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

        var runMethod = type.GetMethod(nameof(FixedAssetsScheduleReportController.Run));
        Assert.NotNull(runMethod);
        var permAttr = runMethod.GetCustomAttribute<RequirePermissionAttribute>();
        Assert.NotNull(permAttr);
        Assert.Equal("accounting.view", permAttr.Permission);

        var catMethod = type.GetMethod(nameof(FixedAssetsScheduleReportController.Categories));
        Assert.NotNull(catMethod);
        var catPerm = catMethod.GetCustomAttribute<RequirePermissionAttribute>();
        Assert.NotNull(catPerm);
        Assert.Equal("accounting.view", catPerm.Permission);

        var lookupsMethod = type.GetMethod(nameof(FixedAssetsScheduleReportController.Lookups));
        Assert.NotNull(lookupsMethod);
        var lookupsPerm = lookupsMethod.GetCustomAttribute<RequirePermissionAttribute>();
        Assert.NotNull(lookupsPerm);
        Assert.Equal("accounting.view", lookupsPerm.Permission);
    }

    [Fact]
    public void Refusals_FailsWhenFromDateMissing()
    {
        var req = new FixedAssetsScheduleReportRequest
        {
            From = null,
            To = new DateOnly(2026, 3, 31)
        };
        var refusals = FixedAssetsScheduleReportRepository.Refusals(req);

        Assert.Contains(refusals, r => r.Field == nameof(FixedAssetsScheduleReportRequest.From));
    }

    [Fact]
    public void Refusals_FailsWhenToDateMissing()
    {
        var req = new FixedAssetsScheduleReportRequest
        {
            From = new DateOnly(2025, 4, 1),
            To = null
        };
        var refusals = FixedAssetsScheduleReportRepository.Refusals(req);

        Assert.Contains(refusals, r => r.Field == nameof(FixedAssetsScheduleReportRequest.To));
    }

    [Fact]
    public void Refusals_FailsWhenToDateEarlierThanFromDate()
    {
        var req = new FixedAssetsScheduleReportRequest
        {
            From = new DateOnly(2026, 3, 31),
            To = new DateOnly(2025, 4, 1)
        };
        var refusals = FixedAssetsScheduleReportRepository.Refusals(req);

        Assert.Contains(refusals, r => r.Field == nameof(FixedAssetsScheduleReportRequest.To));
    }

    [Fact]
    public void Refusals_PassesWhenValidDatesProvided()
    {
        var req = new FixedAssetsScheduleReportRequest
        {
            From = new DateOnly(2025, 4, 1),
            To = new DateOnly(2026, 3, 31)
        };
        var refusals = FixedAssetsScheduleReportRepository.Refusals(req);

        Assert.Empty(refusals);
    }

    [Fact]
    public void Compose_ComputesRollForwardArithmeticCorrectly()
    {
        var from = new DateOnly(2025, 4, 1);
        var to = new DateOnly(2026, 3, 31);

        var rows = new List<FixedAssetPeriodRow>
        {
            // Asset 1: Existing asset held throughout
            new()
            {
                FixedAssetId = 1,
                FixedAssetCategoryId = 10,
                AssetType = "Computer Equipment",
                AssetNumber = "FA-001",
                AssetName = "MacBook Pro M3",
                AssetAccount = "Office Equipment",
                AccumDepAccount = "Accum Dep - Equipment",
                DepExpenseAccount = "Depreciation Expense",
                PurchaseDate = new DateOnly(2024, 1, 15),
                PurchasePrice = 200000m,
                Cost = 200000m,
                OpeningCost = 200000m,
                AdditionCost = 0m,
                DisposalCost = 0m,
                ClosingCost = 200000m,
                OpeningAccumDep = 40000m,
                Depreciation = 30000m,
                DisposalAccumDep = 0m,
                ClosingAccumDep = 70000m,
                OpeningNbv = 160000m,
                ClosingNbv = 130000m,
                DisposalDate = null
            },
            // Asset 2: Addition during the period
            new()
            {
                FixedAssetId = 2,
                FixedAssetCategoryId = 10,
                AssetType = "Computer Equipment",
                AssetNumber = "FA-002",
                AssetName = "Dell Server R750",
                AssetAccount = "Office Equipment",
                AccumDepAccount = "Accum Dep - Equipment",
                DepExpenseAccount = "Depreciation Expense",
                PurchaseDate = new DateOnly(2025, 6, 1),
                PurchasePrice = 150000m,
                Cost = 150000m,
                OpeningCost = 0m,
                AdditionCost = 150000m,
                DisposalCost = 0m,
                ClosingCost = 150000m,
                OpeningAccumDep = 0m,
                Depreciation = 15000m,
                DisposalAccumDep = 0m,
                ClosingAccumDep = 15000m,
                OpeningNbv = 0m,
                ClosingNbv = 135000m,
                DisposalDate = null
            },
            // Asset 3: Disposed during the period
            new()
            {
                FixedAssetId = 3,
                FixedAssetCategoryId = 20,
                AssetType = "Motor Vehicles",
                AssetNumber = "FA-003",
                AssetName = "Delivery Van",
                AssetAccount = "Motor Vehicles",
                AccumDepAccount = "Accum Dep - Vehicles",
                DepExpenseAccount = "Depreciation Expense",
                PurchaseDate = new DateOnly(2023, 2, 10),
                PurchasePrice = 500000m,
                Cost = 500000m,
                OpeningCost = 500000m,
                AdditionCost = 0m,
                DisposalCost = 500000m,
                ClosingCost = 0m,
                OpeningAccumDep = 200000m,
                Depreciation = 50000m,
                DisposalAccumDep = 250000m,
                ClosingAccumDep = 0m,
                OpeningNbv = 300000m,
                ClosingNbv = 0m,
                DisposalDate = new DateOnly(2025, 12, 15),
                SaleProceeds = 280000m,
                NbvAtDisposal = 250000m,
                GainOnDisposal = 30000m,
                LossOnDisposal = 0m,
                CapitalGain = 0m
            }
        };

        var req = new FixedAssetsScheduleReportRequest { From = from, To = to };
        var response = FixedAssetsScheduleReportRepository.Compose(rows, req, from, to);

        Assert.Equal(from, response.FromDate);
        Assert.Equal(to, response.ToDate);
        Assert.Equal(3, response.Summary.TotalAssets);
        Assert.Equal(2, response.Summary.ActiveAssets);
        Assert.Equal(1, response.Summary.DisposedAssets);

        // Roll-forward cost arithmetic: Opening (700k) + Addition (150k) - Disposal (500k) == Closing (350k)
        Assert.Equal(700000m, response.Summary.TotalOpeningCost);
        Assert.Equal(150000m, response.Summary.TotalAdditionCost);
        Assert.Equal(500000m, response.Summary.TotalDisposalCost);
        Assert.Equal(350000m, response.Summary.TotalClosingCost);

        // Accumulated depreciation arithmetic: Opening (240k) + Depreciation (95k) - DisposalAccum (250k) == Closing (85k)
        Assert.Equal(240000m, response.Summary.TotalOpeningAccumDep);
        Assert.Equal(95000m, response.Summary.TotalDepreciation);
        Assert.Equal(250000m, response.Summary.TotalDisposalAccumDep);
        Assert.Equal(85000m, response.Summary.TotalClosingAccumDep);

        // Net book value arithmetic: Closing Cost (350k) - Closing Accum Dep (85k) == Closing NBV (265k)
        Assert.Equal(265000m, response.Summary.TotalClosingNbv);

        // Two distinct categories
        Assert.Equal(2, response.Categories.Count);

        var compCategory = response.Categories.First(c => c.CategoryId == 10);
        Assert.Equal("Computer Equipment", compCategory.CategoryName);
        Assert.Equal(2, compCategory.Assets.Count);
        Assert.Equal(350000m, compCategory.ClosingCost);
        Assert.Equal(85000m, compCategory.ClosingAccumDep);
        Assert.Equal(265000m, compCategory.ClosingNbv);

        var vehicleCategory = response.Categories.First(c => c.CategoryId == 20);
        Assert.Equal("Motor Vehicles", vehicleCategory.CategoryName);
        Assert.Single(vehicleCategory.Assets);
        Assert.Equal(0m, vehicleCategory.ClosingCost);
        Assert.Equal(0m, vehicleCategory.ClosingAccumDep);
        Assert.Equal(0m, vehicleCategory.ClosingNbv);
        Assert.Equal(30000m, vehicleCategory.Assets[0].GainOnDisposal);
    }

    [Fact]
    public void Compose_FiltersByStatusCorrectly()
    {
        var from = new DateOnly(2025, 4, 1);
        var to = new DateOnly(2026, 3, 31);
        var rows = new List<FixedAssetPeriodRow>
        {
            new() { FixedAssetId = 1, AssetType = "Equipment", AssetNumber = "FA-001", AssetName = "Active Item", DisposalDate = null },
            new() { FixedAssetId = 2, AssetType = "Equipment", AssetNumber = "FA-002", AssetName = "Disposed Item", DisposalDate = new DateOnly(2025, 8, 1) }
        };

        var activeReq = new FixedAssetsScheduleReportRequest { From = from, To = to, Status = "Active" };
        var activeRes = FixedAssetsScheduleReportRepository.Compose(rows, activeReq, from, to);
        Assert.Equal(1, activeRes.Summary.TotalAssets);
        Assert.Equal("Active Item", activeRes.Categories[0].Assets[0].AssetName);

        var dispReq = new FixedAssetsScheduleReportRequest { From = from, To = to, Status = "Disposed" };
        var dispRes = FixedAssetsScheduleReportRepository.Compose(rows, dispReq, from, to);
        Assert.Equal(1, dispRes.Summary.TotalAssets);
        Assert.Equal("Disposed Item", dispRes.Categories[0].Assets[0].AssetName);
    }
}
