using Master.Repository.Clients;
using Microsoft.EntityFrameworkCore;
using Master.Entity.Enums;
using Master.Entity.Models;
using Master.Entity.TableEntities;
using Master.Repository;
using Shared.Kernel.Apps;
using Shared.Kernel.Interfaces;
using Shared.Kernel.Persistence;
using Shared.Kernel.Validation;

namespace Master.Repository.Repositories;

public sealed class SignupRepository
{
    private const int TrialDays = 14;
    private const int TrialMaxOrganizations = 1;
    private const int CodeRetryLimit = 5;

    private readonly AdminDbContext _db;
    private readonly IProvisioningQueue _queue;
    private readonly IMasterCurrenciesRepository _master;
    private readonly ITenantSeeder _seeder;

    /// <summary>
    /// Picks the physical database the customer's books go in. Without it the
    /// customer row has no <c>DatabaseName</c>, which is a not-null column and
    /// the value <c>TenantDatabaseResolver</c> reads to route every later
    /// request — so a signup that skipped this step could not be written, and
    /// would be unusable if it had been.
    /// </summary>
    private readonly ITenantDatabaseAllocator _databases;

    private readonly TimeProvider _clock;

    /// <summary>Makes and checks the six-digit codes a public signup is verified with (TK-196).</summary>
    private readonly IOtpRepository _otp;

    /// <summary>Sends them by WhatsApp or SMS.</summary>
    private readonly IMobileMessageClient _messages;

    /// <summary>Sends them by email, from the platform's own mailbox (TK-197).</summary>
    private readonly IEmailSender _email;

    public SignupRepository(
        AdminDbContext db,
        IProvisioningQueue queue,
        IMasterCurrenciesRepository master,
        ITenantSeeder seeder,
        ITenantDatabaseAllocator databases,
        TimeProvider clock,
        IOtpRepository otp,
        IMobileMessageClient messages,
        IEmailSender email)
    {
        _db = db;
        _queue = queue;
        _master = master;
        _seeder = seeder;
        _databases = databases;
        _clock = clock;
        _otp = otp;
        _messages = messages;
        _email = email;
    }

    /// <summary>
    /// Why a signup's business type and add-ons cannot go together, or null
    /// (D-50, TK-136). A business type allows only the add-ons
    /// <see cref="AddOnRules.Allowed"/> gives it, and a customer with no trade
    /// is buying HRMS or Payroll, so needs one of them. Pure.
    /// </summary>
    public static string? ScopeRefusal(BusinessType? businessType, AddOn addOns)
    {
        if (businessType is BusinessType type && !AddOnRules.IsDefined(type))
        {
            return "Choose a business type: Retail, Pharma, Jewellery, School, Service or Restaurant.";
        }

        if (businessType is null && (addOns & (AddOn.Hrms | AddOn.Payroll)) == 0)
        {
            return "Without a business type, choose HRMS or Payroll.";
        }

        return AddOnRules.IsAllowed(businessType, addOns)
            ? null
            : "One of the chosen add-ons is not available for that business type.";
    }

    /// <summary>
    /// Creates the customer, its licence and first branch, and queues the owner
    /// and the seed — with nothing verified. A platform operator's create
    /// (<c>AdminCustomersController</c>) comes here directly; a public signup
    /// comes through <see cref="SignupWithCodeAsync"/>, which proves the email
    /// address or the mobile number first (TK-196, TK-197).
    /// </summary>
    public async Task<SignupResponse> SignupAsync(SignupRequest request, CancellationToken ct)
    {
        (SignupResponse response, ProvisioningJob job) = await CreateTrialAsync(request, verifiedBy: null, ct);
        await _queue.EnqueueAsync(job, ct);
        return response;
    }

    // ---- Public signup: a code first, then the trial (TK-196) ---------------

