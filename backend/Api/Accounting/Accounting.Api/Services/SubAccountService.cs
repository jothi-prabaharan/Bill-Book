using Accounting.Entity.Enums;
using Accounting.Entity.Models;
using Accounting.Entity.TableEntities;
using Accounting.Repository;
using Microsoft.EntityFrameworkCore;

namespace Accounting.Api.Services;

/// <summary>
/// Sub-accounts are provisioned as a side effect of the owning master, never
/// created by a user. Each provision is idempotent, because the events that
/// trigger it are at-least-once.
/// </summary>
public sealed class SubAccountService
{
    // System account names the provisioner targets.
    private const string AccountsReceivable = "Accounts Receivable";
    private const string AccountsPayable = "Accounts Payable";
    private const string Inventory = "Inventory";
    private const string CostOfGoodsSold = "Cost of Goods Sold";
    private const string SalesRevenue = "Sales Revenue";
    private const string InputGst = "Input GST";
    private const string OutputGst = "Output GST";

    private readonly AccountingDbContext _db;

    public SubAccountService(AccountingDbContext db) => _db = db;

    public async Task<IReadOnlyList<SubAccountListItem>> ListAsync(
        SubAccountReferenceType? referenceType, long? referenceId, CancellationToken ct)
    {
        IQueryable<SubAccount> query = _db.SubAccounts;
        if (referenceType is SubAccountReferenceType type)
        {
            query = query.Where(s => s.ReferenceType == type);
        }

        if (referenceId is long id)
        {
            query = query.Where(s => s.ReferenceId == id);
        }

        return await (
            from sub in query
            join account in _db.Accounts on sub.AccountId equals account.AccountId
            orderby sub.SubAccountName
            select new SubAccountListItem
            {
                SubAccountId = sub.SubAccountId,
                AccountId = sub.AccountId,
                AccountName = account.AccountName,
                AccountTypeId = sub.AccountTypeId,
                ReferenceType = sub.ReferenceType,
                ReferenceId = sub.ReferenceId,
                TaxComponent = sub.TaxComponent,
                SubAccountName = sub.SubAccountName,
                IsActive = sub.IsActive,
            }).ToListAsync(ct);
    }

    /// <summary>
    /// Creates the sub-accounts a master owns:
    /// Contact → 2 · Item → 3 · Tax rate → up to 6 (CGST/SGST/IGST per direction).
    /// </summary>
    public async Task<int> ProvisionAsync(ProvisionSubAccountsRequest request, CancellationToken ct)
    {
        List<(string AccountName, TaxComponent Component, string Label)> targets = request.ReferenceType switch
        {
            SubAccountReferenceType.Contact =>
            [
                (AccountsReceivable, TaxComponent.None, $"{AccountsReceivable} — {request.Name}"),
                (AccountsPayable, TaxComponent.None, $"{AccountsPayable} — {request.Name}"),
            ],
            SubAccountReferenceType.Item =>
            [
                (Inventory, TaxComponent.None, $"{Inventory} — {request.Name}"),
                (CostOfGoodsSold, TaxComponent.None, $"{CostOfGoodsSold} — {request.Name}"),
                (SalesRevenue, TaxComponent.None, $"{SalesRevenue} — {request.Name}"),
            ],
            SubAccountReferenceType.Tax => BuildTaxTargets(request),
            _ => [],
        };

        if (targets.Count == 0)
        {
            return 0;
        }

        // One lookup for every control account this provision needs.
        List<string> names = targets.Select(t => t.AccountName).Distinct().ToList();
        Dictionary<string, Account> accounts = await _db.Accounts
            .Where(a => a.AccountSystemName != null && names.Contains(a.AccountSystemName))
            .ToDictionaryAsync(a => a.AccountSystemName!, ct);

        int created = 0;
        foreach ((string accountName, TaxComponent component, string label) in targets)
        {
            if (!accounts.TryGetValue(accountName, out Account? parent))
            {
                // The chart of accounts has not been seeded for this org yet.
                continue;
            }

            bool exists = await _db.SubAccounts.AnyAsync(
                s => s.AccountId == parent.AccountId
                    && s.ReferenceType == request.ReferenceType
                    && s.ReferenceId == request.ReferenceId
                    && s.TaxComponent == component,
                ct);
            if (exists)
            {
                continue;
            }

            _db.SubAccounts.Add(new SubAccount
            {
                AccountId = parent.AccountId,
                // Derived from the parent, never accepted from the caller.
                AccountTypeId = parent.AccountTypeId,
                ReferenceType = request.ReferenceType,
                ReferenceId = request.ReferenceId,
                TaxComponent = component,
                SubAccountName = label,
                IsActive = true,
            });
            created++;
        }

        if (created > 0)
        {
            await _db.SaveChangesAsync(ct);
        }

        return created;
    }

    /// <summary>Deactivates a master's sub-accounts without deleting them, so history survives.</summary>
    public async Task<int> DeactivateAsync(
        SubAccountReferenceType referenceType, long referenceId, CancellationToken ct)
    {
        List<SubAccount> rows = await _db.SubAccounts
            .Where(s => s.ReferenceType == referenceType && s.ReferenceId == referenceId && s.IsActive)
            .ToListAsync(ct);

        foreach (SubAccount row in rows)
        {
            row.IsActive = false;
        }

        if (rows.Count > 0)
        {
            await _db.SaveChangesAsync(ct);
        }

        return rows.Count;
    }

    private static List<(string, TaxComponent, string)> BuildTaxTargets(ProvisionSubAccountsRequest request)
    {
        var targets = new List<(string, TaxComponent, string)>();
        TaxComponent[] components = [TaxComponent.Cgst, TaxComponent.Sgst, TaxComponent.Igst];

        // The parent account gives the direction; TaxComponent gives the component.
        if (request.ForPurchase)
        {
            foreach (TaxComponent component in components)
            {
                targets.Add((InputGst, component, $"Input {component.ToString().ToUpperInvariant()} — {request.Name}"));
            }
        }

        if (request.ForSales)
        {
            foreach (TaxComponent component in components)
            {
                targets.Add((OutputGst, component, $"Output {component.ToString().ToUpperInvariant()} — {request.Name}"));
            }
        }

        return targets;
    }
}
