using Microsoft.EntityFrameworkCore;
using Platform.Entity.Enums;
using Platform.Entity.Models;
using Platform.Entity.TableEntities;
using Platform.Repository;

namespace Platform.Api.Services;

/// <summary>
/// Branches, under one head office.
///
/// A branch is a complete set of books, so creating one is not an insert — it is
/// a small provisioning. The row is written and then every service is asked to
/// seed its master data into the customer's existing database: chart of
/// accounts, tax rates, numbering series, payment terms, contact roles, unit
/// types, units, metal purities. Skip that and the branch comes up empty and
/// cannot save an item, because saving one needs a unit type. That was the exact
/// failure new customers had before provisioning learned to seed.
///
/// No new database. A branch shares its head office's, separated by OrgId.
/// </summary>
public sealed class OrganizationService
{
    private readonly PlatformDbContext _db;
    private readonly ITenantSeeder _seeder;
    private readonly IMasterCurrencies _master;
    private readonly IStateDirectory _states;
    private readonly ILogger<OrganizationService> _log;

    public OrganizationService(
        PlatformDbContext db,
        ITenantSeeder seeder,
        IMasterCurrencies master,
        IStateDirectory states,
        ILogger<OrganizationService> log)
    {
        _db = db;
        _seeder = seeder;
        _master = master;
        _states = states;
        _log = log;
    }

    /// <summary>
    /// Every branch on the account. Scoped by the caller's customer id, which
    /// the controller has already checked against the token — <c>plt</c> holds
    /// every customer's rows, so this filter is the whole boundary.
    /// </summary>
    public async Task<IReadOnlyList<OrganizationListItem>> ListAsync(
        Guid customerId, CancellationToken ct)
    {
        List<Organization> rows = await _db.Organizations
            .Where(o => o.CustomerId == customerId)
            .OrderBy(o => o.CreatedAt)
            .ThenBy(o => o.Name)
            .ToListAsync(ct);

        // The first branch is the one that cannot be deactivated. Decided by
        // creation order rather than a flag: a stored "is first" could drift
        // from the truth, and this cannot.
        Guid? firstOrgId = rows.Count > 0 ? rows[0].OrgId : null;

        return rows.Select(o => Project(o, o.OrgId == firstOrgId)).ToList();
    }

    public async Task<OrganizationListItem?> GetAsync(
        Guid customerId, Guid orgId, CancellationToken ct)
    {
        IReadOnlyList<OrganizationListItem> all = await ListAsync(customerId, ct);
        return all.FirstOrDefault(o => o.OrgId == orgId);
    }

    public async Task<SaveOrganizationResult> CreateAsync(
        Guid customerId, SaveOrganizationRequest request, CancellationToken ct)
    {
        SaveOrganizationOutcome validation =
            await ValidateAsync(customerId, request, null, ct);

        if (validation != SaveOrganizationOutcome.Ok)
        {
            return new SaveOrganizationResult(validation, null, []);
        }

        // A branch has no database of its own, but it is seeded into the head
        // office's — so that database has to exist and be finished.
        bool ready = await _db.CustomerDatabases.AnyAsync(
            d => d.CustomerId == customerId && d.Status == ProvisioningStatus.Ready, ct);

        if (!ready)
        {
            return new SaveOrganizationResult(
                SaveOrganizationOutcome.DatabaseNotReady, null, []);
        }

        int existing = await _db.Organizations.CountAsync(o => o.CustomerId == customerId, ct);

        int allowed = await _db.Licenses
            .Where(l => l.CustomerId == customerId)
            .Select(l => l.MaxOrganizations)
            .FirstOrDefaultAsync(ct);

        // Stored and enforced by nothing until now. A trial allows one branch,
        // which is why this is checked rather than assumed.
        if (allowed > 0 && existing >= allowed)
        {
            return new SaveOrganizationResult(
                SaveOrganizationOutcome.LicenceLimitReached, null, []);
        }

        var organization = new Organization
        {
            OrgId = Guid.NewGuid(),
            CustomerId = customerId,
            // Provisioning until its master data is written. A branch nobody can
            // use is better than one that looks ready and cannot save an item.
            Status = TenantStatus.Provisioning,
        };

        Apply(organization, request);
        _db.Organizations.Add(organization);

        // The base currency is enabled and active from creation and can never be
        // deactivated, because every posting in the branch converts to it.
        int? baseCurrencyId = await _master.FindCurrencyIdAsync(organization.BaseCurrency, ct);
        if (baseCurrencyId is int currencyId)
        {
            _db.OrgCurrencies.Add(new OrgCurrency
            {
                OrgCurrencyId = Guid.NewGuid(),
                OrgId = organization.OrgId,
                CurrencyId = currencyId,
                IsBaseCurrency = true,
                IsActive = true,
            });
        }

        await _db.SaveChangesAsync(ct);

        IReadOnlyList<string> unseeded =
            await _seeder.SeedAsync(customerId, organization.OrgId, ct);

        if (unseeded.Count > 0)
        {
            // Left as Provisioning deliberately. The row stays so the seeding can
            // be retried against it — every seed is idempotent — rather than
            // handing over a branch that looks finished and has no chart of
            // accounts behind it.
            _log.LogError(
                "Branch {OrgId} created but not seeded by: {Services}",
                organization.OrgId,
                string.Join(", ", unseeded));

            return new SaveOrganizationResult(
                SaveOrganizationOutcome.SeedingFailed, organization.OrgId, unseeded);
        }

        organization.Status = TenantStatus.Active;
        await _db.SaveChangesAsync(ct);

        return new SaveOrganizationResult(SaveOrganizationOutcome.Ok, organization.OrgId, []);
    }

