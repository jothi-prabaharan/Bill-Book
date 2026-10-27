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

public sealed class ProjectBudgetVsActualReportTests
{
    [Fact]
    public void Controller_guards_and_attributes_are_correct()
    {
        Type controller = typeof(ProjectBudgetVsActualReportController);

        Assert.NotNull(controller.GetCustomAttribute<ApiControllerAttribute>());
        Assert.NotNull(controller.GetCustomAttribute<AuthorizeAttribute>());
        Assert.NotNull(controller.GetCustomAttribute<RequireBusinessTypeAttribute>());

        RequireModulePermissionAttribute? moduleAttr = controller.GetCustomAttribute<RequireModulePermissionAttribute>();
        Assert.NotNull(moduleAttr);
        Assert.Equal("reports", moduleAttr.Module);

        RouteAttribute? routeAttr = controller.GetCustomAttribute<RouteAttribute>();
        Assert.NotNull(routeAttr);
        Assert.Equal("api/statements/project-budget-vs-actual", routeAttr.Template);

        MethodInfo? runMethod = controller.GetMethod(nameof(ProjectBudgetVsActualReportController.Run));
        Assert.NotNull(runMethod);
        Assert.NotNull(runMethod.GetCustomAttribute<HttpGetAttribute>());
        RequirePermissionAttribute? runPerm = runMethod.GetCustomAttribute<RequirePermissionAttribute>();
        Assert.NotNull(runPerm);
        Assert.Equal("projects.view", runPerm.Permission);

        MethodInfo? lookupsMethod = controller.GetMethod(nameof(ProjectBudgetVsActualReportController.Lookups));
        Assert.NotNull(lookupsMethod);
        HttpGetAttribute? lookupsGet = lookupsMethod.GetCustomAttribute<HttpGetAttribute>();
        Assert.NotNull(lookupsGet);
        Assert.Equal("lookups", lookupsGet.Template);
        RequirePermissionAttribute? lookupsPerm = lookupsMethod.GetCustomAttribute<RequirePermissionAttribute>();
        Assert.NotNull(lookupsPerm);
        Assert.Equal("projects.view", lookupsPerm.Permission);
    }

    [Fact]
    public void AsOfDateRefusal_requires_date()
    {
        Assert.NotNull(ProjectBudgetVsActualReportRepository.AsOfDateRefusal(null));
        Assert.Null(ProjectBudgetVsActualReportRepository.AsOfDateRefusal(new DateOnly(2026, 10, 28)));
    }

    [Fact]
    public void CustomerRefusal_and_ProjectRefusal_detect_nulls()
    {
        Assert.NotNull(ProjectBudgetVsActualReportRepository.CustomerRefusal(null));
        Assert.Null(ProjectBudgetVsActualReportRepository.CustomerRefusal(new ContactRead { ContactId = 1, DisplayName = "Acme" }));

        Assert.NotNull(ProjectBudgetVsActualReportRepository.ProjectRefusal(null));
        Assert.Null(ProjectBudgetVsActualReportRepository.ProjectRefusal(new ProjectRead { ProjectId = 1, ProjectCode = "PRJ-01" }));
    }

    [Fact]
    public void ProjectActualCosts_aggregates_expense_ledger_up_to_as_of()
    {
        DateOnly asOf = new(2026, 10, 28);

        List<AccountRead> accounts =
        [
            new() { AccountId = 101, AccountTypeId = ProjectBudgetVsActualReportRepository.ExpenseAccountType },
            new() { AccountId = 102, AccountTypeId = 4 }, // Income account, should be ignored
        ];

        List<JournalLedgerRead> ledger =
        [
            // PRJ 1 - Valid expense
            new() { ProjectId = 1, AccountId = 101, LedgerDate = new DateOnly(2026, 5, 10), DebitAmountBase = 50000m, CreditAmountBase = 2000m },
            // PRJ 1 - Outside date
            new() { ProjectId = 1, AccountId = 101, LedgerDate = new DateOnly(2026, 11, 1), DebitAmountBase = 15000m, CreditAmountBase = 0m },
            // PRJ 1 - Income line (should be ignored)
            new() { ProjectId = 1, AccountId = 102, LedgerDate = new DateOnly(2026, 5, 10), CreditAmountBase = 80000m, DebitAmountBase = 0m },
            // PRJ 2 - Valid expense
            new() { ProjectId = 2, AccountId = 101, LedgerDate = new DateOnly(2026, 8, 15), DebitAmountBase = 32000m, CreditAmountBase = 0m },
            // No project
            new() { ProjectId = null, AccountId = 101, LedgerDate = new DateOnly(2026, 5, 10), DebitAmountBase = 10000m, CreditAmountBase = 0m },
        ];

        List<ProjectCostSum> costs = [.. ProjectBudgetVsActualReportRepository.ProjectActualCosts(
            ledger.AsQueryable(),
            accounts.AsQueryable(),
            asOf)];

        Assert.Equal(2, costs.Count);

        ProjectCostSum c1 = Assert.Single(costs, c => c.ProjectId == 1);
        Assert.Equal(48000m, c1.ActualCost); // 50,000 - 2,000

        ProjectCostSum c2 = Assert.Single(costs, c => c.ProjectId == 2);
        Assert.Equal(32000m, c2.ActualCost);
    }

