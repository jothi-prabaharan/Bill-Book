using Master.Api.Services;
using Master.Entity.Enums;
using Master.Entity.Models;
using Master.Entity.TableEntities;
using Master.Repository;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Interfaces;
using Xunit;

namespace Master.Api.Tests;

/// <summary>
/// Refresh-token rotation.
///
/// <b>What was there before was storage, not rotation.</b> Tokens were minted at
/// sign-in and hashed into <c>mst.RefreshTokens</c>, and no endpoint anywhere
/// read a presented one — the frontend held a refresh token it could never
/// spend. So a fifteen-minute access token was really a fifteen-minute session,
/// and the seven-day credential in browser storage did nothing but sit there.
///
/// These tests are against a real PostgreSQL because two of the guarantees are
/// the database's: the guarded <c>ExecuteUpdate</c> that decides which of two
/// concurrent refreshes wins, and the unique index over the token hash.
/// </summary>
[Collection(nameof(AdminCollection))]
public sealed class RefreshTokenRotationTests
{
    private readonly AdminFixture _admin;

    public RefreshTokenRotationTests(AdminFixture admin) => _admin = admin;

    /// <summary>Time we control, so an expiry is a fact rather than a wait.</summary>
    private sealed class FixedClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 9, 4, 10, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class NoEmail : IEmailSender
    {
        public Task SendAsync(EmailMessage message, CancellationToken ct = default) =>
            Task.CompletedTask;
    }

    /// <summary>
    /// A service on a context of its own.
    ///
    /// <b>Each call is a request, and a request gets a fresh <c>DbContext</c>.</b>
    /// Reusing one across a sign-in and the refresh that follows would leave the
    /// user and the token in the change tracker, so a change written by
    /// <c>ExecuteUpdate</c> — which goes straight to the database — would be
    /// invisible to the second call. That is a property of the test, not of the
    /// code: in production the two are separate scopes, and a test that shared
    /// one would be asserting against a state no request ever sees.
    /// </summary>
    private (AuthService Auth, AdminDbContext Db) Request(FixedClock clock)
    {
        AdminDbContext db = _admin.CreateContext();

        return (Service(db, clock), db);
    }

    private AuthService Service(AdminDbContext db, FixedClock clock)
    {
        var options = Microsoft.Extensions.Options.Options.Create(new JwtOptions
        {
            Issuer = "bill-book",
            Audience = "bill-book",
            // Long enough for HS256; the tests never validate the signature, but
            // the signer refuses a short key and that refusal is not the subject.
            SigningKey = "a-test-signing-key-that-is-long-enough-for-hmac-sha256",
            RefreshTokenDays = 7,
        });

        var tokens = new JwtTokenService(options, clock);

        return new AuthService(
            db,
            new BcryptPasswordHasher(),
            tokens,
            new OtpService(),
            new OrgContextService(db, clock),
            new NoEmail(),
            clock);
    }