    /// <summary>
    /// Sends a six-digit code by the chosen channel — by email to the signup's
    /// address, the default (TK-197), or on WhatsApp or by SMS to its mobile
    /// number (TK-196) — and creates nothing else: no customer, licence,
    /// branch, shard capacity, seed or token. The controller has already
    /// checked everything it checks before a create, so a code is sent only
    /// for a signup that would be accepted.
    ///
    /// A new code replaces any live one for the address or number, which waits
    /// <see cref="OtpPolicy.ResendAfter"/> between codes and gets
    /// <see cref="OtpPolicy.MaxPerWindow"/> an hour, counted from the codes
    /// stored for it; a code that could not be sent is removed again, so it
    /// neither holds the wait nor counts toward the cap. An email is queued
    /// and sent from the platform's mailbox in the background, so with no
    /// mailbox set up the request is refused before anything is written.
    ///
    /// Runs with no request transaction (<c>[NoTransaction]</c>): each save
    /// here stands on its own.
    /// </summary>
    public async Task<SignupCodeResult> SendCodeAsync(SignupRequest request, CancellationToken ct)
    {
        DateTimeOffset now = _clock.GetUtcNow();
        bool byEmail = request.OtpChannel == OtpChannel.Email;
        string destination = SignupDestination(request);

        // The mailbox SmtpSettingsRepository.ResolveAsync picks for a mail with
        // no customer, which a signup's is. Without one the queued email would
        // fail on the worker after this had answered that it was sent.
        if (byEmail && !await _db.SmtpSettings.AnyAsync(s => s.CustomerId == null && s.IsActive, ct))
        {
            return new SignupCodeResult { Outcome = SignupCodeOutcome.DeliveryUnavailable };
        }

        DateTimeOffset floor = OtpPolicy.WindowExpiryFloor(now);
        List<DateTimeOffset> expiries = await _db.OtpVerifications
            .Where(o => o.Purpose == OtpPurpose.TrialSignup
                && o.Destination == destination
                && o.ExpiresAt > floor)
            .Select(o => o.ExpiresAt)
            .ToListAsync(ct);

        TimeSpan wait = OtpPolicy.WaitBeforeNext(expiries.Select(OtpPolicy.SentAt), now);
        if (wait > TimeSpan.Zero)
        {
            return new SignupCodeResult
            {
                Outcome = SignupCodeOutcome.TooSoon,
                RetryAfterSeconds = (int)Math.Ceiling(wait.TotalSeconds),
            };
        }

        List<OtpVerification> live = await _db.OtpVerifications
            .Where(o => o.Purpose == OtpPurpose.TrialSignup
                && o.Destination == destination
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
            UserId = null,
            Purpose = OtpPurpose.TrialSignup,
            Channel = request.OtpChannel,
            Destination = destination,
            CodeHash = hash,
            ExpiresAt = now.Add(OtpPolicy.Lifetime),
            AttemptCount = 0,
        };
        _db.OtpVerifications.Add(otp);
        await _db.SaveChangesAsync(ct);

        if (byEmail)
        {
            int minutes = (int)OtpPolicy.Lifetime.TotalMinutes;
            try
            {
                await _email.SendAsync(new EmailMessage
                {
                    ToEmail = request.Email.Trim(),
                    ToName = request.DisplayName,
                    Subject = "Your signup verification code",
                    HtmlBody = $"<p>Your verification code is <strong>{code}</strong>. It expires in {minutes} minutes.</p>"
                        + "<p>If you did not start a signup, you can ignore this email.</p>",
                    TextBody = $"Your verification code is {code}. It expires in {minutes} minutes. "
                        + "If you did not start a signup, you can ignore this email.",
                }, ct);
            }
            catch
            {
                // A code that was never queued should not hold the wait. The
                // failure itself goes on to the error handler like any other.
                _db.OtpVerifications.Remove(otp);
                await _db.SaveChangesAsync(CancellationToken.None);
                throw;
            }

            return Sent(request.OtpChannel, request.Email.Trim(), "A 6-digit code has been sent by email.");
        }

        string? phoneCode = await _db.Countries
            .Where(c => c.CountryId == request.CountryId)
            .Select(c => c.PhoneCode)
            .FirstOrDefaultAsync(ct);

        try
        {
            await _messages.SendOtpAsync(new MobileOtpMessage(
                request.OtpChannel,
                MobileMessages.International(destination, phoneCode),
                code,
                OtpPurpose.TrialSignup,
                OtpPolicy.Lifetime), ct);
        }
        catch (OtpDeliveryException failure)
        {
            _db.OtpVerifications.Remove(otp);
            await _db.SaveChangesAsync(ct);

            return new SignupCodeResult
            {
                Outcome = failure.IsUnavailable ? SignupCodeOutcome.DeliveryUnavailable : SignupCodeOutcome.DeliveryFailed,
            };
        }

        return Sent(
            request.OtpChannel,
            OtpPolicy.MaskMobile(destination),
            request.OtpChannel == OtpChannel.Sms
                ? "A 6-digit code has been sent by SMS."
                : "A 6-digit code has been sent on WhatsApp.");
    }

