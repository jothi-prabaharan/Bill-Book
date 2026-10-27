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

public sealed class ProjectProfitabilityReportTests
{
    [Fact]
    public void Controller_guards_and_attributes_are_correct()
    {
        Type controller = typeof(ProjectProfitabilityReportController);

        Assert.NotNull(controller.GetCustomAttribute<ApiControllerAttribute>());
        Assert.NotNull(controller.GetCustomAttribute<AuthorizeAttribute>());
        Assert.NotNull(controller.GetCustomAttribute<RequireBusinessTypeAttribute>());

        RequireModulePermissionAttribute? moduleAttr = controller.GetCustomAttribute<RequireModulePermissionAttribute>();
        Assert.NotNull(moduleAttr);
        Assert.Equal("reports", moduleAttr.Module);

        RouteAttribute? routeAttr = controller.GetCustomAttribute<RouteAttribute>();
        Assert.NotNull(routeAttr);
        Assert.Equal("api/statements/project-profitability", routeAttr.Template);

        MethodInfo? runMethod = controller.GetMethod(nameof(ProjectProfitabilityReportController.Run));
        Assert.NotNull(runMethod);
        Assert.NotNull(runMethod.GetCustomAttribute<HttpGetAttribute>());
        RequirePermissionAttribute? runPerm = runMethod.GetCustomAttribute<RequirePermissionAttribute>();
        Assert.NotNull(runPerm);
        Assert.Equal("projects.view", runPerm.Permission);

        MethodInfo? lookupsMethod = controller.GetMethod(nameof(ProjectProfitabilityReportController.Lookups));
        Assert.NotNull(lookupsMethod);
        HttpGetAttribute? lookupsGet = lookupsMethod.GetCustomAttribute<HttpGetAttribute>();
        Assert.NotNull(lookupsGet);
        Assert.Equal("lookups", lookupsGet.Template);
        RequirePermissionAttribute? lookupsPerm = lookupsMethod.GetCustomAttribute<RequirePermissionAttribute>();
        Assert.NotNull(lookupsPerm);
        Assert.Equal("projects.view", lookupsPerm.Permission);
    }

    [Fact]
    public void DateRefusal_validates_dates_strictly()
    {
        DateOnly start = new(2026, 4, 1);
        DateOnly end = new(2026, 9, 30);

        Assert.NotNull(ProjectProfitabilityReportRepository.DateRefusal(null, end));
        Assert.NotNull(ProjectProfitabilityReportRepository.DateRefusal(start, null));
        Assert.NotNull(ProjectProfitabilityReportRepository.DateRefusal(null, null));

        string? invalidOrder = ProjectProfitabilityReportRepository.DateRefusal(end, start);
        Assert.NotNull(invalidOrder);
        Assert.Contains("on or after", invalidOrder);

        Assert.Null(ProjectProfitabilityReportRepository.DateRefusal(start, end));
        Assert.Null(ProjectProfitabilityReportRepository.DateRefusal(start, start));
    }

    [Fact]
    public void CustomerRefusal_and_ProjectRefusal_detect_nulls()
    {
        Assert.NotNull(ProjectProfitabilityReportRepository.CustomerRefusal(null));
        Assert.Null(ProjectProfitabilityReportRepository.CustomerRefusal(new ContactRead { ContactId = 1, DisplayName = "Acme" }));

        Assert.NotNull(ProjectProfitabilityReportRepository.ProjectRefusal(null));
        Assert.Null(ProjectProfitabilityReportRepository.ProjectRefusal(new ProjectRead { ProjectId = 1, ProjectCode = "PRJ-001" }));
    }