    /// <summary>
    /// A signed-in user in a live branch: customer, organization, licence, user,
    /// role assignment. Everything <c>OrgContextService</c> joins.
    /// </summary>
    private async Task<(Guid UserId, Guid OrgId)> SeedSessionAsync(
        AdminDbContext db, FixedClock clock, bool userActive = true)
    {
        var customerId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        string suffix = Guid.NewGuid().ToString("N")[..8];

        db.Customers.Add(new Master.Entity.TableEntities.Customer
        {
            CustomerId = customerId,
            // Numeric, like every code the product writes. A non-numeric one used to
            // break every later signup, which SignupTests now covers directly.
            CustomerCode = Random.Shared.NextInt64(1_000_000_000, 9_999_999_999).ToString(),
            CountryPrefix = "IN",
            Name = "Test Customer",
            BillingEmail = $"billing-{suffix}@example.com",
            Status = TenantStatus.Active,
            PlanTier = "Standard",
        });

        db.Organizations.Add(new Organization
        {
            OrgId = orgId,
            CustomerId = customerId,
            OrgCode = $"B{suffix}",
            Name = "Head Office",
            BaseCurrency = "INR",
            FinancialYearStartMonth = 4,
            Status = TenantStatus.Active,
        });

        db.Licenses.Add(new License
        {
            CustomerId = customerId,
            LicenseType = LicenseType.Standard,
            StartDate = DateOnly.FromDateTime(clock.Now.UtcDateTime).AddDays(-1),
            ExpiryDate = DateOnly.FromDateTime(clock.Now.UtcDateTime).AddYears(1),
            MaxUsers = 10,
            MaxOrganizations = 5,
            IsActive = true,
        });

        db.Users.Add(new User
        {
            UserId = userId,
            Email = $"user-{suffix}@example.com",
            DisplayName = "Test User",
            PasswordHash = "x",
            IsActive = userActive,
            EmailConfirmed = true,
        });

        await db.SaveChangesAsync();

        Role role = await db.Roles.FirstAsync(r => r.IsSystemRole);

        db.UserOrganizationRoles.Add(new UserOrganizationRole
        {
            UserId = userId,
            OrgId = orgId,
            RoleId = role.RoleId,
            IsActive = true,
        });

        await db.SaveChangesAsync();

        return (userId, orgId);
    }

    // ---- The happy path ---------------------------------------------------

    [SkippableFact]
    public async Task A_valid_refresh_token_returns_a_new_pair_and_spends_the_old_one()
    {
        Skip.If(_admin.SkipReason is not null, _admin.SkipReason ?? string.Empty);

        var clock = new FixedClock();
        await using AdminDbContext db = _admin.CreateContext();
        (Guid userId, Guid orgId) = await SeedSessionAsync(db, clock);

        AuthService auth = Service(db, clock);
        TokenResponse first = await auth.SelectOrganizationAsync(userId, orgId, null, null, default);

        TokenResponse second = await auth.RefreshAsync(first.RefreshToken, null, null, default);

        Assert.NotEqual(first.RefreshToken, second.RefreshToken);
        Assert.False(string.IsNullOrWhiteSpace(second.AccessToken));

        // The presented token is spent, not merely still valid alongside its
        // successor. That is the difference between rotation and renewal.
        string firstHash = HashUtil.Sha256(first.RefreshToken);
        RefreshToken spent = await db.RefreshTokens.AsNoTracking()
            .FirstAsync(t => t.TokenHash == firstHash);

        Assert.NotNull(spent.RevokedAt);
    }

    [SkippableFact]
    public async Task The_rotated_token_stays_in_the_same_family_and_the_same_branch()
    {
        Skip.If(_admin.SkipReason is not null, _admin.SkipReason ?? string.Empty);

        var clock = new FixedClock();
        await using AdminDbContext db = _admin.CreateContext();
        (Guid userId, Guid orgId) = await SeedSessionAsync(db, clock);

        AuthService auth = Service(db, clock);
        TokenResponse first = await auth.SelectOrganizationAsync(userId, orgId, null, null, default);
        TokenResponse second = await auth.RefreshAsync(first.RefreshToken, null, null, default);

        RefreshToken before = await Token(db, first.RefreshToken);
        RefreshToken after = await Token(db, second.RefreshToken);

        Assert.Equal(before.FamilyId, after.FamilyId);

        // A refresh must land the user back in the branch they were in. Without
        // OrgId on the row there would be nothing to land them in.
        Assert.Equal(orgId, after.OrgId);
    }

    [SkippableFact]
    public async Task Only_the_hash_is_stored_never_the_token()
    {
        Skip.If(_admin.SkipReason is not null, _admin.SkipReason ?? string.Empty);

        var clock = new FixedClock();
        await using AdminDbContext db = _admin.CreateContext();
        (Guid userId, Guid orgId) = await SeedSessionAsync(db, clock);

        TokenResponse issued = await Service(db, clock)
            .SelectOrganizationAsync(userId, orgId, null, null, default);

        Assert.False(await db.RefreshTokens.AnyAsync(t => t.TokenHash == issued.RefreshToken));
        Assert.True(await db.RefreshTokens.AnyAsync(
            t => t.TokenHash == HashUtil.Sha256(issued.RefreshToken)));
    }