    /// <summary>
    /// Retries seeding for a branch that was created but could not be finished.
    /// Every seed is idempotent, so this is safe to run repeatedly.
    /// </summary>
    public async Task<SaveOrganizationResult> RetrySeedingAsync(
        Guid customerId, Guid orgId, CancellationToken ct)
    {
        Organization? organization = await _db.Organizations
            .FirstOrDefaultAsync(o => o.CustomerId == customerId && o.OrgId == orgId, ct);

        if (organization is null)
        {
            return new SaveOrganizationResult(SaveOrganizationOutcome.NotFound, null, []);
        }

        IReadOnlyList<string> unseeded = await _seeder.SeedAsync(customerId, orgId, ct);

        if (unseeded.Count > 0)
        {
            return new SaveOrganizationResult(
                SaveOrganizationOutcome.SeedingFailed, orgId, unseeded);
        }

        organization.Status = TenantStatus.Active;
        await _db.SaveChangesAsync(ct);

        return new SaveOrganizationResult(SaveOrganizationOutcome.Ok, orgId, []);
    }

    public async Task<SaveOrganizationResult> UpdateAsync(
        Guid customerId, Guid orgId, SaveOrganizationRequest request, CancellationToken ct)
    {
        Organization? organization = await _db.Organizations
            .FirstOrDefaultAsync(o => o.CustomerId == customerId && o.OrgId == orgId, ct);

        if (organization is null)
        {
            return new SaveOrganizationResult(SaveOrganizationOutcome.NotFound, null, []);
        }

        SaveOrganizationOutcome validation =
            await ValidateAsync(customerId, request, orgId, ct);

        if (validation != SaveOrganizationOutcome.Ok)
        {
            return new SaveOrganizationResult(validation, null, []);
        }

        // Frozen: every amount already posted in this branch was converted to
        // it, so changing it would restate the whole set of books.
        if (!string.Equals(
                organization.BaseCurrency, request.BaseCurrency, StringComparison.OrdinalIgnoreCase))
        {
            return new SaveOrganizationResult(
                SaveOrganizationOutcome.BaseCurrencyLocked, null, []);
        }

        Apply(organization, request);
        await _db.SaveChangesAsync(ct);

        return new SaveOrganizationResult(SaveOrganizationOutcome.Ok, orgId, []);
    }

