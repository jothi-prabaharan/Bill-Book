using Master.Api.Services;
using Master.Repository.Repositories;
using Master.Repository.Clients;
using Master.Entity.Enums;
using Master.Entity.Models;
using Master.Entity.TableEntities;
using Master.Repository;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Interfaces;
using Xunit;

namespace Master.Api.Tests;

/// <summary>
/// Forgot password by WhatsApp, SMS or email (TK-196), against a real database.
///
/// Every request answers the same 200 whether or not anything was sent, so
/// these tests read what the service did — the message recorded, the code row
/// written — rather than anything it said.
/// </summary>
[Collection(nameof(AdminCollection))]
public sealed class PasswordResetOtpTests
{
    private readonly AdminFixture _admin;

    public PasswordResetOtpTests(AdminFixture admin) => _admin = admin;

    private sealed class SettableClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 10, 20, 10, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class RecordingEmail : IEmailSender
    {
        public List<EmailMessage> Sent { get; } = [];

        public Task SendAsync(EmailMessage message, CancellationToken ct = default)
        {
            Sent.Add(message);
            return Task.CompletedTask;
        }
    }

    private sealed class Harness
    {
        public SettableClock Clock { get; } = new();

        public RecordingEmail Email { get; } = new();

        public RecordingMobileMessages Messages { get; set; } = new();
    }

    /// <summary>A service for one request, on a context of its own.</summary>
    private (AuthRepository Auth, AdminDbContext Db) Request(Harness harness)
    {
        AdminDbContext db = _admin.CreateContext();
        var options = Microsoft.Extensions.Options.Options.Create(new JwtOptionsRepository
        {
            Issuer = "bill-book",
            Audience = "bill-book",
            SigningKey = "a-test-signing-key-that-is-long-enough-for-hmac-sha256",
            RefreshTokenDays = 7,
        });

        return (new AuthRepository(
            db,
            new BcryptPasswordHasher(),
            new JwtTokenRepository(options, harness.Clock),
            new OtpService(),
            new OrgContextRepository(db, harness.Clock),
            harness.Email,
            harness.Clock,
            harness.Messages), db);
    }

