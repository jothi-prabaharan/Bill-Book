using Microsoft.EntityFrameworkCore;
using Master.Entity.Enums;
using Master.Entity.Models;
using Master.Entity.TableEntities;
using Master.Repository;

namespace Master.Api.Services;

public sealed class SignupService
{
    private const int TrialDays = 14;
    private const int TrialMaxUsers = 3;
    private const int TrialMaxOrganizations = 1;
    private const int CodeRetryLimit = 5;

    private readonly AdminDbContext _db;
    private readonly IProvisioningQueue _queue;
    private readonly IMasterCurrencies _master;
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

    public SignupService(
        AdminDbContext db,
        IProvisioningQueue queue,
        IMasterCurrencies master,
        ITenantSeeder seeder,
        ITenantDatabaseAllocator databases,
        TimeProvider clock)
    {
        _db = db;
        _queue = queue;
        _master = master;
        _seeder = seeder;
        _databases = databases;
        _clock = clock;
    }

    public async Task<SignupResponse> SignupAsync(SignupRequest request, CancellationToken ct)
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
        string databaseName = await _databases.AllocateAsync("Trial", ct)
            ?? await _databases.AllocateAsync("Pro", ct)
            ?? throw new NoTenantCapacityException("Trial");

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
                PlanTier = "Trial",
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

        var license = new License
        {
            LicenseId = Guid.NewGuid(),
            CustomerId = customer.CustomerId,
            LicenseType = LicenseType.Trial,
            StartDate = today,
            ExpiryDate = today.AddDays(TrialDays),
            MaxUsers = TrialMaxUsers,
            MaxOrganizations = TrialMaxOrganizations,
            IsActive = true,
        };
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
            MobileNumber = request.MobileNumber,
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
        // rather than blocking this response: the signup screen already polls
        // GetStatusAsync for CanLogin, and that contract does not change.
        await _queue.EnqueueAsync(new ProvisioningJob(
            customer.CustomerId,
            org.OrgId,
            request.Email,
            request.DisplayName,
            request.MobileNumber,
            request.Password), ct);

        return new SignupResponse
        {
            CustomerId = customer.CustomerId,
            CustomerCode = customer.CustomerCode,
            Message = "Signup received. Your account is being set up.",
        };
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
    public async Task<IReadOnlyList<CustomerListItem>> ListAsync(CancellationToken ct) =>
        await _db.Customers
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => new CustomerListItem
            {
                CustomerId = c.CustomerId,
                CustomerCode = c.CustomerCode,
                Name = c.Name,
                BillingEmail = c.BillingEmail,
                PlanTier = c.PlanTier,
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
        // through OrganizationService's own retry instead.
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

        customer.Status = TenantStatus.Trial;
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