    /// <summary>
    /// Suspends a branch. Never deleted — its books, documents and ledger rows
    /// all live under its <c>OrgId</c>, and removing the row would leave them
    /// belonging to nothing.
    /// </summary>
    public async Task<SaveOrganizationOutcome> SuspendAsync(
        Guid customerId, Guid orgId, CancellationToken ct)
    {
        List<Organization> all = await _db.Organizations
            .Where(o => o.CustomerId == customerId)
            .OrderBy(o => o.CreatedAt)
            .ToListAsync(ct);

        Organization? organization = all.FirstOrDefault(o => o.OrgId == orgId);

        if (organization is null)
        {
            return SaveOrganizationOutcome.NotFound;
        }

        // The account has to keep one usable branch, or the next sign-in has
        // nowhere to land.
        if (all.Count > 0 && all[0].OrgId == orgId)
        {
            return SaveOrganizationOutcome.FirstOrganizationLocked;
        }

        organization.Status = TenantStatus.Suspended;
        await _db.SaveChangesAsync(ct);

        return SaveOrganizationOutcome.Ok;
    }

    private async Task<SaveOrganizationOutcome> ValidateAsync(
        Guid customerId, SaveOrganizationRequest request, Guid? orgId, CancellationToken ct)
    {
        string code = request.OrgCode.Trim().ToUpperInvariant();
        string name = request.Name.Trim();

        if (await _db.Organizations.AnyAsync(
                o => o.CustomerId == customerId
                    && o.OrgCode == code
                    && (orgId == null || o.OrgId != orgId), ct))
        {
            return SaveOrganizationOutcome.DuplicateCode;
        }

        if (await _db.Organizations.AnyAsync(
                o => o.CustomerId == customerId
                    && o.Name == name
                    && (orgId == null || o.OrgId != orgId), ct))
        {
            return SaveOrganizationOutcome.DuplicateName;
        }

        // Same rule as a contact's GSTIN: the first two digits are the state
        // code, and a mismatch splits every document's tax the wrong way.
        if (!string.IsNullOrWhiteSpace(request.Gstin) && request.StateId is int stateId)
        {
            string? stateCode = await _states.GetStateCodeAsync(stateId, ct);

            if (stateCode is not null
                && !request.Gstin.Trim().StartsWith(stateCode, StringComparison.Ordinal))
            {
                return SaveOrganizationOutcome.GstinStateMismatch;
            }
        }

        return SaveOrganizationOutcome.Ok;
    }

    private static void Apply(Organization organization, SaveOrganizationRequest request)
    {
        organization.OrgCode = request.OrgCode.Trim().ToUpperInvariant();
        organization.Name = request.Name.Trim();
        organization.BaseCurrency = request.BaseCurrency.Trim().ToUpperInvariant();
        organization.FinancialYearStartMonth = request.FinancialYearStartMonth;
        organization.Gstin = Trimmed(request.Gstin)?.ToUpperInvariant();
        organization.Pan = Trimmed(request.Pan)?.ToUpperInvariant();
        organization.AddressLine1 = Trimmed(request.AddressLine1);
        organization.AddressLine2 = Trimmed(request.AddressLine2);
        organization.City = Trimmed(request.City);
        organization.StateId = request.StateId;
        organization.PostalCode = Trimmed(request.PostalCode);
        organization.CountryId = request.CountryId;
        organization.PhoneNumber = Trimmed(request.PhoneNumber);
        organization.MobileNumber = Trimmed(request.MobileNumber);
        organization.Email = Trimmed(request.Email);
        organization.Website = Trimmed(request.Website);
    }

    private static string? Trimmed(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static OrganizationListItem Project(Organization o, bool isFirst) => new()
    {
        OrgId = o.OrgId,
        OrgCode = o.OrgCode,
        Name = o.Name,
        BaseCurrency = o.BaseCurrency,
        FinancialYearStartMonth = o.FinancialYearStartMonth,
        Gstin = o.Gstin,
        Pan = o.Pan,
        AddressLine1 = o.AddressLine1,
        AddressLine2 = o.AddressLine2,
        City = o.City,
        StateId = o.StateId,
        PostalCode = o.PostalCode,
        CountryId = o.CountryId,
        PhoneNumber = o.PhoneNumber,
        MobileNumber = o.MobileNumber,
        Email = o.Email,
        Website = o.Website,
        Status = o.Status.ToString(),
        IsFirst = isFirst,
    };
}
