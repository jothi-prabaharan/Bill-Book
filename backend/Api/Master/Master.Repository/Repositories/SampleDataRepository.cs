using Master.Entity.Enums;
using Master.Entity.TableEntities;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Apps;
using Shared.Kernel.Tenancy;

namespace Master.Repository.Repositories;

/// <summary>Who the sample user is: from configuration, never from source.</summary>
public sealed record SampleUser(string Email, string DisplayName, string? Password);

/// <summary>One sample branch after a run: its business type, its code, and the services that did not seed it yet.</summary>
public sealed record SampleBranchResult(BusinessType BusinessType, string OrgCode, IReadOnlyList<string> Unseeded);

/// <summary>What a run did: whether it made the customer and the user, and where each branch stands.</summary>
public sealed record SampleDataResult(bool CustomerCreated, bool UserCreated, IReadOnlyList<SampleBranchResult> Branches);

/// <summary>
/// The sample data (TK-185): one sample customer with one branch for every
/// <see cref="BusinessType"/>, and one sample user who is Owner in all of them,
/// so a developer signs in once and can switch between every kind of business.
///
/// <b>One customer, several branches</b>, unlike the demo customers
/// (<see cref="DemoCustomerRepository"/>), which are one customer per business
/// type each with its own owner. A Demo-kind customer, so it never lapses, is
/// never billed and has no branch cap (D-44).
///
/// <b>Master data only.</b> Each branch is seeded through the same
/// <see cref="ITenantSeeder"/> fan-out any branch gets — chart of accounts, tax
/// rates, units, numbering series, reports, print templates — and nothing else.
///
/// <b>Idempotent.</b> Fixed ids and codes, so a second run finds what the first
/// made and adds only what is missing: a branch, a role, or the seed rows a
/// service did not write last time (every seed is idempotent itself).
///
/// <b>No credential is in source.</b> The user's address, name and password come
/// from configuration. Without a password the user is created with none, and
/// the way in is forgot-password (D-45). An existing user's password is never
/// touched, so pointing this at an address that already signs in only adds the
/// sample branches to that account.
/// </summary>
public sealed class SampleDataRepository
{
    /// <summary>The sample customer's fixed id.</summary>
    public static readonly Guid CustomerId = Guid.Parse("00000000-0000-0000-5a00-000000000001");

    /// <summary>
    /// The sample customer's code. Not a number, so <c>SignupRepository</c>'s
    /// next-code guess skips it, and a safe storage folder name.
    /// </summary>
    public const string CustomerCode = "SAMPLE";

    /// <summary>The sample customer's name, which its branches' names start with.</summary>
    public const string CustomerName = "Sample Business";

    private readonly AdminDbContext _db;
    private readonly ITenantSeeder _seeder;
    private readonly IPasswordHasherRepository _hasher;
    private readonly TimeProvider _clock;

    public SampleDataRepository(
        AdminDbContext db,
        ITenantSeeder seeder,
        IPasswordHasherRepository hasher,
        TimeProvider clock)
    {
        _db = db;
        _seeder = seeder;
        _hasher = hasher;
        _clock = clock;
    }

    /// <summary>
    /// Every business type the enum defines, in its order. Read from the enum
    /// rather than listed here, so a business type added later gets a sample
    /// branch without anyone remembering to add it.
    /// </summary>
    public static IReadOnlyList<BusinessType> BusinessTypes { get; } =
        Enum.GetValues<BusinessType>().Where(AddOnRules.IsDefined).ToList();

    /// <summary>The branch's fixed id: <c>00000000-0000-0000-5a01-00000000000N</c>, N the business type.</summary>
    public static Guid OrgId(BusinessType type) => Guid.Parse($"00000000-0000-0000-5a01-{(int)type:D12}");

    /// <summary>
    /// The branch's code, which every document number it issues carries
    /// (<c>INV/2526/RTL/00042</c>): the same three letters the demo customers'
    /// codes end in, and for a business type added later its first three.
    /// </summary>
    public static string OrgCode(BusinessType type) => type switch
    {
        BusinessType.Retail => "RTL",
        BusinessType.Pharma => "PHA",
        BusinessType.Jewellery => "JWL",
        BusinessType.School => "SCH",
        BusinessType.Service => "SVC",
        BusinessType.Restaurant => "RST",
        _ => type.ToString()[..Math.Min(3, type.ToString().Length)].ToUpperInvariant(),
    };

    /// <summary>The branch's name: <c>Sample Retail</c> and so on.</summary>
    public static string OrgName(BusinessType type) => $"Sample {type}";

