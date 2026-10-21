using Master.Repository.Clients;
using System.Security.Cryptography;
using Master.Entity.Models;
using Master.Entity.TableEntities;
using Master.Repository;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Interfaces;
using Shared.Kernel.Licensing;
using Shared.Kernel.Validation;

namespace Master.Repository.Repositories;

public sealed class UserRepository
{
    private static readonly TimeSpan InviteLifetime = TimeSpan.FromDays(7);

    private readonly AdminDbContext _db;
    private readonly IEmailSender _email;
    private readonly OrgContextRepository _orgs;
    private readonly IConfiguration _config;
    private readonly TimeProvider _clock;
    private readonly IPasswordHasherRepository _hasher;

    public UserRepository(
        AdminDbContext db,
        IEmailSender email,
        OrgContextRepository orgs,
        IConfiguration config,
        TimeProvider clock,
        IPasswordHasherRepository? hasher = null)
    {
        _db = db;
        _email = email;
        _orgs = orgs;
        _config = config;
        _clock = clock;
        _hasher = hasher ?? new BcryptPasswordHasher();
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
    ///
    /// A branch holds as many users as its plan allows, and a user who has been
    /// revoked still holds a place (D-68, TK-209): the place is taken the first
    /// time someone is given access and is not given back. So the count below
    /// is of everyone who has ever been assigned, and someone being invited
    /// back needs no place of their own.
    /// </summary>
    public async Task<InviteUserResult> InviteAsync(
        Guid orgId, Guid? customerId, InviteUserRequest request, CancellationToken ct)
    {
        OrgContextResponse? org = await _orgs.ResolveAsync(orgId, ct);
        if (org is null)
        {
            return new InviteUserResult(InviteOutcome.OrgNotFound, null);
        }

        User? user = await _db.Users.FirstOrDefaultAsync(u => u.Email == request.Email, ct);

        // What this person already holds in the branch, revoked or not.
        List<UserOrganizationRole> held = user is null
            ? []
            : await _db.UserOrganizationRoles
                .Where(a => a.UserId == user.UserId && a.OrgId == orgId)
                .ToListAsync(ct);

        if (held.Any(a => a.IsActive))
        {
            return new InviteUserResult(InviteOutcome.AlreadyMember, user!.UserId);
        }

        if (held.Count == 0 && await SeatRefusalAsync(org, orgId, ct) is PlanLimitResponse refusal)
        {
            return new InviteUserResult(InviteOutcome.UserLimitReached, null, refusal);
        }

        if (user is null)
        {
            user = new User
            {
                UserId = Guid.NewGuid(),
                Email = request.Email,
                PasswordHash = null,
                DisplayName = request.DisplayName,
                MobileNumber = PhoneNumbers.NormalizeOptional(request.MobileNumber),
                EmailConfirmed = false,
                IsActive = true,
            };
            _db.Users.Add(user);
        }

        // Someone invited back to the role they had gets that assignment back;
        // a new row for it would break the one-row-per-role index.
        UserOrganizationRole? returning = held.FirstOrDefault(a => a.RoleId == request.RoleId);
        if (returning is not null)
        {
            returning.IsActive = true;
        }
        else
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

    /// <summary>
    /// The refusal for a branch whose places are all taken, or null when there
    /// is one left to give. A plan with no user limit has none to run out of.
    /// </summary>
    private async Task<PlanLimitResponse?> SeatRefusalAsync(OrgContextResponse org, Guid orgId, CancellationToken ct)
    {
        if (org.MaxUsers is not int limit)
        {
            return null;
        }

        // Two invitations racing for the last place must not both pass the
        // count below. Updating the licence row holds its lock until the
        // request's transaction ends, so the second waits, then counts the first.
        await _db.Licenses
            .Where(l => l.CustomerId == org.CustomerId)
            .ExecuteUpdateAsync(s => s.SetProperty(l => l.MaxUsers, l => l.MaxUsers), ct);

        int used = await _db.UserOrganizationRoles
            .Where(a => a.OrgId == orgId)
            .Select(a => a.UserId)
            .Distinct()
            .CountAsync(ct);
        if (used < limit)
        {
            return null;
        }

        return new PlanLimitResponse
        {
            Message = PlanCatalogRepository.UserLimitMessage(org.LicenseType, limit),
            Kind = PlanLimitKind.Users,
            Plan = PlanCatalogRepository.Name(org.LicenseType),
            Limit = limit,
            Used = used,
        };
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
            TokenHash = HashUtilRepository.Sha256(token),
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

    public async Task<UserProfileDto?> GetProfileAsync(Guid userId, Guid? orgId, CancellationToken ct)
    {
        var user = await _db.Users.FindAsync(new object[] { userId }, ct);
        if (user is null) return null;

        string? roleName = null;
        if (orgId.HasValue)
        {
            roleName = await (
                from a in _db.UserOrganizationRoles
                join r in _db.Roles on a.RoleId equals r.RoleId
                where a.UserId == userId && a.OrgId == orgId.Value && a.IsActive
                select r.DisplayName
            ).FirstOrDefaultAsync(ct);
        }

        return new UserProfileDto
        {
            UserId = user.UserId,
            Email = user.Email,
            DisplayName = user.DisplayName,
            MobileNumber = user.MobileNumber,
            ThemePreference = user.ThemePreference.ToString(),
            RoleName = roleName ?? (user.IsPlatformOperator ? "Platform Operator" : "User"),
            LastLoginAt = user.LastLoginAt,
        };
    }

    public async Task<(bool success, string? error)> UpdateProfileAsync(
        Guid userId, UpdateUserProfileRequest request, CancellationToken ct)
    {
        var user = await _db.Users.FindAsync(new object[] { userId }, ct);
        if (user is null) return (false, "User not found.");

        user.DisplayName = request.DisplayName.Trim();
        user.MobileNumber = PhoneNumbers.NormalizeOptional(request.MobileNumber);

        if (!string.IsNullOrWhiteSpace(request.ThemePreference)
            && Enum.TryParse<Master.Entity.Enums.ThemePreference>(request.ThemePreference, true, out var theme))
        {
            user.ThemePreference = theme;
        }

        if (!string.IsNullOrWhiteSpace(request.NewPassword))
        {
            if (string.IsNullOrWhiteSpace(request.CurrentPassword))
            {
                return (false, "Current password is required to change password.");
            }

            if (!string.IsNullOrEmpty(user.PasswordHash) && !_hasher.Verify(request.CurrentPassword, user.PasswordHash))
            {
                return (false, "Current password is incorrect.");
            }

            user.PasswordHash = _hasher.Hash(request.NewPassword);
        }

        await _db.SaveChangesAsync(ct);
        return (true, null);
    }
}

public enum InviteOutcome
{
    Ok = 1,
    OrgNotFound = 2,
    UserLimitReached = 3,
    AlreadyMember = 4,
}

/// <summary>The outcome of an invitation; <paramref name="Limit"/> carries the figures when it was refused for a full plan.</summary>
public sealed record InviteUserResult(InviteOutcome Outcome, Guid? UserId, PlanLimitResponse? Limit = null);

public enum RevokeOutcome
{
    Ok = 1,
    NotFound = 2,
    CannotRevokeSelf = 3,
    LastOwner = 4,
}
