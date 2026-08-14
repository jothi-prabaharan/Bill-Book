namespace Accounting.Entity.Models;

/// <summary>
/// One account's ledger over a date range: what it stood at when the range
/// opened, every posting inside it, and what it stands at now.
/// </summary>
public class AccountLedgerView
{
    public long AccountId { get; set; }

    public string AccountCode { get; set; } = null!;

    public string AccountName { get; set; } = null!;

    /// <summary>
    /// From <c>mst.AccountTypes</c>, which lives in the master database. Returned
    /// rather than resolved here so the screen can present a credit-balance
    /// account the right way up without this service keeping a second copy of
    /// which types are which.
    /// </summary>
    public int AccountTypeId { get; set; }

    /// <summary>Normal balance runs opposite its type, so a report subtracts it.</summary>
    public bool IsContra { get; set; }

    public DateOnly? From { get; set; }

    public DateOnly? To { get; set; }

    /// <summary>
    /// Everything posted before <see cref="From"/>, netted. In debit terms:
    /// positive is a debit balance, negative a credit one. Zero when no start
    /// date was given, because then there is nothing before the range.
    /// </summary>
    public decimal OpeningBalance { get; set; }

    public decimal ClosingBalance { get; set; }

    public decimal TotalDebit { get; set; }

    public decimal TotalCredit { get; set; }

    public List<AccountLedgerRow> Rows { get; set; } = [];
}

/// <summary>
/// One posting, with everything needed to get back to the document that wrote
/// it. A ledger row that cannot be traced to its source is a number nobody can
/// check, which is the same as a number nobody believes.
/// </summary>
public class AccountLedgerRow
{
    public long LedgerId { get; set; }

    public DateOnly LedgerDate { get; set; }

    /// <summary>The document type — INV, BIL, JRN, STA.</summary>
    public string TransactionTypeCode { get; set; } = null!;

    public long TransactionId { get; set; }

    public long TransactionDetailId { get; set; }

    /// <summary>The manual journal behind this row, when one wrote it.</summary>
    public long? JournalId { get; set; }

    /// <summary>The journal's own number, so the row reads without a second lookup.</summary>
    public string? JournalNo { get; set; }

    public int LedgerTypeId { get; set; }

    public int LedgerSourceId { get; set; }

    public long? SubAccountId { get; set; }

    public string? SubAccountName { get; set; }

    public long? ContactId { get; set; }

    public string? TransactionDesc { get; set; }

    public decimal DebitAmount { get; set; }

    public decimal CreditAmount { get; set; }

    public decimal DebitAmountBase { get; set; }

    public decimal CreditAmountBase { get; set; }

    public string CurrencyCode { get; set; } = null!;

    public decimal ExchangeRate { get; set; }

    /// <summary>
    /// The account's balance after this row, in debit terms. Computed over the
    /// ordered result rather than by the database, because a ledger is always
    /// read for one account over one date range — the ordering a window function
    /// would need is the ordering this list already has.
    /// </summary>
    public decimal RunningBalance { get; set; }
}

/// <summary>
/// The trial balance: every account with a balance, and the one number that says
/// the whole system is sound — the two columns agreeing.
/// </summary>
public class TrialBalanceView
{
    /// <summary>Null means from the beginning of the books.</summary>
    public DateOnly? From { get; set; }

    public DateOnly? To { get; set; }

    public List<TrialBalanceRow> Rows { get; set; } = [];

    public decimal TotalDebit { get; set; }

    public decimal TotalCredit { get; set; }

    /// <summary>
    /// The two columns agreeing. If this is ever false the books are broken, and
    /// the screen should say so loudly rather than showing a tidy table.
    /// </summary>
    public bool IsBalanced { get; set; }
}

/// <summary>
/// One account's net position. A trial balance shows each account in exactly one
/// column — the side its balance actually falls on — rather than both its totals,
/// which is what makes the two columns comparable at the foot.
/// </summary>
public class TrialBalanceRow
{
    public long AccountId { get; set; }

    public string AccountCode { get; set; } = null!;

    public string AccountName { get; set; } = null!;

    public int AccountTypeId { get; set; }

    public bool IsContra { get; set; }

    /// <summary>Everything posted to the account, before netting.</summary>
    public decimal TotalDebit { get; set; }

    public decimal TotalCredit { get; set; }

    /// <summary>The net, when it falls on the debit side. Zero otherwise.</summary>
    public decimal DebitBalance { get; set; }

    /// <summary>The net, when it falls on the credit side. Zero otherwise.</summary>
    public decimal CreditBalance { get; set; }
}

/// <summary>
/// Whether each control account agrees with the subledger beneath it.
///
/// <b>A control account and its subledger can disagree while the books balance
/// perfectly.</b> A receivable posted to Accounts Receivable with no sub-account
/// beneath it is a balanced entry: debits equal credits, the trial balance foots,
/// and no contact owes the money. The statement is wrong, the aging report is
/// wrong, and the first receipt has nothing to settle against — and nothing in
/// double-entry catches any of it, because nothing in double-entry is broken.
///
/// So this is a second, independent check, and it is the one an opening balance
/// is judged on: a migration ties everywhere or it is not a migration.
/// </summary>
public class SubLedgerTieView
{
    public List<SubLedgerTieRow> Rows { get; set; } = [];

    /// <summary>Every control account agreeing with its subledger.</summary>
    public bool IsTied { get; set; }
}

/// <summary>One control account against the sum of the sub-accounts hanging from it.</summary>
public class SubLedgerTieRow
{
    public long AccountId { get; set; }

    public string AccountCode { get; set; } = null!;

    public string AccountName { get; set; } = null!;

    /// <summary>Everything posted to the account, sub-account or not.</summary>
    public decimal ControlBalance { get; set; }

    /// <summary>The part of it that named a sub-account.</summary>
    public decimal SubLedgerBalance { get; set; }

    /// <summary>
    /// What is posted to the control account and belongs to nobody. Non-zero is
    /// the failure this whole view exists to surface.
    /// </summary>
    public decimal Unattributed { get; set; }

    public int SubAccountCount { get; set; }

    public bool IsTied { get; set; }
}

/// <summary>
/// An outstanding balance for a specific document.
/// </summary>
public class OutstandingBalanceView
{
    public long ContactId { get; set; }
    
    public string TransactionTypeCode { get; set; } = null!;
    
    public long TransactionId { get; set; }
    
    public string DocumentNo { get; set; } = null!;
    
    public DateOnly DocumentDate { get; set; }
    
    public DateOnly? DueDate { get; set; }
    
    public decimal TotalAmount { get; set; }
    
    public decimal PaidAmount { get; set; }
    
    public decimal OutstandingAmount { get; set; }
}
