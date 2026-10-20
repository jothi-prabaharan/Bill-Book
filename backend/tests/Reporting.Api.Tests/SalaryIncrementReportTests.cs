using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Reporting.Api.Controllers;
using Reporting.Entity.Models;
using Reporting.Repository.ReadModels;
using Reporting.Repository.Repositories;
using Reporting.Repository.SeedData;
using Shared.Kernel.Apps;
using Shared.Kernel.Internal;
using Xunit;

namespace Reporting.Api.Tests;

/// <summary>
/// The Salary Increment Report (TK-198).
///
/// <b>Why lists and not a database.</b> The report reads <c>pay</c> and
/// <c>hrm</c> tables that Reporting maps excluded from its migrations, so its
/// test database has none of them. The query is therefore built from plain
/// queryables, and the same expression that runs against the read models runs
/// here against lists; what only a database can prove (the tenant filter) is
/// the read models' own, shared with every other HRMS report.
/// </summary>
public sealed class SalaryIncrementReportTests
{
    private static readonly DateOnly Jan1 = new(2026, 1, 1);
    private static readonly DateOnly Mar31 = new(2026, 3, 31);

    private static readonly Guid Recorder = Guid.NewGuid();

    private static readonly List<DepartmentRead> Departments =
    [
        new() { DepartmentId = 10, Code = "SAL", Name = "Sales", IsActive = true },
        new() { DepartmentId = 20, Code = "ACC", Name = "Accounts", IsActive = true },
    ];

    private static readonly List<DesignationRead> Designations =
    [
        new() { DesignationId = 100, Code = "EXE", Name = "Executive", IsActive = true },
    ];

    private static readonly List<EmployeeRecordRead> Employees =
    [
        new() { EmployeeId = 1, EmployeeCode = "E002", FirstName = "Kavitha", LastName = "R", DepartmentId = 10, DesignationId = 100 },
        new() { EmployeeId = 2, EmployeeCode = "E001", FirstName = "Suresh", LastName = null, DepartmentId = 20, DesignationId = 999 },
        new() { EmployeeId = 3, EmployeeCode = "E003", FirstName = "Meena", LastName = "J", DepartmentId = 999, DesignationId = 100 },
    ];

    private static readonly List<SalaryRevisionRead> Revisions =
    [
        Revision(1, employeeId: 1, new DateOnly(2026, 2, 1), 500_000m, 550_000m, "Annual increment"),
        Revision(2, employeeId: 2, new DateOnly(2026, 2, 1), 400_000m, 420_000m, null),
        Revision(3, employeeId: 3, Jan1, 300_000m, 330_000m, "Promotion"),
        Revision(4, employeeId: 1, Mar31, 550_000m, 560_000m, null),
        Revision(5, employeeId: 2, new DateOnly(2025, 12, 31), 380_000m, 400_000m, "Before the range"),
        Revision(6, employeeId: 3, new DateOnly(2026, 4, 1), 330_000m, 340_000m, "After the range"),
    ];

    private static SalaryRevisionRead Revision(
        long id, long employeeId, DateOnly effective, decimal previous, decimal revised, string? reason) =>
        new()
        {
            SalaryRevisionId = id,
            EmployeeId = employeeId,
            EffectiveFrom = effective,
            PreviousCtc = previous,
            NewCtc = revised,
            Reason = reason,
            CreatedBy = Recorder,
        };

    private static List<SalaryIncrementQueryRow> Run(long? employeeId = null, long? departmentId = null) =>
    [
        .. SalaryIncrementReportRepository.Query(
            Revisions.AsQueryable(), Employees.AsQueryable(), Departments.AsQueryable(), Designations.AsQueryable(),
            Jan1, Mar31, employeeId, departmentId),
    ];

    private static SalaryIncrementReportRowDto Row(long employeeId, decimal previous, decimal revised) =>
        new()
        {
            EmployeeId = employeeId,
            PreviousSalary = previous,
            RevisedSalary = revised,
            IncrementAmount = revised - previous,
            IncrementPercent = SalaryIncrementReportRepository.IncrementPercent(previous, revised),
        };

    // ---- The query -----------------------------------------------------------

    [Fact]
    public void Both_ends_of_the_range_are_included_and_nothing_outside_it()
    {
        Assert.Equal([3L, 2L, 1L, 4L], Run().Select(r => r.SalaryRevisionId));
    }

