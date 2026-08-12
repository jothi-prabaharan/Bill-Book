using System.Security.Cryptography;
using Master.Entity.Models;
using Master.Entity.TableEntities;
using Master.Repository;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Interfaces;

namespace Master.Api.Services;

public sealed class UserService
{
    private static readonly TimeSpan InviteLifetime = TimeSpan.FromDays(7);

    private readonly MasterDbContext _db;
    private readonly IEmailSender _email;
    private readonly OrgContextService _orgs;
    private readonly IConfiguration _config;
    private readonly TimeProvider _clock;

    public UserService(
        MasterDbContext db,
        IEmailSender email,
        OrgContextService orgs,
        IConfiguration config,
        TimeProvider clock)
    {
        _db = db;
        _email = email;
        _orgs = orgs;
        _config = config;
        _clock = clock;
    }

    /// <summary>Users assigned to the current organization.</summary>
    public async Task<IReadOnlyList<UserListItem>> ListAsync(Guid orgId, CancellationToken ct)
    {
        DateTimeOffset now = _clock.GetUtcNow();

        return await (
            from assignment in _db.UserOrganizationRoles
            join user in _db.Users on assignment.UserId equals user.UserId
            join role in _db.Roles on assignment.RoleId equals role.RoleId
            where assignment.OrgId == orgId
            orderby user.DisplayName
            select new UserListItem
            {
                UserId = user.UserId,
                Email = user.Email,
                DisplayName = user.DisplayName,
                MobileNumber = user.MobileNumber,
                RoleId = role.RoleId,
                RoleName = role.DisplayName,
                LastLoginAt = user.LastLoginAt,
                IsActive = assignment.IsActive && user.IsActive,
                // No password yet means the invitation has not been accepted.
                IsInvitePending = user.PasswordHash == null || user.PasswordHash == string.Empty,
                IsLockedOut = user.LockedOutUntil != null && user.LockedOutUntil > now,
            }).ToListAsync(ct);
    }

    /// <summary>
    /// Creates the user (no password), assigns the role for this organization,
    /// issues a 7-day invitation token and emails the link. Never issues a
    /// temporary password.
    /// </summary>
    public async Task<InviteUserResult> InviteAsync(
        Guid orgId, Guid? customerId, InviteUserRequest request, CancellationToken ct)
    {
        OrgContextResponse? org = await _orgs.ResolveAsync(orgId, ct);
        if (org is null)
        {
            return new InviteUserResult(InviteOutcome.OrgNotFound, null);
        }

        int activeUsers = await _db.UserOrganizationRoles
            .CountAsync(a => a.OrgId == orgId && a.IsActive, ct);
        if (activeUsers >= org.MaxUsers)
        {
            return new InviteUserResult(InviteOutcome.UserLimitReached, null);
        }

        User? user = await _db.Users.FirstOrDefaultAsync(u => u.Email == request.Email, ct);
        bool isNewUser = user is null;

        if (user is null)
        {
            user = new User
            {
                UserId = Guid.NewGuid(),
                Email = request.Email,
                PasswordHash = null,
                DisplayName = request.DisplayName,
                MobileNumber = request.MobileNumber,
                EmailConfirmed = false,
                IsActive = true,
            };
            _db.Users.Add(user);
        }

        bool alreadyInOrg = await _db.UserOrganizationRoles
            .AnyAsync(a => a.UserId == user.UserId && a.OrgId == orgId, ct);
        if (alreadyInOrg && !isNewUser)
        {
            return new InviteUserResult(InviteOutcome.AlreadyMember, user.UserId);
        }

        if (!alreadyInOrg)
        {
            _db.UserOrganizationRoles.Add(new UserOrganizationRole
            {
                UserId = user.UserId,
                OrgId = orgId,
                RoleId = request.RoleId,
                IsActive = true,
            });
        }

        string token = await IssueInviteTokenAsync(user.UserId, ct);
        await _db.SaveChangesAsync(ct);

        await SendInviteAsync(user, org.OrgName, token, customerId, ct);
        return new InviteUserResult(InviteOutcome.Ok, user.UserId);
    }