    /// <summary>The branch's add-ons: everything its type allows, so every screen is there to try.</summary>
    public static AddOn AddOnsFor(BusinessType type) => AddOnRules.Allowed(type);

    /// <summary>
    /// The sample user from configuration: <c>Bootstrap:SampleUserEmail</c>,
    /// <c>Bootstrap:SampleUserName</c> (default "Sample User") and
    /// <c>Bootstrap:SampleUserPassword</c> (optional). Null when there is no
    /// address, or it is not one.
    /// </summary>
    public static SampleUser? UserFrom(string? email, string? displayName, string? password)
    {
        string trimmed = (email ?? string.Empty).Trim();
        int at = trimmed.LastIndexOf('@');
        if (at <= 0 || at == trimmed.Length - 1 || trimmed.Contains(' '))
        {
            return null;
        }

        string name = string.IsNullOrWhiteSpace(displayName) ? "Sample User" : displayName.Trim();
        return new SampleUser(trimmed, name, string.IsNullOrEmpty(password) ? null : password);
    }

    /// <summary>
    /// The sample user from configuration, falling back to the bootstrap
    /// owner's settings for anything not given: <c>Bootstrap:SampleUserEmail</c>
    /// or <c>Bootstrap:OwnerEmail</c>, and the same for the name and password.
    /// Master's startup and <c>SampleDataBootstrapWorker</c> both read it here,
    /// so the two agree on who the sample user is.
    /// </summary>
    public static SampleUser? UserFrom(IConfiguration config) =>
        UserFrom(
            config["Bootstrap:SampleUserEmail"] ?? config["Bootstrap:OwnerEmail"],
            config["Bootstrap:SampleUserName"] ?? config["Bootstrap:OwnerName"],
            config["Bootstrap:SampleUserPassword"] ?? config["Bootstrap:OwnerPassword"]);

