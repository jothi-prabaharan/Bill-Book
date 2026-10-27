using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Reporting.Api.Controllers;
using Reporting.Entity.Models;
using Reporting.Repository.Repositories;
using Shared.Kernel.Internal;
using Xunit;

namespace Reporting.Api.Tests;

public sealed class BatchTrackingStatusReportTests
{
    [Fact]
    public void Controller_HasExpectedSecurityAttributes()
    {
        var type = typeof(BatchTrackingStatusReportController);

        Assert.NotNull(type.GetCustomAttribute<ApiControllerAttribute>());
        Assert.NotNull(type.GetCustomAttribute<AuthorizeAttribute>());
        Assert.NotNull(type.GetCustomAttribute<RequireBusinessTypeAttribute>());

        var moduleAttr = type.GetCustomAttribute<RequireModulePermissionAttribute>();
        Assert.NotNull(moduleAttr);
        Assert.Equal("reports", moduleAttr.Module);

        var runMethod = type.GetMethod(nameof(BatchTrackingStatusReportController.Run));
        Assert.NotNull(runMethod);
        var permAttr = runMethod.GetCustomAttribute<RequirePermissionAttribute>();
        Assert.NotNull(permAttr);
        Assert.Equal("inventory.view", permAttr.Permission);

        var itemsMethod = type.GetMethod(nameof(BatchTrackingStatusReportController.Items));
        Assert.NotNull(itemsMethod);
        var itemPerm = itemsMethod.GetCustomAttribute<RequirePermissionAttribute>();
        Assert.NotNull(itemPerm);
        Assert.Equal("inventory.view", itemPerm.Permission);

        var whMethod = type.GetMethod(nameof(BatchTrackingStatusReportController.Warehouses));
        Assert.NotNull(whMethod);
        var whPerm = whMethod.GetCustomAttribute<RequirePermissionAttribute>();
        Assert.NotNull(whPerm);
        Assert.Equal("inventory.view", whPerm.Permission);
    }

    [Fact]
    public void Refusals_RequiresAsOfDate()
    {
        var req = new BatchTrackingStatusReportRequest { AsOfDate = null };
        var refusals = BatchTrackingStatusReportRepository.Refusals(req);

        Assert.Contains(refusals, r => r.Field == nameof(BatchTrackingStatusReportRequest.AsOfDate));
    }

    [Fact]
    public void Refusals_PassesWhenAsOfDateProvided()
    {
        var req = new BatchTrackingStatusReportRequest { AsOfDate = new DateOnly(2026, 10, 27) };
        var refusals = BatchTrackingStatusReportRepository.Refusals(req);

        Assert.Empty(refusals);
    }

    [Theory]
    [InlineData(-5, true, false)]
    [InlineData(0, false, true)]
    [InlineData(15, false, true)]
    [InlineData(30, false, true)]
    [InlineData(31, false, false)]
    [InlineData(100, false, false)]
    public void ExpiryStatus_EvaluatesCorrectly(int daysDiff, bool expectedExpired, bool expectedNearExpiry)
    {
        bool isExpired = daysDiff < 0;
        bool isNearExpiry = daysDiff >= 0 && daysDiff <= 30;

        Assert.Equal(expectedExpired, isExpired);
        Assert.Equal(expectedNearExpiry, isNearExpiry);
    }
}
