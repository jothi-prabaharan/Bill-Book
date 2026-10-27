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

public sealed class FixedAssetReconciliationReportTests
{
    [Fact]
    public void Refusals_RequiresFromAndTo()
    {
        var noDates = new FixedAssetReconciliationReportRequest();
        var refusals = FixedAssetReconciliationReportRepository.Refusals(noDates).ToList();

        Assert.Contains(refusals, r => r.Field == "From" && r.Message.Contains("required", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(refusals, r => r.Field == "To" && r.Message.Contains("required", StringComparison.OrdinalIgnoreCase));

        var invalidOrder = new FixedAssetReconciliationReportRequest
        {
            From = new DateOnly(2026, 10, 31),
            To = new DateOnly(2026, 10, 1),
        };
        var orderRefusals = FixedAssetReconciliationReportRepository.Refusals(invalidOrder).ToList();
        Assert.Contains(orderRefusals, r => r.Field == "To" && r.Message.Contains("earlier", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Compose_ReconcilesAccountsAndIdentifiesDiscrepancies()
    {
        var from = new DateOnly(2026, 4, 1);
        var to = new DateOnly(2026, 10, 28);

        // Account 1: 1510 Computer Equipment (Asset Account) - Matches perfectly
        var a1Register = new FixedAssetReconciliationRow
        {
            AccountId = 1,
            AccountCode = "1510",
            Account = "Computer Equipment",
            Source = nameof(ReconciliationSide.Register),
            OpeningCost = 80000m,
            CostDebits = 20000m,
            CostCredits = 0m,
            ClosingCost = 100000m,
            OpeningBookValue = 80000m,
            ClosingBookValue = 100000m,
        };
        var a1Ledger = new FixedAssetReconciliationRow
        {
            AccountId = 1,
            AccountCode = "1510",
            Account = "Computer Equipment",
            Source = nameof(ReconciliationSide.Ledger),
            OpeningCost = 80000m,
            CostDebits = 20000m,
            CostCredits = 0m,
            ClosingCost = 100000m,
            OpeningBookValue = 80000m,
            ClosingBookValue = 100000m,
        };

        // Account 2: 1520 Furniture & Fixtures (Asset Account) - Ledger has uncapitalised manual journal of 10,000
        var a2Register = new FixedAssetReconciliationRow
        {
            AccountId = 2,
            AccountCode = "1520",
            Account = "Furniture & Fixtures",
            Source = nameof(ReconciliationSide.Register),
            OpeningCost = 50000m,
            CostDebits = 0m,
            CostCredits = 0m,
            ClosingCost = 50000m,
            OpeningBookValue = 50000m,
            ClosingBookValue = 50000m,
        };
        var a2Ledger = new FixedAssetReconciliationRow
        {
            AccountId = 2,
            AccountCode = "1520",
            Account = "Furniture & Fixtures",
            Source = nameof(ReconciliationSide.Ledger),
            OpeningCost = 50000m,
            CostDebits = 10000m,
            CostCredits = 0m,
            ClosingCost = 60000m,
            OpeningBookValue = 50000m,
            ClosingBookValue = 60000m,
        };

        // Account 3: 1519 Accum Dep - Computers (Accum Dep Account) - Register has charged 5,000 more than posted
        var a3Register = new FixedAssetReconciliationRow
        {
            AccountId = 3,
            AccountCode = "1519",
            Account = "Accum Dep - Computers",
            Source = nameof(ReconciliationSide.Register),
            OpeningAccumDep = 20000m,
            AccumDepCredits = 10000m,
            AccumDepDebits = 0m,
            ClosingAccumDep = 30000m,
            OpeningBookValue = -20000m,
            ClosingBookValue = -30000m,
        };
        var a3Ledger = new FixedAssetReconciliationRow
        {
            AccountId = 3,
            AccountCode = "1519",
            Account = "Accum Dep - Computers",
            Source = nameof(ReconciliationSide.Ledger),
            OpeningAccumDep = 20000m,
            AccumDepCredits = 5000m,
            AccumDepDebits = 0m,
            ClosingAccumDep = 25000m,
            OpeningBookValue = -20000m,
            ClosingBookValue = -25000m,
        };

        var accumDepIds = new HashSet<long> { 3 };
        var request = new FixedAssetReconciliationReportRequest
        {
            From = from,
            To = to,
        };

        var response = FixedAssetReconciliationReportRepository.Compose(
            [a1Register, a1Ledger, a2Register, a2Ledger, a3Register, a3Ledger],
            accumDepIds,
            request,
            from,
            to);

        Assert.Equal(from, response.FromDate);
        Assert.Equal(to, response.ToDate);
        Assert.Equal(3, response.Summary.TotalAccounts);
        Assert.Equal(1, response.Summary.ReconciledAccounts);
        Assert.Equal(2, response.Summary.DiscrepantAccounts);

        // Account 1 verified
        var acc1 = response.Accounts.First(a => a.AccountId == 1);
        Assert.True(acc1.IsReconciled);
        Assert.Equal(0m, acc1.ClosingDifference);
        Assert.Equal("Register matches ledger", acc1.DifferenceExplanation);
        Assert.Equal("Asset", acc1.AccountType);

        // Account 2 verified (Register is 10,000 below ledger)
        var acc2 = response.Accounts.First(a => a.AccountId == 2);
        Assert.False(acc2.IsReconciled);
        Assert.Equal(-10000m, acc2.ClosingDifference);
        Assert.Contains("below the ledger", acc2.DifferenceExplanation);
        Assert.Contains("10,000.00", acc2.DifferenceExplanation);
        Assert.Equal("Asset", acc2.AccountType);

        // Account 3 verified (Register is 5,000 above ledger)
        var acc3 = response.Accounts.First(a => a.AccountId == 3);
        Assert.False(acc3.IsReconciled);
        Assert.Equal(5000m, acc3.ClosingDifference);
        Assert.Contains("above the ledger", acc3.DifferenceExplanation);
        Assert.Contains("5,000.00", acc3.DifferenceExplanation);
        Assert.Equal("AccumulatedDepreciation", acc3.AccountType);

        // Net book values:
        // Register NBV = 100k (computers cost) + 50k (furniture cost) - 30k (accum dep) = 120,000
        // Ledger NBV = 100k (computers cost) + 60k (furniture cost) - 25k (accum dep) = 135,000
        Assert.Equal(120000m, response.Summary.RegisterTotalNbv);
        Assert.Equal(135000m, response.Summary.LedgerTotalNbv);
        Assert.Equal(-15000m, response.Summary.TotalNbvDifference);
        Assert.Contains("15,000.00 below the general ledger", response.Summary.Explanation);
    }

    [Fact]
    public void Controller_CarriesExpectedAttributes()
    {
        var type = typeof(FixedAssetReconciliationReportController);

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
        var controller = new FixedAssetReconciliationReportController(null!);
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

        var request = new FixedAssetReconciliationReportRequest
        {
            From = new DateOnly(2026, 4, 1),
            To = new DateOnly(2026, 10, 28),
            OrgId = requestedOrg,
        };

        var result = await controller.Run(request, CancellationToken.None);
        Assert.IsType<ForbidResult>(result);
    }
}
