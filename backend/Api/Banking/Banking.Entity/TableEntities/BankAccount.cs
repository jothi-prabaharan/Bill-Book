using System.ComponentModel.DataAnnotations;
using Banking.Entity.Enums;
using Shared.Kernel.Tenancy;

namespace Banking.Entity.TableEntities;

/// <summary>
/// One of the organization's own accounts. Creating one creates its GL account
/// in Accounting — a bank account with no ledger identity could not be posted
/// to, and a GL account with no account number could not be reconciled.
///
/// Balances are never stored here. They come from the ledger, which is the whole
/// point of the link.
/// </summary>
public class BankAccount : OrgScopedEntity
{
    public long BankAccountId { get; set; }

    /// <summary>Null for cash in hand and wallets, which have no institution.</summary>
    public long? BankId { get; set; }

    /// <summary>
    /// The acc.Accounts row behind this account. Unenforced — Accounting owns
    /// that table and no service reaches into another's. Null only in the window
    /// between the row being inserted and Accounting answering; an account with
    /// a null here cannot be transacted on.
    /// </summary>
    public long? LedgerAccountId { get; set; }

    /// <summary>What staff call it: "HDFC Current A/c". Pushed to the GL account on change.</summary>
    [Required(ErrorMessage = "Account name is required.")]
    [MaxLength(100, ErrorMessage = "Account name cannot exceed 100 characters.")]
    public string AccountName { get; set; } = null!;

    [Required(ErrorMessage = "Account number is required.")]
    [MaxLength(30, ErrorMessage = "Account number cannot exceed 30 characters.")]
    public string AccountNumber { get; set; } = null!;

    public BankAccountType AccountType { get; set; } = BankAccountType.Current;

    [MaxLength(11, ErrorMessage = "IFSC must be 11 characters.")]
    [RegularExpression(@"^[A-Z]{4}0[A-Z0-9]{6}$", ErrorMessage = "IFSC is not in a valid format.")]
    public string? Ifsc { get; set; }

    [MaxLength(9, ErrorMessage = "MICR cannot exceed 9 characters.")]
    public string? Micr { get; set; }

    [MaxLength(11, ErrorMessage = "SWIFT code cannot exceed 11 characters.")]
    public string? SwiftCode { get; set; }

    [MaxLength(34, ErrorMessage = "IBAN cannot exceed 34 characters.")]
    public string? Iban { get; set; }

    [MaxLength(100, ErrorMessage = "Branch name cannot exceed 100 characters.")]
    public string? BranchName { get; set; }

    [Required(ErrorMessage = "Currency is required.")]
    [MaxLength(3, ErrorMessage = "Currency must be a 3-letter code.")]
    public string CurrencyCode { get; set; } = "INR";

    /// <summary>Overdraft and cash-credit limit. Meaningless on other kinds.</summary>
    [Range(0, 999999999999.99, ErrorMessage = "Overdraft limit cannot be negative.")]
    public decimal? OdLimit { get; set; }

    /// <summary>Preselected on receipts and payments. At most one per organization.</summary>
    public bool IsDefault { get; set; }

    public int DisplayOrder { get; set; }

    public bool IsActive { get; set; } = true;
}