    [Fact]
    public void TaskActualHours_aggregates_time_entries_up_to_as_of()
    {
        DateOnly asOf = new(2026, 10, 28);

        List<TimeEntryRead> timeEntries =
        [
            // Task 101
            new() { ProjectId = 1, ProjectTaskId = 101, WorkDate = new DateOnly(2026, 6, 1), Hours = 24.5m },
            new() { ProjectId = 1, ProjectTaskId = 101, WorkDate = new DateOnly(2026, 6, 2), Hours = 15.5m },
            // Task 101 - After asOf (excluded)
            new() { ProjectId = 1, ProjectTaskId = 101, WorkDate = new DateOnly(2026, 11, 1), Hours = 8.0m },
            // Task 102
            new() { ProjectId = 1, ProjectTaskId = 102, WorkDate = new DateOnly(2026, 7, 10), Hours = 12.0m },
        ];

        List<TaskHoursSum> hours = [.. ProjectBudgetVsActualReportRepository.TaskActualHours(
            timeEntries.AsQueryable(),
            asOf)];

        Assert.Equal(2, hours.Count);

        TaskHoursSum h1 = Assert.Single(hours, h => h.ProjectTaskId == 101);
        Assert.Equal(40.0m, h1.ActualHours);

        TaskHoursSum h2 = Assert.Single(hours, h => h.ProjectTaskId == 102);
        Assert.Equal(12.0m, h2.ActualHours);
    }

    [Fact]
    public void Compose_calculates_budget_vs_actual_and_over_budget_flags()
    {
        DateOnly asOf = new(2026, 10, 28);

        List<ProjectRead> projects =
        [
            new()
            {
                ProjectId = 1,
                ProjectCode = "PRJ-01",
                ProjectName = "Portal Setup",
                ContactId = 10,
                Status = "Active",
                BudgetAmount = 100000m
            },
            new()
            {
                ProjectId = 2,
                ProjectCode = "PRJ-02",
                ProjectName = "Legacy Migration",
                ContactId = 20,
                Status = "Active",
                BudgetAmount = 50000m
            }
        ];

        List<ContactRead> contacts =
        [
            new() { ContactId = 10, DisplayName = "Acme" },
            new() { ContactId = 20, DisplayName = "Beta" }
        ];

        List<ProjectTaskRead> tasks =
        [
            new() { ProjectTaskId = 101, ProjectId = 1, TaskName = "Architecture", BudgetHours = 40m },
            new() { ProjectTaskId = 102, ProjectId = 1, TaskName = "Implementation", BudgetHours = 80m },
            new() { ProjectTaskId = 201, ProjectId = 2, TaskName = "Data Extraction", BudgetHours = 50m }
        ];

        List<ProjectCostSum> costSums =
        [
            // PRJ 1: Actual 75,000 vs Budget 100,000 (Under budget)
            new() { ProjectId = 1, ActualCost = 75000m },
            // PRJ 2: Actual 62,000 vs Budget 50,000 (Over budget by 12,000)
            new() { ProjectId = 2, ActualCost = 62000m }
        ];

        List<TaskHoursSum> hourSums =
        [
            // PRJ 1, Task 101: 30 hours vs Budget 40
            new() { ProjectId = 1, ProjectTaskId = 101, ActualHours = 30m },
            // PRJ 1, Task 102: 95 hours vs Budget 80 (Over budget)
            new() { ProjectId = 1, ProjectTaskId = 102, ActualHours = 95m },
            // PRJ 2, Task 201: 45 hours vs Budget 50
            new() { ProjectId = 2, ProjectTaskId = 201, ActualHours = 45m }
        ];

        ProjectBudgetVsActualReportRequest request = new()
        {
            AsOfDate = asOf
        };

        ProjectBudgetVsActualReportResponse response = ProjectBudgetVsActualReportRepository.Compose(
            projects,
            contacts,
            tasks,
            costSums,
            hourSums,
            request);

        Assert.Equal(2, response.Projects.Count);

        // Project 1
        ProjectBudgetVsActualProjectDto p1 = response.Projects[0];
        Assert.Equal("PRJ-01", p1.ProjectCode);
        Assert.Equal("Acme", p1.Customer);
        Assert.Equal(100000m, p1.BudgetAmount);
        Assert.Equal(75000m, p1.ActualCost);
        Assert.Equal(25000m, p1.CostVariance);
        Assert.Equal(75.00m, p1.CostUsedPercent);
        Assert.False(p1.IsCostOverBudget);

        Assert.Equal(120m, p1.BudgetHours); // 40 + 80
        Assert.Equal(125m, p1.ActualHours); // 30 + 95
        Assert.Equal(-5m, p1.HoursVariance);
        Assert.True(p1.IsHoursOverBudget);

        Assert.Equal(2, p1.Tasks.Count);
        ProjectTaskBudgetDto t102 = p1.Tasks.Single(t => t.ProjectTaskId == 102);
        Assert.True(t102.IsOverBudget);
        Assert.Equal(-15m, t102.HoursVariance);

        // Project 2 (Cost Over budget)
        ProjectBudgetVsActualProjectDto p2 = response.Projects[1];
        Assert.Equal("PRJ-02", p2.ProjectCode);
        Assert.Equal(50000m, p2.BudgetAmount);
        Assert.Equal(62000m, p2.ActualCost);
        Assert.Equal(-12000m, p2.CostVariance);
        Assert.Equal(124.00m, p2.CostUsedPercent);
        Assert.True(p2.IsCostOverBudget);

        // Summary totals
        Assert.Equal(2, response.Summary.TotalProjects);
        Assert.Equal(150000m, response.Summary.TotalBudgetAmount);
        Assert.Equal(137000m, response.Summary.TotalActualCost);
        Assert.Equal(13000m, response.Summary.TotalCostVariance);
        Assert.Equal(170m, response.Summary.TotalBudgetHours); // 120 + 50
        Assert.Equal(170m, response.Summary.TotalActualHours); // 125 + 45
        Assert.Equal(0m, response.Summary.TotalHoursVariance);
        Assert.Equal(2, response.Summary.OverBudgetProjectsCount);
    }
}
