using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Reporting.Api.Controllers;
using Reporting.Entity.Enums;
using Reporting.Entity.Models;
using Reporting.Repository.Repositories;
using Shared.Kernel.Internal;
using Xunit;

namespace Reporting.Api.Tests;

public sealed class SerialTrackingStatusReportTests
{
    [Fact]
    public void Controller_HasExpectedSecurityAttributes()
    {
        var type = typeof(SerialTrackingStatusReportController);

        Assert.NotNull(type.GetCustomAttribute<ApiControllerAttribute>());
        Assert.NotNull(type.GetCustomAttribute<AuthorizeAttribute>());
        Assert.NotNull(type.GetCustomAttribute<RequireBusinessTypeAttribute>());

        var moduleAttr = type.GetCustomAttribute<RequireModulePermissionAttribute>();
        Assert.NotNull(moduleAttr);
        Assert.Equal("reports", moduleAttr.Module);

        var runMethod = type.GetMethod(nameof(SerialTrackingStatusReportController.Run));
        Assert.NotNull(runMethod);
        var permAttr = runMethod.GetCustomAttribute<RequirePermissionAttribute>();
        Assert.NotNull(permAttr);
        Assert.Equal("inventory.view", permAttr.Permission);

        var itemsMethod = type.GetMethod(nameof(SerialTrackingStatusReportController.Items));
        Assert.NotNull(itemsMethod);
        var itemsPerm = itemsMethod.GetCustomAttribute<RequirePermissionAttribute>();
        Assert.NotNull(itemsPerm);
        Assert.Equal("inventory.view", itemsPerm.Permission);

        var whMethod = type.GetMethod(nameof(SerialTrackingStatusReportController.Warehouses));
        Assert.NotNull(whMethod);
        var whPerm = whMethod.GetCustomAttribute<RequirePermissionAttribute>();
        Assert.NotNull(whPerm);
        Assert.Equal("inventory.view", whPerm.Permission);
    }

    [Fact]
    public void Refusals_FailsWhenAsOfDateMissing()
    {
        var req = new SerialTrackingStatusReportRequest { AsOfDate = null };
        var refusals = SerialTrackingStatusReportRepository.Refusals(req);

        Assert.Contains(refusals, r => r.Field == nameof(SerialTrackingStatusReportRequest.AsOfDate));
    }

    [Fact]
    public void Refusals_PassesWhenAsOfDateProvided()
    {
        var req = new SerialTrackingStatusReportRequest { AsOfDate = new DateOnly(2026, 10, 28) };
        var refusals = SerialTrackingStatusReportRepository.Refusals(req);

        Assert.Empty(refusals);
    }

    [Theory]
    [InlineData(SerialStatus.InStock, false, true)]
    [InlineData(SerialStatus.InStock, true, false)]
    [InlineData(SerialStatus.Sold, true, false)]
    [InlineData(SerialStatus.Returned, false, true)]
    [InlineData(SerialStatus.Scrapped, true, false)]
    public void EffectiveAvailable_EvaluatesCorrectly(SerialStatus status, bool issuedByAsOf, bool expectedInStock)
    {
        bool isInStock = (status == SerialStatus.InStock || status == SerialStatus.Returned) && !issuedByAsOf;
        Assert.Equal(expectedInStock, isInStock);
    }
}
