using System.ComponentModel.DataAnnotations;
using Shared.Kernel.Tenancy;

namespace Accounting.Entity.TableEntities;

/// <summary>
/// The chart of accounts. Two levels only — mst.AccountTypes above, this below,
/// with ParentAccountId supplying display grouping. Sub-types were removed, so
/// IsContra lives here.
/// </summary>
public class Account : OrgScopedEntity
{
    public long AccountId { get; set; }

    /// <summary>
    /// References mst.AccountTypes in the master database, so no FK is possible.
    /// Chosen directly on create and immutable once the account is used.
    /// </summary>
    public int AccountTypeId { get; set; }

    /// <summary>
    /// Normal balance runs opposite its type — accumulated depreciation, sales
    /// returns, discount given, purchase returns. Reports subtract these.
    /// </summary>
    public bool IsContra { get; set; }

    [Required(ErrorMessage = "Account code is required.")]
    [MaxLength(20, ErrorMessage = "Account code cannot exceed 20 characters.")]
    public string AccountCode { get; set; } = null!;

    /// <summary>System accounts only: the immutable canonical name code keys on.</summary>
    [MaxLength(200, ErrorMessage = "Account system name cannot exceed 200 characters.")]
    public string? AccountSystemName { get; set; }

    /// <summary>Display name. Editable even on system accounts.</summary>
    [Required(ErrorMessage = "Account name is required.")]
    [MaxLength(200, ErrorMessage = "Account name cannot exceed 200 characters.")]
    public string AccountName { get; set; } = null!;

    public long? ParentAccountId { get; set; }

    /// <summary>Null means the organization's base currency.</summary>
    [MaxLength(3, ErrorMessage = "Currency code must be a 3-letter code.")]
    public string? CurrencyCode { get; set; }

    /// <summary>Seeded control account — never deletable, config-locked from creation.</summary>
    public bool IsSystemDefault { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Set true the first time anything references this account, and never
    /// cleared. Together with IsSystemDefault it forms the config lock that
    /// freezes type, code and usage flags — reclassifying an account that
    /// already holds postings would silently move history between reports.
    /// </summary>
    public bool IsUsed { get; set; }

    /// <summary>
    /// Selectable on a manual journal line. Backend-only: never exposed as a
    /// customer-facing toggle, and settable only while the config is unlocked.
    /// </summary>
    public bool IsJE { get; set; }

    /// <summary>Posting freeze — no posting of any kind. Operational and reversible.</summary>
    public bool IsLock { get; set; }

    public bool IsSales { get; set; }

    public bool IsPurchase { get; set; }

    public bool IsPayment { get; set; }

    /// <summary>A bank or cash account: appears in bank pickers, reconciliation and transfers.</summary>
    public bool IsBank { get; set; }
}
