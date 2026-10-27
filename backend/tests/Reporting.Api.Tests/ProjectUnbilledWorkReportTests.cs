using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Reporting.Api.Controllers;
using Reporting.Entity.Enums;
using Reporting.Entity.Models;
using Reporting.Repository.ReadModels;
using Reporting.Repository.Repositories;
using Shared.Kernel.Documents;
using Shared.Kernel.Internal;
using Xunit;

namespace Reporting.Api.Tests;

public sealed class ProjectUnbilledWorkReportTests
{
    private static readonly Guid UserAsha = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid UserRavi = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public void Controller_guards_and_attributes_are_correct()
    {
        Type controller = typeof(ProjectUnbilledWorkReportController);

        Assert.NotNull(controller.GetCustomAttribute<ApiControllerAttribute>());
        Assert.NotNull(controller.GetCustomAttribute<AuthorizeAttribute>());
        Assert.NotNull(controller.GetCustomAttribute<RequireBusinessTypeAttribute>());

        RequireModulePermissionAttribute? moduleAttr = controller.GetCustomAttribute<RequireModulePermissionAttribute>();
        Assert.NotNull(moduleAttr);
        Assert.Equal("reports", moduleAttr.Module);

        RouteAttribute? routeAttr = controller.GetCustomAttribute<RouteAttribute>();
        Assert.NotNull(routeAttr);
        Assert.Equal("api/statements/project-unbilled-work", routeAttr.Template);

        MethodInfo? runMethod = controller.GetMethod(nameof(ProjectUnbilledWorkReportController.Run));
        Assert.NotNull(runMethod);
        Assert.NotNull(runMethod.GetCustomAttribute<HttpGetAttribute>());
        RequirePermissionAttribute? runPerm = runMethod.GetCustomAttribute<RequirePermissionAttribute>();
        Assert.NotNull(runPerm);
        Assert.Equal("projects.view", runPerm.Permission);

        MethodInfo? lookupsMethod = controller.GetMethod(nameof(ProjectUnbilledWorkReportController.Lookups));
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
        Assert.NotNull(ProjectUnbilledWorkReportRepository.AsOfDateRefusal(null));
        Assert.Null(ProjectUnbilledWorkReportRepository.AsOfDateRefusal(new DateOnly(2026, 10, 28)));
    }

    [Fact]
    public void CustomerRefusal_and_ProjectRefusal_detect_nulls()
    {
        Assert.NotNull(ProjectUnbilledWorkReportRepository.CustomerRefusal(null));
        Assert.Null(ProjectUnbilledWorkReportRepository.CustomerRefusal(new ContactRead { ContactId = 1, DisplayName = "Acme" }));

        Assert.NotNull(ProjectUnbilledWorkReportRepository.ProjectRefusal(null));
        Assert.Null(ProjectUnbilledWorkReportRepository.ProjectRefusal(new ProjectRead { ProjectId = 1, ProjectCode = "PRJ-01" }));
    }

