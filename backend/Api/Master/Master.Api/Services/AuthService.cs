using Master.Entity.Enums;
using Master.Entity.Models;
using Master.Entity.TableEntities;
using Master.Repository;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Interfaces;

namespace Master.Api.Services;

public sealed class AuthService
{
    private const int LockoutThreshold = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan OtpLifetime = TimeSpan.FromMinutes(10);
    private const int OtpMaxAttempts = 5;

    private readonly AdminDbContext _db;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenService _tokens;
    private readonly IOtpService _otp;
    private readonly OrgContextService _orgs;
    private readonly IEmailSender _email;
    private readonly TimeProvider _clock;

    public AuthService(
        AdminDbContext db,
        IPasswordHasher passwordHasher,
        ITokenService tokens,
        IOtpService otp,
        OrgContextService orgs,
        IEmailSender email,
        TimeProvider clock)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _tokens = tokens;
        _otp = otp;
        _orgs = orgs;
        _email = email;
        _clock = clock;
    }

    // ---- Step one: credentials -> pre-auth token + accessible orgs --------

    public async Task<LoginResponse> LoginAsync(LoginRequest request, string? ip, string? userAgent, CancellationToken ct)
    {
        DateTimeOffset now = _clock.GetUtcNow();
        User? user = await _db.Users.FirstOrDefaultAsync(u => u.Email == request.Email, ct);

        if (user is null || !user.IsActive)
        {
            throw new InvalidCredentialsException();
        }

        if (user.LockedOutUntil is DateTimeOffset until && until > now)
        {
            throw new AccountLockedException(until);
        }

        if (!_passwordHasher.Verify(request.Password, user.PasswordHash ?? string.Empty))
        {
            user.FailedLoginCount++;
            if (user.FailedLoginCount >= LockoutThreshold)
            {
                user.LockedOutUntil = now.Add(LockoutDuration);
            }

            _db.LoginHistories.Add(Fail(user.UserId, "Invalid password", ip, userAgent, now));
            await _db.SaveChangesAsync(ct);
            throw new InvalidCredentialsException();
        }

        user.FailedLoginCount = 0;
        user.LockedOutUntil = null;
        user.LastLoginAt = now;
        _db.LoginHistories.Add(new LoginHistory
        {
            UserId = user.UserId,
            LoginAt = now,
            IsSuccessful = true,
            IpAddress = ip,
            UserAgent = userAgent,
        });
        await _db.SaveChangesAsync(ct);

        IReadOnlyList<AccessibleOrgDto> orgs = await AccessibleOrgsAsync(user.UserId, ct);

        if (orgs.Count == 0)
        {
            throw new NoOrganizationAccessException();
        }

        Guid targetOrgId = user.LastAccessedOrgId.HasValue && orgs.Any(o => o.OrgId == user.LastAccessedOrgId.Value)
            ? user.LastAccessedOrgId.Value
            : orgs[0].OrgId;

        TokenResponse tokens = await SelectOrganizationAsync(user.UserId, targetOrgId, ip, userAgent, ct);

        return new LoginResponse
        {
            AccessToken = tokens.AccessToken,
            RefreshToken = tokens.RefreshToken,
            AccessExpiresInSeconds = tokens.AccessExpiresInSeconds,
            LicenseStatus = tokens.LicenseStatus,
            LicenseExpiry = tokens.LicenseExpiry,
            ExpiryIsBranchLevel = tokens.ExpiryIsBranchLevel,
            CurrentOrgId = targetOrgId,
            Organizations = orgs
        };
    }

    // ---- Step two: pick an org -> access + refresh token ------------------

    /// <summary>
    /// The branches this user may work in. Read at login to offer a choice, and
    /// again by the switcher — the same list either way, so the two can never
    /// disagree about what someone has access to.
    /// </summary>
    public async Task<IReadOnlyList<AccessibleOrgDto>> AccessibleOrgsAsync(
        Guid userId, CancellationToken ct)
    {
        var assignments = await (
            from uor in _db.UserOrganizationRoles
            join role in _db.Roles on uor.RoleId equals role.RoleId
            where uor.UserId == userId && uor.IsActive
            select new { uor.OrgId, RoleName = role.DisplayName }).ToListAsync(ct);

        var orgs = new List<AccessibleOrgDto>();

        foreach (var assignment in assignments)
        {
            // Names live in Platform, so a branch that cannot be resolved is
            // still listed rather than silently dropped — a missing branch is
            // something to see, not to hide.
            OrgContextResponse? ctx = await _orgs.ResolveAsync(assignment.OrgId, ct);

            orgs.Add(new AccessibleOrgDto
            {
                OrgId = assignment.OrgId,
                OrgName = ctx?.OrgName ?? "(unavailable)",
                RoleName = assignment.RoleName,
            });
        }

        return orgs;
    }

    public async Task<TokenResponse> SelectOrganizationAsync(
        Guid userId, Guid orgId, string? ip, string? userAgent, CancellationToken ct)
    {
        // A fresh sign-in into a branch starts a new family. Nothing links it to
        // whatever chain the previous session was on, which is what makes a
        // reuse of an old chain detectable rather than merely confusing.
        return await IssueAsync(userId, orgId, Guid.NewGuid(), ip, userAgent, ct);
    }

    // ---- Refresh: rotation, with reuse detection --------------------------

    /// <summary>
    /// Exchanges a refresh token for a new access token and a new refresh token.
    ///
    /// <b>Rotation, not renewal.</b> The presented token is spent by this call:
    /// it is revoked and a new one is issued in its place, in the same family.
    /// A refresh token that stayed valid for its whole seven days would be a
    /// seven-day credential sitting in browser storage, and stealing it once
    /// would be worth as much as stealing the password.
    ///
    /// <b>Presenting a token that was already spent ends the whole family.</b>
    /// Either it was stolen and is being replayed, or the legitimate client
    /// replayed it — and nothing in the request distinguishes those, so the safe
    /// reading is the hostile one. Revoking only the presented token would leave
    /// whoever holds its successor still signed in, which is exactly the wrong
    /// half to keep.
    ///
    /// <b>Two simultaneous refreshes cannot both succeed.</b> The revocation is a
    /// guarded <c>ExecuteUpdate</c> whose row count is the answer: the first
    /// caller updates one row and proceeds, the second updates none and is
    /// refused. A read-then-write would let both pass the check before either
    /// wrote, and two live chains from one token is the thing rotation exists to
    /// prevent.
    /// </summary>
    public async Task<TokenResponse> RefreshAsync(
        string presented, string? ip, string? userAgent, CancellationToken ct)
    {
        DateTimeOffset now = _clock.GetUtcNow();
        string hash = HashUtil.Sha256(presented);

        RefreshToken? token = await _db.RefreshTokens
            .FirstOrDefaultAsync(t => t.TokenHash == hash, ct);

        // No such token. Indistinguishable, deliberately, from an expired or a
        // revoked one at the API surface — see AuthController.
        if (token is null)
        {
            throw new InvalidRefreshTokenException();
        }

        if (token.RevokedAt is not null)
        {
            // Reuse. Everything still live in this family goes, including the
            // successor the thief or the client is about to use.
            await _db.RefreshTokens
                .Where(t => t.FamilyId == token.FamilyId && t.RevokedAt == null)
                .ExecuteUpdateAsync(set => set.SetProperty(t => t.RevokedAt, now), ct);

            _db.LoginHistories.Add(new LoginHistory
            {
                UserId = token.UserId,
                LoginAt = now,
                IpAddress = ip,
                UserAgent = userAgent,
                IsSuccessful = false,
                // The event, not the secret. Nothing here names the token.
                FailureReason = "Refresh token reuse detected; session family revoked.",
            });

            await _db.SaveChangesAsync(ct);

            throw new RefreshTokenReuseException();
        }

        if (token.ExpiresAt <= now)
        {
            throw new InvalidRefreshTokenException();
        }

        // The guard. One row updated means this caller won the race and owns the
        // rotation; zero means someone else already spent this token between the
        // read above and here, and this caller gets nothing.
        int claimed = await _db.RefreshTokens
            .Where(t => t.RefreshTokenId == token.RefreshTokenId && t.RevokedAt == null)
            .ExecuteUpdateAsync(set => set.SetProperty(t => t.RevokedAt, now), ct);

        if (claimed != 1)
        {
            throw new InvalidRefreshTokenException();
        }

        // ExecuteUpdate writes past the change tracker, so the entity we read is
        // now stale. Detaching it stops a later SaveChanges writing the old
        // RevokedAt back over the one just set.
        _db.Entry(token).State = EntityState.Detached;

        try
        {
            return await IssueAsync(token.UserId, token.OrgId, token.FamilyId, ip, userAgent, ct);
        }
        catch (NoOrganizationAccessException)
        {
            // Access to the branch was taken away while the session was live.
            // The old token is already spent and no new one is issued, so the
            // session ends here rather than refreshing into a branch the user no
            // longer belongs to.
            throw new InvalidRefreshTokenException();
        }
    }

    /// <summary>
    /// Ends a session: revokes the presented token and everything else in its
    /// family.
    ///
    /// <b>The family, not the token.</b> Signing out on one device should not
    /// leave the chain alive for whatever else holds a link in it; and since a
    /// family is one sign-in, ending it is exactly what "sign out" means. Other
    /// devices signed in separately have their own families and are untouched.
    ///
    /// Silent about whether the token was real. A logout that answered
    /// differently for an unknown token would be an oracle for guessing them.
    /// </summary>
    public async Task LogoutAsync(string presented, CancellationToken ct)
    {
        string hash = HashUtil.Sha256(presented);

        Guid? family = await _db.RefreshTokens
            .Where(t => t.TokenHash == hash)
            .Select(t => (Guid?)t.FamilyId)
            .FirstOrDefaultAsync(ct);

        if (family is not Guid familyId)
        {
            return;
        }

        await _db.RefreshTokens
            .Where(t => t.FamilyId == familyId && t.RevokedAt == null)
            .ExecuteUpdateAsync(set => set.SetProperty(t => t.RevokedAt, _clock.GetUtcNow()), ct);
    }

    /// <summary>
    /// Mints an access token and a refresh token for one user in one branch.
    ///
    /// Shared by sign-in, branch switching and refresh, so all three produce
    /// identical claims — a refreshed token that carried a different permission
    /// set from the one it replaced would be a privilege change nobody asked
    /// for, in either direction.
    ///
    /// <paramref name="familyId"/> is new for a sign-in and carried through for a
    /// refresh, which is what keeps a rotated chain recognisable as one session.
    /// </summary>
    private async Task<TokenResponse> IssueAsync(
        Guid userId,
        Guid orgId,
        Guid familyId,
        string? ip,
        string? userAgent,
        CancellationToken ct)
    {
        UserOrganizationRole? assignment = await _db.UserOrganizationRoles
            .FirstOrDefaultAsync(u => u.UserId == userId && u.OrgId == orgId && u.IsActive, ct);
        if (assignment is null)
        {
            throw new NoOrganizationAccessException();
        }

        OrgContextResponse ctx = await _orgs.ResolveAsync(orgId, ct)
            ?? throw new NoOrganizationAccessException();
        if (!ctx.DatabaseReady)
        {
            throw new DatabaseNotReadyException();
        }

        User user = await _db.Users.FirstAsync(u => u.UserId == userId, ct);
        user.LastAccessedOrgId = orgId;

        if (!user.IsActive)
        {
            // A deactivated account must not be able to refresh its way past the
            // deactivation. Sign-in already refuses one; this is the other door.
            throw new NoOrganizationAccessException();
        }

        List<string> permissions = await (
            from rp in _db.RolePermissions
            join p in _db.Permissions on rp.PermissionId equals p.PermissionId
            where rp.RoleId == assignment.RoleId
            select p.Code).ToListAsync(ct);

        string accessToken = _tokens.CreateAccessToken(new AccessTokenRequest
        {
            UserId = user.UserId,
            CustomerId = ctx.CustomerId,
            OrgId = orgId,
            RoleId = assignment.RoleId,
            DisplayName = user.DisplayName,
            Permissions = permissions,
            LicenseStatus = ctx.LicenseStatus,
            LicenseExpiry = ctx.LicenseExpiry,
            ExpiryIsBranchLevel = ctx.ExpiryIsBranchLevel,
            Vertical = ctx.Vertical,
        });

        (string refreshToken, string hash, DateTimeOffset expiresAt) = _tokens.CreateRefreshToken();
        _db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.UserId,
            OrgId = orgId,
            FamilyId = familyId,
            TokenHash = hash,
            ExpiresAt = expiresAt,
            IpAddress = ip,
            UserAgent = userAgent,
        });
        await _db.SaveChangesAsync(ct);

        return new TokenResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            AccessExpiresInSeconds = 15 * 60,
            LicenseStatus = ctx.LicenseStatus,
            LicenseExpiry = ctx.LicenseExpiry,
            ExpiryIsBranchLevel = ctx.ExpiryIsBranchLevel,
        };
    }

    // ---- Forgot password: OTP --------------------------------------------

    /// <summary>Always succeeds silently — never reveals whether the account exists.</summary>
    public async Task RequestPasswordResetAsync(ForgotPasswordRequest request, CancellationToken ct)
    {
        DateTimeOffset now = _clock.GetUtcNow();
        User? user = await _db.Users.FirstOrDefaultAsync(u => u.Email == request.Email, ct);
        if (user is null || !user.IsActive)
        {
            return;
        }

        // Invalidate any live code for this purpose before issuing a new one.
        List<OtpVerification> live = await _db.OtpVerifications
            .Where(o => o.UserId == user.UserId
                && o.Purpose == OtpPurpose.PasswordReset
                && o.ConsumedAt == null
                && o.ExpiresAt > now)
            .ToListAsync(ct);
        foreach (OtpVerification stale in live)
        {
            stale.ConsumedAt = now;
        }

        (string code, string hash) = _otp.Generate();
        string destination = request.Channel == OtpChannel.Sms
            ? MaskMobile(user.MobileNumber)
            : user.Email;

        _db.OtpVerifications.Add(new OtpVerification
        {
            UserId = user.UserId,
            Purpose = OtpPurpose.PasswordReset,
            Channel = request.Channel,
            Destination = destination,
            CodeHash = hash,
            ExpiresAt = now.Add(OtpLifetime),
            AttemptCount = 0,
        });
        await _db.SaveChangesAsync(ct);

        // SMS delivery is not yet wired — only email actually sends today.
        if (request.Channel == OtpChannel.Email)
        {
            await _email.SendAsync(new EmailMessage
            {
                ToEmail = user.Email,
                ToName = user.DisplayName,
                Subject = "Your password reset code",
                HtmlBody = $"<p>Your verification code is <strong>{code}</strong>. It expires in 10 minutes.</p>",
                TextBody = $"Your verification code is {code}. It expires in 10 minutes.",
            }, ct);
        }
    }

    public async Task<bool> VerifyOtpAsync(VerifyOtpRequest request, CancellationToken ct)
    {
        OtpVerification? otp = await ActiveOtpAsync(request.Email, ct);
        if (otp is null || otp.AttemptCount >= OtpMaxAttempts)
        {
            return false;
        }

        if (_otp.Verify(request.Code, otp.CodeHash))
        {
            return true;
        }

        otp.AttemptCount++;
        await _db.SaveChangesAsync(ct);
        return false;
    }

    public async Task<bool> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken ct)
    {
        DateTimeOffset now = _clock.GetUtcNow();
        User? user = await _db.Users.FirstOrDefaultAsync(u => u.Email == request.Email, ct);
        if (user is null)
        {
            return false;
        }

        OtpVerification? otp = await ActiveOtpAsync(request.Email, ct);
        if (otp is null || otp.AttemptCount >= OtpMaxAttempts || !_otp.Verify(request.Code, otp.CodeHash))
        {
            if (otp is not null)
            {
                otp.AttemptCount++;
                await _db.SaveChangesAsync(ct);
            }

            return false;
        }

        otp.ConsumedAt = now;
        user.PasswordHash = _passwordHasher.Hash(request.NewPassword);

        // Password reset revokes every live session.
        List<RefreshToken> tokens = await _db.RefreshTokens
            .Where(t => t.UserId == user.UserId && t.RevokedAt == null)
            .ToListAsync(ct);
        foreach (RefreshToken token in tokens)
        {
            token.RevokedAt = now;
        }

        await _db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>
    /// Completes an invitation. Uses the link token (PasswordResetTokens), not an
    /// OTP — invitations are link-based. Sets the first password and confirms the
    /// email in one step.
    /// </summary>
    public async Task<bool> AcceptInvitationAsync(AcceptInvitationRequest request, CancellationToken ct)
    {
        DateTimeOffset now = _clock.GetUtcNow();
        User? user = await _db.Users.FirstOrDefaultAsync(u => u.Email == request.Email, ct);
        if (user is null)
        {
            return false;
        }

        string hash = HashUtil.Sha256(request.Token);
        PasswordResetToken? token = await _db.PasswordResetTokens.FirstOrDefaultAsync(
            t => t.UserId == user.UserId && t.TokenHash == hash && t.UsedAt == null && t.ExpiresAt > now,
            ct);
        if (token is null)
        {
            return false;
        }

        token.UsedAt = now;
        user.PasswordHash = _passwordHasher.Hash(request.Password);
        user.EmailConfirmed = true;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    private async Task<OtpVerification?> ActiveOtpAsync(string email, CancellationToken ct)
    {
        DateTimeOffset now = _clock.GetUtcNow();
        return await (
            from o in _db.OtpVerifications
            join u in _db.Users on o.UserId equals u.UserId
            where u.Email == email
                && o.Purpose == OtpPurpose.PasswordReset
                && o.ConsumedAt == null
                && o.ExpiresAt > now
            orderby o.OtpVerificationId descending
            select o).FirstOrDefaultAsync(ct);
    }

    private LoginHistory Fail(Guid userId, string reason, string? ip, string? userAgent, DateTimeOffset now) =>
        new()
        {
            UserId = userId,
            LoginAt = now,
            IsSuccessful = false,
            FailureReason = reason,
            IpAddress = ip,
            UserAgent = userAgent,
        };

    private static string MaskMobile(string? mobile)
    {
        if (string.IsNullOrEmpty(mobile) || mobile.Length < 4)
        {
            return "****";
        }

        return string.Concat(new string('*', mobile.Length - 4), mobile.AsSpan(mobile.Length - 4));
    }
}
