#pragma warning disable CS8618
using Shared.Kernel.Tenancy;

namespace Reporting.Repository.ReadModels;

/// <summary>
/// <c>acc.Accounts</c>, read-only. The chart of accounts.
///
/// <b><c>IsContra</c> is what tells a report to subtract.</b> Accumulated
/// Depreciation is an Asset, Sales Returns and Discount Given are Income,
/// Purchase Returns is an Expense — each running opposite its type. A report that
/// ignores this column overstates silently, which is the worst way to be wrong.
///
/// <c>AccountTypeId</c> points at <c>mst.AccountTypes</c> in the master database.
/// There is no join to be had: resolve the name in C#, batched.
/// </summary>
public class AccountRead : OrgScopedEntity
{
    public long AccountId { get; set; }

    public int AccountTypeId { get; set; }

    public bool IsContra { get; set; }

    public string AccountCode { get; set; }
    public string AccountName { get; set; }
    public long? ParentAccountId { get; set; }

    /// <summary>
    /// Null for an org's own accounts. Set only on the ten control accounts the
    /// seed writes it onto — <c>SubAccountService</c> looks a parent up by this
    /// name when it provisions a contact's, an item's or a tax rate's
    /// sub-accounts, so a report resolving "which of an item's three
    /// sub-accounts is the Inventory one" reads the same name rather than
    /// guessing from <c>AccountName</c>, which an org is free to rename.
    /// </summary>
    public string? AccountSystemName { get; set; }

    public string? CurrencyCode { get; set; }

    public bool IsBank { get; set; }

    public bool IsActive { get; set; }
}