    [Fact]
    public void ProjectSumsByType_aggregates_ledger_by_account_type()
    {
        DateOnly from = new(2026, 4, 1);
        DateOnly to = new(2026, 9, 30);

        List<AccountRead> accounts =
        [
            new() { AccountId = 101, AccountTypeId = ProjectProfitabilityReportRepository.IncomeAccountType },
            new() { AccountId = 102, AccountTypeId = ProjectProfitabilityReportRepository.ExpenseAccountType },
            new() { AccountId = 103, AccountTypeId = 1 }, // Asset account, should be excluded
        ];

        List<JournalLedgerRead> ledger =
        [
            // Project 1 - Income
            new() { ProjectId = 1, AccountId = 101, LedgerDate = new DateOnly(2026, 5, 1), CreditAmountBase = 50000m, DebitAmountBase = 0m },
            new() { ProjectId = 1, AccountId = 101, LedgerDate = new DateOnly(2026, 6, 1), CreditAmountBase = 0m, DebitAmountBase = 5000m }, // Contra / return
            // Project 1 - Expense
            new() { ProjectId = 1, AccountId = 102, LedgerDate = new DateOnly(2026, 5, 15), DebitAmountBase = 20000m, CreditAmountBase = 0m },
            // Project 1 - Outside dates (should be excluded)
            new() { ProjectId = 1, AccountId = 101, LedgerDate = new DateOnly(2026, 3, 31), CreditAmountBase = 10000m, DebitAmountBase = 0m },
            // Postings with no project (should be excluded)
            new() { ProjectId = null, AccountId = 101, LedgerDate = new DateOnly(2026, 5, 1), CreditAmountBase = 10000m, DebitAmountBase = 0m },
            // Project 2 - Expense
            new() { ProjectId = 2, AccountId = 102, LedgerDate = new DateOnly(2026, 7, 1), DebitAmountBase = 8000m, CreditAmountBase = 500m },
        ];

        List<ProjectAccountTypeSum> result = [.. ProjectProfitabilityReportRepository.ProjectSumsByType(
            ledger.AsQueryable(),
            accounts.AsQueryable(),
            from,
            to)];

        Assert.Equal(3, result.Count);

        ProjectAccountTypeSum p1Income = Assert.Single(result, r => r.ProjectId == 1 && r.AccountTypeId == ProjectProfitabilityReportRepository.IncomeAccountType);
        Assert.Equal(50000m, p1Income.Credit);
        Assert.Equal(5000m, p1Income.Debit);

        ProjectAccountTypeSum p1Expense = Assert.Single(result, r => r.ProjectId == 1 && r.AccountTypeId == ProjectProfitabilityReportRepository.ExpenseAccountType);
        Assert.Equal(20000m, p1Expense.Debit);
        Assert.Equal(0m, p1Expense.Credit);

        ProjectAccountTypeSum p2Expense = Assert.Single(result, r => r.ProjectId == 2 && r.AccountTypeId == ProjectProfitabilityReportRepository.ExpenseAccountType);
        Assert.Equal(8000m, p2Expense.Debit);
        Assert.Equal(500m, p2Expense.Credit);
    }

