using System.ComponentModel.DataAnnotations;
using Shared.Kernel.Tenancy;

namespace Accounting.Entity.TableEntities;

/// <summary>
/// One statement as the bank produced it, for one of the organization's
/// accounts.
///
/// <b>The bank's account of what happened, not the organization's.</b> Nothing
/// here posts to the general ledger and nothing here is a document — the money
/// already moved, and whatever recorded it already posted. A statement exists so
/// the two accounts of the same events can be compared, and reconciliation is
/// that comparison rather than a second set of entries. A system that blurs the
/// two doubles a month of transactions the first time somebody imports a
/// statement covering documents already keyed.
///
/// <b>Opening and closing balances are the bank's figures, kept verbatim.</b>
/// They are what makes a statement checkable as a whole: the lines between them
/// must account for the difference, and when they do not, either the import
/// dropped a row or the file is not what it claims to be. That check is worth
/// more than any per-line matching, because it catches the failure per-line
/// matching cannot — a row that never arrived.
/// </summary>
public class BankStatement : OrgScopedEntity
{
    public long BankStatementId { get; set; }

    /// <summary>Which of the organization's accounts this is a statement of.</summary>
    [Range(1, long.MaxValue, ErrorMessage = "Choose the bank account.")]
    public long BankAccountId { get; set; }

    /// <summary>The bank's own reference for the statement, when the file carries one.</summary>
    [MaxLength(100, ErrorMessage = "Statement reference cannot exceed 100 characters.")]
    public string? StatementReference { get; set; }

    /// <summary>The period the statement covers, taken from the earliest and latest line.</summary>
    public DateOnly FromDate { get; set; }

    public DateOnly ToDate { get; set; }

    /// <summary>
    /// What the bank says the account stood at when the period opened. Null when
    /// the file did not say — many exports do not — in which case the whole-file
    /// balance check cannot run and the screen says so rather than implying it
    /// passed.
    /// </summary>
    public decimal? OpeningBalance { get; set; }

    public decimal? ClosingBalance { get; set; }

    /// <summary>The file it came from. What somebody looks for when a figure is queried.</summary>
    [MaxLength(260, ErrorMessage = "File name cannot exceed 260 characters.")]
    public string? FileName { get; set; }

    public DateTimeOffset ImportedAt { get; set; }

    public Guid? ImportedBy { get; set; }
}
