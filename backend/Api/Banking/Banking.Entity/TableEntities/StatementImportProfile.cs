using System.ComponentModel.DataAnnotations;
using Shared.Kernel.Tenancy;

namespace Banking.Entity.TableEntities;

/// <summary>
/// How to read the file one particular bank produces.
///
/// <b>There is no standard, and pretending otherwise is what makes statement
/// import miserable.</b> HDFC exports withdrawal and deposit as two columns;
/// ICICI exports one signed column; some export dates as <c>dd/MM/yy</c> and
/// some as <c>dd-MMM-yyyy</c>; nearly all put several lines of branch address
/// above the header row. So the mapping is chosen once per account, by the
/// person who can see the file, and remembered — rather than guessed at every
/// import, or hard-coded per bank in a list that goes stale the first time a
/// bank changes its export.
///
/// <b>One per bank account, not per bank.</b> The same bank can hand a current
/// account and a credit card entirely different layouts, and an organization may
/// hold accounts at a bank another organization has never heard of.
/// </summary>
public class StatementImportProfile : OrgScopedEntity
{
    public long StatementImportProfileId { get; set; }

    public long BankAccountId { get; set; }

    /// <summary>
    /// Rows to discard before the header — the branch address, the account
    /// summary, the disclaimer. Counted rather than detected: a heuristic that
    /// guesses where the table starts is a heuristic that silently eats a real
    /// row the day a bank adds a line.
    /// </summary>
    public int SkipRows { get; set; }

    /// <summary>
    /// Whether the first row after <see cref="SkipRows"/> names the columns.
    /// When it does the mapping can be by name; when it does not, only by
    /// position.
    /// </summary>
    public bool HasHeaderRow { get; set; } = true;

    /// <summary>
    /// How the date is written. Parsed with this exactly rather than by trying a
    /// list — <c>03/04/2026</c> is a real date under three different formats, and
    /// a parser that shops around will pick the wrong one on the days it matters
    /// least conspicuously.
    /// </summary>
    [MaxLength(40, ErrorMessage = "Date format cannot exceed 40 characters.")]
    public string DateFormat { get; set; } = "dd/MM/yyyy";

    // --- Which column is which. Named when there is a header row, and by
    // position (zero-based) otherwise. Stored as strings either way so one shape
    // covers both rather than two nullable sets of columns.

    [Required(ErrorMessage = "The date column is required.")]
    [MaxLength(100, ErrorMessage = "Column reference cannot exceed 100 characters.")]
    public string DateColumn { get; set; } = null!;

    [MaxLength(100, ErrorMessage = "Column reference cannot exceed 100 characters.")]
    public string? ValueDateColumn { get; set; }

    [MaxLength(100, ErrorMessage = "Column reference cannot exceed 100 characters.")]
    public string? DescriptionColumn { get; set; }

    [MaxLength(100, ErrorMessage = "Column reference cannot exceed 100 characters.")]
    public string? ReferenceColumn { get; set; }

    /// <summary>
    /// Money out, when the bank exports two columns. Left null when it exports
    /// one signed column, in which case <see cref="AmountColumn"/> is used.
    /// </summary>
    [MaxLength(100, ErrorMessage = "Column reference cannot exceed 100 characters.")]
    public string? WithdrawalColumn { get; set; }

    [MaxLength(100, ErrorMessage = "Column reference cannot exceed 100 characters.")]
    public string? DepositColumn { get; set; }

    /// <summary>
    /// One signed column, the other style. Positive is money in.
    /// <see cref="NegativeIsDeposit"/> flips that for the banks that do it the
    /// other way round — and some do, which is why it is a setting rather than an
    /// assumption.
    /// </summary>
    [MaxLength(100, ErrorMessage = "Column reference cannot exceed 100 characters.")]
    public string? AmountColumn { get; set; }

    public bool NegativeIsDeposit { get; set; }

    [MaxLength(100, ErrorMessage = "Column reference cannot exceed 100 characters.")]
    public string? BalanceColumn { get; set; }
}
