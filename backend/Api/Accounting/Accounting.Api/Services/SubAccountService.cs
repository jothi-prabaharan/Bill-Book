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
                AccountCode = account.AccountCode,
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
    ///
    /// Any control account that gains a sub-account is marked used in the same
    /// transaction — a parented sub-account is a reference, and from that moment
    /// the parent's type and code must not move under its postings.
    /// </summary>
    public async Task<ProvisionSubAccountsResult> ProvisionAsync(
        ProvisionSubAccountsRequest request, CancellationToken ct)
    {
        List<(SystemAccount Parent, TaxComponent Component, string Label)> targets = request.ReferenceType switch
        {
            SubAccountReferenceType.Contact =>
            [
                (SystemAccount.AccountsReceivable, TaxComponent.None, Label(SystemAccount.AccountsReceivable, request.Name)),
                (SystemAccount.AccountsPayable, TaxComponent.None, Label(SystemAccount.AccountsPayable, request.Name)),
            ],
            SubAccountReferenceType.Item =>
            [
                (SystemAccount.Inventory, TaxComponent.None, Label(SystemAccount.Inventory, request.Name)),
                (SystemAccount.CostOfGoodsSold, TaxComponent.None, Label(SystemAccount.CostOfGoodsSold, request.Name)),
                (SystemAccount.SalesRevenue, TaxComponent.None, Label(SystemAccount.SalesRevenue, request.Name)),
            ],
            SubAccountReferenceType.Tax => BuildTaxTargets(request),
            _ => [],
        };

        if (targets.Count == 0)
        {
            return new ProvisionSubAccountsResult(0, []);
        }

        // One lookup for every control account this provision needs.
        List<string> names = targets.Select(t => SystemAccountNames.Of(t.Parent)).Distinct().ToList();
        Dictionary<string, Account> accounts = await _db.Accounts
            .Where(a => a.AccountSystemName != null && names.Contains(a.AccountSystemName))
            .ToDictionaryAsync(a => a.AccountSystemName!, ct);

        int created = 0;
        var missing = new List<string>();
        foreach ((SystemAccount parentAccount, TaxComponent component, string label) in targets)
        {
            string parentName = SystemAccountNames.Of(parentAccount);
            if (!accounts.TryGetValue(parentName, out Account? parent))
            {
                // The chart of accounts has not been seeded for this org, or a
                // control account was renamed away. Either way the sub-ledger
                // would be incomplete, so the caller is told rather than left
                // with a silent success.
                if (!missing.Contains(parentName))
                {
                    missing.Add(parentName);
                }

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

            // First reference freezes the parent's configuration. Set in the same
            // SaveChanges as the sub-account, so the two can never disagree.
            parent.IsUsed = true;
            created++;
        }

        if (created > 0)
        {
            await _db.SaveChangesAsync(ct);
        }

        return new ProvisionSubAccountsResult(created, missing);
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

    private static List<(SystemAccount, TaxComponent, string)> BuildTaxTargets(
        ProvisionSubAccountsRequest request)
    {
        var targets = new List<(SystemAccount, TaxComponent, string)>();
        TaxComponent[] components = [TaxComponent.Cgst, TaxComponent.Sgst, TaxComponent.Igst];

        // The parent account gives the direction; TaxComponent gives the component.
        if (request.ForPurchase)
        {
            foreach (TaxComponent component in components)
            {
                targets.Add((SystemAccount.InputGst, component, TaxLabel("Input", component, request.Name)));
            }
        }

        if (request.ForSales)
        {
            foreach (TaxComponent component in components)
            {
                targets.Add((SystemAccount.OutputGst, component, TaxLabel("Output", component, request.Name)));
            }
        }

        return targets;
    }

    private static string Label(SystemAccount parent, string name) =>
        $"{SystemAccountNames.Of(parent)} — {name}";

    private static string TaxLabel(string direction, TaxComponent component, string name) =>
        $"{direction} {component.ToString().ToUpperInvariant()} — {name}";
}

/// <summary>
/// <paramref name="MissingAccounts"/> lists control accounts the provision could
/// not resolve. Non-empty means the sub-ledger is incomplete for that master.
/// </summary>
public sealed record ProvisionSubAccountsResult(int Created, IReadOnlyList<string> MissingAccounts);
