using System.ComponentModel.DataAnnotations;
using Accounting.Entity.Enums;

namespace Accounting.Entity.Models;

public class AccountListItem
{
    public long AccountId { get; set; }

    public int AccountTypeId { get; set; }

    public string AccountCode { get; set; } = null!;

    public string AccountName { get; set; } = null!;

    public long? ParentAccountId { get; set; }

    public string? CurrencyCode { get; set; }

    public bool IsContra { get; set; }

    public bool IsSystemDefault { get; set; }

    public bool IsActive { get; set; }

    public bool IsUsed { get; set; }

    public bool IsLock { get; set; }

    public bool IsJE { get; set; }

    public bool IsSales { get; set; }

    public bool IsPurchase { get; set; }

    public bool IsPayment { get; set; }

    public bool IsBank { get; set; }

    /// <summary>
    /// True when type, code and usage flags are frozen — the account has been
    /// used, or it is a seeded system account. The UI renders those read-only.
    /// </summary>
    public bool IsConfigLocked { get; set; }
}

public class SaveAccountRequest
{
    [Required(ErrorMessage = "Account type is required.")]
    [Range(1, 5, ErrorMessage = "Account type must be one of the five types.")]
    public int AccountTypeId { get; set; }

    [Required(ErrorMessage = "Account code is required.")]
    [MaxLength(20, ErrorMessage = "Account code cannot exceed 20 characters.")]
    public string AccountCode { get; set; } = null!;

    [Required(ErrorMessage = "Account name is required.")]
    [MaxLength(200, ErrorMessage = "Account name cannot exceed 200 characters.")]
    public string AccountName { get; set; } = null!;

    public long? ParentAccountId { get; set; }

    [MaxLength(3, ErrorMessage = "Currency code must be a 3-letter code.")]
    public string? CurrencyCode { get; set; }

    public bool IsContra { get; set; }

    public bool IsActive { get; set; } = true;

    public bool IsLock { get; set; }

    public bool IsSales { get; set; }

    public bool IsPurchase { get; set; }

    public bool IsPayment { get; set; }

    public bool IsBank { get; set; }

    // IsJE is deliberately absent — it is set from the backend only.
}

public class SubAccountListItem
{
    public long SubAccountId { get; set; }

    public long AccountId { get; set; }

    public string AccountName { get; set; } = null!;

    public string AccountCode { get; set; } = null!;

    public int AccountTypeId { get; set; }

    public SubAccountReferenceType ReferenceType { get; set; }

    public long ReferenceId { get; set; }

    public TaxComponent TaxComponent { get; set; }

    public string SubAccountName { get; set; } = null!;

    public bool IsActive { get; set; }
}

/// <summary>
/// Provisions the sub-accounts a master owns. Called by Contacts, Inventory and
/// the tax master rather than by a user — sub-accounts are never hand-created.
/// </summary>
public class ProvisionSubAccountsRequest
{
    [Required(ErrorMessage = "Reference type is required.")]
    public SubAccountReferenceType ReferenceType { get; set; }

    [Range(1, long.MaxValue, ErrorMessage = "Reference id is required.")]
    public long ReferenceId { get; set; }

    [Required(ErrorMessage = "Name is required.")]
    [MaxLength(200, ErrorMessage = "Name cannot exceed 200 characters.")]
    public string Name { get; set; } = null!;

    /// <summary>Tax rates only: which directions to create, from IsSales / IsPurchase on the rate.</summary>
    public bool ForSales { get; set; } = true;

    public bool ForPurchase { get; set; } = true;
}

/// <summary>
/// Asks Accounting to create the GL account behind a bank account. Sent by
/// Banking on create; idempotent, because the account is keyed on the bank
/// account id and a retry must not produce a second one.
/// </summary>
public class ProvisionBankAccountRequest
{
    [Range(1, long.MaxValue, ErrorMessage = "Bank account id is required.")]
    public long BankAccountId { get; set; }

    [Required(ErrorMessage = "Account name is required.")]
    [MaxLength(200, ErrorMessage = "Account name cannot exceed 200 characters.")]
    public string AccountName { get; set; } = null!;

    /// <summary>Savings, Current, OverDraft, CashCredit, CreditCard, Wallet or Cash.</summary>
    [Required(ErrorMessage = "Account type is required.")]
    public string AccountType { get; set; } = "Current";

    /// <summary>Null when the account is in the organization's base currency.</summary>
    [MaxLength(3, ErrorMessage = "Currency must be a 3-letter code.")]
    public string? CurrencyCode { get; set; }
}

public class UpdateBankAccountLedgerRequest
{
    [Required(ErrorMessage = "Account name is required.")]
    [MaxLength(200, ErrorMessage = "Account name cannot exceed 200 characters.")]
    public string AccountName { get; set; } = null!;

    public bool IsActive { get; set; } = true;
}

public enum BankLedgerOutcome
{
    Ok = 0,
    NotFound = 1,
    /// <summary>The seeded parent group is missing, so the account has nowhere to hang.</summary>
    ParentMissing = 2,
    InvalidValue = 3,
}

public sealed record BankLedgerResult(BankLedgerOutcome Outcome, long? AccountId, string? AccountCode);
