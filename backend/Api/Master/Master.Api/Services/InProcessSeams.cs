using Master.Entity.Enums;
using Master.Entity.TableEntities;
using Master.Repository;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Interfaces;
using Shared.Kernel.Numbering;
using Shared.Kernel.Tenancy;

namespace Master.Api.Services;

// The seams that used to cross a service boundary and no longer do.
//
// Platform, Identity and Master were three services over one database, and every
// question that spanned two of them was an HTTP call: which customer owns this
// branch, what database is it in, create the owner user, what is this currency's
// id. Each had an interface, a client, a DTO and an internal controller to
// answer it — four pieces of machinery per question, all to reach a table in the
// same Postgres instance the caller was already connected to.
//
// The interfaces are kept. They are not ceremony now: IFinancialYearProvider is
// a Shared.Kernel contract that other services still satisfy over HTTP, and the
// rest name a real seam — the internal controllers stay, because Inventory,
// Sales and Accounting still call them.
//
// ITenantDirectory and InProcessTenantDirectory, which used to answer "which
// database is this customer's" for ContactsDbContext's per-request connection
// resolution, are gone along with that resolution: every service now points at
// the one shared tenant database, fixed at startup.

/// <summary>
/// Currency reference data, for the join between mst.Currencies and the
/// per-organization currency list that was mst.OrgCurrencies. Both tables are in
/// the mst schema now, so this is a query rather than the C#-side join two
/// services had to do across an API.
/// </summary>
public sealed class MasterCurrencies : IMasterCurrencies
{
    private readonly AdminDbContext _db;

    public MasterCurrencies(AdminDbContext db) => _db = db;

    public async Task<IReadOnlyList<MasterCurrency>> GetAllAsync(CancellationToken ct = default) =>
        await _db.Currencies
            .Where(c => c.IsActive)
            .OrderBy(c => c.Code)
            .Select(c => new MasterCurrency
            {
                CurrencyId = c.CurrencyId,
                Code = c.Code,
                Name = c.Name,
                Symbol = c.Symbol,
                Format = c.Format,
                DecimalPlaces = c.DecimalPlaces,
            })
            .ToListAsync(ct);

    /// <summary>
    /// A single lookup rather than fetching every currency and scanning the list
    /// in memory, which is what the HTTP client had to do — it could only ask for
    /// all of them.
    /// </summary>
    public async Task<int?> FindCurrencyIdAsync(string code, CancellationToken ct = default)
    {
        int id = await _db.Currencies
            .Where(c => c.Code.ToUpper() == code.ToUpper())
            .Select(c => c.CurrencyId)
            .FirstOrDefaultAsync(ct);

        return id == 0 ? null : id;
    }
}

/// <summary>
/// Creates the owner user for a newly provisioned customer.
///
/// Idempotent on email, and it has to be: provisioning retries, and a second
/// attempt must neither fail nor produce a second owner. Same logic as
/// <c>InternalUsersController</c>, which stays for callers outside this service.
/// </summary>
public sealed class InProcessIdentityAdmin : IIdentityAdmin
{
    /// <summary>Seeded system-role id for Owner (see AdminDbContext seed).</summary>
    private const int OwnerRoleId = 1;

    private readonly AdminDbContext _db;
    private readonly IPasswordHasher _hasher;

    public InProcessIdentityAdmin(AdminDbContext db, IPasswordHasher hasher)
    {
        _db = db;
        _hasher = hasher;
    }

    public async Task CreateOwnerUserAsync(CreateOwnerUser request, CancellationToken ct = default)
    {
        User? user = await _db.Users.FirstOrDefaultAsync(u => u.Email == request.Email, ct);

        if (user is null)
        {
            user = new User
            {
                UserId = Guid.NewGuid(),
                Email = request.Email,
                PasswordHash = _hasher.Hash(request.Password),
                DisplayName = request.DisplayName,
                MobileNumber = request.MobileNumber,
                EmailConfirmed = false,
                IsActive = true,
            };

            _db.Users.Add(user);
        }

        bool assigned = await _db.UserOrganizationRoles.AnyAsync(
            a => a.UserId == user.UserId && a.OrgId == request.OrgId && a.RoleId == OwnerRoleId, ct);

        if (!assigned)
        {
            _db.UserOrganizationRoles.Add(new UserOrganizationRole
            {
                UserId = user.UserId,
                OrgId = request.OrgId,
                RoleId = OwnerRoleId,
                IsActive = true,
            });
        }

        await _db.SaveChangesAsync(ct);
    }
}

/// <summary>
/// The branch's financial year start month, for composing document numbers.
///
/// Contact codes are allocated in this service and need it. Other services read
/// the same number from the internal org-context endpoint over HTTP and cache it
/// for hours; here it is a column on a table this context already maps.
/// </summary>
public sealed class InProcessFinancialYearProvider : IFinancialYearProvider
{
    /// <summary>April, the Indian financial year. Used when the branch is unknown.</summary>
    private const int DefaultStartMonth = 4;

    private readonly AdminDbContext _db;
    private readonly ICurrentUser _user;

    public InProcessFinancialYearProvider(AdminDbContext db, ICurrentUser user)
    {
        _db = db;
        _user = user;
    }

    public async Task<int> GetStartMonthAsync(CancellationToken ct = default)
    {
        if (_user.OrgId is not Guid orgId)
        {
            return DefaultStartMonth;
        }

        int month = await _db.Organizations
            .Where(o => o.OrgId == orgId)
            .Select(o => o.FinancialYearStartMonth)
            .FirstOrDefaultAsync(ct);

        return month == 0 ? DefaultStartMonth : month;
    }
}