    [Fact]
    public void Compose_calculates_profitability_and_summary_accurately()
    {
        DateOnly from = new(2026, 4, 1);
        DateOnly to = new(2026, 9, 30);

        List<ProjectRead> projects =
        [
            new()
            {
                ProjectId = 1,
                ProjectCode = "PRJ-01",
                ProjectName = "Alpha Website",
                ContactId = 10,
                Status = "Active",
                BillingMethod = "FixedFee",
                BudgetAmount = 100000m
            },
            new()
            {
                ProjectId = 2,
                ProjectCode = "PRJ-02",
                ProjectName = "Beta Mobile App",
                ContactId = 20,
                Status = "Active",
                BillingMethod = "TimeAndMaterials",
                BudgetAmount = 50000m
            },
            new()
            {
                ProjectId = 3,
                ProjectCode = "PRJ-03",
                ProjectName = "Internal R&D",
                ContactId = null,
                Status = "Completed",
                BillingMethod = "NonBillable",
                BudgetAmount = null
            }
        ];

        List<ContactRead> contacts =
        [
            new() { ContactId = 10, DisplayName = "Acme Corp" },
            new() { ContactId = 20, DisplayName = "Global Logistics" }
        ];

        List<ProjectAccountTypeSum> sums =
        [
            // PRJ-01: Revenue = 80,000 - 5,000 = 75,000; Cost = 30,000 - 0 = 30,000; Profit = 45,000
            new() { ProjectId = 1, AccountTypeId = ProjectProfitabilityReportRepository.IncomeAccountType, Credit = 80000m, Debit = 5000m },
            new() { ProjectId = 1, AccountTypeId = ProjectProfitabilityReportRepository.ExpenseAccountType, Debit = 30000m, Credit = 0m },
            // PRJ-02: Revenue = 40,000; Cost = 50,000 (Loss of 10,000)
            new() { ProjectId = 2, AccountTypeId = ProjectProfitabilityReportRepository.IncomeAccountType, Credit = 40000m, Debit = 0m },
            new() { ProjectId = 2, AccountTypeId = ProjectProfitabilityReportRepository.ExpenseAccountType, Debit = 50000m, Credit = 0m },
            // PRJ-03: No revenue; Cost = 12,000
            new() { ProjectId = 3, AccountTypeId = ProjectProfitabilityReportRepository.ExpenseAccountType, Debit = 12000m, Credit = 0m },
        ];

        ProjectProfitabilityReportRequest request = new()
        {
            From = from,
            To = to
        };

        ProjectProfitabilityReportResponse response = ProjectProfitabilityReportRepository.Compose(
            projects,
            contacts,
            sums,
            request);

        Assert.Equal(3, response.Rows.Count);

        // PRJ-01 checks
        ProjectProfitabilityRowDto r1 = response.Rows[0];
        Assert.Equal("PRJ-01", r1.ProjectCode);
        Assert.Equal("Acme Corp", r1.Customer);
        Assert.Equal(75000m, r1.Revenue);
        Assert.Equal(30000m, r1.Cost);
        Assert.Equal(45000m, r1.Profit);
        Assert.Equal(60.00m, r1.MarginPercent); // 45,000 / 75,000 * 100 = 60%
        Assert.Equal(100000m, r1.BudgetAmount);
        Assert.Equal(70000m, r1.BudgetVariance); // 100,000 - 30,000

        // PRJ-02 checks
        ProjectProfitabilityRowDto r2 = response.Rows[1];
        Assert.Equal("PRJ-02", r2.ProjectCode);
        Assert.Equal("Global Logistics", r2.Customer);
        Assert.Equal(40000m, r2.Revenue);
        Assert.Equal(50000m, r2.Cost);
        Assert.Equal(-10000m, r2.Profit);
        Assert.Equal(-25.00m, r2.MarginPercent); // -10,000 / 40,000 * 100 = -25%

        // PRJ-03 checks (Internal)
        ProjectProfitabilityRowDto r3 = response.Rows[2];
        Assert.Equal("PRJ-03", r3.ProjectCode);
        Assert.Equal("Internal", r3.Customer);
        Assert.Equal(0m, r3.Revenue);
        Assert.Equal(12000m, r3.Cost);
        Assert.Equal(-12000m, r3.Profit);
        Assert.Null(r3.MarginPercent);

        // Summary checks
        Assert.Equal(3, response.Summary.TotalProjects);
        Assert.Equal(115000m, response.Summary.TotalRevenue); // 75,000 + 40,000
        Assert.Equal(92000m, response.Summary.TotalCost); // 30,000 + 50,000 + 12,000
        Assert.Equal(23000m, response.Summary.TotalProfit); // 115,000 - 92,000
        Assert.Equal(150000m, response.Summary.TotalBudgetAmount); // 100,000 + 50,000
        Assert.Equal(20.00m, response.Summary.OverallMarginPercent); // 23,000 / 115,000 * 100 = 20%
    }

    [Fact]
    public void Compose_filters_by_customer_status_and_billing_method()
    {
        DateOnly from = new(2026, 4, 1);
        DateOnly to = new(2026, 9, 30);

        List<ProjectRead> projects =
        [
            new() { ProjectId = 1, ProjectCode = "P1", ContactId = 10, Status = "Active", BillingMethod = "FixedFee" },
            new() { ProjectId = 2, ProjectCode = "P2", ContactId = 20, Status = "Active", BillingMethod = "TimeAndMaterials" },
            new() { ProjectId = 3, ProjectCode = "P3", ContactId = 10, Status = "Completed", BillingMethod = "FixedFee" }
        ];

        List<ContactRead> contacts =
        [
            new() { ContactId = 10, DisplayName = "Acme" },
            new() { ContactId = 20, DisplayName = "Beta" }
        ];

        List<ProjectAccountTypeSum> sums = [];

        // Filter by Customer
        ProjectProfitabilityReportResponse resCust = ProjectProfitabilityReportRepository.Compose(
            projects, contacts, sums,
            new ProjectProfitabilityReportRequest { From = from, To = to, CustomerId = 10 });
        Assert.Equal(2, resCust.Rows.Count);

        // Filter by Status
        ProjectProfitabilityReportResponse resStatus = ProjectProfitabilityReportRepository.Compose(
            projects, contacts, sums,
            new ProjectProfitabilityReportRequest { From = from, To = to, Status = "Completed" });
        Assert.Single(resStatus.Rows);
        Assert.Equal("P3", resStatus.Rows[0].ProjectCode);

        // Filter by BillingMethod
        ProjectProfitabilityReportResponse resMethod = ProjectProfitabilityReportRepository.Compose(
            projects, contacts, sums,
            new ProjectProfitabilityReportRequest { From = from, To = to, BillingMethod = "TimeAndMaterials" });
        Assert.Single(resMethod.Rows);
        Assert.Equal("P2", resMethod.Rows[0].ProjectCode);
    }
}