    private static SignupCodeResult Sent(OtpChannel channel, string destination, string message) => new()
    {
        Outcome = SignupCodeOutcome.Sent,
        Response = new SignupCodeResponse
        {
            Channel = channel,
            Destination = destination,
            ExpiresInSeconds = (int)OtpPolicy.Lifetime.TotalSeconds,
            ResendAfterSeconds = (int)OtpPolicy.ResendAfter.TotalSeconds,
            Message = message,
        },
    };

    /// <summary>
    /// Creates the trial for a signup whose code is right, and only then.
    ///
    /// The code is matched on the signup's own address or mobile number, as its
    /// channel says, so a code sent to one cannot create an account for
    /// another. A wrong code is
    /// counted and saved on its own before the refusal is returned, which is
    /// why the route runs with no request transaction: a transaction rolled
    /// back with the 400 would take the count with it, and the five-try limit
    /// would never be reached.
    ///
    /// A right code is spent and the customer, licence and branch written in
    /// one transaction, with the code spent by a guarded update so that of two
    /// requests carrying it only one creates a trial. A refusal after that
    /// (no shard with room) rolls the spend back too, so the same code works
    /// on a retry. The owner and the seed are queued after the commit, so the
    /// worker never looks for a customer that is not there yet.
    /// </summary>
    public async Task<TrialSignupResult> SignupWithCodeAsync(SignupRequest request, CancellationToken ct)
    {
        DateTimeOffset now = _clock.GetUtcNow();
        string destination = SignupDestination(request);

        OtpVerification? otp = await _db.OtpVerifications
            .Where(o => o.Purpose == OtpPurpose.TrialSignup
                && o.Destination == destination
                && o.ConsumedAt == null
                && o.ExpiresAt > now)
            .OrderByDescending(o => o.OtpVerificationId)
            .FirstOrDefaultAsync(ct);

        if (otp is null)
        {
            return new TrialSignupResult { Outcome = TrialSignupOutcome.CodeExpired };
        }

        if (otp.AttemptCount >= OtpPolicy.MaxAttempts)
        {
            return new TrialSignupResult { Outcome = TrialSignupOutcome.TooManyTries };
        }

        if (!_otp.Verify(request.OtpCode ?? string.Empty, otp.CodeHash))
        {
            otp.AttemptCount++;
            await _db.SaveChangesAsync(ct);

            return otp.AttemptCount >= OtpPolicy.MaxAttempts
                ? new TrialSignupResult { Outcome = TrialSignupOutcome.TooManyTries }
                : new TrialSignupResult
                {
                    Outcome = TrialSignupOutcome.CodeWrong,
                    TriesLeft = OtpPolicy.MaxAttempts - otp.AttemptCount,
                };
        }

        SignupResponse response;
        ProvisioningJob job;

        await using (ITransactionScope scope = await _db.Database.BeginScopeAsync(ct))
        {
            int spent = await _db.OtpVerifications
                .Where(o => o.OtpVerificationId == otp.OtpVerificationId && o.ConsumedAt == null)
                .ExecuteUpdateAsync(set => set.SetProperty(o => o.ConsumedAt, now), ct);

            if (spent == 0)
            {
                return new TrialSignupResult { Outcome = TrialSignupOutcome.CodeExpired };
            }

            (response, job) = await CreateTrialAsync(request, verifiedBy: otp.Channel, ct);
            await scope.CommitAsync(ct);
        }

        await _queue.EnqueueAsync(job, ct);

        return new TrialSignupResult { Outcome = TrialSignupOutcome.Created, Response = response };
    }

