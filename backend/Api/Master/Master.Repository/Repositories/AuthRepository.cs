using Master.Repository.Clients;
using Master.Entity.Enums;
using Master.Entity.Models;
using Master.Entity.TableEntities;
using Master.Repository;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Apps;
using Shared.Kernel.Interfaces;
using Shared.Kernel.Persistence;
using Shared.Kernel.Validation;

namespace Master.Repository.Repositories;

public sealed class AuthRepository
{
    private const int LockoutThreshold = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    private readonly AdminDbContext _db;
    private readonly IPasswordHasherRepository _passwordHasher;
    private readonly ITokenRepository _tokens;
    private readonly IOtpRepository _otp;
    private readonly OrgContextRepository _orgs;
    private readonly IEmailSender _email;
    private readonly TimeProvider _clock;

    /// <summary>Sends WhatsApp and SMS reset codes (TK-196).</summary>
    private readonly IMobileMessageClient _messages;

    public AuthRepository(
        AdminDbContext db,
        IPasswordHasherRepository passwordHasher,
        ITokenRepository tokens,
        IOtpRepository otp,
        OrgContextRepository orgs,
        IEmailSender email,
        TimeProvider clock,
        IMobileMessageClient messages)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _tokens = tokens;
        _otp = otp;
        _orgs = orgs;
        _email = email;
        _clock = clock;
        _messages = messages;
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
    /// disagree about what someone has access to. A branch where they hold
    /// several roles is listed once.
    /// </summary>
    public async Task<IReadOnlyList<AccessibleOrgDto>> AccessibleOrgsAsync(
        Guid userId, CancellationToken ct)
    {
        var assignments = (await (
            from uor in _db.UserOrganizationRoles
            join role in _db.Roles on uor.RoleId equals role.RoleId
            where uor.UserId == userId && uor.IsActive && role.IsActive
            orderby uor.OrgId, role.RoleId
            select new { uor.OrgId, RoleName = role.DisplayName }).ToListAsync(ct))
            .GroupBy(a => a.OrgId)
            .Select(g => g.First())
            .ToList();

        // One read for every branch, with what the switcher shows beside each:
        // its organisation, trade, currency, GSTIN and city (TK-189).
        List<Guid> orgIds = assignments.Select(a => a.OrgId).ToList();
        var branches = await (
            from o in _db.Organizations.AsNoTracking()
            join c in _db.Customers on o.CustomerId equals c.CustomerId
            where orgIds.Contains(o.OrgId)
            select new
            {
                o.OrgId,
                o.Name,
                CustomerName = c.Name,
                o.BusinessType,
                o.BaseCurrency,
                o.Gstin,
                o.City,
                o.AddressLine1,
                o.AddressLine2,
                o.PostalCode,
                o.StateId,
            }).ToDictionaryAsync(b => b.OrgId, ct);

        var orgs = new List<AccessibleOrgDto>();

        foreach (var assignment in assignments)
        {
            // A branch that cannot be resolved is still listed rather than
            // silently dropped — a missing branch is something to see, not to hide.
            branches.TryGetValue(assignment.OrgId, out var branch);

            orgs.Add(new AccessibleOrgDto
            {
                OrgId = assignment.OrgId,
                OrgName = branch?.Name ?? "(unavailable)",
                RoleName = assignment.RoleName,
                CustomerName = branch?.CustomerName,
                BusinessType = (branch?.BusinessType ?? BusinessType.Retail).ToString(),
                BaseCurrency = branch?.BaseCurrency,
                Gstin = branch?.Gstin,
                City = branch?.City,
                AddressLine1 = branch?.AddressLine1,
                AddressLine2 = branch?.AddressLine2,
                PostalCode = branch?.PostalCode,
                StateId = branch?.StateId,
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
        string hash = HashUtilRepository.Sha256(presented);

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
        string hash = HashUtilRepository.Sha256(presented);

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
        // The roles the user holds in this branch.
        List<int> roleIds = await (
            from u in _db.UserOrganizationRoles
            join r in _db.Roles on u.RoleId equals r.RoleId
            where u.UserId == userId && u.OrgId == orgId && u.IsActive && r.IsActive
            orderby r.RoleId
            select r.RoleId).ToListAsync(ct);
        if (roleIds.Count == 0)
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

        // platform.* never comes from a role, even one somebody managed to give
        // it to: Role rows are shared across customers (D-01).
        List<string> permissions = await (
            from rp in _db.RolePermissions
            join p in _db.Permissions on rp.PermissionId equals p.PermissionId
            where roleIds.Contains(rp.RoleId) && p.Module != PlatformOperatorRepository.PlatformModule
            select p.Code).Distinct().ToListAsync(ct);

        // It comes from the user instead, and only for an operator. Read here,
        // at every issue, so a revoked operator loses it at their next refresh.
        if (user.IsPlatformOperator)
        {
            permissions.AddRange(
                 await PlatformOperatorRepository.PlatformPermissionCodes(_db).ToListAsync(ct));
        }

        string accessToken = _tokens.CreateAccessToken(new AccessTokenRequest
        {
            UserId = user.UserId,
            CustomerId = ctx.CustomerId,
            CustomerCode = ctx.CustomerCode,
            DatabaseName = ctx.DatabaseName,
            OrgId = orgId,
            BusinessType = ctx.BusinessType,
            AddOns = ctx.AddOns,
            PlanTier = ctx.PlanTier,
            // The first of the roles: the claim names one role, for the
            // period-lock rule that keys on it.
            RoleId = roleIds[0],
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

    /// <summary>
    /// Always succeeds silently — never reveals whether the account exists.
    ///
    /// The code goes by the channel asked for (TK-196): WhatsApp or SMS to the
    /// mobile number on the account, or email. Every reason nothing is sent —
    /// no such account, no mobile number on it, a code sent under
    /// <see cref="OtpPolicy.ResendAfter"/> ago or <see cref="OtpPolicy.MaxPerWindow"/>
    /// this hour, a provider that refused — ends the same way, because the caller
    /// answers 200 whatever happens and any difference would tell a stranger
    /// which addresses have accounts. The page keeps its own resend countdown.
    /// </summary>
    public async Task RequestPasswordResetAsync(ForgotPasswordRequest request, CancellationToken ct)
    {
        DateTimeOffset now = _clock.GetUtcNow();
        User? user = await _db.Users.FirstOrDefaultAsync(u => u.Email == request.Email, ct);
        if (user is null || !user.IsActive)
        {
            return;
        }

        bool byPhone = request.Channel is OtpChannel.WhatsApp or OtpChannel.Sms;
        string? mobile = PhoneNumbers.NormalizeOptional(user.MobileNumber);
        if (byPhone && mobile is null)
        {
            return;
        }

        DateTimeOffset floor = OtpPolicy.WindowExpiryFloor(now);
        List<DateTimeOffset> expiries = await _db.OtpVerifications
            .Where(o => o.UserId == user.UserId
                && o.Purpose == OtpPurpose.PasswordReset
                && o.ExpiresAt > floor)
            .Select(o => o.ExpiresAt)
            .ToListAsync(ct);
        if (OtpPolicy.WaitBeforeNext(expiries.Select(OtpPolicy.SentAt), now) > TimeSpan.Zero)
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
        var otp = new OtpVerification
        {
            UserId = user.UserId,
            Purpose = OtpPurpose.PasswordReset,
            Channel = request.Channel,
            Destination = byPhone ? OtpPolicy.MaskMobile(mobile) : user.Email,
            CodeHash = hash,
            ExpiresAt = now.Add(OtpPolicy.Lifetime),
            AttemptCount = 0,
        };
        _db.OtpVerifications.Add(otp);
        await _db.SaveChangesAsync(ct);

        if (!byPhone)
        {
            await _email.SendAsync(new EmailMessage
            {
                ToEmail = user.Email,
                ToName = user.DisplayName,
                Subject = "Your password reset code",
                HtmlBody = $"<p>Your verification code is <strong>{code}</strong>. It expires in 10 minutes.</p>",
                TextBody = $"Your verification code is {code}. It expires in 10 minutes.",
            }, ct);
            return;
        }

        try
        {
            await _messages.SendOtpAsync(new MobileOtpMessage(
                request.Channel,
                MobileMessages.International(mobile!, await UserPhoneCodeAsync(user, ct)),
                code,
                OtpPurpose.PasswordReset,
                OtpPolicy.Lifetime), ct);
        }
        catch (OtpDeliveryException)
        {
            // The client has logged why. A code nobody received should not hold
            // the resend wait, and the answer stays the same 200.
            _db.OtpVerifications.Remove(otp);
            await _db.SaveChangesAsync(ct);
        }
    }

    /// <summary>
    /// The country code for a local mobile number: that of the branch the user
    /// last worked in, else of any branch they belong to. Null falls back to
    /// India's in <see cref="MobileMessages.International"/>.
    /// </summary>
    private async Task<string?> UserPhoneCodeAsync(User user, CancellationToken ct) =>
        await (
            from a in _db.UserOrganizationRoles
            join o in _db.Organizations on a.OrgId equals o.OrgId
            join c in _db.Countries on o.CountryId equals c.CountryId
            where a.UserId == user.UserId && c.PhoneCode != null
            orderby o.OrgId == user.LastAccessedOrgId descending
            select c.PhoneCode).FirstOrDefaultAsync(ct);

    public async Task<bool> VerifyOtpAsync(VerifyOtpRequest request, CancellationToken ct)
    {
        OtpVerification? otp = await ActiveOtpAsync(request.Email, ct);
        if (otp is null || otp.AttemptCount >= OtpPolicy.MaxAttempts)
        {
            return false;
        }

        if (_otp.Verify(request.Code, otp.CodeHash))
        {
            return true;
        }

        // Saved on its own: the route runs with no request transaction, so the
        // count survives the 400 it answers with (TK-196). Before, it was rolled
        // back with the refusal and the five-try limit never engaged.
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
        if (otp is null || otp.AttemptCount >= OtpPolicy.MaxAttempts || !_otp.Verify(request.Code, otp.CodeHash))
        {
            if (otp is not null)
            {
                // Its own save, kept whatever the answer (see VerifyOtpAsync).
                otp.AttemptCount++;
                await _db.SaveChangesAsync(ct);
            }

            return false;
        }

        // The route has no request transaction, so the writes that must land
        // together get one of their own.
        await using ITransactionScope scope = await _db.Database.BeginScopeAsync(ct);

        otp.ConsumedAt = now;
        user.PasswordHash = _passwordHasher.Hash(request.NewPassword);

        // A WhatsApp or SMS code that worked proves the number on the account.
        if (otp.Channel is OtpChannel.WhatsApp or OtpChannel.Sms)
        {
            user.MobileConfirmed = true;
        }

        // Password reset revokes every live session.
        List<RefreshToken> tokens = await _db.RefreshTokens
            .Where(t => t.UserId == user.UserId && t.RevokedAt == null)
            .ToListAsync(ct);
        foreach (RefreshToken token in tokens)
        {
            token.RevokedAt = now;
        }

        await _db.SaveChangesAsync(ct);
        await scope.CommitAsync(ct);
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

        string hash = HashUtilRepository.Sha256(request.Token);
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
}
