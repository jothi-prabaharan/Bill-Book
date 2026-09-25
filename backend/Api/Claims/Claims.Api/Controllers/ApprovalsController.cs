using Claims.Api.Services;
using Claims.Entity.Enums;
using Claims.Entity.Models;
using Claims.Repository;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Kernel.Apps;
using Shared.Kernel.Interfaces;
using Shared.Kernel.Internal;

namespace Claims.Api.Controllers;

[ApiController]
[Authorize]
[RequireApp(App.Hrms)]
[RequireModulePermission("claims")]
[Route("api/clm/approvals")]
public sealed class ApprovalsController : ControllerBase
{
    private readonly ClaimsDbContext _db;
    private readonly ClaimService _claimService;
    private readonly ICurrentUser _currentUser;

    public ApprovalsController(ClaimsDbContext db, ClaimService claimService, ICurrentUser currentUser)
    {
        _db = db;
        _claimService = claimService;
        _currentUser = currentUser;
    }

    [HttpGet("pending")]
    public async Task<IActionResult> Pending(CancellationToken ct)
    {
        var pendingSteps = await _db.ApprovalSteps
            .AsNoTracking()
            .Include(s => s.Claim)
            .Where(s => s.StepStatus == ApprovalStepStatus.Pending && s.Claim.ClaimStatus == ClaimStatus.Submitted)
            .OrderBy(s => s.Claim.ClaimDate)
            .Select(s => new PendingClaimApprovalDto(
                s.ApprovalStepId,
                s.ExpenseClaimId,
                s.Claim.ClaimNo,
                s.Claim.EmployeeId,
                null,
                s.Claim.ClaimDate,
                s.Claim.TotalAmount,
                s.Label,
                s.Sequence))
            .ToListAsync(ct);

        return Ok(pendingSteps);
    }

    [HttpPost("{claimId:long}/act")]
    public async Task<IActionResult> Act(long claimId, [FromBody] ActClaimApprovalRequest req, CancellationToken ct)
    {
        Guid userId = _currentUser.UserId ?? Guid.Empty;
        var claim = await _claimService.ActApprovalAsync(claimId, req, userId, ct);
        return Ok(claim);
    }
}
