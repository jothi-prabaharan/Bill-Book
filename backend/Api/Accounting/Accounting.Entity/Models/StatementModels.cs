using System.ComponentModel.DataAnnotations;

namespace Accounting.Entity.Models;

/// <summary>How to read one bank's export. Chosen once per account and remembered.</summary>
public class SaveImportProfileRequest
{
    [Range(1, long.MaxValue, ErrorMessage = "Choose the bank account.")]
    public long BankAccountId { get; set; }

    [Range(0, 100, ErrorMessage = "Rows to skip must be between 0 and 100.")]
    public int SkipRows { get; set; }

    public bool HasHeaderRow { get; set; } = true;

    [Required(ErrorMessage = "The date format is required.")]
    [MaxLength(40, ErrorMessage = "Date format cannot exceed 40 characters.")]
    public string DateFormat { get; set; } = "dd/MM/yyyy";

    [Required(ErrorMessage = "The date column is required.")]
    [MaxLength(100, ErrorMessage = "Column reference cannot exceed 100 characters.")]
    public string DateColumn { get; set; } = null!;

    [MaxLength(100, ErrorMessage = "Column reference cannot exceed 100 characters.")]
    public string? ValueDateColumn { get; set; }

    [MaxLength(100, ErrorMessage = "Column reference cannot exceed 100 characters.")]
    public string? DescriptionColumn { get; set; }

    [MaxLength(100, ErrorMessage = "Column reference cannot exceed 100 characters.")]
    public string? ReferenceColumn { get; set; }

    [MaxLength(100, ErrorMessage = "Column reference cannot exceed 100 characters.")]
    public string? WithdrawalColumn { get; set; }

    [MaxLength(100, ErrorMessage = "Column reference cannot exceed 100 characters.")]
    public string? DepositColumn { get; set; }

    [MaxLength(100, ErrorMessage = "Column reference cannot exceed 100 characters.")]
    public string? AmountColumn { get; set; }

    public bool NegativeIsDeposit { get; set; }

    [MaxLength(100, ErrorMessage = "Column reference cannot exceed 100 characters.")]
    public string? BalanceColumn { get; set; }
}

public class ImportProfileView : SaveImportProfileRequest
{
    public long StatementImportProfileId { get; set; }
}

/// <summary>
/// What an import did. <b>Reported per outcome rather than as one count</b>,
/// because "312 rows, 40 imported" is the normal and correct result of
/// re-importing an overlapping period — and a single number would make it look
/// like something went wrong.
/// </summary>
public class ImportStatementResult
{
    public long BankStatementId { get; set; }

    public int RowsRead { get; set; }

    public int Imported { get; set; }

    /// <summary>Already present from an earlier import of an overlapping period.</summary>
    public int AlreadyPresent { get; set; }

    /// <summary>Matched to a money document without anybody being asked.</summary>
    public int AutoMatched { get; set; }

    public DateOnly? FromDate { get; set; }

    public DateOnly? ToDate { get; set; }

    /// <summary>
    /// Whether the bank's own opening and closing balances account for the lines
    /// between them. Null when the file did not carry them, which is not a pass.
    /// </summary>
    public bool? BalancesAgree { get; set; }

    public decimal? BalanceDifference { get; set; }
}

public class BankStatementListItem
{
    public long BankStatementId { get; set; }

    public long BankAccountId { get; set; }

    public string BankAccountName { get; set; } = null!;

    public string? StatementReference { get; set; }

    public DateOnly FromDate { get; set; }

    public DateOnly ToDate { get; set; }

    public string? FileName { get; set; }

    public DateTimeOffset ImportedAt { get; set; }

    public int LineCount { get; set; }

    public int MatchedCount { get; set; }

    public int IgnoredCount { get; set; }

    /// <summary>Nothing left undecided. What "reconciled" means for a statement.</summary>
    public bool IsReconciled => LineCount > 0 && MatchedCount + IgnoredCount == LineCount;
}

public class StatementLineView
{
    public long BankStatementLineId { get; set; }

    public int LineNumber { get; set; }

    public DateOnly TransactionDate { get; set; }

    public DateOnly? ValueDate { get; set; }

    public string? Description { get; set; }

    public string? ReferenceNo { get; set; }

    public decimal WithdrawalAmount { get; set; }

    public decimal DepositAmount { get; set; }

    public decimal? RunningBalance { get; set; }

    public string Status { get; set; } = null!;

    public string? MatchedTransactionTypeCode { get; set; }

    public long? MatchedTransactionId { get; set; }

    /// <summary>The matched document's number, so a person sees SPM/2026/00042 rather than an id.</summary>
    public string? MatchedTransactionNo { get; set; }

    public bool MatchedAutomatically { get; set; }

    public string? Note { get; set; }

    /// <summary>
    /// Documents this line could be, when it is unmatched. Empty once matched —
    /// there is nothing left to choose.
    /// </summary>
    public List<MatchCandidateView> Candidates { get; set; } = [];
}

/// <summary>A money document a statement line might be the bank's version of.</summary>
public class MatchCandidateView
{
    public string TransactionTypeCode { get; set; } = null!;

    public long TransactionId { get; set; }

    public string? TransactionNo { get; set; }

    public DateOnly TransactionDate { get; set; }

    public decimal Amount { get; set; }

    public long? ContactId { get; set; }

    public string? ReferenceNo { get; set; }

    /// <summary>Why this is a candidate, in words — what agreed and what did not.</summary>
    public string Reason { get; set; } = null!;

    /// <summary>
    /// Whether this one is good enough to take without asking. Only ever true for
    /// exactly one candidate: a line with two equally good matches is a line a
    /// person has to look at, and picking one would be a coin toss recorded as a
    /// reconciliation.
    /// </summary>
    public bool IsConfident { get; set; }
}

public class MatchStatementLineRequest
{
    [Required(ErrorMessage = "The document type is required.")]
    [MaxLength(3, ErrorMessage = "Transaction type code must be a 3-letter code.")]
    public string TransactionTypeCode { get; set; } = null!;

    [Range(1, long.MaxValue, ErrorMessage = "Choose the document this line settles.")]
    public long TransactionId { get; set; }
}

public class IgnoreStatementLineRequest
{
    /// <summary>Required. A line set aside without a reason is indistinguishable from a mistake.</summary>
    [Required(ErrorMessage = "Say why this line is being set aside.")]
    [MaxLength(300, ErrorMessage = "Note cannot exceed 300 characters.")]
    public string Note { get; set; } = null!;
}

public enum StatementOutcome
{
    Ok = 0,
    NotFound = 1,

    /// <summary>The bank account does not exist in this branch.</summary>
    BankAccountUnusable = 2,

    /// <summary>No import profile has been set up for this account yet.</summary>
    ProfileMissing = 3,

    /// <summary>The file could not be read. <c>Detail</c> says which row and why.</summary>
    FileUnreadable = 4,

    /// <summary>The named document is not a posted money document on this account.</summary>
    CandidateUnusable = 5,

    /// <summary>The line is already matched, or already ignored.</summary>
    AlreadyDecided = 6,
}

public sealed record StatementResult(
    StatementOutcome Outcome, long Id = 0, string? Detail = null);