    /// <summary>Re-issues the invitation and sends it again, invalidating the previous link.</summary>
    public async Task<bool> ResendInviteAsync(
        Guid orgId, Guid? customerId, Guid userId, CancellationToken ct)
    {
        User? user = await _db.Users.FirstOrDefaultAsync(u => u.UserId == userId, ct);
        bool member = await _db.UserOrganizationRoles.AnyAsync(
            a => a.UserId == userId && a.OrgId == orgId, ct);
        if (user is null || !member)
        {
            return false;
        }

        OrgContextResponse? org = await _orgs.ResolveAsync(orgId, ct);
        string token = await IssueInviteTokenAsync(userId, ct);
        await _db.SaveChangesAsync(ct);

        await SendInviteAsync(user, org?.OrgName ?? "your organization", token, customerId, ct);
        return true;
    }

    /// <summary>Revokes access by deactivating the org assignment — never deletes.</summary>
    public async Task<RevokeOutcome> RevokeAsync(
        Guid orgId, Guid actingUserId, Guid userId, CancellationToken ct)
    {
        if (actingUserId == userId)
        {
            return RevokeOutcome.CannotRevokeSelf;
        }

        UserOrganizationRole? assignment = await _db.UserOrganizationRoles
            .FirstOrDefaultAsync(a => a.OrgId == orgId && a.UserId == userId && a.IsActive, ct);
        if (assignment is null)
        {
            return RevokeOutcome.NotFound;
        }

        // Never strand an organization with no Owner.
        const int ownerRoleId = 1;
        if (assignment.RoleId == ownerRoleId)
        {
            int owners = await _db.UserOrganizationRoles
                .CountAsync(a => a.OrgId == orgId && a.RoleId == ownerRoleId && a.IsActive, ct);
            if (owners <= 1)
            {
                return RevokeOutcome.LastOwner;
            }
        }

        assignment.IsActive = false;
        await _db.SaveChangesAsync(ct);
        return RevokeOutcome.Ok;
    }

    private async Task<string> IssueInviteTokenAsync(Guid userId, CancellationToken ct)
    {
        DateTimeOffset now = _clock.GetUtcNow();

        // A fresh invitation supersedes any outstanding one.
        List<PasswordResetToken> live = await _db.PasswordResetTokens
            .Where(t => t.UserId == userId && t.UsedAt == null && t.ExpiresAt > now)
            .ToListAsync(ct);
        foreach (PasswordResetToken stale in live)
        {
            stale.UsedAt = now;
        }

        string token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48))
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');

        _db.PasswordResetTokens.Add(new PasswordResetToken
        {
            UserId = userId,
            TokenHash = HashUtil.Sha256(token),
            ExpiresAt = now.Add(InviteLifetime),
        });

        return token;
    }

    private async Task SendInviteAsync(
        User user, string orgName, string token, Guid? customerId, CancellationToken ct)
    {
        // Guarded rather than defaulted: an unset value yields a relative link that
        // is dead in every mail client, and the send still reports success.
        string baseUrl = _config["App:BaseUrl"] is { Length: > 0 } configured
            ? configured.TrimEnd('/')
            : throw new InvalidOperationException(
                "App:BaseUrl is not configured. Set it in appsettings.{Environment}.json " +
                "or via the App__BaseUrl environment variable.");
        string link = $"{baseUrl}/accept-invitation?token={Uri.EscapeDataString(token)}" +
                      $"&email={Uri.EscapeDataString(user.Email)}";

        await _email.SendAsync(new EmailMessage
        {
            ToEmail = user.Email,
            ToName = user.DisplayName,
            CustomerId = customerId,
            Subject = $"You have been invited to {orgName} on Bill-Book",
            HtmlBody =
                $"<p>Hello {user.DisplayName},</p>" +
                $"<p>You have been invited to join <strong>{orgName}</strong> on Bill-Book.</p>" +
                $"<p><a href=\"{link}\">Set your password</a></p>" +
                "<p>This link expires in 7 days.</p>",
            TextBody =
                $"Hello {user.DisplayName},\n\n" +
                $"You have been invited to join {orgName} on Bill-Book.\n\n" +
                $"Set your password: {link}\n\nThis link expires in 7 days.",
        }, ct);
    }
}

public enum InviteOutcome
{
    Ok = 1,
    OrgNotFound = 2,
    UserLimitReached = 3,
    AlreadyMember = 4,
}

public sealed record InviteUserResult(InviteOutcome Outcome, Guid? UserId);

public enum RevokeOutcome
{
    Ok = 1,
    NotFound = 2,
    CannotRevokeSelf = 3,
    LastOwner = 4,
}