    // ---- Refusals ---------------------------------------------------------

    [SkippableFact]
    public async Task An_unknown_token_is_refused()
    {
        Skip.If(_admin.SkipReason is not null, _admin.SkipReason ?? string.Empty);

        var clock = new FixedClock();
        await using AdminDbContext db = _admin.CreateContext();

        await Assert.ThrowsAsync<InvalidRefreshTokenException>(
            () => Service(db, clock).RefreshAsync("not-a-token", null, null, default));
    }

    [SkippableFact]
    public async Task An_expired_token_is_refused()
    {
        Skip.If(_admin.SkipReason is not null, _admin.SkipReason ?? string.Empty);

        var clock = new FixedClock();
        await using AdminDbContext db = _admin.CreateContext();
        (Guid userId, Guid orgId) = await SeedSessionAsync(db, clock);

        AuthService auth = Service(db, clock);
        TokenResponse issued = await auth.SelectOrganizationAsync(userId, orgId, null, null, default);

        // Seven days is the refresh lifetime; a day past it is past it.
        clock.Now = clock.Now.AddDays(8);

        await Assert.ThrowsAsync<InvalidRefreshTokenException>(
            () => auth.RefreshAsync(issued.RefreshToken, null, null, default));
    }

    [SkippableFact]
    public async Task A_token_revoked_by_a_password_reset_is_refused()
    {
        Skip.If(_admin.SkipReason is not null, _admin.SkipReason ?? string.Empty);

        var clock = new FixedClock();
        await using AdminDbContext db = _admin.CreateContext();
        (Guid userId, Guid orgId) = await SeedSessionAsync(db, clock);

        TokenResponse issued = await Service(db, clock)
            .SelectOrganizationAsync(userId, orgId, null, null, default);

        // What ResetPasswordAsync does to every live token for the user.
        await db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null)
            .ExecuteUpdateAsync(set => set.SetProperty(t => t.RevokedAt, clock.Now));

        (AuthService auth, AdminDbContext next) = Request(clock);
        await using AdminDbContext _ = next;

