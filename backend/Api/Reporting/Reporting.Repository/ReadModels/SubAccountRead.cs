using Shared.Kernel.Tenancy;

namespace Reporting.Repository.ReadModels;

/// <summary>
/// <c>acc.SubAccounts</c>, read-only. The per-contact, per-item and per-tax
/// detail beneath a control account.
///
/// <b><c>ReferenceType</c> and <c>ReferenceId</c> are what a report joins on</b> —
/// a contact sub-account carries the contact's id, an item's carries the item's.
/// Both are stored as their underlying integers here rather than as the enums
/// Accounting declares them with, because this context deliberately does not
/// reference that project.
///
/// <c>Purpose</c> separates the three sub-accounts a contact has beneath each
/// control account — trade, prepayment advance, overpayment advance. Aged
/// receivables must report advances apart from trade balances, and this column is
/// what splits them.
/// </summary>
public class SubAccountRead : OrgScopedEntity
{
    public long SubAccountId { get; set; }

    public int AccountTypeId { get; set; }

    public long AccountId { get; set; }

    /// <summary>Matches <c>SubAccountReferenceType</c> in Accounting: contact, item or tax.</summary>
    public int ReferenceType { get; set; }

    public long ReferenceId { get; set; }

    /// <summary>Matches <c>SubAccountPurpose</c> in Accounting.</summary>
    public int Purpose { get; set; }

    public string SubAccountName { get; set; } = null!;
}
