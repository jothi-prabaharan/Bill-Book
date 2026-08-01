using Identity.Entity.Enums;
using Identity.Entity.Models;
using Identity.Entity.TableEntities;
using Identity.Repository;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Interfaces;

namespace Identity.Api.Services;

public sealed class AuthService
{
    private const int LockoutThreshold = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan OtpLifetime = TimeSpan.FromMinutes(10);
    private const int OtpMaxAttempts = 5;

    private readonly IdentityDbContext _db;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenService _tokens;
    private readonly IOtpService _otp;
    private readonly IPlatformDirectory _platform;
    private readonly IEmailSender _email;
    private readonly TimeProvider _clock;

    public AuthService(
        IdentityDbContext db,
        IPasswordHasher passwordHasher,
        ITokenService tokens,
        IOtpService otp,
        IPlatformDirectory platform,
        IEmailSender email,
        TimeProvider clock)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _tokens = tokens;
        _otp = otp;
        _platform = platform;
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

        return new LoginResponse
        {
            PreAuthToken = _tokens.CreatePreAuthToken(user.UserId, user.Email),
            ExpiresInSeconds = 5 * 60,
            Organizations = orgs,
            RequiresOrgSelection = orgs.Count > 1,
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
            OrgContext? ctx = await _platform.ResolveOrgAsync(assignment.OrgId, ct);

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
        UserOrganizationRole? assignment = await _db.UserOrganizationRoles
            .FirstOrDefaultAsync(u => u.UserId == userId && u.OrgId == orgId && u.IsActive, ct);
        if (assignment is null)
        {
            throw new NoOrganizationAccessException();
        }

        OrgContext ctx = await _platform.ResolveOrgAsync(orgId, ct)
            ?? throw new NoOrganizationAccessException();
        if (!ctx.DatabaseReady)
        {
            throw new DatabaseNotReadyException();
        }

        User user = await _db.Users.FirstAsync(u => u.UserId == userId, ct);

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
            DisplayName = user.DisplayName,
            Permissions = permissions,
            LicenseStatus = ctx.LicenseStatus,
            LicenseExpiry = ctx.LicenseExpiry,
        });

        (string refreshToken, string hash, DateTimeOffset expiresAt) = _tokens.CreateRefreshToken();
        _db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.UserId,
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
