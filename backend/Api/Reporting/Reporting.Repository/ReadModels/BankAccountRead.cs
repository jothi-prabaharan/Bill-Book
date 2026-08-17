using Shared.Kernel.Tenancy;

namespace Reporting.Repository.ReadModels;

/// <summary>
/// <c>acc.BankAccounts</c>, read-only.
///
/// <b><c>LedgerAccountId</c> is the join that makes Bank Summary possible.</b>
/// Each bank account provisions its own account in the chart of accounts, so the
/// opening balance, the money received and the money spent all come from ledger
/// rows against that account rather than from anything stored here.
/// </summary>
public class BankAccountRead : OrgScopedEntity
{
    public long BankAccountId { get; set; }

    public long? BankId { get; set; }

    public long? LedgerAccountId { get; set; }

    public string AccountName { get; set; } = null!;

    public string AccountNumber { get; set; } = null!;

    /// <summary>Matches <c>BankAccountType</c> in Accounting: current, savings, OD, and so on.</summary>
    public int AccountType { get; set; }

    public string CurrencyCode { get; set; } = null!;

    public bool IsActive { get; set; }
}
