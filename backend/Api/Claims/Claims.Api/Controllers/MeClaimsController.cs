using Claims.Api.Services;
using Claims.Entity.Enums;
using Claims.Entity.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Kernel.Apps;
using Shared.Kernel.Interfaces;
using Shared.Kernel.Internal;
using Shared.Kernel.Tenancy;

namespace Claims.Api.Controllers;

[ApiController]
[Authorize]
[RequireApp(App.Hrms | App.Payroll)]
[Route("api/clm/me/claims")]
[Route("api/me/claims")]
public sealed class MeClaimsController : ControllerBase
{
    private readonly ClaimService _claimService;
    private readonly ICurrentUser _currentUser;
    private readonly ITenantContext _tenant;
    private readonly IHrmClient _hrm;

    public MeClaimsController(
        ClaimService claimService,
        ICurrentUser currentUser,
        ITenantContext tenant,
        IHrmClient hrm)
    {
        _claimService = claimService;
        _currentUser = currentUser;
        _tenant = tenant;
        _hrm = hrm;
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

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var empId = await ResolveCurrentEmployeeIdAsync(ct);
        if (empId is null)
        {
            return NotFound(new { message = "No active employee profile linked to your user account." });
        }

        var claims = await _claimService.ListClaimsAsync(empId.Value, null, null, null, ct);
        return Ok(claims);
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Get(long id, CancellationToken ct)
    {
        var empId = await ResolveCurrentEmployeeIdAsync(ct);
        if (empId is null)
        {
            return NotFound(new { message = "No active employee profile linked to your user account." });
        }

        var claim = await _claimService.GetClaimByIdAsync(id, ct);
        if (claim is null || claim.EmployeeId != empId.Value)
        {
            return NotFound();
        }

        return Ok(claim);
    }

    [HttpPost]
    public async Task<IActionResult> Apply([FromBody] ApplyClaimSelfRequest req, CancellationToken ct)
    {
        var empId = await ResolveCurrentEmployeeIdAsync(ct);
        if (empId is null)
        {
            return NotFound(new { message = "No active employee profile linked to your user account." });
        }

        var createReq = new CreateExpenseClaimRequest
        {
            EmployeeId = empId.Value,
            ClaimDate = req.ClaimDate,
            PayoutMode = req.PayoutMode,
            Lines = req.Lines
        };

        var claim = await _claimService.CreateClaimAsync(createReq, ct);
        return CreatedAtAction(nameof(Get), new { id = claim.ExpenseClaimId }, claim);
    }

    [HttpPost("{id:long}/submit")]
    public async Task<IActionResult> Submit(long id, CancellationToken ct)
    {
        var empId = await ResolveCurrentEmployeeIdAsync(ct);
        if (empId is null)
        {
            return NotFound(new { message = "No active employee profile linked to your user account." });
        }

        var claim = await _claimService.GetClaimByIdAsync(id, ct);
        if (claim is null || claim.EmployeeId != empId.Value)
        {
            return NotFound();
        }

        var submitted = await _claimService.SubmitClaimAsync(id, ct);
        return Ok(submitted);
    }
}