    [Fact]
    public void Rows_come_oldest_effective_date_first_then_by_employee_code()
    {
        List<SalaryIncrementQueryRow> rows = Run();

        Assert.Equal(Jan1, rows[0].EffectiveDate);
        // Two on 1 February: E001 before E002, whatever order they were entered in.
        Assert.Equal(["E001", "E002"], rows.Skip(1).Take(2).Select(r => r.EmployeeCode));
        Assert.Equal(Mar31, rows[^1].EffectiveDate);
    }

    [Fact]
    public void An_employee_filter_keeps_that_employees_revisions_only()
    {
        Assert.Equal([1L, 4L], Run(employeeId: 1).Select(r => r.SalaryRevisionId));
    }

    [Fact]
    public void A_department_filter_keeps_that_departments_employees_only()
    {
        Assert.Equal([2L], Run(departmentId: 20).Select(r => r.SalaryRevisionId));
    }

    [Fact]
    public void Each_row_carries_the_employee_their_department_designation_and_the_reason()
    {
        SalaryIncrementQueryRow kavitha = Run().Single(r => r.SalaryRevisionId == 1);

        Assert.Equal("Kavitha R", kavitha.EmployeeName);
        Assert.Equal("Sales", kavitha.Department);
        Assert.Equal("Executive", kavitha.Designation);
        Assert.Equal(500_000m, kavitha.PreviousSalary);
        Assert.Equal(550_000m, kavitha.RevisedSalary);
        Assert.Equal("Annual increment", kavitha.Remarks);
        Assert.Equal(Recorder, kavitha.RecordedById);
    }

    [Fact]
    public void A_missing_department_or_designation_reads_as_none_and_keeps_the_row()
    {
        SalaryIncrementQueryRow suresh = Run().Single(r => r.SalaryRevisionId == 2);
        SalaryIncrementQueryRow meena = Run().Single(r => r.SalaryRevisionId == 3);

        Assert.Equal("Suresh", suresh.EmployeeName);
        Assert.Null(suresh.Designation);
        Assert.Null(meena.Department);
    }

    // ---- The figures -----------------------------------------------------------

    [Theory]
    [InlineData(500_000, 550_000, 10.00)]
    [InlineData(400_000, 420_000, 5.00)]
    [InlineData(300_000, 270_000, -10.00)]
    [InlineData(3, 4, 33.33)]
    // 0.145 exactly: half away from zero gives 0.15 where banker's rounding gives 0.14.
    [InlineData(200, 200.29, 0.15)]
    public void The_increment_percentage_is_of_the_previous_salary_to_two_places(
        double previous, double revised, double expected)
    {
        Assert.Equal(
            (decimal)expected,
            SalaryIncrementReportRepository.IncrementPercent((decimal)previous, (decimal)revised));
    }

    [Fact]
    public void A_revision_from_a_zero_salary_has_no_percentage()
    {
        Assert.Null(SalaryIncrementReportRepository.IncrementPercent(0m, 300_000m));
    }

    [Fact]
    public void The_footer_adds_up_the_rows_and_counts_each_employee_once()
    {
        SalaryIncrementReportTotalsDto totals = SalaryIncrementReportRepository.Summarise(
        [
            Row(1, 500_000m, 550_000m),
            Row(2, 400_000m, 420_000m),
            Row(1, 550_000m, 560_000m),
        ]);

        Assert.Equal(3, totals.RevisionCount);
        Assert.Equal(2, totals.EmployeeCount);
        Assert.Equal(1_450_000m, totals.TotalPreviousSalary);
        Assert.Equal(80_000m, totals.TotalIncrementAmount);
        Assert.Equal(1_530_000m, totals.TotalRevisedSalary);
    }

    [Fact]
    public void The_average_percentage_is_the_mean_of_the_rows_leaving_out_a_zero_salary()
    {
        SalaryIncrementReportTotalsDto totals = SalaryIncrementReportRepository.Summarise(
        [
            Row(1, 500_000m, 550_000m), // 10%
            Row(2, 400_000m, 420_000m), // 5%
            Row(3, 0m, 300_000m), // no percentage
        ]);

        Assert.Equal(7.50m, totals.AverageIncrementPercent);
    }

    [Fact]
    public void The_average_is_rounded_once_from_the_salaries_not_from_the_rounded_rows()
    {
        // 0.006% shows as 0.01 and 0.003% as 0.00. The mean of the shown figures
        // is 0.005, which would round to 0.01; the true mean is 0.0045, which is 0.00.
        SalaryIncrementReportTotalsDto totals = SalaryIncrementReportRepository.Summarise(
        [
            Row(1, 100_000m, 100_006m),
            Row(2, 100_000m, 100_003m),
        ]);

        Assert.Equal(0.01m, SalaryIncrementReportRepository.IncrementPercent(100_000m, 100_006m));
        Assert.Equal(0.00m, SalaryIncrementReportRepository.IncrementPercent(100_000m, 100_003m));
        Assert.Equal(0.00m, totals.AverageIncrementPercent);
    }

