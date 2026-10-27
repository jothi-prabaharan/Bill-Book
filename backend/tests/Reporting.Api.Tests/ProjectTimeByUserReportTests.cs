using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Reporting.Api.Controllers;
using Reporting.Entity.Models;
using Reporting.Repository.ReadModels;
using Reporting.Repository.Repositories;
using Shared.Kernel.Internal;
using Xunit;

namespace Reporting.Api.Tests;

public sealed class ProjectTimeByUserReportTests
{
    private static readonly Guid UserAsha = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid UserRavi = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public void Controller_guards_and_attributes_are_correct()
    {
        Type controller = typeof(ProjectTimeByUserReportController);

        Assert.NotNull(controller.GetCustomAttribute<ApiControllerAttribute>());
        Assert.NotNull(controller.GetCustomAttribute<AuthorizeAttribute>());
        Assert.NotNull(controller.GetCustomAttribute<RequireBusinessTypeAttribute>());

        RequireModulePermissionAttribute? moduleAttr = controller.GetCustomAttribute<RequireModulePermissionAttribute>();
        Assert.NotNull(moduleAttr);
        Assert.Equal("reports", moduleAttr.Module);

        RouteAttribute? routeAttr = controller.GetCustomAttribute<RouteAttribute>();
        Assert.NotNull(routeAttr);
        Assert.Equal("api/statements/project-time-by-user", routeAttr.Template);

        MethodInfo? runMethod = controller.GetMethod(nameof(ProjectTimeByUserReportController.Run));
        Assert.NotNull(runMethod);
        Assert.NotNull(runMethod.GetCustomAttribute<HttpGetAttribute>());
        RequirePermissionAttribute? runPerm = runMethod.GetCustomAttribute<RequirePermissionAttribute>();
        Assert.NotNull(runPerm);
        Assert.Equal("projects.view", runPerm.Permission);

        MethodInfo? lookupsMethod = controller.GetMethod(nameof(ProjectTimeByUserReportController.Lookups));
        Assert.NotNull(lookupsMethod);
        HttpGetAttribute? lookupsGet = lookupsMethod.GetCustomAttribute<HttpGetAttribute>();
        Assert.NotNull(lookupsGet);
        Assert.Equal("lookups", lookupsGet.Template);
        RequirePermissionAttribute? lookupsPerm = lookupsMethod.GetCustomAttribute<RequirePermissionAttribute>();
        Assert.NotNull(lookupsPerm);
        Assert.Equal("projects.view", lookupsPerm.Permission);
    }

    [Fact]
    public void DateRangeRefusal_and_ProjectRefusal_validations()
    {
        DateOnly start = new(2026, 10, 1);
        DateOnly end = new(2026, 10, 28);

        Assert.NotNull(ProjectTimeByUserReportRepository.DateRangeRefusal(null, end));
        Assert.NotNull(ProjectTimeByUserReportRepository.DateRangeRefusal(start, null));
        Assert.NotNull(ProjectTimeByUserReportRepository.DateRangeRefusal(end, start)); // inverted
        Assert.Null(ProjectTimeByUserReportRepository.DateRangeRefusal(start, end));

        Assert.NotNull(ProjectTimeByUserReportRepository.ProjectRefusal(null));
        Assert.Null(ProjectTimeByUserReportRepository.ProjectRefusal(new ProjectRead { ProjectId = 1, ProjectCode = "PRJ-01" }));
    }

