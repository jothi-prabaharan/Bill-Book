using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Employees;
using Shared.Kernel.Interfaces;
using Shared.Kernel.Tenancy;
using TimeLeave.Api.Controllers;
using TimeLeave.Api.Services;
using TimeLeave.Entity.Enums;
using TimeLeave.Entity.Models;
using TimeLeave.Entity.TableEntities;
using TimeLeave.Repository;
using Xunit;

namespace TimeLeave.Api.Tests;

[Collection(nameof(PostgresCollection))]
public sealed class SelfServiceAndApprovalsTests
{
    private readonly PostgresFixture _postgres;

    public SelfServiceAndApprovalsTests(PostgresFixture postgres) => _postgres = postgres;

    [SkippableFact]
    public async Task Employee_applies_for_leave_via_self_service_and_manager_approves_via_inbox()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        Guid customerId = Guid.NewGuid(), orgId = Guid.NewGuid();
        Guid empUserId = Guid.NewGuid();
        Guid managerUserId = Guid.NewGuid();
        long empId = 3001;
        long managerId = 3002;

        await using TimeLeaveDbContext db = _postgres.CreateContext(customerId, orgId);

        var leaveType = new LeaveType
        {
            OrgId = orgId,
            Code = "EL",
            Name = "Earned Leave",
            IsPaid = true,
            IsActive = true
        };
        db.LeaveTypes.Add(leaveType);
        await db.SaveChangesAsync();

        db.LeaveBalances.Add(new LeaveBalance
        {
            OrgId = orgId,
            EmployeeId = empId,
            LeaveTypeId = leaveType.LeaveTypeId,
            LeaveYear = 2026,
            Opening = 15m,
            Accrued = 0m,
            Taken = 0m
        });
        await db.SaveChangesAsync();

        var fakeEmployee = new FakeEmployeeLookupClient();
        fakeEmployee.AddEmployee(new EmployeeProfile
        {
            EmployeeId = empId,
            UserId = empUserId,
            EmployeeCode = "EMP001",
            FullName = "Jane Doe",
            ReportsToEmployeeId = managerId
        });
        fakeEmployee.AddEmployee(new EmployeeProfile
        {
            EmployeeId = managerId,
            UserId = managerUserId,
            EmployeeCode = "MGR001",
            FullName = "John Manager"
        });

        var empUser = new FakeCurrentUser(empUserId);
        var tenant = new TenantContext { CustomerId = customerId, OrgId = orgId };
        var engine = new LeaveCalculationEngine();
        var leaveService = new LeaveService(db, engine);
        var attendanceService = new AttendanceService(db, engine);

        var meController = new MeTimeLeaveController(db, empUser, tenant, fakeEmployee, leaveService, attendanceService);

        // 1. Employee applies for leave
        var applyReq = new ApplyLeaveSelfRequest
        {
            LeaveTypeId = leaveType.LeaveTypeId,
            FromDate = new DateOnly(2026, 11, 2),
            ToDate = new DateOnly(2026, 11, 4),
            FromHalf = false,
            ToHalf = false,
            Reason = "Family vacation"
        };

        var applyResult = await meController.ApplyLeave(applyReq, default);
        var okResult = Assert.IsType<OkObjectResult>(applyResult);
        var appDto = Assert.IsType<LeaveApplicationDto>(okResult.Value);

        Assert.Equal(empId, appDto.EmployeeId);
        Assert.Equal(3m, appDto.Days);
        Assert.Equal(ApprovalStatus.InApproval, appDto.ApprovalStatus);

        // 2. Manager views pending inbox
        var managerUser = new FakeCurrentUser(managerUserId);
        var approvalsController = new ApprovalsController(db, managerUser, tenant, fakeEmployee, leaveService);

        var pendingResult = await approvalsController.GetPendingApprovals(default);
        var pendingOk = Assert.IsType<OkObjectResult>(pendingResult);
        var pendingList = Assert.IsAssignableFrom<IEnumerable<PendingApprovalItemDto>>(pendingOk.Value);
        var item = Assert.Single(pendingList, p => p.RequestId == appDto.LeaveApplicationId && p.RequestKind == "Leave");

        // 3. Manager approves the request
        var actResult = await approvalsController.Act(item.ApprovalStepId, new ActApprovalRequest
        {
            Action = "Approve",
            Comments = "Approved, enjoy your vacation"
        }, default);

        Assert.IsType<OkObjectResult>(actResult);

        // 4. Verify in DB: Leave status is approved and balance taken is deducted
        var savedApp = await db.LeaveApplications.FirstAsync(a => a.LeaveApplicationId == appDto.LeaveApplicationId);
        Assert.Equal(LeaveStatus.Approved, savedApp.LeaveStatus);
        Assert.Equal(ApprovalStatus.Approved, savedApp.ApprovalStatus);

