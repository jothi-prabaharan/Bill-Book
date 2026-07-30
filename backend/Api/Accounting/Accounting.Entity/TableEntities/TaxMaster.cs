using System.ComponentModel.DataAnnotations;
using Shared.Kernel.Tenancy;

namespace Accounting.Entity.TableEntities;

/// <summary>
/// A GST rate, effective-dated. Rates are revised by law, so a change never
/// overwrites: the current row's EffectiveTo is closed and a new row inserted.
/// A document dated last March must resolve the rate in force on its own date.
/// </summary>
public class TaxMaster : OrgScopedEntity
{
    public long TaxMasterId { get; set; }

    /// <summary>
    /// Stable across revisions — every version of "GST 18%" shares one group id,
    /// which is what sub-accounts reference. Without it a revision would spawn a
    /// second set of GST sub-accounts and split the sub-ledger at that date.
    /// Set to the row's own id on first creation.
    /// </summary>
    public long TaxGroupId { get; set; }

    /// <summary>Seeded rows only: the immutable canonical name code matches on, e.g. GST18.</summary>
    [MaxLength(50, ErrorMessage = "Tax system name cannot exceed 50 characters.")]
    public string? TaxSystemName { get; set; }

    /// <summary>Display name — editable even on seeded rows.</summary>
    [Required(ErrorMessage = "Tax name is required.")]
    [MaxLength(50, ErrorMessage = "Tax name cannot exceed 50 characters.")]
    public string TaxName { get; set; } = null!;

    [Range(0, 999.99, ErrorMessage = "Total rate must be between 0 and 999.99.")]
    public decimal TotalRate { get; set; }

    [Range(0, 999.99, ErrorMessage = "CGST rate must be between 0 and 999.99.")]
    public decimal CgstRate { get; set; }

    [Range(0, 999.99, ErrorMessage = "SGST rate must be between 0 and 999.99.")]
    public decimal SgstRate { get; set; }

    [Range(0, 999.99, ErrorMessage = "IGST rate must be between 0 and 999.99.")]
    public decimal IgstRate { get; set; }

    /// <summary>
    /// Percentage cess. Note some goods are cessed as a fixed amount per unit,
    /// which this column cannot express — see the tax documentation.
    /// </summary>
    [Range(0, 999.99, ErrorMessage = "Cess rate must be between 0 and 999.99.")]
    public decimal CessRate { get; set; }

    public DateOnly EffectiveFrom { get; set; }

    /// <summary>Null means currently in force.</summary>
    public DateOnly? EffectiveTo { get; set; }

    /// <summary>Selectable as output tax on sales documents.</summary>
    public bool IsSales { get; set; } = true;

    /// <summary>Selectable as input tax on purchase documents.</summary>
    public bool IsPurchase { get; set; } = true;

    public bool IsActive { get; set; } = true;
}
