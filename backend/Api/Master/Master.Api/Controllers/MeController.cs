using Master.Api.Services;
using Master.Repository.Repositories;
using Master.Repository.Clients;
using Master.Entity.Models;
using Master.Repository;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Apps;
using Shared.Kernel.Interfaces;
using Shared.Kernel.Internal;

namespace Master.Api.Controllers;

/// <summary>
/// The signed-in session, for pages that do not decode the token (H0.2, TK-43).
///
/// <b>Signed in only, no module permission</b>, like the menu and the formats:
/// every user of every app needs it to draw any page, and it only ever describes
/// the caller. The user and branch come from the token, never from the URL.
/// </summary>
[ApiController]
[Authorize]
[RequireBusinessType(AllowStandalone = true)]
[Route("api/me")]
public sealed class MeController : ControllerBase
{
    private readonly AdminDbContext _db;
    private readonly OrgContextRepository _orgs;
    private readonly ICurrentUser _currentUser;

    public MeController(AdminDbContext db, OrgContextRepository orgs, ICurrentUser currentUser)
    {
        _db = db;
        _orgs = orgs;
        _currentUser = currentUser;
    }

    [HttpGet("context")]
    public async Task<IActionResult> Context(CancellationToken ct)
    {
        if (_currentUser.UserId is not Guid userId || _currentUser.OrgId is not Guid orgId)
        {
            return Forbid();
        }

        App app = RequireAppAttribute.AppOf(User);

        var user = await _db.Users
            .Where(u => u.UserId == userId && u.IsActive)
            .Select(u => new { u.DisplayName, u.Email })
            .FirstOrDefaultAsync(ct);
        OrgContextResponse? org = await _orgs.ResolveAsync(orgId, ct, app);
        string? code = await _db.Organizations
            .Where(o => o.OrgId == orgId)
            .Select(o => o.OrgCode)
            .FirstOrDefaultAsync(ct);

        if (user is null || org is null)
        {
            return Forbid();
        }

        List<SessionAppResponse> apps = [];
        if (app != App.None)
        {
            apps.Add(new SessionAppResponse
            {
                App = app.ToString(),
                LicenseStatus = org.LicenseStatus,
            });
        }

        return Ok(new SessionContextResponse
        {
            DisplayName = user.DisplayName,
            Email = user.Email,
            BranchName = org.OrgName,
            BranchCode = code ?? string.Empty,
            App = app != App.None ? app.ToString() : (org.BusinessType?.ToString() ?? "General"),
            BusinessType = org.BusinessType,
            AddOns = org.AddOns,
            PlanTier = org.PlanTier.ToString(),
            LicenseStatus = org.LicenseStatus,
            LicenseExpiry = org.LicenseExpiry,
            ExpiryIsBranchLevel = org.ExpiryIsBranchLevel,
            // The token's own permissions, since they are what every service
            // enforces for this token; a fresh read could disagree until refresh.
            Permissions = User.FindAll("permission").Select(c => c.Value).Distinct().OrderBy(p => p).ToList(),
            Apps = apps,
        });
    }
}