        // A revoked token presented again is reuse, and reuse is refused.
        await Assert.ThrowsAsync<RefreshTokenReuseException>(
            () => auth.RefreshAsync(issued.RefreshToken, null, null, default));
    }

    [SkippableFact]
    public async Task A_deactivated_user_cannot_refresh_their_way_past_the_deactivation()
    {
        Skip.If(_admin.SkipReason is not null, _admin.SkipReason ?? string.Empty);

        var clock = new FixedClock();
        await using AdminDbContext db = _admin.CreateContext();
        (Guid userId, Guid orgId) = await SeedSessionAsync(db, clock);

        TokenResponse issued = await Service(db, clock)
            .SelectOrganizationAsync(userId, orgId, null, null, default);

        await db.Users
            .Where(u => u.UserId == userId)
            .ExecuteUpdateAsync(set => set.SetProperty(u => u.IsActive, false));

        (AuthService auth, AdminDbContext next) = Request(clock);
        await using AdminDbContext _ = next;

        await Assert.ThrowsAsync<InvalidRefreshTokenException>(
            () => auth.RefreshAsync(issued.RefreshToken, null, null, default));
    }

    [SkippableFact]
    public async Task Losing_access_to_the_branch_ends_the_session_rather_than_refreshing_it()
    {
        Skip.If(_admin.SkipReason is not null, _admin.SkipReason ?? string.Empty);

        var clock = new FixedClock();
        await using AdminDbContext db = _admin.CreateContext();
        (Guid userId, Guid orgId) = await SeedSessionAsync(db, clock);

        TokenResponse issued = await Service(db, clock)
            .SelectOrganizationAsync(userId, orgId, null, null, default);

        await db.UserOrganizationRoles
            .Where(u => u.UserId == userId && u.OrgId == orgId)
            .ExecuteUpdateAsync(set => set.SetProperty(u => u.IsActive, false));

        (AuthService auth, AdminDbContext next) = Request(clock);
        await using AdminDbContext _ = next;

        await Assert.ThrowsAsync<InvalidRefreshTokenException>(
            () => auth.RefreshAsync(issued.RefreshToken, null, null, default));
    }

    // ---- Reuse detection --------------------------------------------------

    [SkippableFact]
    public async Task Presenting_a_spent_token_again_is_reuse_and_ends_the_whole_family()
    {
        Skip.If(_admin.SkipReason is not null, _admin.SkipReason ?? string.Empty);

        var clock = new FixedClock();
        await using AdminDbContext db = _admin.CreateContext();
        (Guid userId, Guid orgId) = await SeedSessionAsync(db, clock);

        TokenResponse first = await Service(db, clock)
            .SelectOrganizationAsync(userId, orgId, null, null, default);

        (AuthService rotate, AdminDbContext rotateDb) = Request(clock);
        await using AdminDbContext _ = rotateDb;
        TokenResponse second = await rotate.RefreshAsync(first.RefreshToken, null, null, default);

        // The stolen copy, replayed.
        (AuthService replay, AdminDbContext replayDb) = Request(clock);
        await using AdminDbContext __ = replayDb;
        await Assert.ThrowsAsync<RefreshTokenReuseException>(
            () => replay.RefreshAsync(first.RefreshToken, null, null, default));

        // The successor goes too. Revoking only the replayed token would leave
        // whoever holds this one still signed in — the wrong half to keep.
        await using AdminDbContext check = _admin.CreateContext();
        Assert.NotNull((await Token(check, second.RefreshToken)).RevokedAt);

        (AuthService after, AdminDbContext afterDb) = Request(clock);
        await using AdminDbContext ___ = afterDb;
        await Assert.ThrowsAsync<RefreshTokenReuseException>(
            () => after.RefreshAsync(second.RefreshToken, null, null, default));
    }

    [SkippableFact]
    public async Task Reuse_is_recorded_as_a_security_event_without_the_token_in_it()
    {
        Skip.If(_admin.SkipReason is not null, _admin.SkipReason ?? string.Empty);

        var clock = new FixedClock();
        await using AdminDbContext db = _admin.CreateContext();
        (Guid userId, Guid orgId) = await SeedSessionAsync(db, clock);

        AuthService auth = Service(db, clock);
        TokenResponse first = await auth.SelectOrganizationAsync(userId, orgId, null, null, default);
        await auth.RefreshAsync(first.RefreshToken, null, null, default);

        await Assert.ThrowsAsync<RefreshTokenReuseException>(
            () => auth.RefreshAsync(first.RefreshToken, null, null, default));

        LoginHistory recorded = await db.LoginHistories.AsNoTracking()
            .Where(h => h.UserId == userId && !h.IsSuccessful)
            .OrderByDescending(h => h.LoginHistoryId)
            .FirstAsync();

        Assert.Contains("reuse", recorded.FailureReason ?? string.Empty, StringComparison.OrdinalIgnoreCase);

        // The event, never the secret.
        Assert.DoesNotContain(
            first.RefreshToken, recorded.FailureReason ?? string.Empty, StringComparison.Ordinal);
    }

    [SkippableFact]
    public async Task Reuse_in_one_session_leaves_another_sign_in_alone()
    {
        Skip.If(_admin.SkipReason is not null, _admin.SkipReason ?? string.Empty);

        var clock = new FixedClock();
        await using AdminDbContext db = _admin.CreateContext();
        (Guid userId, Guid orgId) = await SeedSessionAsync(db, clock);

        AuthService auth = Service(db, clock);

        // Two separate sign-ins: two families, as a phone and a laptop would be.
        TokenResponse phone = await auth.SelectOrganizationAsync(userId, orgId, null, null, default);
        TokenResponse laptop = await auth.SelectOrganizationAsync(userId, orgId, null, null, default);

        await auth.RefreshAsync(phone.RefreshToken, null, null, default);
        await Assert.ThrowsAsync<RefreshTokenReuseException>(
            () => auth.RefreshAsync(phone.RefreshToken, null, null, default));

        // The laptop was never part of that chain and must still work.
        TokenResponse rotated = await auth.RefreshAsync(laptop.RefreshToken, null, null, default);

        Assert.NotEqual(laptop.RefreshToken, rotated.RefreshToken);
    }

    // ---- Concurrency ------------------------------------------------------

    [SkippableFact]
    public async Task Two_simultaneous_refreshes_of_one_token_yield_exactly_one_winner()
    {
        Skip.If(_admin.SkipReason is not null, _admin.SkipReason ?? string.Empty);

        var clock = new FixedClock();
        await using AdminDbContext seed = _admin.CreateContext();
        (Guid userId, Guid orgId) = await SeedSessionAsync(seed, clock);

        TokenResponse issued = await Service(seed, clock)
            .SelectOrganizationAsync(userId, orgId, null, null, default);

        // Separate contexts, as two requests would have. One shared context
        // would serialise the calls and prove nothing about the race.
        await using AdminDbContext one = _admin.CreateContext();
        await using AdminDbContext two = _admin.CreateContext();

        Task<TokenResponse>[] both =
        [
            Service(one, clock).RefreshAsync(issued.RefreshToken, null, null, default),
            Service(two, clock).RefreshAsync(issued.RefreshToken, null, null, default),
        ];

        Exception?[] outcomes = await Task.WhenAll(both.Select(async task =>
        {
            try
            {
                await task;
                return (Exception?)null;
            }
            catch (Exception ex)
            {
                return ex;
            }
        }));

        // Exactly one succeeded. Two live chains from one token is precisely
        // what rotation exists to prevent, and a read-then-write would allow it.
        Assert.Equal(1, outcomes.Count(o => o is null));
        Assert.All(
            outcomes.Where(o => o is not null),
            ex => Assert.True(ex is InvalidRefreshTokenException or RefreshTokenReuseException));
    }

    // ---- Logout -----------------------------------------------------------

    [SkippableFact]
    public async Task Logout_revokes_the_presented_token_and_its_family()
    {
        Skip.If(_admin.SkipReason is not null, _admin.SkipReason ?? string.Empty);

        var clock = new FixedClock();
        await using AdminDbContext db = _admin.CreateContext();
        (Guid userId, Guid orgId) = await SeedSessionAsync(db, clock);

        TokenResponse issued = await Service(db, clock)
            .SelectOrganizationAsync(userId, orgId, null, null, default);

        (AuthService rotate, AdminDbContext rotateDb) = Request(clock);
        await using AdminDbContext _ = rotateDb;
        TokenResponse rotated = await rotate.RefreshAsync(issued.RefreshToken, null, null, default);

        (AuthService signOut, AdminDbContext signOutDb) = Request(clock);
        await using AdminDbContext __ = signOutDb;
        await signOut.LogoutAsync(rotated.RefreshToken, default);

        await using AdminDbContext check = _admin.CreateContext();
        Assert.NotNull((await Token(check, rotated.RefreshToken)).RevokedAt);

        (AuthService after, AdminDbContext afterDb) = Request(clock);
        await using AdminDbContext ___ = afterDb;
        await Assert.ThrowsAsync<RefreshTokenReuseException>(
            () => after.RefreshAsync(rotated.RefreshToken, null, null, default));
    }

    [SkippableFact]
    public async Task Logout_of_an_unknown_token_is_silent()
    {
        Skip.If(_admin.SkipReason is not null, _admin.SkipReason ?? string.Empty);

        var clock = new FixedClock();
        await using AdminDbContext db = _admin.CreateContext();

        // No throw. A logout that answered differently for an unknown token
        // would be an oracle for guessing live ones.
        await Service(db, clock).LogoutAsync("not-a-token", default);
    }

    private static async Task<RefreshToken> Token(AdminDbContext db, string plaintext)
    {
        string hash = HashUtil.Sha256(plaintext);

        return await db.RefreshTokens.AsNoTracking().FirstAsync(t => t.TokenHash == hash);
    }
}
