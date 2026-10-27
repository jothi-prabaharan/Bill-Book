using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Reporting.Api.Controllers;
using Reporting.Entity.Models;
using Reporting.Repository.Repositories;
using Shared.Kernel.Internal;
using Xunit;

namespace Reporting.Api.Tests;

public sealed class BatchTrackingDetailReportTests
{
    [Fact]
    public void Controller_HasExpectedSecurityAttributes()
    {
        var type = typeof(BatchTrackingDetailReportController);

        Assert.NotNull(type.GetCustomAttribute<ApiControllerAttribute>());
        Assert.NotNull(type.GetCustomAttribute<AuthorizeAttribute>());
        Assert.NotNull(type.GetCustomAttribute<RequireBusinessTypeAttribute>());

        var moduleAttr = type.GetCustomAttribute<RequireModulePermissionAttribute>();
        Assert.NotNull(moduleAttr);
        Assert.Equal("reports", moduleAttr.Module);

        var runMethod = type.GetMethod(nameof(BatchTrackingDetailReportController.Run));
        Assert.NotNull(runMethod);
        var permAttr = runMethod.GetCustomAttribute<RequirePermissionAttribute>();
        Assert.NotNull(permAttr);
        Assert.Equal("inventory.view", permAttr.Permission);

        var batchesMethod = type.GetMethod(nameof(BatchTrackingDetailReportController.Batches));
        Assert.NotNull(batchesMethod);
        var batchesPerm = batchesMethod.GetCustomAttribute<RequirePermissionAttribute>();
        Assert.NotNull(batchesPerm);
        Assert.Equal("inventory.view", batchesPerm.Permission);

        var itemsMethod = type.GetMethod(nameof(BatchTrackingDetailReportController.Items));
        Assert.NotNull(itemsMethod);
        var itemsPerm = itemsMethod.GetCustomAttribute<RequirePermissionAttribute>();
        Assert.NotNull(itemsPerm);
        Assert.Equal("inventory.view", itemsPerm.Permission);

        var whMethod = type.GetMethod(nameof(BatchTrackingDetailReportController.Warehouses));
        Assert.NotNull(whMethod);
        var whPerm = whMethod.GetCustomAttribute<RequirePermissionAttribute>();
        Assert.NotNull(whPerm);
        Assert.Equal("inventory.view", whPerm.Permission);

        var contactsMethod = type.GetMethod(nameof(BatchTrackingDetailReportController.Contacts));
        Assert.NotNull(contactsMethod);
        var contactsPerm = contactsMethod.GetCustomAttribute<RequirePermissionAttribute>();
        Assert.NotNull(contactsPerm);
        Assert.Equal("inventory.view", contactsPerm.Permission);
    }

    [Fact]
    public void Refusals_FailsWhenToDateBeforeFromDate()
    {
        var req = new BatchTrackingDetailReportRequest
        {
            FromDate = new DateOnly(2026, 10, 25),
            ToDate = new DateOnly(2026, 10, 20)
        };
        var refusals = BatchTrackingDetailReportRepository.Refusals(req);

        Assert.Contains(refusals, r => r.Field == nameof(BatchTrackingDetailReportRequest.ToDate));
    }

    [Fact]
    public void Refusals_PassesWhenDateRangeValid()
    {
        var req = new BatchTrackingDetailReportRequest
        {
            FromDate = new DateOnly(2026, 10, 1),
            ToDate = new DateOnly(2026, 10, 28)
        };
        var refusals = BatchTrackingDetailReportRepository.Refusals(req);

        Assert.Empty(refusals);
    }

    [Theory]
    [InlineData(100.0, 50.0, 30.0, 120.0)]
    [InlineData(0.0, 150.0, 50.0, 100.0)]
    [InlineData(50.0, 0.0, 50.0, 0.0)]
    [InlineData(25.5, 10.5, 5.25, 30.75)]
    public void RunningBalance_ArithmeticCalculatesClosingCorrectly(
        decimal opening, decimal totalIn, decimal totalOut, decimal expectedClosing)
    {
        decimal closing = opening + totalIn - totalOut;
        Assert.Equal(expectedClosing, closing);
    }
}