    /// <summary>
    /// Where a public signup's code goes, as it is stored and matched: the
    /// address, lower-cased, for email; the mobile number as it is stored for
    /// WhatsApp or SMS. The controller refuses a phone channel with no number.
    /// </summary>
    private static string SignupDestination(SignupRequest request) =>
        request.OtpChannel == OtpChannel.Email
            ? request.Email.Trim().ToLowerInvariant()
            : PhoneNumbers.NormalizeOptional(request.MobileNumber)
                ?? throw new InvalidOperationException("A code by WhatsApp or SMS needs a mobile number; the controller checks it first.");

    /// <summary>
    /// The writes every signup shares: the customer on a shard, its licence and
    /// first branch, and the provisioning job for the caller to queue once
    /// those are committed. <paramref name="verifiedBy"/> is the channel whose
    /// code proved the signup, null for an operator's create; the owner's email
    /// or mobile is created confirmed to match.
    /// </summary>
    private async Task<(SignupResponse Response, ProvisioningJob Job)> CreateTrialAsync(
        SignupRequest request, OtpChannel? verifiedBy, CancellationToken ct)
    {
        DateOnly today = DateOnly.FromDateTime(_clock.GetUtcNow().UtcDateTime);

        var country = await _db.Countries
            .Where(c => c.CountryId == request.CountryId)
            .Select(c => new { c.CountryCode, c.CurrencyCode })
            .FirstOrDefaultAsync(ct);

        string countryPrefix = country?.CountryCode ?? "IN";
        string defaultCurrency = country?.CurrencyCode ?? "INR";

        // Before anything is written. A customer row cannot exist without a
        // database to point at, and finding out after the insert would mean a
        // half-created customer to clean up.
        // A trial shares a pooled shard, and a full pool provisions the next
        // one (TK-46), so this refuses only when no shard can be made at all.
        string databaseName = await _databases.AllocateAsync(PlanTier.Trial, ct)
            ?? throw new NoTenantCapacityException(PlanTier.Trial);

        // Public signup is always Standard; the controllers decide (TK-152).
        CustomerKind kind = request.Kind ?? CustomerKind.Standard;
        (LicenseType licenceType, DateOnly licenceExpiry) = CustomerKindRepository.FirstLicence(kind, today, TrialDays);

        CustomerEntity customer = null!;

        // CustomerCode is read-max-then-increment; the unique index makes the
        // retry safe under concurrent signups (CLAUDE.md blocking-gap fix).
        for (int attempt = 1; ; attempt++)
        {
            string code = await NextCustomerCodeAsync(ct);

            customer = new CustomerEntity
            {
                CustomerId = Guid.NewGuid(),
                CustomerCode = code,
                CountryPrefix = countryPrefix,
                Name = request.CompanyName,
                BillingEmail = request.Email,
                Status = TenantStatus.Provisioning,
                PlanTier = CustomerKindRepository.FirstPlanTier(kind),
                Kind = kind,
                DatabaseName = databaseName,
            };
            _db.Customers.Add(customer);

            try
            {
                await _db.SaveChangesAsync(ct);
                break;
            }
            catch (DbUpdateException) when (attempt < CodeRetryLimit)
            {
                // Another signup took the code — detach and try the next one.
                _db.Entry(customer).State = EntityState.Detached;
            }
        }

        // The first branch's business type and add-ons, as signed up for (D-50, TK-136).
        // The controller has already refused a pair ScopeRefusal would refuse.
        BusinessType? initialBusinessType = request.BusinessType;
        AddOn initialAddOns = request.AddOns;

        var license = new License
        {
            LicenseId = Guid.NewGuid(),
            CustomerId = customer.CustomerId,
            LicenseType = licenceType,
            StartDate = today,
            ExpiryDate = licenceExpiry,
            MaxOrganizations = CustomerKindRepository.NeverLapses(kind) ? CustomerKindRepository.UnbilledMaxOrganizations : TrialMaxOrganizations,
            IsActive = true,
        };

        // A trial's three users and its transaction limits, or none for an unbilled customer.
        PlanCatalogRepository.Apply(license);
        _db.Licenses.Add(license);

        var org = new Organization
        {
            OrgId = Guid.NewGuid(),
            CustomerId = customer.CustomerId,
            Name = request.OrganizationName,
            // The first branch on a new account is the head office itself, so
            // it takes the head-office code rather than asking for one during
            // signup. Later branches are named by the customer.
            OrgCode = "HO",
            BusinessType = initialBusinessType,
            AddOns = initialAddOns,
            BaseCurrency = request.BaseCurrency ?? defaultCurrency,
            FinancialYearStartMonth = request.FinancialYearStartMonth,
            Gstin = request.Gstin,
            Pan = request.Pan,
            Tan = request.Tan,
            Tin = request.Tin,
            Cin = request.Cin,
            UdyamNumber = request.UdyamNumber,
            Status = TenantStatus.Provisioning,
            // The branch carries its own copy of when access ends, so the login
            // check has a date on the organization itself rather than only on
            // the account above it. On a new customer the two are the same by
            // construction; they only diverge if a head office later winds this
            // branch down early.
            ExpiryDate = license.ExpiryDate,
            CountryId = request.CountryId,
            StateId = request.StateId,
            AddressLine1 = request.AddressLine1,
            AddressLine2 = request.AddressLine2,
            City = request.City,
            PostalCode = request.PostalCode,
            MobileNumber = PhoneNumbers.NormalizeOptional(request.MobileNumber),
            Email = request.Email,
        };
        _db.Organizations.Add(org);

        // The org's base currency is enabled and active from creation, and can
        // never be deactivated — every posting converts to it.
        int? baseCurrencyId = await _master.FindCurrencyIdAsync(org.BaseCurrency, ct);
        if (baseCurrencyId is int currencyId)
        {
            _db.OrgCurrencies.Add(new OrgCurrency
            {
                OrgCurrencyId = Guid.NewGuid(),
                OrgId = org.OrgId,
                CurrencyId = currencyId,
                IsBaseCurrency = true,
                IsActive = true,
            });
        }

        await _db.SaveChangesAsync(ct);

        // No database to create — every customer already shares the one
        // tenant database. What is left is the owner user and the seed
        // (chart of accounts, tax master, numbering series, units...), which
        // can still fail or run long, so it stays a queued background step
        // rather than blocking the response: the signup screen polls
        // GetStatusAsync for CanLogin. The caller queues the job once these
        // rows are committed.
        var job = new ProvisioningJob(
            customer.CustomerId,
            org.OrgId,
            request.Email,
            request.DisplayName,
            request.MobileNumber,
            request.Password,
            IsOwnerMobileConfirmed: verifiedBy is OtpChannel.WhatsApp or OtpChannel.Sms,
            IsOwnerEmailConfirmed: verifiedBy == OtpChannel.Email);

        return (new SignupResponse
        {
            CustomerId = customer.CustomerId,
            CustomerCode = customer.CustomerCode,
            Message = "Signup received. Your account is being set up.",
        }, job);
    }

