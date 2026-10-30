using Master.Repository.Clients;
using Microsoft.EntityFrameworkCore;
using Master.Entity.Enums;
using Master.Entity.Models;
using Master.Repository;
using Shared.Kernel.Apps;

namespace Master.Repository.Repositories;

/// <summary>Resolves what Identity needs at login: customer, database readiness, licence.</summary>
public sealed class OrgContextRepository
{
    private readonly AdminDbContext _db;
    private readonly TimeProvider _clock;

    public OrgContextRepository(AdminDbContext db, TimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    /// <summary>
    /// The branch, its customer and the customer's one licence (D-36). A
    /// customer with no licence resolves with
    /// <see cref="LicenseStatus.NotLicensed"/>, never null, because the branch
    /// itself exists and services read its GSTIN and address from here.
    /// </summary>
    /// <summary>The configuration key that holds a branch's UPI id (TK-158).</summary>
    public const string UpiIdConfigCode = "payment.upiId";

    public async Task<OrgContextResponse?> ResolveAsync(Guid orgId, CancellationToken ct)
    {
        var row = await (
            from o in _db.Organizations
            join c in _db.Customers on o.CustomerId equals c.CustomerId
            join l in _db.Licenses on c.CustomerId equals l.CustomerId into lGroup
            from l in lGroup.DefaultIfEmpty()
            join s in _db.States on o.StateId equals s.StateId into sGroup
            from s in sGroup.DefaultIfEmpty()
            where o.OrgId == orgId
            select new
            {
                o.OrgId,
                o.CustomerId,
                c.CustomerCode,
                c.DatabaseName,
                OrgName = o.Name,
                o.FinancialYearStartMonth,
                o.BaseCurrency,
                o.DiscountBeforeTax,
                o.Vertical,
                o.BusinessType,
                o.AddOns,
                o.Gstin,
                o.AddressLine1,
                o.AddressLine2,
                o.City,
                o.PostalCode,
                o.EInvoiceFrom,
                o.EwayBillEnabled,
                o.BudgetWarningYearToDate,
                o.PhoneNumber,
                o.Email,
                StateCode = s != null ? s.StateCode : null,
                OrgExpiryDate = o.ExpiryDate,
                CustomerStatus = c.Status,
                CustomerPlanTier = c.PlanTier,
                CustomerKind = c.Kind,
                HasLicence = l != null,
                ExpiryDate = l != null ? l.ExpiryDate : DateOnly.MinValue,
                GraceDays = l != null ? l.GraceDays : 0,
                LicenseType = l != null ? l.LicenseType : LicenseType.Trial,
                LicenseActive = l != null && l.IsActive,
                MaxUsers = l != null ? l.MaxUsers : 0,
                SalesLimit = l != null ? l.MonthlySalesTransactionLimit : 0,
                PurchaseLimit = l != null ? l.MonthlyPurchaseTransactionLimit : 0,
            }).FirstOrDefaultAsync(ct);

        if (row is null)
        {
            return null;
        }

        // The branch's active currencies, by code. The base currency is always among them, even
        // when its OrgCurrency row has been switched off, because the books are kept in it.
        List<string> currencyCodes = await (
            from oc in _db.OrgCurrencies
            join cur in _db.Currencies on oc.CurrencyId equals cur.CurrencyId
            where oc.OrgId == orgId && oc.IsActive
            select cur.Code).ToListAsync(ct);
        if (row.BaseCurrency is { Length: 3 } baseCode && !currencyCodes.Contains(baseCode))
        {
            currencyCodes.Add(baseCode);
        }

        DateOnly today = DateOnly.FromDateTime(_clock.GetUtcNow().UtcDateTime);

        // Internal and Demo customers are never billed, so they never lapse,
        // whatever dates their licence and branches carry (D-44, TK-152).
        bool neverLapses = CustomerKindRepository.NeverLapses(row.CustomerKind);

        // The branch's own date can only bring access forward, never push it
        // out — otherwise a branch would be a way to outlive the licence paying
        // for it. So the effective date is whichever ends first.
        DateOnly effectiveExpiry =
            row.OrgExpiryDate is DateOnly branchExpiry && branchExpiry < row.ExpiryDate
                ? branchExpiry
                : row.ExpiryDate;

        // Grace is applied once, to whichever date ended access. Applying it to
        // the licence alone would let a wound-down branch keep working past the
        // date it was wound down on.
        bool expired = !neverLapses && today > effectiveExpiry.AddDays(row.GraceDays);

        // Expiry blocks the app, never the login — the caller gates on this.
        // That is deliberate for a branch too: a user whose Chennai branch has
        // ended still has to sign in to see why, and to switch to one that
        // has not.
        LicenseStatus licenseStatus = !row.HasLicence
            ? LicenseStatus.NotLicensed
            : !row.LicenseActive
            ? LicenseStatus.Suspended
            : expired
                ? LicenseStatus.Expired
                : row.LicenseType == LicenseType.Trial
                    ? LicenseStatus.Trial
                    : LicenseStatus.Active;

        // Only the licence's own expiry marks the account expired. A branch that
        // has been wound down says nothing about the account paying for it —
        // stamping the customer here would expire a head office because one
        // seasonal counter reached its end date.
        //
        // The account is expired only when every licence it holds is.
        bool licenceItselfExpired = !neverLapses && row.HasLicence && today > row.ExpiryDate.AddDays(row.GraceDays);

        if (licenceItselfExpired
            && row.CustomerStatus != TenantStatus.Expired
            && !await _db.Licenses.AnyAsync(
                x => x.CustomerId == row.CustomerId && x.ExpiryDate.AddDays(x.GraceDays) >= today, ct))
        {
            // Stamp lazily at first observation; no nightly job required.
            Entity.TableEntities.Customer customer =
                await _db.Customers.FirstAsync(c => c.CustomerId == row.CustomerId, ct);
            customer.Status = TenantStatus.Expired;
            await _db.SaveChangesAsync(ct);
        }

        // The branch's UPI id (TK-158): its own value, else the shipped default, which is empty.
        string? upiId = await _db.Configurations.AsNoTracking()
            .Where(c => c.Code == UpiIdConfigCode && (c.OrgId == orgId || c.OrgId == null))
            .OrderBy(c => c.OrgId == null)
            .Select(c => c.Value)
            .FirstOrDefaultAsync(ct);

        // State code for GST and place of supply. Prefer the linked State's code
        // (normalized to 2-digit GST code), or derive from the branch's GSTIN.
        string? stateCode = row.StateCode;
        if (string.IsNullOrWhiteSpace(stateCode))
        {
            stateCode = Shared.Kernel.Tax.Gstin.StateCodeOf(row.Gstin);
        }
        else
        {
            stateCode = Shared.Kernel.Tax.IndianStateCodes.ToGstCode(stateCode) ?? stateCode;
        }

        if (string.IsNullOrWhiteSpace(stateCode) && (row.BaseCurrency == "INR" || string.IsNullOrWhiteSpace(row.BaseCurrency)))
        {
            stateCode = "33";
        }

        return new OrgContextResponse
        {
            OrgId = row.OrgId,
            CustomerId = row.CustomerId,
            CustomerCode = row.CustomerCode,
            DatabaseName = row.DatabaseName,
            OrgName = row.OrgName,
            // No per-customer database to be ready any more — "ready" now means
            // this customer's own seeding finished (Trial/Active) rather than
            // never having started (Provisioning) or having failed (Failed).
            // Suspended and Expired both still count as ready: expiry blocks the
            // app below, never the login itself, per the comment there.
            DatabaseReady = row.CustomerStatus is not (TenantStatus.Provisioning or TenantStatus.Failed),
            LicenseStatus = licenseStatus.ToString(),
            // The date that actually governs, which is the one the user needs to
            // be shown. Reporting the licence date while refusing on the branch
            // date would put a future date on the screen that says access ended.
            // An unbilled customer has no date to show.
            LicenseExpiry = row.HasLicence && !neverLapses ? effectiveExpiry : null,
            // Which of the two dates it is. Without this the caller cannot tell
            // "your licence has lapsed, renew it" from "this branch was wound
            // down, talk to your head office" — and telling a paying customer
            // to renew a licence that is perfectly valid is the worse message.
            ExpiryIsBranchLevel = !neverLapses && row.HasLicence && row.OrgExpiryDate is DateOnly branchDate && branchDate < row.ExpiryDate,
            // An unbilled customer is held to none of a plan's limits (D-44); a
            // customer with no licence at all is held to zero of them, never to
            // none, which is what an empty limit means.
            MaxUsers = neverLapses ? null : row.MaxUsers,
            LicenseType = row.LicenseType,
            PlanName = PlanCatalogRepository.Name(row.LicenseType),
            MonthlySalesTransactionLimit = neverLapses ? null : row.SalesLimit,
            MonthlyPurchaseTransactionLimit = neverLapses ? null : row.PurchaseLimit,
            FinancialYearStartMonth = row.FinancialYearStartMonth,
            BaseCurrency = row.BaseCurrency,
            ActiveCurrencyCodes = currencyCodes,
            StateCode = stateCode,
            DiscountBeforeTax = row.DiscountBeforeTax,
            Vertical = row.Vertical.ToString(),
            BusinessType = row.BusinessType,
            AddOns = row.AddOns,
            PlanTier = row.CustomerPlanTier,
            // The seller block on a printed tax invoice. Sales cannot read
            // mst.Organizations, so the only alternative to carrying it here is
            // the placeholder it used to print — an invoice headed "Our Company"
            // with somebody else's GSTIN under it.
            Gstin = row.Gstin,
            AddressLine1 = row.AddressLine1,
            AddressLine2 = row.AddressLine2,
            City = row.City,
            PostalCode = row.PostalCode,
            EInvoiceFrom = row.EInvoiceFrom,
            EwayBillEnabled = row.EwayBillEnabled,
            BudgetWarningYearToDate = row.BudgetWarningYearToDate,
            PhoneNumber = row.PhoneNumber,
            Email = row.Email,
            UpiId = string.IsNullOrWhiteSpace(upiId) ? null : upiId.Trim(),
        };
    }
}
