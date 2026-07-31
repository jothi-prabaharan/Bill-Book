using System.ComponentModel.DataAnnotations;
using Inventory.Entity.Enums;
using Shared.Kernel.Tenancy;

namespace Inventory.Entity.TableEntities;

/// <summary>
/// Pharma attributes, one row per item at most. ItemId is both key and foreign
/// key.
///
/// Every required column here is a plain NOT NULL, which is the whole reason
/// this is a separate table: inline on the item they would all have to be
/// nullable, and "a medicine must have a salt name" would become a conditional
/// check that grows with every vertical added.
/// </summary>
public class ItemPharmaDetails : OrgScopedEntity
{
    /// <summary>Primary key and foreign key both.</summary>
    public long ItemId { get; set; }

    /// <summary>The salt or composition — what a substitute is searched by.</summary>
    [Required(ErrorMessage = "Generic name is required.")]
    [MaxLength(200, ErrorMessage = "Generic name cannot exceed 200 characters.")]
    public string GenericName { get; set; } = null!;

    [MaxLength(50, ErrorMessage = "Strength cannot exceed 50 characters.")]
    public string? Strength { get; set; }

    public DosageForm DosageForm { get; set; } = DosageForm.Tablet;

    /// <summary>"10 tablets", "100 ml" — what one saleable unit contains.</summary>
    [Required(ErrorMessage = "Pack size is required.")]
    [MaxLength(50, ErrorMessage = "Pack size cannot exceed 50 characters.")]
    public string PackSize { get; set; } = null!;

    [Required(ErrorMessage = "Manufacturer is required.")]
    [MaxLength(200, ErrorMessage = "Manufacturer cannot exceed 200 characters.")]
    public string ManufacturerName { get; set; } = null!;

    /// <summary>Often a different company from the manufacturer.</summary>
    [MaxLength(200, ErrorMessage = "Marketed by cannot exceed 200 characters.")]
    public string? MarketedBy { get; set; }

    public DrugSchedule DrugSchedule { get; set; } = DrugSchedule.None;

    /// <summary>Forced true for schedules H, H1 and X.</summary>
    public bool IsPrescriptionRequired { get; set; }

    public bool IsNarcotic { get; set; }

    public StorageCondition StorageCondition { get; set; } = StorageCondition.Ambient;

    [Range(1, 3650, ErrorMessage = "Shelf life must be between 1 and 3650 days.")]
    public int? ShelfLifeDays { get; set; }

    /// <summary>
    /// Inward stock expiring sooner than this is refused. The single most useful
    /// field on this table — it stops a distributor offloading short-dated stock
    /// that will be written off rather than sold.
    /// </summary>
    [Range(0, 3650, ErrorMessage = "Minimum expiry days must be between 0 and 3650.")]
    public int MinExpiryDaysOnReceipt { get; set; }

    [Range(0, 3650, ErrorMessage = "Expiry alert days must be between 0 and 3650.")]
    public int ExpiryAlertDays { get; set; } = 90;
}