    private async Task<User> SeedUserAsync(string? mobile)
    {
        await using AdminDbContext db = _admin.CreateContext();
        var user = new User
        {
            UserId = Guid.NewGuid(),
            Email = $"reset-{Guid.NewGuid():N}@example.com",
            DisplayName = "Suresh M",
            PasswordHash = new BcryptPasswordHasher().Hash("the-old-password"),
            MobileNumber = mobile,
            IsActive = true,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    private async Task ForgotAsync(Harness harness, string email, OtpChannel? channel = null)
    {
        (AuthRepository auth, AdminDbContext db) = Request(harness);
        await using (db)
        {
            var request = new ForgotPasswordRequest { Email = email };
            if (channel is OtpChannel chosen)
            {
                request.Channel = chosen;
            }

            await auth.RequestPasswordResetAsync(request, default);
        }
    }

    private async Task<bool> ResetAsync(Harness harness, string email, string code)
    {
        (AuthRepository auth, AdminDbContext db) = Request(harness);
        await using (db)
        {
            return await auth.ResetPasswordAsync(new ResetPasswordRequest
            {
                Email = email,
                Code = code,
                NewPassword = "the-new-password",
                ConfirmPassword = "the-new-password",
            }, default);
        }
    }

    private async Task<bool> VerifyAsync(Harness harness, string email, string code)
    {
        (AuthRepository auth, AdminDbContext db) = Request(harness);
        await using (db)
        {
            return await auth.VerifyOtpAsync(new VerifyOtpRequest { Email = email, Code = code }, default);
        }
    }

    private static string WrongCode(string code) => ((int.Parse(code) + 1) % 1_000_000).ToString("D6");

    [SkippableFact]
    public async Task WhatsApp_is_the_default_and_goes_to_the_mobile_number_on_the_account()
    {
        Skip.If(_admin.SkipReason is not null, _admin.SkipReason ?? string.Empty);

        var harness = new Harness();
        User user = await SeedUserAsync("9843055120");

        await ForgotAsync(harness, user.Email);

        MobileOtpMessage message = Assert.Single(harness.Messages.Sent);
        Assert.Equal(OtpChannel.WhatsApp, message.Channel);
        Assert.Equal(OtpPurpose.PasswordReset, message.Purpose);
        Assert.Equal("+919843055120", message.MobileNumber);
        Assert.Empty(harness.Email.Sent);

        await using AdminDbContext db = _admin.CreateContext();
        OtpVerification otp = await db.OtpVerifications.AsNoTracking().SingleAsync(o => o.UserId == user.UserId);
        Assert.Equal(OtpChannel.WhatsApp, otp.Channel);
        Assert.Equal("******5120", otp.Destination);
    }

    [SkippableFact]
    public async Task Sms_and_email_go_by_the_channel_asked_for()
    {
        Skip.If(_admin.SkipReason is not null, _admin.SkipReason ?? string.Empty);

        var harness = new Harness();
        User bySms = await SeedUserAsync("9843055121");
        User byEmail = await SeedUserAsync("9843055122");

        await ForgotAsync(harness, bySms.Email, OtpChannel.Sms);
        await ForgotAsync(harness, byEmail.Email, OtpChannel.Email);

        Assert.Equal(OtpChannel.Sms, Assert.Single(harness.Messages.Sent).Channel);
        Assert.Equal(byEmail.Email, Assert.Single(harness.Email.Sent).ToEmail);
    }

    [SkippableFact]
    public async Task An_account_with_no_mobile_number_is_sent_nothing_by_phone()
    {
        Skip.If(_admin.SkipReason is not null, _admin.SkipReason ?? string.Empty);

        var harness = new Harness();
        User user = await SeedUserAsync(null);

        await ForgotAsync(harness, user.Email, OtpChannel.WhatsApp);

        Assert.Empty(harness.Messages.Sent);
        Assert.Empty(harness.Email.Sent);

        await using AdminDbContext db = _admin.CreateContext();
        Assert.False(await db.OtpVerifications.AnyAsync(o => o.UserId == user.UserId));
    }

    [SkippableFact]
    public async Task An_unknown_email_is_sent_nothing()
    {
        Skip.If(_admin.SkipReason is not null, _admin.SkipReason ?? string.Empty);

        var harness = new Harness();

        await ForgotAsync(harness, $"nobody-{Guid.NewGuid():N}@example.com");

        Assert.Empty(harness.Messages.Sent);
        Assert.Empty(harness.Email.Sent);
    }

    [SkippableFact]
    public async Task A_second_request_inside_the_minute_sends_nothing()
    {
        Skip.If(_admin.SkipReason is not null, _admin.SkipReason ?? string.Empty);

        var harness = new Harness();
        User user = await SeedUserAsync("9843055123");

        await ForgotAsync(harness, user.Email);
        harness.Clock.Now = harness.Clock.Now.AddSeconds(30);
        await ForgotAsync(harness, user.Email, OtpChannel.Email);

        Assert.Single(harness.Messages.Sent);
        Assert.Empty(harness.Email.Sent);

        harness.Clock.Now = harness.Clock.Now.AddSeconds(31);
        await ForgotAsync(harness, user.Email, OtpChannel.Email);
        Assert.Single(harness.Email.Sent);
    }

    [SkippableFact]
    public async Task A_code_that_cannot_be_sent_leaves_no_live_code()
    {
        Skip.If(_admin.SkipReason is not null, _admin.SkipReason ?? string.Empty);

        var harness = new Harness { Messages = RecordingMobileMessages.Unavailable() };
        User user = await SeedUserAsync("9843055124");

        await ForgotAsync(harness, user.Email);

        await using AdminDbContext db = _admin.CreateContext();
        Assert.False(await db.OtpVerifications.AnyAsync(o => o.UserId == user.UserId && o.ConsumedAt == null));
    }

    /// <summary>
    /// The five-try limit holds. Before TK-196 each wrong try was saved and
    /// then rolled back with the 400, so the limit was never reached; the
    /// routes now run with no request transaction and the count is its own
    /// commit, which a fresh context reads back here.
    /// </summary>
    [SkippableFact]
    public async Task Wrong_tries_are_kept_and_five_lock_the_code()
    {
        Skip.If(_admin.SkipReason is not null, _admin.SkipReason ?? string.Empty);

        var harness = new Harness();
        User user = await SeedUserAsync("9843055125");

        await ForgotAsync(harness, user.Email);
        string code = harness.Messages.Sent[0].Code;

        Assert.False(await VerifyAsync(harness, user.Email, WrongCode(code)));

        await using (AdminDbContext db = _admin.CreateContext())
        {
            Assert.Equal(1, await db.OtpVerifications
                .Where(o => o.UserId == user.UserId).Select(o => o.AttemptCount).SingleAsync());
        }

        for (int i = 1; i < OtpPolicy.MaxAttempts; i++)
        {
            Assert.False(await ResetAsync(harness, user.Email, WrongCode(code)));
        }

        Assert.False(await VerifyAsync(harness, user.Email, code));
        Assert.False(await ResetAsync(harness, user.Email, code));
    }

    [SkippableFact]
    public async Task A_reset_by_whatsapp_sets_the_password_and_confirms_the_mobile()
    {
        Skip.If(_admin.SkipReason is not null, _admin.SkipReason ?? string.Empty);

        var harness = new Harness();
        User user = await SeedUserAsync("9843055126");

        await ForgotAsync(harness, user.Email);
        string code = harness.Messages.Sent[0].Code;

        Assert.True(await VerifyAsync(harness, user.Email, code));
        Assert.True(await ResetAsync(harness, user.Email, code));

        await using AdminDbContext db = _admin.CreateContext();
        User saved = await db.Users.AsNoTracking().SingleAsync(u => u.UserId == user.UserId);
        Assert.True(new BcryptPasswordHasher().Verify("the-new-password", saved.PasswordHash!));
        Assert.True(saved.MobileConfirmed);

        // Spent: the same code does not reset twice.
        Assert.False(await ResetAsync(harness, user.Email, code));
    }

    [SkippableFact]
    public async Task A_reset_by_email_leaves_the_mobile_unconfirmed()
    {
        Skip.If(_admin.SkipReason is not null, _admin.SkipReason ?? string.Empty);

        var harness = new Harness();
        User user = await SeedUserAsync("9843055127");

        await ForgotAsync(harness, user.Email, OtpChannel.Email);
        string code = System.Text.RegularExpressions.Regex.Match(harness.Email.Sent[0].TextBody!, @"\d{6}").Value;

        Assert.True(await ResetAsync(harness, user.Email, code));

        await using AdminDbContext db = _admin.CreateContext();
        Assert.False(await db.Users.Where(u => u.UserId == user.UserId).Select(u => u.MobileConfirmed).SingleAsync());
    }
}
