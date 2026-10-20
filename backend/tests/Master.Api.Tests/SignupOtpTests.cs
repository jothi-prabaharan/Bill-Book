using Master.Repository.Repositories;
using Master.Repository.Clients;
using Master.Entity.Enums;
using Master.Entity.Models;
using Master.Entity.TableEntities;
using Master.Repository;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Shared.Kernel.Apps;
using Shared.Kernel.Interfaces;
using Xunit;

namespace Master.Api.Tests;

/// <summary>
/// A public trial signup is verified by a code by email (the default, TK-197),
/// WhatsApp or SMS (TK-196) before anything is created, through the service,
/// against a real database.
///
/// <b>Why a real database.</b> Two of the guarantees are the database's: the
/// wrong-try count has to be committed on its own (a test against an in-memory
/// store would pass with the count rolled back), and the code is spent by a
/// guarded update whose row count decides which of two signups carrying it
/// creates the trial.
///
/// Each call builds its repository on a fresh context, because each call is a
/// request: a shared change tracker would hide what <c>ExecuteUpdate</c> wrote.
/// </summary>
[Collection(nameof(AdminCollection))]
public sealed class SignupOtpTests
{
    private readonly AdminFixture _admin;

    public SignupOtpTests(AdminFixture admin) => _admin = admin;

    private sealed class SettableClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 10, 20, 10, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class RecordingQueue : IProvisioningQueue
    {
        public List<ProvisioningJob> Jobs { get; } = [];

        public ValueTask EnqueueAsync(ProvisioningJob job, CancellationToken ct = default)
        {
            Jobs.Add(job);
            return ValueTask.CompletedTask;
        }

        public ValueTask<ProvisioningJob> DequeueAsync(CancellationToken ct) =>
            throw new NotSupportedException("The test never drains the queue.");
    }

    private sealed class StubCurrencies : IMasterCurrenciesRepository
    {
        public Task<IReadOnlyList<MasterCurrency>> GetAllAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<MasterCurrency>>([]);

        public Task<int?> FindCurrencyIdAsync(string code, CancellationToken ct = default) =>
            Task.FromResult<int?>(1);
    }

