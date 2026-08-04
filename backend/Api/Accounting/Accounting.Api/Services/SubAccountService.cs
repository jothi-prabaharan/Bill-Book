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
                ReferenceType = sub.ReferenceType.ToString(),
                ReferenceId = sub.ReferenceId,
                TaxComponent = sub.TaxComponent.ToString(),
                Purpose = sub.Purpose.ToString(),
                SubAccountName = sub.SubAccountName,
                IsActive = sub.IsActive,
            }).ToListAsync(ct);
    }

    /// <summary>
    /// Creates the sub-accounts a master owns:
    /// Contact → 6 · Item → 3 · Tax rate → up to 6 (CGST/SGST/IGST per direction).
    ///
    /// <b>A contact gets six, under two parents.</b> Three beneath Accounts
    /// Receivable — the trade balance, a prepayment advance and an overpayment
    /// advance — and the same three beneath Accounts Payable. Grouped by the
    /// direction the balance runs rather than by who the contact is, so every
    /// sub-account's nature matches the parent it hangs from: everything under
    /// Accounts Receivable is an asset, everything under Accounts Payable a
    /// liability. <c>AccountTypeId</c> is copied from the parent, so grouping any
    /// other way would have a report contradict itself depending on whether it
    /// grouped by type or by account.
    ///
    /// All six are per contact because every one of these balances is answered
    /// about a named contact — you refund a particular customer's deposit, not a
    /// pooled one — and a control account whose balance cannot be split by
    /// contact cannot be reconciled at all.
    ///
    /// <b>The advances live inside the AR and AP control accounts</b>, which
    /// means neither control total is a Schedule III line on its own: advances
    /// to suppliers and from customers have to be reported apart from trade
    /// receivables and payables. <see cref="SubAccountPurpose"/> is what splits
    /// them back out.
    ///
    /// Any control account that gains a sub-account is marked used in the same
    /// transaction — a parented sub-account is a reference, and from that moment
    /// the parent's type and code must not move under its postings.
    ///
    /// <b>Re-runnable.</b> Each target is skipped when it already exists, so
    /// calling this again for a contact created before the advances existed
    /// backfills exactly the four it is missing.
    /// </summary>
    public async Task<ProvisionSubAccountsResult> ProvisionAsync(
        ProvisionSubAccountsRequest request, CancellationToken ct)
    {
        List<(SystemAccount Parent, TaxComponent Component, SubAccountPurpose Purpose, string Label)> targets = request.ReferenceType switch
        {
            SubAccountReferenceType.Contact => BuildContactTargets(request),
            SubAccountReferenceType.Item =>
            [
                Primary(SystemAccount.Inventory, request.Name),
                Primary(SystemAccount.CostOfGoodsSold, request.Name),
                Primary(SystemAccount.SalesRevenue, request.Name),
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
        foreach ((SystemAccount parentAccount, TaxComponent component, SubAccountPurpose purpose, string label) in targets)
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

            // The full key, purpose included — otherwise a contact's three
            // sub-accounts under one parent would each see the first as
            // "already there" and only one would ever be written.
            bool exists = await _db.SubAccounts.AnyAsync(
                s => s.AccountId == parent.AccountId
                    && s.ReferenceType == request.ReferenceType
                    && s.ReferenceId == request.ReferenceId
                    && s.TaxComponent == component
                    && s.Purpose == purpose,
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
                Purpose = purpose,
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

    /// <summary>
    /// A contact's six: the trade balance, a prepayment advance and an
    /// overpayment advance, beneath each of Accounts Receivable and Accounts
    /// Payable.
    ///
    /// Both directions are provisioned for every contact regardless of role.
    /// Contacts is one master with roles, so the same row can buy and sell, and
    /// a contact who becomes a supplier next quarter would otherwise have a
    /// payable with no sub-account and drop silently out of the aging.
    /// </summary>
    private static List<(SystemAccount, TaxComponent, SubAccountPurpose, string)> BuildContactTargets(
        ProvisionSubAccountsRequest request) =>
    [
        Primary(SystemAccount.AccountsReceivable, request.Name),
        Advance(SystemAccount.AccountsReceivable, SubAccountPurpose.PrepaymentAdvance, "Receivable", request.Name),
        Advance(SystemAccount.AccountsReceivable, SubAccountPurpose.OverpaymentAdvance, "Receivable", request.Name),

        Primary(SystemAccount.AccountsPayable, request.Name),
        Advance(SystemAccount.AccountsPayable, SubAccountPurpose.PrepaymentAdvance, "Payable", request.Name),
        Advance(SystemAccount.AccountsPayable, SubAccountPurpose.OverpaymentAdvance, "Payable", request.Name),
    ];

    private static List<(SystemAccount, TaxComponent, SubAccountPurpose, string)> BuildTaxTargets(
        ProvisionSubAccountsRequest request)
    {
        var targets = new List<(SystemAccount, TaxComponent, SubAccountPurpose, string)>();
        TaxComponent[] components = [TaxComponent.Cgst, TaxComponent.Sgst, TaxComponent.Igst];

        // The parent account gives the direction; TaxComponent gives the component.
        if (request.ForPurchase)
        {
            foreach (TaxComponent component in components)
            {
                targets.Add((SystemAccount.InputGst, component, SubAccountPurpose.Primary,
                    TaxLabel("Input", component, request.Name)));
            }
        }

        if (request.ForSales)
        {
            foreach (TaxComponent component in components)
            {
                targets.Add((SystemAccount.OutputGst, component, SubAccountPurpose.Primary,
                    TaxLabel("Output", component, request.Name)));
            }
        }

        return targets;
    }

    /// <summary>The trade balance under a control account — what most sub-accounts are.</summary>
    private static (SystemAccount, TaxComponent, SubAccountPurpose, string) Primary(
        SystemAccount parent, string name) =>
        (parent, TaxComponent.None, SubAccountPurpose.Primary, Label(parent, name));

    /// <summary>
    /// An advance held under a control account. Named by the direction the
    /// balance runs rather than by the parent, because "Accounts Receivable —
    /// Sharma Traders" and "Prepayment Advance Receivable — Sharma Traders" have
    /// to be told apart at a glance on a picker.
    /// </summary>
    private static (SystemAccount, TaxComponent, SubAccountPurpose, string) Advance(
        SystemAccount parent, SubAccountPurpose purpose, string direction, string name) =>
        (parent, TaxComponent.None, purpose, $"{Describe(purpose)} Advance {direction} — {name}");

    private static string Describe(SubAccountPurpose purpose) => purpose switch
    {
        SubAccountPurpose.PrepaymentAdvance => "Prepayment",
        SubAccountPurpose.OverpaymentAdvance => "Overpayment",
        _ => throw new ArgumentOutOfRangeException(
            nameof(purpose), purpose, "Only an advance purpose has a label of its own."),
    };

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