    [Fact]
    public void QueryUnbilledTimeEntries_filters_by_asOf_and_calculates_rate_basis()
    {
        DateOnly asOf = new(2026, 10, 28);

        List<ProjectRead> projects =
        [
            new() { ProjectId = 1, ProjectCode = "PRJ-01", ProjectName = "Task Rate Proj", RateBasis = "TaskRate", HourlyRate = 50m },
            new() { ProjectId = 2, ProjectCode = "PRJ-02", ProjectName = "User Rate Proj", RateBasis = "UserRate", HourlyRate = 50m },
            new() { ProjectId = 3, ProjectCode = "PRJ-03", ProjectName = "Proj Rate Proj", RateBasis = "ProjectRate", HourlyRate = 200m },
        ];

        List<ProjectTaskRead> tasks =
        [
            new() { ProjectTaskId = 11, ProjectId = 1, TaskName = "Dev", HourlyRate = 120m },
            new() { ProjectTaskId = 21, ProjectId = 2, TaskName = "Design", HourlyRate = 80m },
            new() { ProjectTaskId = 31, ProjectId = 3, TaskName = "QA", HourlyRate = 60m },
        ];

        List<ProjectMemberRead> members =
        [
            new() { ProjectMemberId = 1, ProjectId = 2, UserId = UserAsha, HourlyRate = 150m }
        ];

        List<TimeEntryRead> timeEntries =
        [
            // PRJ-01: Task rate (120/hr) * 2 hrs = 240
            new() { TimeEntryId = 101, ProjectId = 1, ProjectTaskId = 11, UserId = UserAsha, WorkDate = new(2026, 10, 20), Hours = 2m, IsBillable = true, BillingStatus = "Unbilled" },
            // PRJ-02: User rate for Asha (150/hr) * 3 hrs = 450
            new() { TimeEntryId = 102, ProjectId = 2, ProjectTaskId = 21, UserId = UserAsha, WorkDate = new(2026, 10, 22), Hours = 3m, IsBillable = true, BillingStatus = "Unbilled" },
            // PRJ-03: Project rate (200/hr) * 4 hrs = 800
            new() { TimeEntryId = 103, ProjectId = 3, ProjectTaskId = 31, UserId = UserRavi, WorkDate = new(2026, 10, 25), Hours = 4m, IsBillable = true, BillingStatus = "Unbilled" },
            // Billed entry - excluded
            new() { TimeEntryId = 104, ProjectId = 1, ProjectTaskId = 11, UserId = UserAsha, WorkDate = new(2026, 10, 20), Hours = 5m, IsBillable = true, BillingStatus = "Billed" },
            // Non-billable entry - excluded
            new() { TimeEntryId = 105, ProjectId = 1, ProjectTaskId = 11, UserId = UserAsha, WorkDate = new(2026, 10, 20), Hours = 5m, IsBillable = false, BillingStatus = "Unbilled" },
            // Future entry beyond asOf - excluded
            new() { TimeEntryId = 106, ProjectId = 1, ProjectTaskId = 11, UserId = UserAsha, WorkDate = new(2026, 10, 30), Hours = 5m, IsBillable = true, BillingStatus = "Unbilled" },
        ];

        List<UnbilledTimeDto> results = [.. ProjectUnbilledWorkReportRepository.QueryUnbilledTimeEntries(
            timeEntries.AsQueryable(),
            projects.AsQueryable(),
            tasks.AsQueryable(),
            members.AsQueryable(),
            asOf)];

        Assert.Equal(3, results.Count);

        UnbilledTimeDto t1 = Assert.Single(results, r => r.ProjectId == 1);
        Assert.Equal(120m, t1.Rate);
        Assert.Equal(240m, t1.Amount);

        UnbilledTimeDto t2 = Assert.Single(results, r => r.ProjectId == 2);
        Assert.Equal(150m, t2.Rate);
        Assert.Equal(450m, t2.Amount);

        UnbilledTimeDto t3 = Assert.Single(results, r => r.ProjectId == 3);
        Assert.Equal(200m, t3.Rate);
        Assert.Equal(800m, t3.Amount);
    }

    [Fact]
    public void QueryUnbilledExpenses_and_Milestones_work_correctly()
    {
        DateOnly asOf = new(2026, 10, 28);

        List<SpendMoneyRead> spend =
        [
            new() { SpendMoneyId = 1, TransactionDate = new(2026, 10, 15), TransactionNo = "SM-01", Status = MoneyDocumentStatus.Posted },
            new() { SpendMoneyId = 2, TransactionDate = new(2026, 10, 16), TransactionNo = "SM-02", Status = MoneyDocumentStatus.Draft } // Not posted
        ];

        List<SpendMoneyDetailRead> spendDetails =
        [
            // 1,000 base with 10% markup = 1,100
            new() { SpendMoneyDetailId = 11, SpendMoneyId = 1, ProjectId = 1, AmountBase = 1000m, MarkupPercent = 10m, IsBillable = true, BilledInvoiceId = null, LineMemo = "Travel" },
            // Already billed on invoice 99
            new() { SpendMoneyDetailId = 12, SpendMoneyId = 1, ProjectId = 1, AmountBase = 500m, MarkupPercent = 10m, IsBillable = true, BilledInvoiceId = 99 },
            // On draft spend money
            new() { SpendMoneyDetailId = 13, SpendMoneyId = 2, ProjectId = 1, AmountBase = 200m, MarkupPercent = 10m, IsBillable = true, BilledInvoiceId = null }
        ];

        List<BillRead> bills =
        [
            new() { BillId = 1, DocumentDate = new(2026, 10, 18), DocumentNo = "BILL-01", ExchangeRate = 80m, Status = DocumentStatus.Posted }
        ];

        List<BillDetailRead> billDetails =
        [
            // $10 at 80 = 800 base with 25% markup = 1,000
            new() { BillDetailId = 21, BillId = 1, ProjectId = 1, TaxableAmount = 10m, MarkupPercent = 25m, IsBillable = true, BilledInvoiceId = null, Description = "Hosting" }
        ];

        List<ProjectMilestoneRead> milestones =
        [
            new() { ProjectMilestoneId = 31, ProjectId = 1, Amount = 5000m, InvoiceId = null },
            new() { ProjectMilestoneId = 32, ProjectId = 1, Amount = 7000m, InvoiceId = 42 } // Invoiced
        ];

        List<UnbilledSpendDto> spendResults = [.. ProjectUnbilledWorkReportRepository.QueryUnbilledSpendLines(
            spendDetails.AsQueryable(),
            spend.AsQueryable(),
            asOf)];

        Assert.Single(spendResults);
        Assert.Equal(1100m, spendResults[0].BillableAmount);

        List<UnbilledBillDto> billResults = [.. ProjectUnbilledWorkReportRepository.QueryUnbilledBillLines(
            billDetails.AsQueryable(),
            bills.AsQueryable(),
            asOf)];

        Assert.Single(billResults);
        Assert.Equal(1000m, billResults[0].BillableAmount);

        List<UnbilledMilestoneDto> milestoneResults = [.. ProjectUnbilledWorkReportRepository.QueryUnbilledMilestones(
            milestones.AsQueryable())];

        Assert.Single(milestoneResults);
        Assert.Equal(5000m, milestoneResults[0].Amount);
    }