    private sealed class StubSeeder : ITenantSeeder
    {
        public Task<IReadOnlyList<string>> SeedAsync(Guid customerId, Guid orgId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<string>>([]);

        public Task<IReadOnlyList<string>> SeedAsync(
            Guid customerId, Guid orgId, BusinessType? businessType, AddOn addOns, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<string>>([]);
    }

    private sealed class Harness
    {
        public SettableClock Clock { get; } = new();

        public RecordingQueue Queue { get; } = new();

        public RecordingMobileMessages Messages { get; set; } = new();

        public RecordingEmailSender Email { get; } = new();
    }

    /// <summary>A repository for one request, on a context of its own.</summary>
    private (SignupRepository Signup, AdminDbContext Db) Request(Harness harness)
    {
        AdminDbContext db = _admin.CreateContext();

        return (new SignupRepository(
            db,
            harness.Queue,
            new StubCurrencies(),
            new StubSeeder(),
            new TenantDatabaseAllocatorRepository(db, NullLogger<TenantDatabaseAllocatorRepository>.Instance),
            harness.Clock,
            new OtpService(),
            harness.Messages,
            harness.Email), db);
    }

    /// <summary>A number no other test uses: tests share one database.</summary>
    private static string NewMobile() =>
        "98" + Random.Shared.NextInt64(10_000_000, 99_999_999).ToString();

    private static SignupRequest Signup(string mobile, string? code = null, OtpChannel channel = OtpChannel.WhatsApp)
    {
        string suffix = mobile[^6..] + Guid.NewGuid().ToString("N")[..4];
        return new SignupRequest
        {
            CompanyName = $"Verified Traders {suffix}",
            Email = $"verified-{suffix}@example.com",
            Password = "a-long-enough-password",
            DisplayName = "Kavitha R",
            OrganizationName = $"Head Office {suffix}",
            MobileNumber = mobile,
            CountryId = 1,
            OtpChannel = channel,
            OtpCode = code,
        };
    }

    private async Task SeedShardAsync()
    {
        await using AdminDbContext db = _admin.CreateContext();
        db.TenantDatabases.Add(new TenantDatabase
        {
            DatabaseName = $"OTP{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}",
            PlanType = PlanTier.Trial,
            MaxCustomers = 100,
            CurrentCustomers = 0,
        });
        await db.SaveChangesAsync();
    }

    private static string WrongCode(string code) => ((int.Parse(code) + 1) % 1_000_000).ToString("D6");

    private async Task<SignupCodeResult> SendAsync(Harness harness, SignupRequest request)
    {
        (SignupRepository signup, AdminDbContext db) = Request(harness);
        await using (db)
        {
            return await signup.SendCodeAsync(request, default);
        }
    }

    private async Task<TrialSignupResult> SignUpAsync(Harness harness, SignupRequest request)
    {
        (SignupRepository signup, AdminDbContext db) = Request(harness);
        await using (db)
        {
            return await signup.SignupWithCodeAsync(request, default);
        }
    }

    private async Task<bool> CustomerExistsAsync(string email)
    {
        await using AdminDbContext db = _admin.CreateContext();
        return await db.Customers.AnyAsync(c => c.BillingEmail == email);
    }

    /// <summary>
    /// The platform's own mailbox, which a signup's email is sent from. The
    /// recorded sender never opens it, so its password is never decrypted.
    /// </summary>
    private async Task EnsurePlatformMailboxAsync()
    {
        await using AdminDbContext db = _admin.CreateContext();
        SmtpSettings? row = await db.SmtpSettings.FirstOrDefaultAsync(s => s.CustomerId == null);
        if (row is null)
        {
            db.SmtpSettings.Add(new SmtpSettings
            {
                SmtpSettingsId = Guid.NewGuid(),
                Host = "localhost",
                Port = 25,
                UseSsl = false,
                FromEmail = "codes@example.com",
                FromName = "Bill Book",
                Username = "codes@example.com",
                PasswordEncrypted = "not-read-by-these-tests",
                IsActive = true,
            });
        }
        else
        {
            row.IsActive = true;
        }

        await db.SaveChangesAsync();
    }

    /// <summary>A signup verified by email, with no mobile number at all.</summary>
    private static SignupRequest EmailSignup(string? code = null)
    {
        SignupRequest request = Signup(NewMobile(), code, OtpChannel.Email);
        request.MobileNumber = null;
        return request;
    }

    // ---- Sending a code -----------------------------------------------------

    [SkippableFact]
    public async Task Sending_a_code_creates_no_customer_branch_licence_or_shard_claim()
    {
        Skip.If(_admin.SkipReason is not null, _admin.SkipReason ?? string.Empty);

        await SeedShardAsync();
        var harness = new Harness();
        string mobile = NewMobile();
        SignupRequest request = Signup(mobile);

        int capacityBefore;
        await using (AdminDbContext db = _admin.CreateContext())
        {
            capacityBefore = await db.TenantDatabases.SumAsync(d => d.CurrentCustomers);
        }

        SignupCodeResult sent = await SendAsync(harness, request);

        Assert.Equal(SignupCodeOutcome.Sent, sent.Outcome);
        Assert.False(await CustomerExistsAsync(request.Email));
        Assert.Empty(harness.Queue.Jobs);

        await using AdminDbContext check = _admin.CreateContext();
        Assert.False(await check.Organizations.AnyAsync(o => o.Name == request.OrganizationName));
        Assert.Equal(capacityBefore, await check.TenantDatabases.SumAsync(d => d.CurrentCustomers));

        OtpVerification otp = await check.OtpVerifications.AsNoTracking()
            .SingleAsync(o => o.Destination == mobile && o.Purpose == OtpPurpose.TrialSignup);
        Assert.Null(otp.UserId);
        Assert.Equal(OtpChannel.WhatsApp, otp.Channel);
        Assert.NotEqual(Assert.Single(harness.Messages.Sent).Code, otp.CodeHash);
    }

    [SkippableFact]
    public async Task WhatsApp_sends_the_number_in_international_form()
    {
        Skip.If(_admin.SkipReason is not null, _admin.SkipReason ?? string.Empty);

        var harness = new Harness();
        string mobile = NewMobile();
        SignupRequest request = Signup(mobile, channel: OtpChannel.WhatsApp);

        SignupCodeResult sent = await SendAsync(harness, request);

        MobileOtpMessage message = Assert.Single(harness.Messages.Sent);
        Assert.Equal(OtpChannel.WhatsApp, message.Channel);
        Assert.Equal(OtpPurpose.TrialSignup, message.Purpose);
        Assert.Equal("+91" + mobile, message.MobileNumber);
        Assert.Matches(@"^\d{6}$", message.Code);

        // The answer shows where the code went without showing the number.
        Assert.Equal(OtpChannel.WhatsApp, sent.Response!.Channel);
        Assert.EndsWith(mobile[^4..], sent.Response.Destination);
        Assert.DoesNotContain(mobile[..6], sent.Response.Destination);
    }

    [SkippableFact]
    public async Task Sms_is_sent_and_recorded_as_sms()
    {
        Skip.If(_admin.SkipReason is not null, _admin.SkipReason ?? string.Empty);

        var harness = new Harness();
        string mobile = NewMobile();

        await SendAsync(harness, Signup(mobile, channel: OtpChannel.Sms));

        Assert.Equal(OtpChannel.Sms, Assert.Single(harness.Messages.Sent).Channel);

        await using AdminDbContext db = _admin.CreateContext();
        Assert.Equal(
            OtpChannel.Sms,
            await db.OtpVerifications.Where(o => o.Destination == mobile).Select(o => o.Channel).SingleAsync());
    }

    [SkippableFact]
    public async Task A_code_that_cannot_be_sent_leaves_no_code_and_no_wait_behind()
    {
        Skip.If(_admin.SkipReason is not null, _admin.SkipReason ?? string.Empty);

        var harness = new Harness { Messages = RecordingMobileMessages.Unavailable() };
        string mobile = NewMobile();

        Assert.Equal(SignupCodeOutcome.DeliveryUnavailable, (await SendAsync(harness, Signup(mobile))).Outcome);

        harness.Messages = RecordingMobileMessages.Failing();
        Assert.Equal(SignupCodeOutcome.DeliveryFailed, (await SendAsync(harness, Signup(mobile))).Outcome);

        await using (AdminDbContext db = _admin.CreateContext())
        {
            Assert.False(await db.OtpVerifications.AnyAsync(o => o.Destination == mobile));
        }

        // Nothing reached the phone, so nothing holds the minute's wait.
        harness.Messages = new RecordingMobileMessages();
        Assert.Equal(SignupCodeOutcome.Sent, (await SendAsync(harness, Signup(mobile))).Outcome);
    }

    [SkippableFact]
    public async Task Another_code_waits_a_minute_and_replaces_the_first()
    {
        Skip.If(_admin.SkipReason is not null, _admin.SkipReason ?? string.Empty);

        await SeedShardAsync();
        var harness = new Harness();
        string mobile = NewMobile();

        await SendAsync(harness, Signup(mobile));

        harness.Clock.Now = harness.Clock.Now.AddSeconds(20);
        SignupCodeResult early = await SendAsync(harness, Signup(mobile));
        Assert.Equal(SignupCodeOutcome.TooSoon, early.Outcome);
        Assert.Equal(40, early.RetryAfterSeconds);
        Assert.Single(harness.Messages.Sent);

        harness.Clock.Now = harness.Clock.Now.AddSeconds(41);
        Assert.Equal(SignupCodeOutcome.Sent, (await SendAsync(harness, Signup(mobile))).Outcome);

        string first = harness.Messages.Sent[0].Code;
        string second = harness.Messages.Sent[1].Code;

        // The first code no longer works; only the newest one does.
        if (first != second)
        {
            Assert.Equal(TrialSignupOutcome.CodeWrong, (await SignUpAsync(harness, Signup(mobile, first))).Outcome);
        }

        Assert.Equal(TrialSignupOutcome.Created, (await SignUpAsync(harness, Signup(mobile, second))).Outcome);
    }

    [SkippableFact]
    public async Task A_number_gets_five_codes_an_hour()
    {
        Skip.If(_admin.SkipReason is not null, _admin.SkipReason ?? string.Empty);

        var harness = new Harness();
        string mobile = NewMobile();

        for (int i = 0; i < OtpPolicy.MaxPerWindow; i++)
        {
            Assert.Equal(SignupCodeOutcome.Sent, (await SendAsync(harness, Signup(mobile))).Outcome);
            harness.Clock.Now = harness.Clock.Now.AddSeconds(61);
        }

        SignupCodeResult sixth = await SendAsync(harness, Signup(mobile));

        Assert.Equal(SignupCodeOutcome.TooSoon, sixth.Outcome);
        Assert.True(sixth.RetryAfterSeconds > 60, "The hourly cap, not the minute's wait, holds the sixth.");
        Assert.Equal(OtpPolicy.MaxPerWindow, harness.Messages.Sent.Count);
    }

    // ---- Signing up with the code ----------------------------------------------

    [SkippableFact]
    public async Task The_right_code_creates_the_trial_and_queues_the_owner_with_the_mobile_confirmed()
    {
        Skip.If(_admin.SkipReason is not null, _admin.SkipReason ?? string.Empty);

        await SeedShardAsync();
        var harness = new Harness();
        string mobile = NewMobile();
        SignupRequest request = Signup(mobile);

        await SendAsync(harness, request);
        request.OtpCode = Assert.Single(harness.Messages.Sent).Code;

        TrialSignupResult result = await SignUpAsync(harness, request);

        Assert.Equal(TrialSignupOutcome.Created, result.Outcome);
        Assert.True(await CustomerExistsAsync(request.Email));

        ProvisioningJob job = Assert.Single(harness.Queue.Jobs);
        Assert.Equal(result.Response!.CustomerId, job.CustomerId);
        Assert.True(job.IsOwnerMobileConfirmed);

        await using AdminDbContext db = _admin.CreateContext();
        Assert.NotNull(await db.OtpVerifications
            .Where(o => o.Destination == mobile).Select(o => o.ConsumedAt).SingleAsync());
    }

    [SkippableFact]
    public async Task A_code_creates_one_trial_only()
    {
        Skip.If(_admin.SkipReason is not null, _admin.SkipReason ?? string.Empty);

        await SeedShardAsync();
        var harness = new Harness();
        string mobile = NewMobile();

        await SendAsync(harness, Signup(mobile));
        string code = harness.Messages.Sent[0].Code;

        Assert.Equal(TrialSignupOutcome.Created, (await SignUpAsync(harness, Signup(mobile, code))).Outcome);

        SignupRequest again = Signup(mobile, code);
        Assert.Equal(TrialSignupOutcome.CodeExpired, (await SignUpAsync(harness, again)).Outcome);
        Assert.False(await CustomerExistsAsync(again.Email));
    }

    [SkippableFact]
    public async Task A_wrong_code_creates_nothing_and_the_try_is_kept()
    {
        Skip.If(_admin.SkipReason is not null, _admin.SkipReason ?? string.Empty);

        var harness = new Harness();
        string mobile = NewMobile();
        SignupRequest request = Signup(mobile);

        await SendAsync(harness, request);
        request.OtpCode = WrongCode(harness.Messages.Sent[0].Code);

        TrialSignupResult result = await SignUpAsync(harness, request);

        Assert.Equal(TrialSignupOutcome.CodeWrong, result.Outcome);
        Assert.Equal(OtpPolicy.MaxAttempts - 1, result.TriesLeft);
        Assert.False(await CustomerExistsAsync(request.Email));
        Assert.Empty(harness.Queue.Jobs);

        // Read on a fresh context: the count was committed, not merely tracked.
        await using AdminDbContext db = _admin.CreateContext();
        Assert.Equal(1, await db.OtpVerifications
            .Where(o => o.Destination == mobile).Select(o => o.AttemptCount).SingleAsync());
    }

    [SkippableFact]
    public async Task Five_wrong_tries_lock_the_code_even_against_the_right_one()
    {
        Skip.If(_admin.SkipReason is not null, _admin.SkipReason ?? string.Empty);

        await SeedShardAsync();
        var harness = new Harness();
        string mobile = NewMobile();

        await SendAsync(harness, Signup(mobile));
        string code = harness.Messages.Sent[0].Code;

        TrialSignupResult last = null!;
        for (int i = 0; i < OtpPolicy.MaxAttempts; i++)
        {
            last = await SignUpAsync(harness, Signup(mobile, WrongCode(code)));
        }

        Assert.Equal(TrialSignupOutcome.TooManyTries, last.Outcome);

        SignupRequest right = Signup(mobile, code);
        Assert.Equal(TrialSignupOutcome.TooManyTries, (await SignUpAsync(harness, right)).Outcome);
        Assert.False(await CustomerExistsAsync(right.Email));
    }

    [SkippableFact]
    public async Task An_expired_code_creates_nothing()
    {
        Skip.If(_admin.SkipReason is not null, _admin.SkipReason ?? string.Empty);

        var harness = new Harness();
        string mobile = NewMobile();

        await SendAsync(harness, Signup(mobile));
        harness.Clock.Now = harness.Clock.Now.Add(OtpPolicy.Lifetime).AddSeconds(1);

        SignupRequest late = Signup(mobile, harness.Messages.Sent[0].Code);

        Assert.Equal(TrialSignupOutcome.CodeExpired, (await SignUpAsync(harness, late)).Outcome);
        Assert.False(await CustomerExistsAsync(late.Email));
    }

    [SkippableFact]
    public async Task A_code_sent_to_one_number_does_not_sign_up_another()
    {
        Skip.If(_admin.SkipReason is not null, _admin.SkipReason ?? string.Empty);

        await SeedShardAsync();
        var harness = new Harness();
        string proved = NewMobile();

        await SendAsync(harness, Signup(proved));

        SignupRequest other = Signup(NewMobile(), harness.Messages.Sent[0].Code);

        Assert.Equal(TrialSignupOutcome.CodeExpired, (await SignUpAsync(harness, other)).Outcome);
        Assert.False(await CustomerExistsAsync(other.Email));
    }

    /// <summary>
    /// A refusal after the code is spent — every shard full — rolls the spend
    /// back with everything else, so the person can try again with the same
    /// code once there is room.
    /// </summary>
    [SkippableFact]
    public async Task A_signup_refused_for_capacity_keeps_its_code()
    {
        Skip.If(_admin.SkipReason is not null, _admin.SkipReason ?? string.Empty);

        var harness = new Harness();
        string mobile = NewMobile();

        await SendAsync(harness, Signup(mobile));
        string code = harness.Messages.Sent[0].Code;

        await using (AdminDbContext db = _admin.CreateContext())
        {
            await db.TenantDatabases.ExecuteUpdateAsync(
                set => set.SetProperty(d => d.CurrentCustomers, d => d.MaxCustomers));
        }

        await Assert.ThrowsAsync<NoTenantCapacityException>(() => SignUpAsync(harness, Signup(mobile, code)));

        await using (AdminDbContext db = _admin.CreateContext())
        {
            Assert.Null(await db.OtpVerifications
                .Where(o => o.Destination == mobile).Select(o => o.ConsumedAt).SingleAsync());
        }

        await SeedShardAsync();
        Assert.Equal(TrialSignupOutcome.Created, (await SignUpAsync(harness, Signup(mobile, code))).Outcome);
    }

    // ---- By email (TK-197) -----------------------------------------------------

    [SkippableFact]
    public async Task Email_is_the_default_and_the_code_goes_to_the_address_being_signed_up()
    {
        Skip.If(_admin.SkipReason is not null, _admin.SkipReason ?? string.Empty);

        await EnsurePlatformMailboxAsync();
        var harness = new Harness();
        SignupRequest request = EmailSignup();
        request.Email = request.Email.ToUpperInvariant();

        Assert.Equal(OtpChannel.Email, new SignupRequest().OtpChannel);

        SignupCodeResult sent = await SendAsync(harness, request);

        Assert.Equal(SignupCodeOutcome.Sent, sent.Outcome);
        EmailMessage message = Assert.Single(harness.Email.Sent);
        Assert.Equal(request.Email, message.ToEmail);
        Assert.Null(message.CustomerId);
        Assert.Matches(@"^\d{6}$", harness.Email.LastCode());
        Assert.Empty(harness.Messages.Sent);

        // The person typed the address, so the answer shows it whole.
        Assert.Equal(OtpChannel.Email, sent.Response!.Channel);
        Assert.Equal(request.Email, sent.Response.Destination);

        Assert.False(await CustomerExistsAsync(request.Email));
        Assert.Empty(harness.Queue.Jobs);

        await using AdminDbContext db = _admin.CreateContext();
        OtpVerification otp = await db.OtpVerifications.AsNoTracking()
            .SingleAsync(o => o.Destination == request.Email.ToLowerInvariant() && o.Purpose == OtpPurpose.TrialSignup);
        Assert.Null(otp.UserId);
        Assert.Equal(OtpChannel.Email, otp.Channel);
    }

    [SkippableFact]
    public async Task The_right_code_by_email_creates_the_trial_with_the_owner_email_confirmed()
    {
        Skip.If(_admin.SkipReason is not null, _admin.SkipReason ?? string.Empty);

        await EnsurePlatformMailboxAsync();
        await SeedShardAsync();
        var harness = new Harness();
        SignupRequest request = EmailSignup();

        await SendAsync(harness, request);
        request.OtpCode = harness.Email.LastCode();

        TrialSignupResult result = await SignUpAsync(harness, request);

        Assert.Equal(TrialSignupOutcome.Created, result.Outcome);
        Assert.True(await CustomerExistsAsync(request.Email));

        ProvisioningJob job = Assert.Single(harness.Queue.Jobs);
        Assert.True(job.IsOwnerEmailConfirmed);
        Assert.False(job.IsOwnerMobileConfirmed);
        Assert.Null(job.OwnerMobileNumber);
    }

    [SkippableFact]
    public async Task A_wrong_code_by_email_creates_nothing_and_the_try_is_kept()
    {
        Skip.If(_admin.SkipReason is not null, _admin.SkipReason ?? string.Empty);

        await EnsurePlatformMailboxAsync();
        var harness = new Harness();
        SignupRequest request = EmailSignup();

        await SendAsync(harness, request);
        request.OtpCode = WrongCode(harness.Email.LastCode());

        TrialSignupResult result = await SignUpAsync(harness, request);

        Assert.Equal(TrialSignupOutcome.CodeWrong, result.Outcome);
        Assert.False(await CustomerExistsAsync(request.Email));

        await using AdminDbContext db = _admin.CreateContext();
        Assert.Equal(1, await db.OtpVerifications
            .Where(o => o.Destination == request.Email.ToLowerInvariant()).Select(o => o.AttemptCount).SingleAsync());
    }

    [SkippableFact]
    public async Task A_code_sent_by_email_does_not_sign_up_by_phone()
    {
        Skip.If(_admin.SkipReason is not null, _admin.SkipReason ?? string.Empty);

        await EnsurePlatformMailboxAsync();
        await SeedShardAsync();
        var harness = new Harness();
        string mobile = NewMobile();
        SignupRequest byEmail = Signup(mobile, channel: OtpChannel.Email);

        await SendAsync(harness, byEmail);

        SignupRequest byPhone = Signup(mobile, harness.Email.LastCode(), OtpChannel.WhatsApp);
        byPhone.Email = byEmail.Email;

        Assert.Equal(TrialSignupOutcome.CodeExpired, (await SignUpAsync(harness, byPhone)).Outcome);
        Assert.False(await CustomerExistsAsync(byEmail.Email));
    }

    /// <summary>
    /// With no platform mailbox the email would fail on the worker after the
    /// request had said it was sent, so the request is refused instead and
    /// nothing is written. Every other test here expects the mailbox, so it is
    /// put back however this one ends.
    /// </summary>
    [SkippableFact]
    public async Task With_no_platform_mailbox_a_code_by_email_is_refused_and_nothing_is_written()
    {
        Skip.If(_admin.SkipReason is not null, _admin.SkipReason ?? string.Empty);

        await EnsurePlatformMailboxAsync();
        var harness = new Harness();
        SignupRequest request = EmailSignup();

        try
        {
            await using (AdminDbContext db = _admin.CreateContext())
            {
                await db.SmtpSettings.Where(s => s.CustomerId == null)
                    .ExecuteUpdateAsync(set => set.SetProperty(s => s.IsActive, false));
            }

            Assert.Equal(SignupCodeOutcome.DeliveryUnavailable, (await SendAsync(harness, request)).Outcome);
            Assert.Empty(harness.Email.Sent);

            await using AdminDbContext check = _admin.CreateContext();
            Assert.False(await check.OtpVerifications.AnyAsync(o => o.Destination == request.Email.ToLowerInvariant()));
        }
        finally
        {
            await EnsurePlatformMailboxAsync();
        }
    }

    // ---- The rules, without a database -----------------------------------------

    [Fact]
    public void A_number_with_no_recent_code_may_have_one_now()
    {
        var now = new DateTimeOffset(2026, 10, 20, 10, 0, 0, TimeSpan.Zero);

        Assert.Equal(TimeSpan.Zero, OtpPolicy.WaitBeforeNext([], now));
        Assert.Equal(TimeSpan.Zero, OtpPolicy.WaitBeforeNext([now.AddHours(-2)], now));
    }

    [Fact]
    public void The_wait_is_what_is_left_of_the_minute_or_of_the_hour_whichever_is_longer()
    {
        var now = new DateTimeOffset(2026, 10, 20, 10, 0, 0, TimeSpan.Zero);

        Assert.Equal(TimeSpan.FromSeconds(40), OtpPolicy.WaitBeforeNext([now.AddSeconds(-20)], now));

        DateTimeOffset[] five =
        [
            now.AddMinutes(-50), now.AddMinutes(-40), now.AddMinutes(-30), now.AddMinutes(-20), now.AddMinutes(-10),
        ];

        // The oldest of the five leaves the hour in ten minutes.
        Assert.Equal(TimeSpan.FromMinutes(10), OtpPolicy.WaitBeforeNext(five, now));
    }

    [Theory]
    [InlineData("9843011234", "+91", "+919843011234")]
    [InlineData("09843011234", "+91", "+919843011234")]
    [InlineData("98430 11234", "+91", "+919843011234")]
    [InlineData("+44 7700 900123", "+91", "+447700900123")]
    [InlineData("2025550123", "+1", "+12025550123")]
    [InlineData("9843011234", null, "+919843011234")]
    public void A_stored_number_is_sent_in_international_form(string stored, string? phoneCode, string expected) =>
        Assert.Equal(expected, MobileMessages.International(stored, phoneCode));

    [Fact]
    public void A_masked_number_shows_only_its_last_four_digits()
    {
        Assert.Equal("******1234", OtpPolicy.MaskMobile("9843011234"));
        Assert.Equal("****", OtpPolicy.MaskMobile(null));
    }
}
