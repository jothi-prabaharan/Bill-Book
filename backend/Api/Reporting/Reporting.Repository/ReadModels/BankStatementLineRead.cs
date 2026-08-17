using Shared.Kernel.Tenancy;

namespace Reporting.Repository.ReadModels;

/// <summary>
/// <c>acc.BankStatementLines</c>, read-only. What the bank said happened, against
/// what the books say happened.
///
/// <b><c>Status</c> is the whole point of the Reconciliation report</b>: which
/// lines matched a transaction, which are still unmatched, and which were matched
/// by a person rather than by the importer. Withdrawal and deposit are separate
/// non-negative columns, the same convention the ledger uses — one signed column
/// cannot be summed without knowing which way its writer meant it.
/// </summary>
public class BankStatementLineRead : OrgScopedEntity
{
    public long BankStatementLineId { get; set; }

    public long BankStatementId { get; set; }

    public long BankAccountId { get; set; }

    public int LineNumber { get; set; }

    public DateOnly TransactionDate { get; set; }

    public DateOnly? ValueDate { get; set; }

    public string? Description { get; set; }

    public string? ReferenceNo { get; set; }

    public decimal WithdrawalAmount { get; set; }

    public decimal DepositAmount { get; set; }

    public decimal? RunningBalance { get; set; }

    /// <summary>Matches <c>StatementLineStatus</c> in Accounting.</summary>
    public int Status { get; set; }

    public string? MatchedTransactionTypeCode { get; set; }

    public long? MatchedTransactionId { get; set; }

    public bool MatchedAutomatically { get; set; }
}
