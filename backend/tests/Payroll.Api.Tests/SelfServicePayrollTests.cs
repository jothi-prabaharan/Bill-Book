using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Payroll.Api.Controllers;
using Payroll.Api.Services;
using Payroll.Entity.Enums;
using Payroll.Entity.TableEntities;
using Payroll.Repository;
using Shared.Kernel.Employees;
using Shared.Kernel.Interfaces;
using Shared.Kernel.Tenancy;
using Xunit;

namespace Payroll.Api.Tests;

[Collection(nameof(PostgresCollection))]
public sealed class SelfServicePayrollTests
{
    private readonly PostgresFixture _postgres;

    public SelfServicePayrollTests(PostgresFixture postgres) => _postgres = postgres;

    [SkippableFact]
    public async Task Employee_can_download_payslip_as_html()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        Guid customerId = Guid.NewGuid(), orgId = Guid.NewGuid();
        Guid empUserId = Guid.NewGuid();
        long empId = 4001;

        await using PayrollDbContext db = _postgres.CreateContext(customerId, orgId);

        var run = new PayrollRun
        {
            OrgId = orgId,
            Month = new DateOnly(2026, 10, 1),
            Status = PayrollRunStatus.Approved,
            DaysSource = "Attendance"
        };
        db.PayrollRuns.Add(run);
        await db.SaveChangesAsync();

        var compBasic = new SalaryComponent { OrgId = orgId, Name = "Basic", Kind = ComponentKind.Earning, ValueType = SalaryValueType.FlatAmount };
        var compPf = new SalaryComponent { OrgId = orgId, Name = "PF Employee", Kind = ComponentKind.Deduction, ValueType = SalaryValueType.Percentage };
        db.SalaryComponents.AddRange(compBasic, compPf);
        await db.SaveChangesAsync();

        var slip = new Payslip
        {
            PayrollRunId = run.PayrollRunId,
            EmployeeId = empId,
            PaidDays = 30m,
            GrossEarnings = 50000m,
            GrossDeductions = 1800m,
            NetPay = 48200m,
            Lines = new List<PayslipLine>
            {
                new PayslipLine { SalaryComponentId = compBasic.SalaryComponentId, Amount = 50000m },
                new PayslipLine { SalaryComponentId = compPf.SalaryComponentId, Amount = 1800m }
            }
        };
        db.Payslips.Add(slip);
        await db.SaveChangesAsync();

        var fakeEmployee = new FakeEmployeeClient();
        fakeEmployee.AddEmployee(new EmployeeProfile
        {
            EmployeeId = empId,
            UserId = empUserId,
            EmployeeCode = "EMP4001",
            FullName = "Alice Developer"
        });

        var currentUser = new FakeCurrentUser(empUserId);
        var tenant = new TenantContext { CustomerId = customerId, OrgId = orgId };
        var taxService = new TaxCalculationService(db);

        var meController = new MePayrollController(db, currentUser, tenant, fakeEmployee, taxService);

        var result = await meController.DownloadPayslip(slip.PayslipId, default);
        var contentResult = Assert.IsType<ContentResult>(result);

        Assert.Equal("text/html", contentResult.ContentType);
        Assert.Contains("Alice Developer", contentResult.Content);
        Assert.Contains("48,200.00", contentResult.Content);
        Assert.Contains("Basic", contentResult.Content);
        Assert.Contains("PF Employee", contentResult.Content);
    }

    [SkippableFact]
    public async Task Employee_cannot_view_another_employees_payslip()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        Guid customerId = Guid.NewGuid(), orgId = Guid.NewGuid();
        Guid empUserId = Guid.NewGuid();
        long empId = 4002;
        long otherEmpId = 4003;

        await using PayrollDbContext db = _postgres.CreateContext(customerId, orgId);

        var run = new PayrollRun
        {
            OrgId = orgId,
            Month = new DateOnly(2026, 10, 1),
            Status = PayrollRunStatus.Approved,
            DaysSource = "Attendance"
        };
        db.PayrollRuns.Add(run);
        await db.SaveChangesAsync();

        var otherSlip = new Payslip
        {
            PayrollRunId = run.PayrollRunId,
            EmployeeId = otherEmpId,
            PaidDays = 30m,
            GrossEarnings = 90000m,
            GrossDeductions = 5000m,
            NetPay = 85000m
        };
        db.Payslips.Add(otherSlip);
        await db.SaveChangesAsync();

        var fakeEmployee = new FakeEmployeeClient();
        fakeEmployee.AddEmployee(new EmployeeProfile
        {
            EmployeeId = empId,
            UserId = empUserId,
            EmployeeCode = "EMP4002",
            FullName = "Bob Analyst"
        });

        var currentUser = new FakeCurrentUser(empUserId);
        var tenant = new TenantContext { CustomerId = customerId, OrgId = orgId };
        var taxService = new TaxCalculationService(db);

        var meController = new MePayrollController(db, currentUser, tenant, fakeEmployee, taxService);

        // Attempting to access other employee's payslip must yield 404 NotFound
        var result = await meController.GetPayslipDetail(otherSlip.PayslipId, default);
        Assert.IsType<NotFoundObjectResult>(result);

        var downloadResult = await meController.DownloadPayslip(otherSlip.PayslipId, default);
        Assert.IsType<NotFoundObjectResult>(downloadResult);
    }

    [Fact]
    public async Task Self_service_endpoints_reject_when_user_id_is_missing()
    {
        Guid customerId = Guid.NewGuid(), orgId = Guid.NewGuid();
        var emptyUser = new FakeCurrentUser(null);
        var tenant = new TenantContext { CustomerId = customerId, OrgId = orgId };
        var fakeEmployee = new FakeEmployeeClient();

        var meController = new MePayrollController(null!, emptyUser, tenant, fakeEmployee, null!);

        var result = await meController.GetPayslips(default);
        Assert.IsType<NotFoundObjectResult>(result);
    }

    private sealed class FakeEmployeeClient : IEmployeeClient
    {
        private readonly List<EmployeeProfile> _employees = [];

        public void AddEmployee(EmployeeProfile emp) => _employees.Add(emp);

        public Task SettleEmployeeAsync(long employeeId, DateOnly lastWorkingDate, CancellationToken ct) => Task.CompletedTask;

        public Task<EmployeeProfile?> FindByUserIdAsync(Guid customerId, Guid orgId, Guid userId, CancellationToken ct) =>
            Task.FromResult(_employees.FirstOrDefault(e => e.UserId == userId));

        public Task<EmployeeProfile?> FindByIdAsync(Guid customerId, Guid orgId, long employeeId, CancellationToken ct) =>
            Task.FromResult(_employees.FirstOrDefault(e => e.EmployeeId == employeeId));
    }

    private sealed class FakeCurrentUser : ICurrentUser
    {
        public FakeCurrentUser(Guid? userId, Guid? customerId = null, Guid? orgId = null, int? roleId = null)
        {
            UserId = userId;
            CustomerId = customerId;
            OrgId = orgId;
            RoleId = roleId;
        }

        public Guid? UserId { get; }
        public Guid? CustomerId { get; }
        public Guid? OrgId { get; }
        public int? RoleId { get; }
    }
}
