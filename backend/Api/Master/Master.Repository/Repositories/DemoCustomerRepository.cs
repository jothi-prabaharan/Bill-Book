using Master.Entity.Enums;
using Master.Entity.TableEntities;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Apps;
using Shared.Kernel.Tenancy;

namespace Master.Repository.Repositories;

/// <summary>What happened to one demo customer on a run.</summary>
public sealed record DemoCustomerResult(BusinessType BusinessType, string CustomerCode, bool Created, IReadOnlyList<string> Unseeded);

/// <summary>
/// Demo customers (D-44, TK-154): one per business type, for testing and for
/// showing the product, each with one head-office branch of that type with
/// every add-on the type allows switched on, and an owner.
///
/// <b>Master data only.</b> Each branch is seeded through the same
/// <see cref="ITenantSeeder"/> fan-out any branch gets — chart of accounts,
/// tax rates, units, numbering series, reports, print templates — and nothing
/// else: no items, contacts or documents (owner, 2026-10-01).
///
/// <b>Idempotent.</b> Fixed codes and ids, so a second run finds each customer
/// and adds only what is missing: the owner's role, and whatever seed rows a
/// service did not write last time (every seed is idempotent itself).
///
/// <b>Owners follow D-45's default</b>: created with no password, so the way
/// in is forgot-password against the mailbox. No password is in source.
/// </summary>
public sealed class DemoCustomerRepository
{
    /// <summary>The business types a demo customer is made for, in order.</summary>
    public static readonly IReadOnlyList<BusinessType> BusinessTypes =
    [
        BusinessType.Retail,
        BusinessType.Pharma,
        BusinessType.Jewellery,
        BusinessType.School,
        BusinessType.Service,
        BusinessType.Restaurant,
    ];

    private readonly AdminDbContext _db;
    private readonly ITenantSeeder _seeder;
    private readonly ITenantDatabaseAllocator _databases;
    private readonly TimeProvider _clock;

    public DemoCustomerRepository(
        AdminDbContext db, ITenantSeeder seeder, ITenantDatabaseAllocator databases, TimeProvider clock)
    {
        _db = db;
        _seeder = seeder;
        _databases = databases;
        _clock = clock;
    }