    [Fact]
    public void QueryTimeByUser_filters_dates_and_splits_hour_types()
    {
        DateOnly from = new(2026, 10, 10);
        DateOnly to = new(2026, 10, 20);

        List<ProjectRead> projects =
        [
            new() { ProjectId = 1, ProjectCode = "PRJ-01", ProjectName = "Alpha" },
            new() { ProjectId = 2, ProjectCode = "PRJ-02", ProjectName = "Beta" }
        ];

        List<ProjectTaskRead> tasks =
        [
            new() { ProjectTaskId = 11, ProjectId = 1, TaskName = "Dev" },
            new() { ProjectTaskId = 12, ProjectId = 1, TaskName = "QA" },
            new() { ProjectTaskId = 21, ProjectId = 2, TaskName = "Design" }
        ];

        List<TimeEntryRead> timeEntries =
        [
            // Within range: Dev for Asha, 5 hrs unbilled billable
            new() { TimeEntryId = 1, ProjectId = 1, ProjectTaskId = 11, UserId = UserAsha, WorkDate = new(2026, 10, 12), Hours = 5m, IsBillable = true, BillingStatus = "Unbilled" },
            // Within range: Dev for Asha, 3 hrs billed
            new() { TimeEntryId = 2, ProjectId = 1, ProjectTaskId = 11, UserId = UserAsha, WorkDate = new(2026, 10, 14), Hours = 3m, IsBillable = true, BillingStatus = "Billed" },
            // Within range: Dev for Asha, 2 hrs non-billable
            new() { TimeEntryId = 3, ProjectId = 1, ProjectTaskId = 11, UserId = UserAsha, WorkDate = new(2026, 10, 15), Hours = 2m, IsBillable = false, BillingStatus = "Unbilled" },
            // Within range: QA for Ravi, 4 hrs billable
            new() { TimeEntryId = 4, ProjectId = 1, ProjectTaskId = 12, UserId = UserRavi, WorkDate = new(2026, 10, 16), Hours = 4m, IsBillable = true, BillingStatus = "Unbilled" },
            // Within range: Design for Asha on Beta, 6 hrs billable
            new() { TimeEntryId = 5, ProjectId = 2, ProjectTaskId = 21, UserId = UserAsha, WorkDate = new(2026, 10, 18), Hours = 6m, IsBillable = true, BillingStatus = "Unbilled" },
            // Outside range (earlier) - excluded
            new() { TimeEntryId = 6, ProjectId = 1, ProjectTaskId = 11, UserId = UserAsha, WorkDate = new(2026, 10, 5), Hours = 10m, IsBillable = true, BillingStatus = "Unbilled" },
            // Outside range (later) - excluded
            new() { TimeEntryId = 7, ProjectId = 1, ProjectTaskId = 11, UserId = UserAsha, WorkDate = new(2026, 10, 25), Hours = 10m, IsBillable = true, BillingStatus = "Unbilled" }
        ];

        List<ProjectTimeByUserRow> rows = [.. ProjectTimeByUserReportRepository.QueryTimeByUser(
            timeEntries.AsQueryable(),
            projects.AsQueryable(),
            tasks.AsQueryable(),
            from,
            to)];

        Assert.Equal(3, rows.Count);

        // Check Dev for Asha on Alpha: total 10h (5 billable unbilled + 3 billed = 8 billable, 2 non-billable)
        ProjectTimeByUserRow ashaDev = Assert.Single(rows, r => r.ProjectCode == "PRJ-01" && r.TaskName == "Dev" && r.UserId == UserAsha);
        Assert.Equal(10m, ashaDev.Hours);
        Assert.Equal(8m, ashaDev.BillableHours);
        Assert.Equal(3m, ashaDev.BilledHours);
        Assert.Equal(2m, ashaDev.NonBillableHours);

        // Check QA for Ravi on Alpha: total 4h
        ProjectTimeByUserRow raviQa = Assert.Single(rows, r => r.ProjectCode == "PRJ-01" && r.TaskName == "QA" && r.UserId == UserRavi);
        Assert.Equal(4m, raviQa.Hours);
        Assert.Equal(4m, raviQa.BillableHours);
        Assert.Equal(0m, raviQa.BilledHours);
        Assert.Equal(0m, raviQa.NonBillableHours);

        // Check Design for Asha on Beta: total 6h
        ProjectTimeByUserRow ashaDesign = Assert.Single(rows, r => r.ProjectCode == "PRJ-02" && r.TaskName == "Design" && r.UserId == UserAsha);
        Assert.Equal(6m, ashaDesign.Hours);
        Assert.Equal(6m, ashaDesign.BillableHours);
    }

