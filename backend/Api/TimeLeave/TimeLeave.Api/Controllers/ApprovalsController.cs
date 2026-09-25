using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Apps;
using Shared.Kernel.Interfaces;
using Shared.Kernel.Internal;
using Shared.Kernel.Tenancy;
using TimeLeave.Api.Services;
using TimeLeave.Entity.Enums;
using TimeLeave.Entity.Models;
using TimeLeave.Entity.TableEntities;
using TimeLeave.Repository;

namespace TimeLeave.Api.Controllers;

/// <summary>
/// Approvals inbox and workflow action processing (H8, TK-55).
/// Managers review pending leave, regularisation, and overtime requests and approve/reject/send back.
/// </summary>
[ApiController]
[Authorize]
[RequireApp(App.Hrms | App.Payroll)]
[Route("api/tla/approvals")]
[Route("api/approvals")]
public sealed class ApprovalsController : ControllerBase
{
    private readonly TimeLeaveDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly ITenantContext _tenant;
    private readonly IHrmClient _hrm;
    private readonly LeaveService _leave;

    public ApprovalsController(
        TimeLeaveDbContext db,
        ICurrentUser currentUser,
        ITenantContext tenant,
        IHrmClient hrm,
        LeaveService leave)
    {
        _db = db;
        _currentUser = currentUser;
        _tenant = tenant;
        _hrm = hrm;
        _leave = leave;
    }

    private async Task<long?> ResolveCurrentEmployeeIdAsync(CancellationToken ct)
    {
        if (_currentUser.UserId is not Guid userId || _tenant.CustomerId is not Guid customerId || _tenant.OrgId is not Guid orgId)
        {
            return null;
        }

        var profile = await _hrm.FindByUserIdAsync(customerId, orgId, userId, ct);
        return profile?.EmployeeId;
    }

    [HttpGet("pending")]
    public async Task<IActionResult> GetPendingApprovals(CancellationToken ct)
    {
        long? empId = await ResolveCurrentEmployeeIdAsync(ct);
        Guid? userId = _currentUser.UserId;

        // Pending steps where approver is current employee or current user (or null/all)
        var steps = await _db.ApprovalSteps
            .AsNoTracking()
            .Where(s => s.StepStatus == ApprovalStepStatus.Pending)
            .Where(s => (empId.HasValue && s.ApproverEmployeeId == empId.Value)
                     || (s.ApproverEmployeeId == null))
            .OrderBy(s => s.DueDate ?? DateOnly.MaxValue)
            .ThenBy(s => s.CreatedAt)
            .ToListAsync(ct);

        var result = new List<PendingApprovalItemDto>();

        foreach (var step in steps)
        {
            if (step.RequestKind == RequestKind.Leave)
            {
                var app = await _db.LeaveApplications.AsNoTracking().FirstOrDefaultAsync(a => a.LeaveApplicationId == step.RequestId, ct);
                if (app != null)
                {
                    string? empName = null;
                    if (_tenant.CustomerId is Guid cid && _tenant.OrgId is Guid oid)
                    {
                        var p = await _hrm.FindByIdAsync(cid, oid, app.EmployeeId, ct);
                        empName = p?.FullName;
                    }

                    result.Add(new PendingApprovalItemDto
                    {
                        ApprovalStepId = step.ApprovalStepId,
                        Sequence = step.Sequence,
                        Label = step.Label,
                        RequestKind = "Leave",
                        RequestId = app.LeaveApplicationId,
                        EmployeeId = app.EmployeeId,
                        EmployeeName = empName,
                        Reason = app.Reason,
                        FromDate = app.FromDate,
                        ToDate = app.ToDate,
                        Days = app.Days,
                        CreatedAt = app.CreatedAt
                    });
                }
            }
            else if (step.RequestKind == RequestKind.Regularisation)
            {
                var reg = await _db.RegularisationRequests.AsNoTracking().FirstOrDefaultAsync(r => r.RegularisationRequestId == step.RequestId, ct);
                if (reg != null)
                {
                    string? empName = null;
                    if (_tenant.CustomerId is Guid cid && _tenant.OrgId is Guid oid)
                    {
                        var p = await _hrm.FindByIdAsync(cid, oid, reg.EmployeeId, ct);
                        empName = p?.FullName;
                    }

                    result.Add(new PendingApprovalItemDto
                    {
                        ApprovalStepId = step.ApprovalStepId,
                        Sequence = step.Sequence,
                        Label = step.Label,
                        RequestKind = "Regularisation",
                        RequestId = reg.RegularisationRequestId,
                        EmployeeId = reg.EmployeeId,
                        EmployeeName = empName,
                        Reason = reg.Reason,
                        AttendanceDate = reg.AttendanceDate,
                        CreatedAt = reg.CreatedAt
                    });
                }
            }
            else if (step.RequestKind == RequestKind.Overtime)
            {
                var ot = await _db.OvertimeRequests.AsNoTracking().FirstOrDefaultAsync(o => o.OvertimeRequestId == step.RequestId, ct);
                if (ot != null)
                {
                    string? empName = null;
                    if (_tenant.CustomerId is Guid cid && _tenant.OrgId is Guid oid)
                    {
                        var p = await _hrm.FindByIdAsync(cid, oid, ot.EmployeeId, ct);
                        empName = p?.FullName;
                    }

                    result.Add(new PendingApprovalItemDto
                    {
                        ApprovalStepId = step.ApprovalStepId,
                        Sequence = step.Sequence,
                        Label = step.Label,
                        RequestKind = "Overtime",
                        RequestId = ot.OvertimeRequestId,
                        EmployeeId = ot.EmployeeId,
                        EmployeeName = empName,
                        AttendanceDate = ot.AttendanceDate,
                        OvertimeMinutes = ot.Minutes,
                        CreatedAt = ot.CreatedAt
                    });
                }
            }
        }

        return Ok(result);
    }

