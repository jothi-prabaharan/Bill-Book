using Master.Api.Services;
using Master.Repository.Repositories;
using Master.Repository.Clients;
using Master.Entity.Models;
using Master.Entity.TableEntities;
using Master.Repository;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Apps;
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
    private readonly IPasswordHasherRepository _hasher;

    public InternalUsersController(AdminDbContext db, IPasswordHasherRepository hasher)
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

    /// <summary>
    /// Display names for a batch of user ids, for a report showing who logged
    /// or posted something (Reporting's <c>BatchedNameResolver</c>). An id with
    /// no user is left out. At most 500 ids a call.
    /// </summary>
    /// <summary>
    /// The branch's active users in an app, each with the permissions a token
    /// for that branch would carry now (TK-114). Built the way sign-in builds
    /// them: the user's active roles in the branch for the app, never
    /// <c>platform.*</c> from a role. A deactivated user is not listed.
    /// </summary>
    [HttpGet("branch")]
    public async Task<IActionResult> Branch([FromQuery] Guid orgId, [FromQuery] App app = App.RetailErp, CancellationToken ct = default)
    {
        var grants = await (
            from u in _db.UserOrganizationRoles
            join r in _db.Roles on u.RoleId equals r.RoleId
            join user in _db.Users on u.UserId equals user.UserId
            where u.OrgId == orgId && u.IsActive && r.IsActive && user.IsActive
            select new { user.UserId, user.DisplayName, user.Email, u.RoleId }).ToListAsync(ct);

        List<int> roleIds = [.. grants.Select(g => g.RoleId).Distinct()];
        var rolePermissions = await (
            from rp in _db.RolePermissions
            join p in _db.Permissions on rp.PermissionId equals p.PermissionId
            where roleIds.Contains(rp.RoleId) && p.Module != PlatformOperatorRepository.PlatformModule
            select new { rp.RoleId, p.Code }).ToListAsync(ct);

        ILookup<int, string> byRole = rolePermissions.ToLookup(x => x.RoleId, x => x.Code);

        List<BranchUserAccess> users =
        [
            .. grants
                .GroupBy(g => g.UserId)
                .Select(g => new BranchUserAccess
                {
                    UserId = g.Key,
                    DisplayName = g.First().DisplayName,
                    Email = g.First().Email,
                    Permissions = [.. g.SelectMany(x => byRole[x.RoleId]).Distinct().Order(StringComparer.Ordinal)],
                })
                .OrderBy(u => u.DisplayName),
        ];

        return Ok(users);
    }

    [HttpPost("batch")]
    public async Task<IActionResult> Names([FromBody] List<Guid> userIds, CancellationToken ct)
    {
        if (userIds.Count > 500)
        {
            return BadRequest(new MessageResponse { Message = "Ask for at most 500 users at a time." });
        }

        List<Guid> ids = [.. userIds.Distinct()];
        Dictionary<Guid, string> names = await _db.Users.AsNoTracking()
            .Where(u => ids.Contains(u.UserId))
            .ToDictionaryAsync(u => u.UserId, u => u.DisplayName, ct);

        return Ok(names);
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
