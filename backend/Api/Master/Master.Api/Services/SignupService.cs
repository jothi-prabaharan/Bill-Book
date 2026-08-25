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
    private readonly TimeProvider _clock;

    public SignupService(
        AdminDbContext db,
        IProvisioningQueue queue,
        IMasterCurrencies master,
        TimeProvider clock)
    {
        _db = db;
        _queue = queue;
        _master = master;
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

        Customer customer = null!;

        // CustomerCode is read-max-then-increment; the unique index makes the
        // retry safe under concurrent signups (CLAUDE.md blocking-gap fix).
        for (int attempt = 1; ; attempt++)
        {
            string code = await NextCustomerCodeAsync(ct);

            customer = new Customer
            {
                CustomerId = Guid.NewGuid(),
                CustomerCode = code,
                CountryPrefix = countryPrefix,
                Name = request.CompanyName,
                BillingEmail = request.Email,
                Status = TenantStatus.Provisioning,
                PlanTier = "Trial",
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

    private async Task<string> NextCustomerCodeAsync(CancellationToken ct)
    {
        string? max = await _db.Customers.MaxAsync(c => (string?)c.CustomerCode, ct);
        long next = max is null ? 1 : long.Parse(max) + 1;
        return next.ToString("D10");
    }

    public async Task<bool> EmailExistsAsync(string email, CancellationToken ct)
    {
        return await _db.Users.AnyAsync(u => u.Email == email, ct)
            || await _db.Customers.AnyAsync(c => c.BillingEmail == email, ct);
    }
}
