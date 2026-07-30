using System.ComponentModel.DataAnnotations;
using Accounting.Entity.Enums;
using Shared.Kernel.Tenancy;

namespace Accounting.Entity.TableEntities;

/// <summary>
/// Per-contact, per-item and per-tax detail beneath a control account. Keeps the
/// chart of accounts small while giving the ledger a sub-dimension to report on.
///
/// Never created by hand — always a side effect of the owning master:
/// Contact → 2 (AR, AP) · Item → 3 (Inventory, COGS, Sales Revenue) ·
/// Tax rate → up to 6 (CGST/SGST/IGST under each of Input and Output GST).
/// </summary>
public class SubAccount : OrgScopedEntity
{
    public long SubAccountId { get; set; }

    /// <summary>Denormalized from the parent account — derived on write, never accepted from a caller.</summary>
    public int AccountTypeId { get; set; }

    public long AccountId { get; set; }

    public SubAccountReferenceType ReferenceType { get; set; }

    /// <summary>ContactId, ItemId or TaxRateId. Polymorphic, so no FK.</summary>
    public long ReferenceId { get; set; }

    /// <summary>
    /// Completes the key for tax sub-accounts, so CGST, SGST and IGST can share
    /// one parent account and one rate. None for contact and item sub-accounts.
    /// </summary>
    public TaxComponent TaxComponent { get; set; } = TaxComponent.None;

    [Required(ErrorMessage = "Sub-account name is required.")]
    [MaxLength(200, ErrorMessage = "Sub-account name cannot exceed 200 characters.")]
    public string SubAccountName { get; set; } = null!;

    public bool IsActive { get; set; } = true;
}