    [Fact]
    public void Compose_groups_by_user_and_calculates_utilisation()
    {
        DateOnly from = new(2026, 10, 1);
        DateOnly to = new(2026, 10, 28);

        List<ProjectTimeByUserRow> rows =
        [
            new() { ProjectCode = "PRJ-01", ProjectName = "Alpha", TaskName = "Dev", UserId = UserAsha, Hours = 10m, BillableHours = 8m, BilledHours = 3m, NonBillableHours = 2m },
            new() { ProjectCode = "PRJ-02", ProjectName = "Beta", TaskName = "Design", UserId = UserAsha, Hours = 6m, BillableHours = 6m, BilledHours = 0m, NonBillableHours = 0m },
            new() { ProjectCode = "PRJ-01", ProjectName = "Alpha", TaskName = "QA", UserId = UserRavi, Hours = 4m, BillableHours = 1m, BilledHours = 0m, NonBillableHours = 3m }
        ];

        Dictionary<Guid, string> userNames = new()
        {
            [UserAsha] = "Asha Sharma",
            [UserRavi] = "Ravi Kumar"
        };

        ProjectTimeByUserReportRequest request = new()
        {
            FromDate = from,
            ToDate = to,
            GroupBy = "user"
        };

        ProjectTimeByUserReportResponse response = ProjectTimeByUserReportRepository.Compose(rows, userNames, request);

        Assert.Equal(2, response.Groups.Count);

        // Asha's group: 16 total hours (14 billable, 3 billed, 2 non-billable), util = 14 / 16 * 100 = 87.5%
        ProjectTimeGroupDto ashaGroup = response.Groups[0];
        Assert.Equal("Asha Sharma", ashaGroup.GroupName);
        Assert.Equal(16m, ashaGroup.TotalHours);
        Assert.Equal(14m, ashaGroup.TotalBillableHours);
        Assert.Equal(3m, ashaGroup.TotalBilledHours);
        Assert.Equal(2m, ashaGroup.TotalNonBillableHours);
        Assert.Equal(87.5m, ashaGroup.TotalUtilisationPercent);
        Assert.Equal(2, ashaGroup.Items.Count);

        // Ravi's group: 4 total hours (1 billable, 0 billed, 3 non-billable), util = 1 / 4 * 100 = 25%
        ProjectTimeGroupDto raviGroup = response.Groups[1];
        Assert.Equal("Ravi Kumar", raviGroup.GroupName);
        Assert.Equal(4m, raviGroup.TotalHours);
        Assert.Equal(1m, raviGroup.TotalBillableHours);
        Assert.Equal(25m, raviGroup.TotalUtilisationPercent);
        Assert.Single(raviGroup.Items);

        // Grand Summary: 20 total hours (15 billable), util = 15 / 20 * 100 = 75%
        Assert.Equal(2, response.Summary.TotalUsers);
        Assert.Equal(2, response.Summary.TotalProjects);
        Assert.Equal(20m, response.Summary.TotalHours);
        Assert.Equal(15m, response.Summary.TotalBillableHours);
        Assert.Equal(3m, response.Summary.TotalBilledHours);
        Assert.Equal(5m, response.Summary.TotalNonBillableHours);
        Assert.Equal(75m, response.Summary.OverallUtilisationPercent);
    }

    [Fact]
    public void Compose_groups_by_project_accurately()
    {
        DateOnly from = new(2026, 10, 1);
        DateOnly to = new(2026, 10, 28);

        List<ProjectTimeByUserRow> rows =
        [
            new() { ProjectCode = "PRJ-01", ProjectName = "Alpha", TaskName = "Dev", UserId = UserAsha, Hours = 10m, BillableHours = 8m, BilledHours = 3m, NonBillableHours = 2m },
            new() { ProjectCode = "PRJ-02", ProjectName = "Beta", TaskName = "Design", UserId = UserAsha, Hours = 6m, BillableHours = 6m, BilledHours = 0m, NonBillableHours = 0m },
            new() { ProjectCode = "PRJ-01", ProjectName = "Alpha", TaskName = "QA", UserId = UserRavi, Hours = 4m, BillableHours = 1m, BilledHours = 0m, NonBillableHours = 3m }
        ];

        Dictionary<Guid, string> userNames = new()
        {
            [UserAsha] = "Asha Sharma",
            [UserRavi] = "Ravi Kumar"
        };

        ProjectTimeByUserReportRequest request = new()
        {
            FromDate = from,
            ToDate = to,
            GroupBy = "project"
        };

        ProjectTimeByUserReportResponse response = ProjectTimeByUserReportRepository.Compose(rows, userNames, request);

        Assert.Equal(2, response.Groups.Count);

        // PRJ-01 (Alpha): Dev (10h) + QA (4h) = 14h total, 9 billable, util = 9 / 14 * 100 = 64.29%
        ProjectTimeGroupDto prj1 = response.Groups[0];
        Assert.Equal("PRJ-01", prj1.GroupKey);
        Assert.Equal("Alpha", prj1.GroupName);
        Assert.Equal(14m, prj1.TotalHours);
        Assert.Equal(9m, prj1.TotalBillableHours);
        Assert.Equal(64.29m, prj1.TotalUtilisationPercent);
        Assert.Equal(2, prj1.Items.Count);

        // PRJ-02 (Beta): Design (6h) = 6h total, 6 billable, util = 100%
        ProjectTimeGroupDto prj2 = response.Groups[1];
        Assert.Equal("PRJ-02", prj2.GroupKey);
        Assert.Equal("Beta", prj2.GroupName);
        Assert.Equal(6m, prj2.TotalHours);
        Assert.Equal(6m, prj2.TotalBillableHours);
        Assert.Equal(100m, prj2.TotalUtilisationPercent);
        Assert.Single(prj2.Items);
    }
}