    /// <summary>
    /// Makes sure the sample customer, every sample branch and the sample user
    /// exist, the user is Owner in every branch, and each branch is seeded.
    /// Each branch is seeded on its own, so one service being down leaves the
    /// others finished and the rest reported for the next run.
    /// </summary>
    public async Task<SampleDataResult> EnsureAsync(SampleUser user, CancellationToken ct)
    {
        (bool customerCreated, bool userCreated) = await EnsureRowsAsync(user, ct);

        List<SampleBranchResult> branches = [];
        foreach (BusinessType type in BusinessTypes)
        {
            Guid orgId = OrgId(type);
            IReadOnlyList<string> unseeded = await _seeder.SeedAsync(CustomerId, orgId, type, AddOnsFor(type), ct);

            if (unseeded.Count == 0)
            {
                await _db.Organizations
                    .Where(o => o.OrgId == orgId && o.Status != TenantStatus.Active)
                    .ExecuteUpdateAsync(s => s.SetProperty(o => o.Status, TenantStatus.Active), ct);
            }

            branches.Add(new SampleBranchResult(type, OrgCode(type), unseeded));
        }

        // Sign-in waits for every branch, the way it waits for any customer
        // still provisioning.
        if (branches.All(b => b.Unseeded.Count == 0))
        {
            await _db.Customers
                .Where(c => c.CustomerId == CustomerId && c.Status != TenantStatus.Active)
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.Status, TenantStatus.Active), ct);
        }

        return new SampleDataResult(customerCreated, userCreated, branches);
    }

    /// <summary>
    /// The rows only — customer, licence, branches, user and Owner roles — and
    /// no seeding. Master's startup calls this before it listens (TK-186): the
    /// seed fan-out calls every other service over HTTP, and before Master
    /// listens those are still starting, so the fan-out is left to
    /// <c>SampleDataBootstrapWorker</c>, which runs once the host is up.
    /// </summary>
    public async Task<(bool CustomerCreated, bool UserCreated)> EnsureRowsAsync(SampleUser user, CancellationToken ct)
    {
        bool customerCreated = await EnsureCustomerAsync(ct);

        foreach (BusinessType type in BusinessTypes)
        {
            await EnsureBranchAsync(type, ct);
        }

        bool userCreated = await EnsureUserAsync(user, ct);
        return (customerCreated, userCreated);
    }

    private async Task<bool> EnsureCustomerAsync(CancellationToken ct)
    {
        if (await _db.Customers.AnyAsync(c => c.CustomerId == CustomerId, ct))
        {
            return false;
        }

        DateOnly today = DateOnly.FromDateTime(_clock.GetUtcNow().UtcDateTime);

        // The first shard, which every start creates and migrates, and never a
        // pooled one. A branch's master data is written by each service's
        // internal seed endpoint, and those build their context before the
        // request's customer is known, so they write to the configured tenant
        // database whatever shard the customer is in. A sample customer in a
        // pool would sign in to empty books. Isolation inside a shard is the
        // query filter and RLS either way.
        string databaseName = await _db.TenantDatabases
                .OrderBy(d => d.DatabaseName)
                .Select(d => d.DatabaseName)
                .FirstOrDefaultAsync(ct)
            ?? throw new NoTenantCapacityException(PlanTier.Trial);

        (LicenseType licenceType, DateOnly expiry) = CustomerKindRepository.FirstLicence(CustomerKind.Demo, today, 0);

        _db.Customers.Add(new Customer
        {
            CustomerId = CustomerId,
            CustomerCode = CustomerCode,
            CountryPrefix = "IN",
            Name = CustomerName,
            BillingEmail = "sample@sample.invalid",
            Status = TenantStatus.Provisioning,
            PlanTier = CustomerKindRepository.FirstPlanTier(CustomerKind.Demo),
            Kind = CustomerKind.Demo,
            DatabaseName = databaseName,
        });

        var licence = new License
        {
            LicenseId = Guid.NewGuid(),
            CustomerId = CustomerId,
            LicenseType = licenceType,
            StartDate = today,
            ExpiryDate = expiry,
            MaxOrganizations = CustomerKindRepository.UnbilledMaxOrganizations,
            IsActive = true,
        };
        PlanCatalogRepository.Apply(licence);
        _db.Licenses.Add(licence);

        await _db.SaveChangesAsync(ct);
        return true;
    }

    private async Task EnsureBranchAsync(BusinessType type, CancellationToken ct)
    {
        Guid orgId = OrgId(type);
        if (await _db.Organizations.AnyAsync(o => o.OrgId == orgId, ct))
        {
            return;
        }

        int? defaultStateId = await _db.States
            .Where(s => s.CountryId == 1 && s.StateCode == "33")
            .Select(s => (int?)s.StateId)
            .FirstOrDefaultAsync(ct);

        _db.Organizations.Add(new Organization
        {
            OrgId = orgId,
            CustomerId = CustomerId,
            OrgCode = OrgCode(type),
            Name = OrgName(type),
            BusinessType = type,
            Vertical = Enum.Parse<Vertical>(type.ToString()),
            AddOns = AddOnsFor(type),
            BaseCurrency = "INR",
            FinancialYearStartMonth = 4,
            CountryId = 1,
            StateId = defaultStateId,
            Status = TenantStatus.Provisioning,
        });

        int? inr = await _db.Currencies.Where(c => c.Code == "INR").Select(c => (int?)c.CurrencyId).FirstOrDefaultAsync(ct);
        if (inr is int currencyId)
        {
            _db.OrgCurrencies.Add(new OrgCurrency
            {
                OrgCurrencyId = Guid.NewGuid(),
                OrgId = orgId,
                CurrencyId = currencyId,
                IsBaseCurrency = true,
                IsActive = true,
            });
        }

        await _db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// The sample user, created once, holding the Owner role in every sample
    /// branch. Found by address, so an account that already exists keeps its
    /// own password and name and only gains the roles.
    /// </summary>
    private async Task<bool> EnsureUserAsync(SampleUser sample, CancellationToken ct)
    {
        User? user = await _db.Users.FirstOrDefaultAsync(u => u.Email == sample.Email, ct);
        bool created = user is null;

        if (user is null)
        {
            user = new User
            {
                UserId = Guid.NewGuid(),
                Email = sample.Email,
                DisplayName = sample.DisplayName,
                PasswordHash = sample.Password is null ? null : _hasher.Hash(sample.Password),
                EmailConfirmed = false,
                IsActive = true,
            };
            _db.Users.Add(user);
        }

        foreach (BusinessType type in BusinessTypes)
        {
            Guid orgId = OrgId(type);
            if (!await _db.UserOrganizationRoles.AnyAsync(
                    a => a.UserId == user.UserId && a.OrgId == orgId && a.RoleId == AdminDbContext.OwnerRoleId, ct))
            {
                _db.UserOrganizationRoles.Add(new UserOrganizationRole
                {
                    UserId = user.UserId,
                    OrgId = orgId,
                    RoleId = AdminDbContext.OwnerRoleId,
                    IsActive = true,
                });
            }
        }

        await _db.SaveChangesAsync(ct);
        return created;
    }
}