    [Fact]
    public void Compose_assembles_projects_and_summarizes_accurately()
    {
        DateOnly asOf = new(2026, 10, 28);

        List<ProjectRead> projects =
        [
            new() { ProjectId = 1, ProjectCode = "PRJ-01", ProjectName = "Alpha", ContactId = 10, BillingMethod = "TimeAndMaterials" },
            new() { ProjectId = 2, ProjectCode = "PRJ-02", ProjectName = "Beta", ContactId = 20, BillingMethod = "FixedFee" },
            new() { ProjectId = 3, ProjectCode = "PRJ-03", ProjectName = "Gamma", ContactId = null, BillingMethod = "NonBillable" }, // No unbilled items -> should be omitted
        ];

        List<ContactRead> contacts =
        [
            new() { ContactId = 10, DisplayName = "Acme Corp" },
            new() { ContactId = 20, DisplayName = "Globex" },
        ];

        List<UnbilledTimeDto> timeEntries =
        [
            new() { ProjectId = 1, TimeEntryId = 101, ProjectTaskId = 11, TaskName = "Dev", UserId = UserAsha, WorkDate = new(2026, 10, 20), Hours = 10m, Rate = 100m, Amount = 1000m }
        ];

        List<UnbilledSpendDto> spendLines =
        [
            new() { ProjectId = 1, SpendMoneyDetailId = 201, TransactionDate = new(2026, 10, 22), TransactionNo = "SM-01", AmountBase = 200m, MarkupPercent = 10m, BillableAmount = 220m }
        ];

        List<UnbilledBillDto> billLines = [];

        List<UnbilledMilestoneDto> milestones =
        [
            new() { ProjectId = 2, ProjectMilestoneId = 301, Amount = 4000m }
        ];

        Dictionary<Guid, string> userNames = new()
        {
            [UserAsha] = "Asha Sharma"
        };

        ProjectUnbilledWorkReportRequest request = new() { AsOfDate = asOf };

        ProjectUnbilledWorkReportResponse response = ProjectUnbilledWorkReportRepository.Compose(
            projects,
            contacts,
            timeEntries,
            spendLines,
            billLines,
            milestones,
            userNames,
            request);

        // Project 3 is omitted because it has zero unbilled hours and zero total
        Assert.Equal(2, response.Projects.Count);

        ProjectUnbilledProjectDto prj1 = response.Projects[0];
        Assert.Equal("PRJ-01", prj1.ProjectCode);
        Assert.Equal("Acme Corp", prj1.Customer);
        Assert.Equal(10m, prj1.UnbilledHours);
        Assert.Equal(1000m, prj1.TimeValue);
        Assert.Equal(220m, prj1.Expenses);
        Assert.Equal(0m, prj1.Milestones);
        Assert.Equal(1220m, prj1.Total);
        Assert.Equal(2, prj1.Lines.Count);

        ProjectUnbilledProjectDto prj2 = response.Projects[1];
        Assert.Equal("PRJ-02", prj2.ProjectCode);
        Assert.Equal("Globex", prj2.Customer);
        Assert.Equal(0m, prj2.UnbilledHours);
        Assert.Equal(0m, prj2.TimeValue);
        Assert.Equal(0m, prj2.Expenses);
        Assert.Equal(4000m, prj2.Milestones);
        Assert.Equal(4000m, prj2.Total);
        Assert.Single(prj2.Lines);

        // Check summary
        Assert.Equal(2, response.Summary.TotalProjects);
        Assert.Equal(10m, response.Summary.TotalUnbilledHours);
        Assert.Equal(1000m, response.Summary.TotalTimeValue);
        Assert.Equal(220m, response.Summary.TotalExpenses);
        Assert.Equal(4000m, response.Summary.TotalMilestones);
        Assert.Equal(5220m, response.Summary.GrandTotal);
    }
}