        var balance = await db.LeaveBalances.FirstAsync(b => b.EmployeeId == empId && b.LeaveTypeId == leaveType.LeaveTypeId);
        Assert.Equal(3m, balance.Taken);
    }

    [SkippableFact]
    public async Task Manager_can_reject_leave_request_with_comments()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        Guid customerId = Guid.NewGuid(), orgId = Guid.NewGuid();
        Guid empUserId = Guid.NewGuid();
        Guid managerUserId = Guid.NewGuid();
        long empId = 3010;
        long managerId = 3011;

        await using TimeLeaveDbContext db = _postgres.CreateContext(customerId, orgId);

        var leaveType = new LeaveType { OrgId = orgId, Code = "SL", Name = "Sick Leave", IsPaid = true, IsActive = true };
        db.LeaveTypes.Add(leaveType);
        await db.SaveChangesAsync();

        db.LeaveBalances.Add(new LeaveBalance
        {
            OrgId = orgId,
            EmployeeId = empId,
            LeaveTypeId = leaveType.LeaveTypeId,
            LeaveYear = 2026,
            Opening = 10m
        });
        await db.SaveChangesAsync();

        var fakeEmployee = new FakeEmployeeLookupClient();
        fakeEmployee.AddEmployee(new EmployeeProfile
        {
            EmployeeId = empId,
            UserId = empUserId,
            ReportsToEmployeeId = managerId
        });
        fakeEmployee.AddEmployee(new EmployeeProfile
        {
            EmployeeId = managerId,
            UserId = managerUserId
        });

        var empUser = new FakeCurrentUser(empUserId);
        var managerUser = new FakeCurrentUser(managerUserId);
        var tenant = new TenantContext { CustomerId = customerId, OrgId = orgId };
        var engine = new LeaveCalculationEngine();
        var leaveService = new LeaveService(db, engine);
        var attendanceService = new AttendanceService(db, engine);

        var meController = new MeTimeLeaveController(db, empUser, tenant, fakeEmployee, leaveService, attendanceService);
        var approvalsController = new ApprovalsController(db, managerUser, tenant, fakeEmployee, leaveService);

        var applyResult = await meController.ApplyLeave(new ApplyLeaveSelfRequest
        {
            LeaveTypeId = leaveType.LeaveTypeId,
            FromDate = new DateOnly(2026, 12, 1),
            ToDate = new DateOnly(2026, 12, 1),
            Reason = "Personal work"
        }, default);

        var appDto = Assert.IsType<LeaveApplicationDto>(Assert.IsType<OkObjectResult>(applyResult).Value);

        var pendingOk = Assert.IsType<OkObjectResult>(await approvalsController.GetPendingApprovals(default));
        var item = Assert.Single((IEnumerable<PendingApprovalItemDto>)pendingOk.Value!, p => p.RequestId == appDto.LeaveApplicationId);

        // Reject
        var rejectResult = await approvalsController.Act(item.ApprovalStepId, new ActApprovalRequest
        {
            Action = "Reject",
            Comments = "Critical sprint delivery"
        }, default);

        Assert.IsType<OkObjectResult>(rejectResult);

        var savedApp = await db.LeaveApplications.FirstAsync(a => a.LeaveApplicationId == appDto.LeaveApplicationId);
        Assert.Equal(LeaveStatus.Rejected, savedApp.LeaveStatus);
        Assert.Equal(ApprovalStatus.Rejected, savedApp.ApprovalStatus);

        // Balance must remain unchanged
        var balance = await db.LeaveBalances.FirstAsync(b => b.EmployeeId == empId && b.LeaveTypeId == leaveType.LeaveTypeId);
        Assert.Equal(0m, balance.Taken);
    }

    [Fact]
    public async Task Self_service_endpoints_return_not_found_when_user_id_is_absent()
    {
        Guid customerId = Guid.NewGuid(), orgId = Guid.NewGuid();
        var emptyUser = new FakeCurrentUser(null); // No UserId in token
        var tenant = new TenantContext { CustomerId = customerId, OrgId = orgId };
        var fakeEmployee = new FakeEmployeeLookupClient();

        var meController = new MeTimeLeaveController(null!, emptyUser, tenant, fakeEmployee, null!, null!);

        var result = await meController.GetLeaveBalances(null, default);
        Assert.IsType<NotFoundObjectResult>(result);

        var applyResult = await meController.ApplyLeave(new ApplyLeaveSelfRequest(), default);
        Assert.IsType<NotFoundObjectResult>(applyResult);
    }

    [Fact]
    public async Task Self_service_endpoints_return_not_found_when_user_has_no_employee_profile()
    {
        Guid customerId = Guid.NewGuid(), orgId = Guid.NewGuid();
        Guid unlinkedUserId = Guid.NewGuid();
        var unlinkedUser = new FakeCurrentUser(unlinkedUserId);
        var tenant = new TenantContext { CustomerId = customerId, OrgId = orgId };
        var fakeEmployee = new FakeEmployeeLookupClient(); // empty, no employee linked to unlinkedUserId

        var meController = new MeTimeLeaveController(null!, unlinkedUser, tenant, fakeEmployee, null!, null!);

        var result = await meController.GetLeaveBalances(null, default);
        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        Assert.NotNull(notFound.Value);
    }

    private sealed class FakeEmployeeLookupClient : IEmployeeClient
    {
        private readonly List<EmployeeProfile> _employees = [];

        public void AddEmployee(EmployeeProfile emp) => _employees.Add(emp);

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
