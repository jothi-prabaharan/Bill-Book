using Master.Api.Services;
using Master.Entity.Models;
using Master.Entity.TableEntities;
using Master.Repository;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Internal;
using Shared.Kernel.Validation;

namespace Master.Api.Controllers;

/// <summary>
/// Internal service-to-service API — called by the Platform provisioner.
/// Must not be routed through the public gateway.
/// </summary>
[ApiController]
[AllowAnonymous]
[InternalOnly]
[Route("internal/users")]
public sealed class InternalUsersController : ControllerBase
{
    /// <summary>Seeded system-role id for Owner (see AdminDbContext seed).</summary>
    private const int OwnerRoleId = 1;

    private readonly AdminDbContext _db;
    private readonly IPasswordHasher _hasher;

    public InternalUsersController(AdminDbContext db, IPasswordHasher hasher)
    {
        _db = db;
        _hasher = hasher;
    }

    [HttpPost("owner")]
    public async Task<IActionResult> CreateOwner([FromBody] CreateOwnerUserRequest request, CancellationToken ct)
    {
        // Idempotent on email — a provisioning retry must not fail or duplicate.
        User? user = await _db.Users.FirstOrDefaultAsync(u => u.Email == request.Email, ct);
        if (user is null)
        {
            user = new User
            {
                UserId = Guid.NewGuid(),
                Email = request.Email,
                PasswordHash = _hasher.Hash(request.Password),
                DisplayName = request.DisplayName,
                MobileNumber = PhoneNumbers.NormalizeOptional(request.MobileNumber),
                EmailConfirmed = false,
                IsActive = true,
            };
            _db.Users.Add(user);
        }

        bool assigned = await _db.UserOrganizationRoles.AnyAsync(
            a => a.UserId == user.UserId && a.OrgId == request.OrgId && a.RoleId == OwnerRoleId, ct);
        if (!assigned)
        {
            _db.UserOrganizationRoles.Add(new UserOrganizationRole
            {
                UserId = user.UserId,
                OrgId = request.OrgId,
                RoleId = OwnerRoleId,
                IsActive = true,
            });
        }

        await _db.SaveChangesAsync(ct);
        return Ok(new { user.UserId });
    }

    [HttpPost("{userId:guid}/deactivate")]
    public async Task<IActionResult> DeactivateUser(Guid userId, CancellationToken ct)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.UserId == userId, ct);
        if (user is null)
        {
            return NotFound();
        }

        user.IsActive = false;
        var roles = await _db.UserOrganizationRoles.Where(r => r.UserId == userId).ToListAsync(ct);
        foreach (var r in roles)
        {
            r.IsActive = false;
        }

        await _db.SaveChangesAsync(ct);
        return NoContent();
    }
}
