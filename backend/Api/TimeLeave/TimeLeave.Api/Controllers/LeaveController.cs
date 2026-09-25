using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Kernel.Apps;
using Shared.Kernel.Interfaces;
using Shared.Kernel.Internal;
using Shared.Kernel.Security;
using TimeLeave.Api.Services;
using TimeLeave.Entity.Models;

namespace TimeLeave.Api.Controllers;

[ApiController]
[Route("api/tla/leave")]
[RequireApp(App.Hrms)]
[Authorize]
[RequireModulePermission("leave")]
public class LeaveController : ControllerBase
{
    private readonly LeaveService _leaveService;
    private readonly ICurrentUser _currentUser;

    public LeaveController(LeaveService leaveService, ICurrentUser currentUser)
    {
        _leaveService = leaveService;
        _currentUser = currentUser;
    }

    [HttpGet("types")]
    public async Task<ActionResult<List<LeaveTypeDto>>> GetTypes(CancellationToken ct)
    {
        return Ok(await _leaveService.GetLeaveTypesAsync(ct));
    }

    [HttpPost("types")]
    public async Task<ActionResult<LeaveTypeDto>> CreateType([FromBody] CreateLeaveTypeRequest req, CancellationToken ct)
    {
        var result = await _leaveService.CreateLeaveTypeAsync(req, ct);
        return Created($"/api/tla/leave/types/{result.LeaveTypeId}", result);
    }

    [HttpGet("policies")]
    public async Task<ActionResult<List<LeavePolicyDto>>> GetPolicies(CancellationToken ct)
    {
        return Ok(await _leaveService.GetLeavePoliciesAsync(ct));
    }

    [HttpPost("policies")]
    public async Task<ActionResult<LeavePolicyDto>> CreatePolicy([FromBody] CreateLeavePolicyRequest req, CancellationToken ct)
    {
        var result = await _leaveService.CreateLeavePolicyAsync(req, ct);
        return Created($"/api/tla/leave/policies/{result.LeavePolicyId}", result);
    }

    [HttpGet("balances")]
    public async Task<ActionResult<List<LeaveBalanceDto>>> GetBalances([FromQuery] long employeeId, [FromQuery] int year, CancellationToken ct)
    {
        return Ok(await _leaveService.GetBalancesAsync(employeeId, year, ct));
    }

    [HttpPost("applications")]
    public async Task<ActionResult<LeaveApplicationDto>> ApplyLeave([FromBody] ApplyLeaveRequest req, CancellationToken ct)
    {
        var result = await _leaveService.ApplyLeaveAsync(req, _currentUser.UserId, ct);
        return Ok(result);
    }

    [HttpPost("applications/{id:long}/approve")]
    public async Task<ActionResult<LeaveApplicationDto>> ApproveLeave([FromRoute] long id, [FromBody] ApprovalActionRequest? req, CancellationToken ct)
    {
        var result = await _leaveService.ApproveLeaveAsync(id, _currentUser.UserId, req?.Comments, ct);
        return Ok(result);
    }

    [HttpPost("applications/{id:long}/reject")]
    public async Task<IActionResult> RejectLeave([FromRoute] long id, [FromBody] ApprovalActionRequest? req, CancellationToken ct)
    {
        await _leaveService.RejectLeaveAsync(id, _currentUser.UserId, req?.Comments, ct);
        return NoContent();
    }

    [HttpPost("applications/{id:long}/cancel")]
    public async Task<IActionResult> CancelLeave([FromRoute] long id, CancellationToken ct)
    {
        await _leaveService.CancelLeaveAsync(id, ct);
        return NoContent();
    }

    [HttpPost("encashments")]
    public async Task<ActionResult<LeaveEncashmentDto>> ApplyEncashment([FromBody] ApplyEncashmentRequest req, CancellationToken ct)
    {
        var result = await _leaveService.ApplyEncashmentAsync(req, ct);
        return Ok(result);
    }

    [HttpPost("encashments/{id:long}/approve")]
    public async Task<ActionResult<LeaveEncashmentDto>> ApproveEncashment([FromRoute] long id, CancellationToken ct)
    {
        var result = await _leaveService.ApproveEncashmentAsync(id, _currentUser.UserId, ct);
        return Ok(result);
    }
}