    public async Task<CustomerStatusResponse?> GetStatusAsync(Guid customerId, CancellationToken ct)
    {
        TenantStatus? status = await _db.Customers
            .Where(c => c.CustomerId == customerId)
            .Select(c => (TenantStatus?)c.Status)
            .FirstOrDefaultAsync(ct);

        if (status is not TenantStatus customerStatus)
        {
            return null;
        }

        return new CustomerStatusResponse
        {
            CustomerId = customerId,
            CustomerStatus = customerStatus.ToString(),
            ProvisioningStatus = customerStatus.ToString(),
            // Trial counts. ProvisioningWorker finishes a successful signup by
            // setting the customer to Trial, not Active, so requiring Active here
            // meant CanLogin was false for every account that provisioned
            // correctly — the signup screen polls this until it gives up, and the
            // customer never reaches the app. Suspended and Expired stay refused.
            CanLogin = customerStatus is TenantStatus.Trial or TenantStatus.Active,
        };
    }

    /// <summary>
    /// Every customer, for the platform admin's customer list. Ordered newest
    /// first — a freshly provisioned or stuck signup is what an operator opens
    /// this screen to look at.
    /// </summary>
    /// <summary>The platform admin's customer list, newest first, optionally of one kind (TK-152).</summary>
    public async Task<IReadOnlyList<CustomerListItem>> ListAsync(CancellationToken ct, CustomerKind? kind = null) =>
        await _db.Customers
            .Where(c => kind == null || c.Kind == kind)
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => new CustomerListItem
            {
                CustomerId = c.CustomerId,
                CustomerCode = c.CustomerCode,
                Name = c.Name,
                BillingEmail = c.BillingEmail,
                PlanTier = c.PlanTier.ToString(),
                Kind = c.Kind.ToString(),
                Status = c.Status.ToString(),
                CreatedAt = c.CreatedAt,
            })
            .ToListAsync(ct);

    /// <summary>
    /// Re-runs the seed for a customer stuck at Provisioning or Failed — the
    /// platform admin's retry action. Only the seed, not the owner user: a
    /// retry has no password to create one with, and if the customer row
    /// exists at all the owner-creation step already ran (it precedes the
    /// seed in both SignupAsync's queue and ProvisioningWorker), so there is
    /// nothing left to redo there. The seed itself is idempotent regardless
    /// of how far a previous attempt got.
    /// </summary>
    public async Task<RetryProvisioningResult> RetryProvisioningAsync(Guid customerId, CancellationToken ct)
    {
        CustomerEntity? customer = await _db.Customers
            .FirstOrDefaultAsync(c => c.CustomerId == customerId, ct);

        if (customer is null)
        {
            return RetryProvisioningResult.NotFoundResult;
        }

        // The head office — the first organization created for this customer,
        // by signup. There is exactly one at this point; later branches go
        // through OrganizationRepository's own retry instead.
        Organization? org = await _db.Organizations
            .Where(o => o.CustomerId == customerId)
            .OrderBy(o => o.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (org is null)
        {
            return RetryProvisioningResult.NotFoundResult;
        }

        IReadOnlyList<string> unseeded = await _seeder.SeedAsync(customerId, org.OrgId, ct);

        if (unseeded.Count > 0)
        {
            customer.Status = TenantStatus.Failed;
            await _db.SaveChangesAsync(ct);
            return RetryProvisioningResult.Failed(unseeded);
        }

        customer.Status = CustomerKindRepository.ReadyStatus(customer.Kind);
        org.Status = TenantStatus.Active;
        await _db.SaveChangesAsync(ct);

        return RetryProvisioningResult.OkResult;
    }

    /// <summary>
    /// The next customer code: one past the highest numeric one in use.
    ///
    /// <b>Codes that are not numbers are skipped rather than parsed.</b> This
    /// took the single greatest <c>CustomerCode</c> and called
    /// <c>long.Parse</c> on it, so one row whose code was not ten digits —
    /// imported, hand-written, or written by a future feature that codes
    /// customers differently — would throw <c>FormatException</c> and make
    /// <i>every subsequent signup</i> fail, permanently, with an error naming
    /// neither the row nor the reason. Every code the product writes today is
    /// numeric, which is exactly why nothing had noticed.
    ///
    /// <c>MaxAsync</c> also compared codes as text, where "9" sorts above "10".
    /// It happens to be right for zero-padded ten-digit codes and wrong for
    /// anything else, so the ordering is now done on the parsed number.
    ///
    /// The insert is still the authority: a duplicate loses the unique index and
    /// the caller retries with the next code, which is what makes two
    /// simultaneous signups safe. This only has to be a good guess.
    /// </summary>
    private async Task<string> NextCustomerCodeAsync(CancellationToken ct)
    {
        List<string> codes = await _db.Customers
            .Select(c => c.CustomerCode)
            .ToListAsync(ct);

        long highest = 0;

        foreach (string code in codes)
        {
            if (long.TryParse(code, out long value) && value > highest)
            {
                highest = value;
            }
        }

        return (highest + 1).ToString("D10");
    }

    public async Task<bool> EmailExistsAsync(string email, CancellationToken ct)
    {
        return await _db.Users.AnyAsync(u => u.Email == email, ct)
            || await _db.Customers.AnyAsync(c => c.BillingEmail == email, ct);
    }
}

