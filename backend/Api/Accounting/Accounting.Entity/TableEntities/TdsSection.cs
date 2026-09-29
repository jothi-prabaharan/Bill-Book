using System.ComponentModel.DataAnnotations;
using Accounting.Entity.Enums;
using Shared.Kernel.Tenancy;

namespace Accounting.Entity.TableEntities;

/// <summary>
/// A TDS section with its rates and thresholds (TK-117), effective-dated like
/// the Tax Master: a Finance Act revises a rate by closing the current row and
/// adding one, so a bill dated last year is taxed as it was then.
///
/// Seeded per branch and editable (D-32): the familiar codes with the 2025-26
/// figures. A contact's <c>TdsSection</c> names a <see cref="SectionCode"/>.
/// </summary>
public class TdsSection : OrgScopedEntity
{
    public long TdsSectionId { get; set; }

    /// <summary>194C, 194J… Several rows share a code, one per period.</summary>
    [Required(ErrorMessage = "Section code is required.")]
    [MaxLength(10, ErrorMessage = "Section code cannot exceed 10 characters.")]
    public string SectionCode { get; set; } = null!;

    [Required(ErrorMessage = "Nature of payment is required.")]
    [MaxLength(200, ErrorMessage = "Nature of payment cannot exceed 200 characters.")]
    public string NatureOfPayment { get; set; } = null!;

    public DateOnly EffectiveFrom { get; set; }

    /// <summary>Null while in force.</summary>
    public DateOnly? EffectiveTo { get; set; }

    /// <summary>Percent, for an individual or HUF with a PAN.</summary>
    [Range(0, 100, ErrorMessage = "Rate must be between 0 and 100 percent.")]
    public decimal RateIndividual { get; set; }

    /// <summary>Percent, for every other deductee with a PAN.</summary>
    [Range(0, 100, ErrorMessage = "Rate must be between 0 and 100 percent.")]
    public decimal RateOther { get; set; }

    /// <summary>Percent, for a deductee with no valid PAN (section 206AA).</summary>
    [Range(0, 100, ErrorMessage = "Rate must be between 0 and 100 percent.")]
    public decimal RateNoPan { get; set; }

    /// <summary>One document above this is taxed on its own. Null: no single threshold.</summary>
    [Range(typeof(decimal), "0", "79228162514264337593543950335", ErrorMessage = "Threshold cannot be negative.")]
    public decimal? SingleThreshold { get; set; }

    /// <summary>The year's total above this is taxed. Null: no aggregate threshold.</summary>
    [Range(typeof(decimal), "0", "79228162514264337593543950335", ErrorMessage = "Threshold cannot be negative.")]
    public decimal? AggregateThreshold { get; set; }

    public TdsThresholdBasis ThresholdBasis { get; set; } = TdsThresholdBasis.WholeAmount;

    public bool IsActive { get; set; } = true;
}