    [HttpGet("history")]
    public async Task<IActionResult> GetApprovalHistory(CancellationToken ct)
    {
        Guid? userId = _currentUser.UserId;
        if (!userId.HasValue) return Forbid();

        var history = await _db.ApprovalSteps
            .AsNoTracking()
            .Where(s => s.ActedByUserId == userId.Value)
            .OrderByDescending(s => s.ActedAt)
            .Take(50)
            .Select(s => new
            {
                s.ApprovalStepId,
                s.Sequence,
                s.Label,
                RequestKind = s.RequestKind.ToString(),
                s.RequestId,
                Status = s.StepStatus.ToString(),
                s.Comments,
                s.ActedAt
            })
            .ToListAsync(ct);

        return Ok(history);
    }

    [HttpPost("{stepId:long}/act")]
    public async Task<IActionResult> Act(long stepId, [FromBody] ActApprovalRequest req, CancellationToken ct)
    {
        long? empId = await ResolveCurrentEmployeeIdAsync(ct);
        Guid? userId = _currentUser.UserId;

        var step = await _db.ApprovalSteps.FirstOrDefaultAsync(s => s.ApprovalStepId == stepId, ct);
        if (step is null) return NotFound(new { message = $"Approval step {stepId} not found." });

        if (step.StepStatus != ApprovalStepStatus.Pending)
        {
            return BadRequest(new { message = "Step is not currently pending." });
        }

        if (step.ApproverEmployeeId.HasValue && empId.HasValue && step.ApproverEmployeeId.Value != empId.Value)
        {
            return Forbid();
        }

        step.ActedByUserId = userId;
        step.ActedAt = DateTimeOffset.UtcNow;
        step.Comments = req.Comments;

        if (req.Action.Equals("Approve", StringComparison.OrdinalIgnoreCase))
        {
            step.StepStatus = ApprovalStepStatus.Approved;

            // Check if there is a next step
            var nextStep = await _db.ApprovalSteps
                .Where(s => s.RequestKind == step.RequestKind && s.RequestId == step.RequestId && s.Sequence > step.Sequence && s.StepStatus == ApprovalStepStatus.Waiting)
                .OrderBy(s => s.Sequence)
                .FirstOrDefaultAsync(ct);

            if (nextStep != null)
            {
                nextStep.StepStatus = ApprovalStepStatus.Pending;
                if (step.RequestKind == RequestKind.Leave)
                {
                    var app = await _db.LeaveApplications.FindAsync(new object[] { step.RequestId }, ct);
                    if (app != null) app.CurrentStepLabel = nextStep.Label;
                }
            }
            else
            {
                // Final approval!
                if (step.RequestKind == RequestKind.Leave)
                {
                    var app = await _db.LeaveApplications.FindAsync(new object[] { step.RequestId }, ct);
                    if (app != null)
                    {
                        app.LeaveStatus = LeaveStatus.Approved;
                        app.ApprovalStatus = ApprovalStatus.Approved;
                        app.CurrentStepLabel = "Approved";

                        int year = app.FromDate.Year;
                        await _db.LeaveBalances
                            .Where(b => b.EmployeeId == app.EmployeeId && b.LeaveTypeId == app.LeaveTypeId && b.LeaveYear == year)
                            .ExecuteUpdateAsync(s => s.SetProperty(b => b.Taken, b => b.Taken + app.Days), ct);

                        for (var d = app.FromDate; d <= app.ToDate; d = d.AddDays(1))
                        {
                            var daily = await _db.DailyAttendances.FirstOrDefaultAsync(a => a.EmployeeId == app.EmployeeId && a.AttendanceDate == d, ct);
                            if (daily != null)
                            {
                                daily.AttendanceStatus = AttendanceStatus.OnLeave;
                                daily.WorkedMinutes = 0;
                            }
                            else
                            {
                                _db.DailyAttendances.Add(new DailyAttendance
                                {
                                    EmployeeId = app.EmployeeId,
                                    AttendanceDate = d,
                                    AttendanceStatus = AttendanceStatus.OnLeave,
                                    WorkedMinutes = 0
                                });
                            }
                        }
                    }
                }
                else if (step.RequestKind == RequestKind.Regularisation)
                {
                    var reg = await _db.RegularisationRequests.FindAsync(new object[] { step.RequestId }, ct);
                    if (reg != null)
                    {
                        reg.ApprovalStatus = ApprovalStatus.Approved;
                        reg.CurrentStepLabel = "Approved";

                        var daily = await _db.DailyAttendances.FirstOrDefaultAsync(d => d.EmployeeId == reg.EmployeeId && d.AttendanceDate == reg.AttendanceDate, ct);
                        if (daily != null)
                        {
                            daily.AttendanceStatus = reg.RequestedStatus;
                            if (reg.RequestedIn.HasValue) daily.FirstIn = reg.RequestedIn.Value;
                            if (reg.RequestedOut.HasValue) daily.LastOut = reg.RequestedOut.Value;
                            daily.LateMinutes = 0;
                            daily.EarlyOutMinutes = 0;
                            daily.AttendanceSource = AttendanceSource.Regularised;
                            if (reg.RequestedIn.HasValue && reg.RequestedOut.HasValue)
                            {
                                daily.WorkedMinutes = Math.Max(0, (int)(reg.RequestedOut.Value - reg.RequestedIn.Value).TotalMinutes - 60);
                            }
                        }
                        else
                        {
                            _db.DailyAttendances.Add(new DailyAttendance
                            {
                                EmployeeId = reg.EmployeeId,
                                AttendanceDate = reg.AttendanceDate,
                                AttendanceStatus = reg.RequestedStatus,
                                AttendanceSource = AttendanceSource.Regularised,
                                FirstIn = reg.RequestedIn,
                                LastOut = reg.RequestedOut,
                                LateMinutes = 0,
                                EarlyOutMinutes = 0,
                                WorkedMinutes = reg.RequestedIn.HasValue && reg.RequestedOut.HasValue ? Math.Max(0, (int)(reg.RequestedOut.Value - reg.RequestedIn.Value).TotalMinutes - 60) : 480
                            });
                        }
                    }
                }
                else if (step.RequestKind == RequestKind.Overtime)
                {
                    var ot = await _db.OvertimeRequests.FindAsync(new object[] { step.RequestId }, ct);
                    if (ot != null)
                    {
                        ot.ApprovalStatus = ApprovalStatus.Approved;
                        ot.CurrentStepLabel = "Approved";
                    }
                }
            }
        }
        else if (req.Action.Equals("Reject", StringComparison.OrdinalIgnoreCase))
        {
            step.StepStatus = ApprovalStepStatus.Rejected;
            if (step.RequestKind == RequestKind.Leave)
            {
                var app = await _db.LeaveApplications.FindAsync(new object[] { step.RequestId }, ct);
                if (app != null) { app.LeaveStatus = LeaveStatus.Rejected; app.ApprovalStatus = ApprovalStatus.Rejected; app.CurrentStepLabel = "Rejected"; }
            }
            else if (step.RequestKind == RequestKind.Regularisation)
            {
                var reg = await _db.RegularisationRequests.FindAsync(new object[] { step.RequestId }, ct);
                if (reg != null) { reg.ApprovalStatus = ApprovalStatus.Rejected; reg.CurrentStepLabel = "Rejected"; }
            }
            else if (step.RequestKind == RequestKind.Overtime)
            {
                var ot = await _db.OvertimeRequests.FindAsync(new object[] { step.RequestId }, ct);
                if (ot != null) { ot.ApprovalStatus = ApprovalStatus.Rejected; ot.CurrentStepLabel = "Rejected"; }
            }
        }
        else if (req.Action.Equals("SendBack", StringComparison.OrdinalIgnoreCase))
        {
            step.StepStatus = ApprovalStepStatus.Waiting;
            var prevStep = await _db.ApprovalSteps
                .Where(s => s.RequestKind == step.RequestKind && s.RequestId == step.RequestId && s.Sequence < step.Sequence)
                .OrderByDescending(s => s.Sequence)
                .FirstOrDefaultAsync(ct);

            if (prevStep != null)
            {
                prevStep.StepStatus = ApprovalStepStatus.Pending;
            }
            else
            {
                if (step.RequestKind == RequestKind.Leave)
                {
                    var app = await _db.LeaveApplications.FindAsync(new object[] { step.RequestId }, ct);
                    if (app != null) { app.LeaveStatus = LeaveStatus.Draft; app.ApprovalStatus = ApprovalStatus.Draft; }
                }
            }
        }

        await _db.SaveChangesAsync(ct);
        return Ok(new { message = "Action processed successfully." });
    }
}
