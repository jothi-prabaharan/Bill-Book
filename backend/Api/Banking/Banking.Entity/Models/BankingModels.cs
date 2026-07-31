using System.ComponentModel.DataAnnotations;

namespace Banking.Entity.Models;

public class BankListItem
{
    public long BankId { get; set; }

    public string BankCode { get; set; } = null!;

    public string BankName { get; set; } = null!;

    public int DisplayOrder { get; set; }

    public bool IsSystem { get; set; }

    public bool IsActive { get; set; }

    /// <summary>How many accounts sit under it — what makes it undeletable.</summary>
    public int AccountCount { get; set; }
}

public class SaveBankRequest
{
    [MaxLength(20, ErrorMessage = "Bank code cannot exceed 20 characters.")]
    public string? BankCode { get; set; }

    [Required(ErrorMessage = "Bank name is required.")]
    [MaxLength(100, ErrorMessage = "Bank name cannot exceed 100 characters.")]
    public string BankName { get; set; } = null!;

    public bool IsActive { get; set; } = true;
}

public class BankAccountListItem
{
    public long BankAccountId { get; set; }

    public long? BankId { get; set; }

    public string? BankName { get; set; }

    public long? LedgerAccountId { get; set; }

    /// <summary>The GL account's code, so the link is visible without opening Accounting.</summary>
    public string? LedgerAccountCode { get; set; }

    public string AccountName { get; set; } = null!;

    public string AccountNumber { get; set; } = null!;

    public string AccountType { get; set; } = null!;

    public string? Ifsc { get; set; }

    public string? Micr { get; set; }

    public string? SwiftCode { get; set; }

    public string? Iban { get; set; }

    public string? BranchName { get; set; }

    public string CurrencyCode { get; set; } = null!;

    public decimal? OdLimit { get; set; }

    public bool IsDefault { get; set; }

    public int DisplayOrder { get; set; }

    public bool IsActive { get; set; }

    /// <summary>False when Accounting has not answered yet. The account cannot be used until it is true.</summary>
    public bool IsLedgerLinked { get; set; }
}

public class SaveBankAccountRequest
{
    public long? BankId { get; set; }

    [Required(ErrorMessage = "Account name is required.")]
    [MaxLength(100, ErrorMessage = "Account name cannot exceed 100 characters.")]
    public string AccountName { get; set; } = null!;

    [Required(ErrorMessage = "Account number is required.")]
    [MaxLength(30, ErrorMessage = "Account number cannot exceed 30 characters.")]
    public string AccountNumber { get; set; } = null!;

    [Required(ErrorMessage = "Account type is required.")]
    public string AccountType { get; set; } = "Current";

    [MaxLength(11, ErrorMessage = "IFSC must be 11 characters.")]
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

    public decimal? OdLimit { get; set; }

    public bool IsActive { get; set; } = true;
}

/// <summary>
/// A drag-and-drop drop, described by its neighbours rather than a computed
/// position, so the server picks the value.
/// </summary>
public class ReorderRequest
{
    public long MovedId { get; set; }

    public long? PreviousId { get; set; }

    public long? NextId { get; set; }
}

public enum SaveBankOutcome
{
    Ok = 0,
    NotFound = 1,
    DuplicateCode = 2,
    DuplicateName = 3,
    DuplicateAccountNumber = 4,
    /// <summary>Accounts still point at this bank.</summary>
    BankInUse = 5,
    /// <summary>Everything but cash and wallets must name a bank.</summary>
    BankRequired = 6,
    /// <summary>Only overdrafts, cash credit and credit cards can carry a limit.</summary>
    OdLimitNotAllowed = 7,
    /// <summary>The default account cannot be deactivated while it is the default.</summary>
    DefaultAccountLocked = 8,
    /// <summary>Accounting could not create or find the GL account.</summary>
    LedgerUnavailable = 9,
    InvalidValue = 10,
}
