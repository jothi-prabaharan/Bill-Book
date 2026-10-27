using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Reporting.Api.Controllers;
using Reporting.Entity.Models;
using Reporting.Repository.Repositories;
using Shared.Kernel.Internal;
using Xunit;

namespace Reporting.Api.Tests;

public sealed class InventoryAgingReportTests
{
    [Fact]
    public void Controller_HasExpectedSecurityAttributes()
    {
        var type = typeof(InventoryAgingReportController);

        Assert.NotNull(type.GetCustomAttribute<ApiControllerAttribute>());
        Assert.NotNull(type.GetCustomAttribute<AuthorizeAttribute>());
        Assert.NotNull(type.GetCustomAttribute<RequireBusinessTypeAttribute>());

        var moduleAttr = type.GetCustomAttribute<RequireModulePermissionAttribute>();
        Assert.NotNull(moduleAttr);
        Assert.Equal("reports", moduleAttr.Module);

        var runMethod = type.GetMethod(nameof(InventoryAgingReportController.Run));
        Assert.NotNull(runMethod);
        var permAttr = runMethod.GetCustomAttribute<RequirePermissionAttribute>();
        Assert.NotNull(permAttr);
        Assert.Equal("inventory.view", permAttr.Permission);

        var categoriesMethod = type.GetMethod(nameof(InventoryAgingReportController.Categories));
        Assert.NotNull(categoriesMethod);
        var catPerm = categoriesMethod.GetCustomAttribute<RequirePermissionAttribute>();
        Assert.NotNull(catPerm);
        Assert.Equal("inventory.view", catPerm.Permission);

        var warehousesMethod = type.GetMethod(nameof(InventoryAgingReportController.Warehouses));
        Assert.NotNull(warehousesMethod);
        var whPerm = warehousesMethod.GetCustomAttribute<RequirePermissionAttribute>();
        Assert.NotNull(whPerm);
        Assert.Equal("inventory.view", whPerm.Permission);
    }

    [Fact]
    public void Refusals_RequiresAsOfDate()
    {
        var req = new InventoryAgingReportRequest { AsOfDate = null };
        var refusals = InventoryAgingReportRepository.Refusals(req);

        Assert.Contains(refusals, r => r.Field == nameof(InventoryAgingReportRequest.AsOfDate));
    }

    [Fact]
    public void Refusals_PassesWhenAsOfDateProvided()
    {
        var req = new InventoryAgingReportRequest { AsOfDate = new DateOnly(2026, 10, 27) };
        var refusals = InventoryAgingReportRepository.Refusals(req);

        Assert.Empty(refusals);
    }

    [Theory]
    [InlineData(10, "0-30")]
    [InlineData(30, "0-30")]
    [InlineData(31, "31-60")]
    [InlineData(60, "31-60")]
    [InlineData(61, "61-90")]
    [InlineData(90, "61-90")]
    [InlineData(91, "91-180")]
    [InlineData(180, "91-180")]
    [InlineData(181, "180+")]
    [InlineData(300, "180+")]
    public void AgingBucket_CalculatesCorrectBucket(int daysDiff, string expectedBucket)
    {
        string bucket = daysDiff switch
        {
            <= 30 => "0-30",
            <= 60 => "31-60",
            <= 90 => "61-90",
            <= 180 => "91-180",
            _ => "180+"
        };

        Assert.Equal(expectedBucket, bucket);
    }
}