    [Fact]
    public void With_no_rows_the_footer_is_zero_and_there_is_no_average()
    {
        SalaryIncrementReportTotalsDto totals = SalaryIncrementReportRepository.Summarise([]);

        Assert.Equal(0, totals.RevisionCount);
        Assert.Equal(0m, totals.TotalIncrementAmount);
        Assert.Null(totals.AverageIncrementPercent);
    }

    // ---- The request -------------------------------------------------------------

    [Fact]
    public void A_to_date_before_the_from_date_is_refused_and_one_day_is_not()
    {
        Assert.NotNull(SalaryIncrementReportRepository.RangeRefusal(new() { FromDate = Mar31, ToDate = Jan1 }));
        Assert.Null(SalaryIncrementReportRepository.RangeRefusal(new() { FromDate = Jan1, ToDate = Jan1 }));
        Assert.Null(SalaryIncrementReportRepository.RangeRefusal(new() { FromDate = null, ToDate = Jan1 }));
    }

    // ---- The endpoint ------------------------------------------------------------

    private static SalaryIncrementReportController Controller(Guid? tokenOrg)
    {
        Claim[] claims = tokenOrg is Guid org ? [new Claim("org_id", org.ToString())] : [];

        // The repository is never reached on these paths: the refusal comes first.
        return new SalaryIncrementReportController(null!)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")) },
            },
        };
    }

    [Fact]
    public async Task Another_branch_in_the_request_is_forbidden_not_hidden()
    {
        Guid tokenOrg = Guid.NewGuid();

        IActionResult result = await Controller(tokenOrg).Run(
            new SalaryIncrementReportRequest { FromDate = Jan1, ToDate = Mar31, OrgId = Guid.NewGuid() }, default);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task A_branch_with_no_token_branch_to_match_is_forbidden()
    {
        IActionResult result = await Controller(tokenOrg: null).Run(
            new SalaryIncrementReportRequest { FromDate = Jan1, ToDate = Mar31, OrgId = Guid.NewGuid() }, default);

        Assert.IsType<ForbidResult>(result);
    }

    /// <summary>
    /// The add-on alone guards it. <c>ScopeGuard</c> admits a controller with
    /// both a business-type and an add-on guard when either matches, so a
    /// business-type guard allowing every trade beside it let a branch without
    /// Payroll run the report; a local run found that, and this keeps it out.
    /// </summary>
    [Fact]
    public void The_endpoint_is_guarded_by_the_payroll_add_on_alone()
    {
        Type controller = typeof(SalaryIncrementReportController);

        Assert.Equal(AddOn.Payroll, controller.GetCustomAttribute<RequireAddOnAttribute>()!.AddOn);
        Assert.Null(controller.GetCustomAttribute<RequireBusinessTypeAttribute>());
        Assert.Equal("payroll", controller.GetCustomAttribute<RequireModulePermissionAttribute>()!.Module);
        Assert.Equal(
            "view",
            controller.GetMethod(nameof(SalaryIncrementReportController.Run))!
                .GetCustomAttribute<PermissionActionAttribute>()!.Action);
    }

    // ---- The catalog ---------------------------------------------------------------

    [Fact]
    public void The_report_is_in_the_catalog_as_a_page_of_its_own_for_payroll()
    {
        DedicatedReportPage? page = DedicatedReportPages.Find(SalaryIncrementReportRepository.ReportKey);

        Assert.NotNull(page);
        Assert.Equal(AddOn.Payroll, page!.RequiredAddOn);
        Assert.Null(page.RequiredBusinessType);
        Assert.True(ReportCatalogSeeder.SeededColumnKeys.ContainsKey(SalaryIncrementReportRepository.ReportKey));
    }

    [Fact]
    public void A_branch_without_payroll_is_not_offered_it()
    {
        var withPayroll = new ReportAudience(AddOn.Payroll, BusinessType.Retail, null);
        var without = new ReportAudience(AddOn.Hrms, BusinessType.Retail, null);
        var payrollOnly = new ReportAudience(AddOn.Payroll, null, null);

        Assert.True(withPayroll.Allows(AddOn.Payroll, null));
        Assert.False(without.Allows(AddOn.Payroll, null));
        Assert.True(payrollOnly.Allows(AddOn.Payroll, null));
    }
}
