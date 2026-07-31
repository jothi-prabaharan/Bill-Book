using System.ComponentModel.DataAnnotations;
using Accounting.Entity.Enums;
using Shared.Kernel.Tenancy;

namespace Accounting.Entity.TableEntities;

/// <summary>
/// Credit terms — Net 30, Due on Receipt, End of Month. Owned by Accounting and
/// surfaced as a Settings screen, the same arrangement as Tax Master: Sales and
/// Purchase both need it, so it cannot live inside either.
/// </summary>
public class PaymentTerm : OrgScopedEntity
{
    public long PaymentTermId { get; set; }

    /// <summary>Seeded rows only: the immutable canonical name. Null for user-created terms.</summary>
    [MaxLength(30, ErrorMessage = "Term system name cannot exceed 30 characters.")]
    public string? TermSystemName { get; set; }

    /// <summary>Display name — editable even on seeded terms.</summary>
    [Required(ErrorMessage = "Term name is required.")]
    [MaxLength(50, ErrorMessage = "Term name cannot exceed 50 characters.")]
    public string TermName { get; set; } = null!;

    public PaymentTermType TermType { get; set; } = PaymentTermType.Net;

    [Range(0, 365, ErrorMessage = "Due days must be between 0 and 365.")]
    public int DueDays { get; set; }

    /// <summary>Only meaningful for DayOfNextMonth. Clamped to the month's length on calculation.</summary>
    [Range(1, 31, ErrorMessage = "Day of month must be between 1 and 31.")]
    public int? DueDayOfMonth { get; set; }

    /// <summary>Early-payment discount, as in "2/10 net 30".</summary>
    [Range(0, 100, ErrorMessage = "Discount percent must be between 0 and 100.")]
    public decimal DiscountPercent { get; set; }

    [Range(0, 365, ErrorMessage = "Discount days must be between 0 and 365.")]
    public int DiscountDays { get; set; }

    /// <summary>Selectable on sales documents.</summary>
    public bool IsSales { get; set; } = true;

    /// <summary>Selectable on purchase documents.</summary>
    public bool IsPurchase { get; set; } = true;

    /// <summary>Preselected on new contacts and documents. At most one per organization.</summary>
    public bool IsDefault { get; set; }

    /// <summary>Seeded terms: renamable, never deletable.</summary>
    public bool IsSystem { get; set; }

    public bool IsActive { get; set; } = true;

    public int DisplayOrder { get; set; }
}
