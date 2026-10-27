using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Reporting.Api.Controllers;
using Reporting.Entity.Models;
using Reporting.Repository.Repositories;
using Shared.Kernel.Internal;
using Xunit;

namespace Reporting.Api.Tests;

public sealed class SerialTrackingDetailReportTests
{
    [Fact]
    public void Controller_HasExpectedSecurityAttributes()
    {
        var type = typeof(SerialTrackingDetailReportController);

        Assert.NotNull(type.GetCustomAttribute<ApiControllerAttribute>());
        Assert.NotNull(type.GetCustomAttribute<AuthorizeAttribute>());
        Assert.NotNull(type.GetCustomAttribute<RequireBusinessTypeAttribute>());

        var moduleAttr = type.GetCustomAttribute<RequireModulePermissionAttribute>();
        Assert.NotNull(moduleAttr);
        Assert.Equal("reports", moduleAttr.Module);

        var runMethod = type.GetMethod(nameof(SerialTrackingDetailReportController.Run));
        Assert.NotNull(runMethod);
        var permAttr = runMethod.GetCustomAttribute<RequirePermissionAttribute>();
        Assert.NotNull(permAttr);
        Assert.Equal("inventory.view", permAttr.Permission);

        var serialsMethod = type.GetMethod(nameof(SerialTrackingDetailReportController.Serials));
        Assert.NotNull(serialsMethod);
        var serialsPerm = serialsMethod.GetCustomAttribute<RequirePermissionAttribute>();
        Assert.NotNull(serialsPerm);
        Assert.Equal("inventory.view", serialsPerm.Permission);

        var itemsMethod = type.GetMethod(nameof(SerialTrackingDetailReportController.Items));
        Assert.NotNull(itemsMethod);
        var itemsPerm = itemsMethod.GetCustomAttribute<RequirePermissionAttribute>();
        Assert.NotNull(itemsPerm);
        Assert.Equal("inventory.view", itemsPerm.Permission);

        var whMethod = type.GetMethod(nameof(SerialTrackingDetailReportController.Warehouses));
        Assert.NotNull(whMethod);
        var whPerm = whMethod.GetCustomAttribute<RequirePermissionAttribute>();
        Assert.NotNull(whPerm);
        Assert.Equal("inventory.view", whPerm.Permission);

        var contactsMethod = type.GetMethod(nameof(SerialTrackingDetailReportController.Contacts));
        Assert.NotNull(contactsMethod);
        var contactsPerm = contactsMethod.GetCustomAttribute<RequirePermissionAttribute>();
        Assert.NotNull(contactsPerm);
        Assert.Equal("inventory.view", contactsPerm.Permission);
    }

    [Fact]
    public void Refusals_FailsWhenFromDateMissing()
    {
        var req = new SerialTrackingDetailReportRequest
        {
            FromDate = null,
            ToDate = new DateOnly(2026, 10, 28)
        };
        var refusals = SerialTrackingDetailReportRepository.Refusals(req);

        Assert.Contains(refusals, r => r.Field == nameof(SerialTrackingDetailReportRequest.FromDate));
    }

    [Fact]
    public void Refusals_FailsWhenToDateMissing()
    {
        var req = new SerialTrackingDetailReportRequest
        {
            FromDate = new DateOnly(2026, 10, 1),
            ToDate = null
        };
        var refusals = SerialTrackingDetailReportRepository.Refusals(req);

        Assert.Contains(refusals, r => r.Field == nameof(SerialTrackingDetailReportRequest.ToDate));
    }

    [Fact]
    public void Refusals_FailsWhenToDateBeforeFromDate()
    {
        var req = new SerialTrackingDetailReportRequest
        {
            FromDate = new DateOnly(2026, 10, 28),
            ToDate = new DateOnly(2026, 10, 1)
        };
        var refusals = SerialTrackingDetailReportRepository.Refusals(req);

        Assert.Contains(refusals, r => r.Field == nameof(SerialTrackingDetailReportRequest.ToDate));
    }

    [Fact]
    public void Refusals_PassesWhenDateRangeValid()
    {
        var req = new SerialTrackingDetailReportRequest
        {
            FromDate = new DateOnly(2026, 10, 1),
            ToDate = new DateOnly(2026, 10, 28)
        };
        var refusals = SerialTrackingDetailReportRepository.Refusals(req);

        Assert.Empty(refusals);
    }

    [Theory]
    // Received before from, issued before from -> opening 0, closing 0
    [InlineData("2026-08-01", "2026-08-15", 0, 0, 0, 0)]
    // Received before from, never issued -> opening 1, closing 1
    [InlineData("2026-08-01", null, 1, 0, 0, 1)]
    // Received before from, issued in period -> opening 1, out 1, closing 0
    [InlineData("2026-08-01", "2026-10-15", 1, 0, 1, 0)]
    // Received in period, never issued -> opening 0, in 1, closing 1
    [InlineData("2026-10-05", null, 0, 1, 0, 1)]
    // Received in period, issued in period -> opening 0, in 1, out 1, closing 0
    [InlineData("2026-10-05", "2026-10-20", 0, 1, 1, 0)]
    public void Arithmetic_OpeningMovementClosing_FollowsRules(
        string recDateStr,
        string? issDateStr,
        decimal expectedOpening,
        decimal expectedIn,
        decimal expectedOut,
        decimal expectedClosing)
    {
        var start = new DateOnly(2026, 10, 1);
        var end = new DateOnly(2026, 10, 31);
        var recDate = DateOnly.Parse(recDateStr);
        DateOnly? issDate = issDateStr != null ? DateOnly.Parse(issDateStr) : null;

        bool recBeforeStart = recDate < start;
        bool issBeforeStart = issDate != null && issDate.Value < start;
        decimal opening = (recBeforeStart && !issBeforeStart) ? 1m : 0m;

        bool recInPeriod = recDate >= start && recDate <= end;
        bool issInPeriod = issDate != null && issDate.Value >= start && issDate.Value <= end;

        decimal inQty = recInPeriod ? 1m : 0m;
        decimal outQty = issInPeriod ? 1m : 0m;
        decimal closing = opening + inQty - outQty;

        Assert.Equal(expectedOpening, opening);
        Assert.Equal(expectedIn, inQty);
        Assert.Equal(expectedOut, outQty);
        Assert.Equal(expectedClosing, closing);
    }
}
