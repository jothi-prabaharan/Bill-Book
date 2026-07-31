using System.ComponentModel.DataAnnotations;

namespace Accounting.Entity.Models;

public class TaxMasterListItem
{
    public long TaxMasterId { get; set; }

    public long TaxGroupId { get; set; }

    public string? TaxSystemName { get; set; }

    public string TaxName { get; set; } = null!;

    public decimal TotalRate { get; set; }

    public decimal CgstRate { get; set; }

    public decimal SgstRate { get; set; }

    public decimal IgstRate { get; set; }

    public decimal CessRate { get; set; }

    public DateOnly EffectiveFrom { get; set; }

    public DateOnly? EffectiveTo { get; set; }

    public bool IsSales { get; set; }

    public bool IsPurchase { get; set; }

    public bool IsActive { get; set; }

    /// <summary>True when EffectiveTo is null — this is the version in force.</summary>
    public bool IsCurrent { get; set; }

    /// <summary>Seeded rate: the split and system name are fixed, only the display name is editable.</summary>
    public bool IsSystemRate { get; set; }
}

public class SaveTaxMasterRequest
{
    [Required(ErrorMessage = "Tax name is required.")]
    [MaxLength(50, ErrorMessage = "Tax name cannot exceed 50 characters.")]
    public string TaxName { get; set; } = null!;

    [Range(0, 999.99, ErrorMessage = "Total rate must be between 0 and 999.99.")]
    public decimal TotalRate { get; set; }

    [Range(0, 999.99, ErrorMessage = "Cess rate must be between 0 and 999.99.")]
    public decimal CessRate { get; set; }

    /// <summary>
    /// When the new rate takes effect. On a revision the previous version is
    /// closed the day before this date.
    /// </summary>
    [Required(ErrorMessage = "Effective from date is required.")]
    public DateOnly EffectiveFrom { get; set; }

    public bool IsSales { get; set; } = true;

    public bool IsPurchase { get; set; } = true;

    public bool IsActive { get; set; } = true;

    // CGST, SGST and IGST are derived from TotalRate, never supplied — if a
    // caller could set them independently the split invariant would drift.
}
