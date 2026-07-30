using Microsoft.EntityFrameworkCore;
using Platform.Entity.Enums;
using Platform.Entity.Models;
using Platform.Entity.TableEntities;
using Platform.Repository;

namespace Platform.Api.Services;

public sealed class SignupService
{
    private const int TrialDays = 14;
    private const int TrialMaxUsers = 3;
    private const int TrialMaxOrganizations = 1;
    private const int CodeRetryLimit = 5;

    private readonly PlatformDbContext _db;
    private readonly IProvisioningQueue _queue;
    private readonly IMasterCurrencies _master;
    private readonly TimeProvider _clock;

    public SignupService(
        PlatformDbContext db,
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
                CountryPrefix = "IN",
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
            BaseCurrency = request.BaseCurrency ?? "INR",
            FinancialYearStartMonth = request.FinancialYearStartMonth,
            Gstin = request.Gstin,
            Pan = request.Pan,
            Tan = request.Tan,
            Tin = request.Tin,
            Cin = request.Cin,
            UdyamNumber = request.UdyamNumber,
            Status = TenantStatus.Provisioning,
            CountryId = request.CountryId,
            StateId = request.StateId,
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

        string databaseName = customer.CountryPrefix + customer.CustomerCode;
        var database = new CustomerDatabase
        {
            CustomerId = customer.CustomerId,
            DatabaseName = databaseName,
            ConnectionSecretRef = $"tenant-db-{databaseName}",
            Status = ProvisioningStatus.Provisioning,
        };
        _db.CustomerDatabases.Add(database);

        await _db.SaveChangesAsync(ct);

        await _queue.EnqueueAsync(new ProvisioningJob(
            customer.CustomerId,
            org.OrgId,
            databaseName,
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
        var row = await (
            from c in _db.Customers
            join d in _db.CustomerDatabases on c.CustomerId equals d.CustomerId
            where c.CustomerId == customerId
            select new { c.Status, DbStatus = d.Status }).FirstOrDefaultAsync(ct);

        if (row is null)
        {
            return null;
        }

        return new CustomerStatusResponse
        {
            CustomerId = customerId,
            CustomerStatus = row.Status.ToString(),
            DatabaseStatus = row.DbStatus.ToString(),
            CanLogin = row.Status == TenantStatus.Active && row.DbStatus == ProvisioningStatus.Ready,
        };
    }

    private async Task<string> NextCustomerCodeAsync(CancellationToken ct)
    {
        string? max = await _db.Customers.MaxAsync(c => (string?)c.CustomerCode, ct);
        long next = max is null ? 1 : long.Parse(max) + 1;
        return next.ToString("D10");
    }
}
