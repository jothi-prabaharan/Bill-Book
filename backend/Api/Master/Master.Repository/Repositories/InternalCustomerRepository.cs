using Master.Entity.Enums;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Apps;
using Shared.Kernel.Tenancy;

namespace Master.Repository.Repositories;

/// <summary>
/// The Internal customer: our own organisation (D-44, TK-153). It is the
/// customer the first start of an install creates, with fixed ids, and its
/// head-office branch is a <see cref="BusinessType.Service"/> business.
///
/// An install that predates TK-153 already has that customer as a Standard
/// one on a ten-year licence; <see cref="ConvertAsync"/> makes it Internal
/// once, and every later start finds nothing to do.
/// </summary>
public sealed class InternalCustomerRepository
{
    /// <summary>The bootstrap customer's id, and its head-office branch's.</summary>
    public static readonly Guid CustomerId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    /// <inheritdoc cref="CustomerId"/>
    public static readonly Guid OrgId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    public const string CustomerCode = "0000000001";

    public const string BranchCode = "HO";

    /// <summary>The Internal customer's trade.</summary>
    public const BusinessType BranchBusinessType = BusinessType.Service;

    private readonly AdminDbContext _db;

    public InternalCustomerRepository(AdminDbContext db) => _db = db;

    /// <summary>
    /// The add-ons named by <c>Bootstrap:InternalAddOns</c> ("Hrms, Payroll",
    /// say; none by default), keeping only those a Service branch allows.
    /// Pure. <paramref name="problem"/> says what was dropped and why, for the
    /// start-up log, so a mistyped setting is seen rather than silently ignored.
    /// </summary>
    public static AddOn AddOnsFrom(string? setting, out string? problem)
    {
        problem = null;

        if (!AddOnRules.TryParseAddOn(setting, out AddOn named))
        {
            problem = $"Bootstrap:InternalAddOns '{setting}' is not a list of add-ons, so none were switched on.";
            return AddOn.None;
        }

        AddOn allowed = AddOnRules.Allowed(BranchBusinessType);
        AddOn refused = named & ~allowed;
        if (refused != AddOn.None)
        {
            problem = $"A Service branch cannot have {refused}, so it was left off.";
        }

        return named & allowed;
    }

    /// <summary>
    /// Makes an existing bootstrap customer Internal, once: the kind, an Elite
    /// licence that never lapses with no user or branch cap, its branches
    /// freed of their own expiry, and the head office made a Service branch
    /// with <paramref name="addOns"/>. Returns false, and writes nothing, when
    /// there is no such customer or it is Internal already — so a second start,
    /// or an operator's later change to the branch, is left alone.
    /// </summary>
    public async Task<bool> ConvertAsync(Guid customerId, Guid headOfficeId, AddOn addOns, CancellationToken ct)
    {
        Entity.TableEntities.Customer? customer = await _db.Customers
            .FirstOrDefaultAsync(c => c.CustomerId == customerId, ct);

        if (customer is null || customer.Kind == CustomerKind.Internal)
        {
            return false;
        }

        customer.Kind = CustomerKind.Internal;
        customer.PlanTier = PlanTier.Elite;
        if (customer.Status == TenantStatus.Expired)
        {
            customer.Status = TenantStatus.Active;
        }

        foreach (Entity.TableEntities.License licence in await _db.Licenses
            .Where(l => l.CustomerId == customerId).ToListAsync(ct))
        {
            licence.LicenseType = LicenseType.Elite;
            licence.ExpiryDate = CustomerKindRepository.NoExpiry;
            PlanCatalogRepository.Apply(licence);
            licence.MaxOrganizations = Math.Max(licence.MaxOrganizations, CustomerKindRepository.UnbilledMaxOrganizations);
            licence.IsActive = true;
        }

        foreach (Entity.TableEntities.Organization branch in await _db.Organizations
            .Where(o => o.CustomerId == customerId).ToListAsync(ct))
        {
            branch.ExpiryDate = null;
            branch.IsTrial = false;

            if (branch.OrgId == headOfficeId)
            {
                branch.BusinessType = BranchBusinessType;
                branch.Vertical = Vertical.Service;
                branch.AddOns = addOns;
            }
        }

        await _db.SaveChangesAsync(ct);
        return true;
    }
}
