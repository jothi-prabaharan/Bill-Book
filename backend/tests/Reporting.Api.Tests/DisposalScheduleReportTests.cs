using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Reporting.Api.Controllers;
using Reporting.Entity.Enums;
using Reporting.Entity.Models;
using Reporting.Repository.Repositories;
using Shared.Kernel.Apps;
using Shared.Kernel.Internal;
using Xunit;

namespace Reporting.Api.Tests;

public sealed class DisposalScheduleReportTests
{
    [Fact]
    public void Refusals_RequiresFromAndTo()
    {
        var noDates = new DisposalScheduleReportRequest();
        var refusals = DisposalScheduleReportRepository.Refusals(noDates).ToList();

        Assert.Contains(refusals, r => r.Field == "From" && r.Message.Contains("required", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(refusals, r => r.Field == "To" && r.Message.Contains("required", StringComparison.OrdinalIgnoreCase));

        var invalidOrder = new DisposalScheduleReportRequest
        {
            From = new DateOnly(2026, 10, 31),
            To = new DateOnly(2026, 10, 1),
        };
        var orderRefusals = DisposalScheduleReportRepository.Refusals(invalidOrder).ToList();
        Assert.Contains(orderRefusals, r => r.Field == "To" && r.Message.Contains("earlier", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Compose_CalculatesDisposalGainsLossesAndTotals()
    {
        var from = new DateOnly(2026, 4, 1);
        var to = new DateOnly(2026, 10, 28);

        // Asset 1: Sold at a LOSS (Proceeds 8,000 < NBV 10,000)
        // Cost: 25,000, AccumDep: 15,000, NBV: 10,000, Proceeds: 8,000 => Loss: 2,000, Gain: 0, CapGain: 0
        var asset1 = new FixedAssetPeriodRow
        {
            FixedAssetId = 1,
            FixedAssetCategoryId = 10,
            AssetType = "Machinery",
            AssetNumber = "AST-001",
            AssetName = "Industrial Drill",
            PurchaseDate = new DateOnly(2024, 5, 1),
            Cost = 25000m,
            DisposalDate = new DateOnly(2026, 6, 15),
            DisposalAccumDep = 15000m,
            NbvAtDisposal = 10000m,
            SaleProceeds = 8000m,
            GainOnDisposal = 0m,
            CapitalGain = 0m,
            LossOnDisposal = 2000m,
        };

        // Asset 2: Sold at a NORMAL GAIN (Proceeds 15,000 > NBV 10,000, but <= Cost 20,000)
        // Cost: 20,000, AccumDep: 10,000, NBV: 10,000, Proceeds: 15,000 => Gain: 5,000, CapGain: 0, Loss: 0
        var asset2 = new FixedAssetPeriodRow
        {
            FixedAssetId = 2,
            FixedAssetCategoryId = 10,
            AssetType = "Machinery",
            AssetNumber = "AST-002",
            AssetName = "Lathe Machine",
            PurchaseDate = new DateOnly(2024, 8, 1),
            Cost = 20000m,
            DisposalDate = new DateOnly(2026, 7, 20),
            DisposalAccumDep = 10000m,
            NbvAtDisposal = 10000m,
            SaleProceeds = 15000m,
            GainOnDisposal = 5000m,
            CapitalGain = 0m,
            LossOnDisposal = 0m,
        };

        // Asset 3: Sold at a CAPITAL GAIN (Proceeds 60,000 > Cost 50,000)
        // Cost: 50,000, AccumDep: 30,000, NBV: 20,000, Proceeds: 60,000 => Gain: 30,000, CapGain: 10,000, Loss: 0
        var asset3 = new FixedAssetPeriodRow
        {
            FixedAssetId = 3,
            FixedAssetCategoryId = 20,
            AssetType = "Vehicles",
            AssetNumber = "AST-003",
            AssetName = "Delivery Van",
            PurchaseDate = new DateOnly(2023, 2, 1),
            Cost = 50000m,
            DisposalDate = new DateOnly(2026, 9, 10),
            DisposalAccumDep = 30000m,
            NbvAtDisposal = 20000m,
            SaleProceeds = 6000m, // note proceeds 60000
            GainOnDisposal = 30000m,
            CapitalGain = 10000m,
            LossOnDisposal = 0m,
        };
        asset3.SaleProceeds = 60000m;

        // Asset 4: Not disposed (DisposalDate is null) - should be excluded from disposal report
        var asset4 = new FixedAssetPeriodRow
        {
            FixedAssetId = 4,
            FixedAssetCategoryId = 10,
            AssetType = "Machinery",
            AssetNumber = "AST-004",
            AssetName = "Active Generator",
            PurchaseDate = new DateOnly(2025, 1, 1),
            Cost = 30000m,
            DisposalDate = null,
        };

        var notes = new Dictionary<long, string?>
        {
            [1] = "Sold for scrap to Metal Recyclers",
            [2] = "Sold via Trade-in Invoice #INV-882",
            [3] = "Sold to Fleet Buyers via Bank Receipt",
        };

        var request = new DisposalScheduleReportRequest
        {
            From = from,
            To = to,
        };

        var response = DisposalScheduleReportRepository.Compose(
            [asset1, asset2, asset3, asset4],
            notes,
            request,
            from,
            to);

        Assert.Equal(from, response.FromDate);
        Assert.Equal(to, response.ToDate);
        Assert.Equal(3, response.Summary.TotalDisposals);
        Assert.Equal(95000m, response.Summary.TotalCost); // 25k + 20k + 50k
        Assert.Equal(55000m, response.Summary.TotalAccumDep); // 15k + 10k + 30k
        Assert.Equal(40000m, response.Summary.TotalNbvAtDisposal); // 10k + 10k + 20k
        Assert.Equal(83000m, response.Summary.TotalProceeds); // 8k + 15k + 60k
        Assert.Equal(35000m, response.Summary.TotalGain); // 5k + 30k
        Assert.Equal(10000m, response.Summary.TotalCapitalGain); // 10k
        Assert.Equal(2000m, response.Summary.TotalLoss); // 2k
        Assert.Equal(43000m, response.Summary.NetGainLoss); // 35k + 10k - 2k = 43k

        // 2 category groups: Machinery (2 disposals) and Vehicles (1 disposal)
        Assert.Equal(2, response.Categories.Count);

        var machinery = response.Categories.First(c => c.CategoryName == "Machinery");
        Assert.Equal(2, machinery.AssetCount);
        Assert.Equal(45000m, machinery.TotalCost);
        Assert.Equal(3000m, machinery.NetGainLoss); // 5k gain - 2k loss

        var drill = machinery.Disposals.First(d => d.FixedAssetId == 1);
        Assert.Equal("Sold for scrap to Metal Recyclers", drill.Reference);
        Assert.Equal(2000m, drill.LossOnDisposal);
        Assert.Equal(0m, drill.GainOnDisposal);
    }

    [Fact]
    public void Controller_CarriesExpectedAttributes()
    {
        var type = typeof(DisposalScheduleReportController);

        var addOnAttr = type.GetCustomAttribute<RequireAddOnAttribute>();
        Assert.NotNull(addOnAttr);
        Assert.Equal(AddOn.FixedAssets, addOnAttr.AddOn);

        // Strict architectural rule: must NOT have RequireBusinessType beside RequireAddOn
        var bizTypeAttr = type.GetCustomAttribute<RequireBusinessTypeAttribute>();
        Assert.Null(bizTypeAttr);

        var moduleAttr = type.GetCustomAttribute<RequireModulePermissionAttribute>();
        Assert.NotNull(moduleAttr);
        Assert.Equal("reports", moduleAttr.Module);

        var runMethod = type.GetMethod("Run");
        Assert.NotNull(runMethod);
        var permAttr = runMethod.GetCustomAttribute<RequirePermissionAttribute>();
        Assert.NotNull(permAttr);
        Assert.Equal("accounting.view", permAttr.Permission);
    }

    [Fact]
    public async Task Controller_ForbidsCrossTenantAccess()
    {
        var controller = new DisposalScheduleReportController(null!);
        var userOrg = Guid.NewGuid();
        var requestedOrg = Guid.NewGuid();

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new Claim(OrgRouteMustMatchTokenAttribute.ClaimType, userOrg.ToString())
                ])),
            },
        };

        var request = new DisposalScheduleReportRequest
        {
            From = new DateOnly(2026, 4, 1),
            To = new DateOnly(2026, 10, 28),
            OrgId = requestedOrg,
        };

        var result = await controller.Run(request, CancellationToken.None);
        Assert.IsType<ForbidResult>(result);
    }
}