    /// <summary>
    /// The demo customer's code: <c>DEMO-RTL</c> and so on. Not a number, so
    /// <c>SignupRepository</c>'s next-code guess skips it and a signup is never
    /// numbered after a demo; still a safe storage folder name.
    /// </summary>
    public static string CustomerCode(BusinessType type) => type switch
    {
        BusinessType.Retail => "DEMO-RTL",
        BusinessType.Pharma => "DEMO-PHA",
        BusinessType.Jewellery => "DEMO-JWL",
        BusinessType.School => "DEMO-SCH",
        BusinessType.Service => "DEMO-SVC",
        BusinessType.Restaurant => "DEMO-RST",
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "No demo customer for that business type."),
    };

    /// <summary>The demo customer's fixed id: <c>00000000-0000-0000-0d00-00000000000N</c>, N the business type.</summary>
    public static Guid CustomerId(BusinessType type) => Guid.Parse($"00000000-0000-0000-0d00-{(int)type:D12}");

    /// <summary>Its head-office branch's fixed id: <c>00000000-0000-0000-0d01-00000000000N</c>.</summary>
    public static Guid OrgId(BusinessType type) => Guid.Parse($"00000000-0000-0000-0d01-{(int)type:D12}");

    /// <summary>
    /// The demo owner's sign-in, one per demo customer, made from one mailbox
    /// by plus-addressing: <c>you@example.com</c> becomes
    /// <c>you+demo-retail@example.com</c>, so every reset lands in the same
    /// inbox. Null when <paramref name="mailbox"/> is not an address.
    /// </summary>
    public static string? OwnerEmail(string? mailbox, BusinessType type)
    {
        string trimmed = (mailbox ?? string.Empty).Trim();
        int at = trimmed.LastIndexOf('@');
        if (at <= 0 || at == trimmed.Length - 1 || trimmed.Contains(' '))
        {
            return null;
        }

        string local = trimmed[..at];
        int plus = local.IndexOf('+');
        if (plus >= 0)
        {
            local = local[..plus];
        }

        return $"{local}+demo-{type.ToString().ToLowerInvariant()}{trimmed[at..]}";
    }

    /// <summary>The branch's add-ons: everything its type allows.</summary>
    public static AddOn AddOnsFor(BusinessType type) => AddOnRules.Allowed(type);

    /// <summary>
    /// Makes sure every demo customer exists and is seeded. Each one is done on
    /// its own, so one service being down leaves the others finished and the
    /// failed one reported for the next run.
    /// </summary>
    public async Task<IReadOnlyList<DemoCustomerResult>> EnsureAllAsync(string mailbox, CancellationToken ct)
    {
        List<DemoCustomerResult> results = [];
        foreach (BusinessType type in BusinessTypes)
        {
            results.Add(await EnsureAsync(type, mailbox, ct));
        }

        return results;
    }

    private async Task<DemoCustomerResult> EnsureAsync(BusinessType type, string mailbox, CancellationToken ct)
    {
        Guid customerId = CustomerId(type);
        Guid orgId = OrgId(type);
        string email = OwnerEmail(mailbox, type)
            ?? throw new ArgumentException("The demo owners' mailbox is not an email address.", nameof(mailbox));

        Customer? customer = await _db.Customers.FirstOrDefaultAsync(c => c.CustomerId == customerId, ct);
        bool created = customer is null;

        if (customer is null)
        {
            customer = await CreateAsync(type, customerId, orgId, ct);
        }

        await EnsureOwnerAsync(orgId, email, type, ct);

        IReadOnlyList<string> unseeded = await _seeder.SeedAsync(customerId, orgId, type, AddOnsFor(type), ct);

        if (unseeded.Count == 0)
        {
            customer.Status = TenantStatus.Active;
            await _db.Organizations.Where(o => o.OrgId == orgId)
                .ExecuteUpdateAsync(s => s.SetProperty(o => o.Status, TenantStatus.Active), ct);
            await _db.SaveChangesAsync(ct);
        }

        return new DemoCustomerResult(type, customer.CustomerCode, created, unseeded);
    }

    private async Task<Customer> CreateAsync(BusinessType type, Guid customerId, Guid orgId, CancellationToken ct)
    {
        DateOnly today = DateOnly.FromDateTime(_clock.GetUtcNow().UtcDateTime);

        // A pooled shard like any small customer. A fresh install has none
        // until one is provisioned, so a demo shares the Internal customer's
        // database instead — both are ours, and isolation inside a shard is the
        // query filter and RLS either way.
        string databaseName = await _databases.AllocateAsync(PlanTier.Trial, ct)
            ?? await _db.Customers
                .Where(c => c.CustomerId == InternalCustomerRepository.CustomerId)
                .Select(c => c.DatabaseName)
                .FirstOrDefaultAsync(ct)
            ?? throw new NoTenantCapacityException(PlanTier.Trial);

        (LicenseType licenceType, DateOnly expiry) = CustomerKindRepository.FirstLicence(CustomerKind.Demo, today, 0);
        string name = $"Demo {type}";

        var customer = new Customer
        {
            CustomerId = customerId,
            CustomerCode = CustomerCode(type),
            CountryPrefix = "IN",
            Name = name,
            BillingEmail = $"{CustomerCode(type).ToLowerInvariant()}@demo.invalid",
            Status = TenantStatus.Provisioning,
            PlanTier = CustomerKindRepository.FirstPlanTier(CustomerKind.Demo),
            Kind = CustomerKind.Demo,
            DatabaseName = databaseName,
        };
        _db.Customers.Add(customer);

        var licence = new License
        {
            LicenseId = Guid.NewGuid(),
            CustomerId = customerId,
            LicenseType = licenceType,
            StartDate = today,
            ExpiryDate = expiry,
            MaxOrganizations = CustomerKindRepository.UnbilledMaxOrganizations,
            IsActive = true,
        };
        PlanCatalogRepository.Apply(licence);
        _db.Licenses.Add(licence);

        int? defaultStateId = await _db.States
            .Where(s => s.CountryId == 1 && s.StateCode == "33")
            .Select(s => (int?)s.StateId)
            .FirstOrDefaultAsync(ct);

        _db.Organizations.Add(new Organization
        {
            OrgId = orgId,
            CustomerId = customerId,
            OrgCode = "HO",
            Name = name,
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
        return customer;
    }

    /// <summary>The owner, with no password (D-45), holding the Owner role in the demo branch.</summary>
    private async Task EnsureOwnerAsync(Guid orgId, string email, BusinessType type, CancellationToken ct)
    {
        User? user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);
        if (user is null)
        {
            user = new User
            {
                UserId = Guid.NewGuid(),
                Email = email,
                DisplayName = $"Demo {type} owner",
                PasswordHash = null,
                EmailConfirmed = false,
                IsActive = true,
            };
            _db.Users.Add(user);
        }

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

        await _db.SaveChangesAsync(ct);
    }
}
