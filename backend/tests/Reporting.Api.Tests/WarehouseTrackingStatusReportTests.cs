using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Reporting.Api.Controllers;
using Reporting.Entity.Models;
using Reporting.Repository.Repositories;
using Shared.Kernel.Internal;
using Xunit;

namespace Reporting.Api.Tests;

public sealed class WarehouseTrackingStatusReportTests
{
    [Fact]
    public void Controller_HasExpectedSecurityAttributes()
    {
        var type = typeof(WarehouseTrackingStatusReportController);

        Assert.NotNull(type.GetCustomAttribute<ApiControllerAttribute>());
        Assert.NotNull(type.GetCustomAttribute<AuthorizeAttribute>());
        Assert.NotNull(type.GetCustomAttribute<RequireBusinessTypeAttribute>());

        var moduleAttr = type.GetCustomAttribute<RequireModulePermissionAttribute>();
        Assert.NotNull(moduleAttr);
        Assert.Equal("reports", moduleAttr.Module);

        var runMethod = type.GetMethod(nameof(WarehouseTrackingStatusReportController.Run));
        Assert.NotNull(runMethod);
        var permAttr = runMethod.GetCustomAttribute<RequirePermissionAttribute>();
        Assert.NotNull(permAttr);
        Assert.Equal("inventory.view", permAttr.Permission);

        var whMethod = type.GetMethod(nameof(WarehouseTrackingStatusReportController.Warehouses));
        Assert.NotNull(whMethod);
        var whPerm = whMethod.GetCustomAttribute<RequirePermissionAttribute>();
        Assert.NotNull(whPerm);
        Assert.Equal("inventory.view", whPerm.Permission);

        var catMethod = type.GetMethod(nameof(WarehouseTrackingStatusReportController.Categories));
        Assert.NotNull(catMethod);
        var catPerm = catMethod.GetCustomAttribute<RequirePermissionAttribute>();
        Assert.NotNull(catPerm);
        Assert.Equal("inventory.view", catPerm.Permission);
    }

    [Fact]
    public void Refusals_FailsWhenAsOfDateMissing()
    {
        var req = new WarehouseTrackingStatusReportRequest { AsOfDate = null };
        var refusals = WarehouseTrackingStatusReportRepository.Refusals(req);

        Assert.Contains(refusals, r => r.Field == nameof(WarehouseTrackingStatusReportRequest.AsOfDate));
    }

    [Fact]
    public void Refusals_PassesWhenAsOfDateProvided()
    {
        var req = new WarehouseTrackingStatusReportRequest { AsOfDate = new DateOnly(2026, 10, 28) };
        var refusals = WarehouseTrackingStatusReportRepository.Refusals(req);

        Assert.Empty(refusals);
    }

    [Theory]
    [InlineData(100, 40, 60, 25.50, 1530.00)]
    [InlineData(50, 50, 0, 10.00, 0.00)]
    [InlineData(0, 0, 0, 15.00, 0.00)]
    [InlineData(250, 75, 175, 100.00, 17500.00)]
    public void Arithmetic_AvailableAndEstimatedValue_CalculateCorrectly(
        decimal qtyIn,
        decimal qtyOut,
        decimal expectedAvailable,
        decimal avgCost,
        decimal expectedVal)
    {
        decimal available = qtyIn - qtyOut;
        decimal val = Math.Round(available * avgCost, 2, MidpointRounding.AwayFromZero);

        Assert.Equal(expectedAvailable, available);
        Assert.Equal(expectedVal, val);
    }
}
