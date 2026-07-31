using System.ComponentModel.DataAnnotations;
using Shared.Kernel.Tenancy;

namespace Inventory.Entity.TableEntities;

/// <summary>
/// A unit, carrying its factor to the type's base unit. That factor is the only
/// conversion mechanism in the system: any-to-any within a type is
/// <c>qty × Factor(from) ÷ Factor(to)</c>, so there is no pairwise table and no
/// way to record an inconsistent set of conversions.
///
/// Pack sizes are units of their type, not per-item facts: a 50 kg bag is a
/// Weight unit with a factor of 50.
/// </summary>
public class UnitOfMeasure : OrgScopedEntity
{
    public long UomId { get; set; }

    /// <summary>Immutable once an item uses the unit — its factor is relative to this type.</summary>
    public long UomTypeId { get; set; }

    /// <summary>Seeded rows only: the immutable canonical name that seed logic keys on.</summary>
    [MaxLength(20, ErrorMessage = "Unit system name cannot exceed 20 characters.")]
    public string? UomSystemName { get; set; }

    /// <summary>The organization's own code, typed by staff. Unique per organization.</summary>
    [Required(ErrorMessage = "Unit code is required.")]
    [MaxLength(10, ErrorMessage = "Unit code cannot exceed 10 characters.")]
    public string UomCode { get; set; } = null!;

    /// <summary>
    /// The notified GST unit code this reports as on GSTR-1 and the e-invoice.
    /// Separate from UomCode because carat and tola are not notified units — a
    /// jeweller needs their own code and a legal one to report under.
    /// </summary>
    [Required(ErrorMessage = "UQC code is required.")]
    [MaxLength(3, ErrorMessage = "UQC code must be 3 characters.")]
    public string UqcCode { get; set; } = "OTH";

    [Required(ErrorMessage = "Unit name is required.")]
    [MaxLength(50, ErrorMessage = "Unit name cannot exceed 50 characters.")]
    public string UomName { get; set; } = null!;

    /// <summary>At most one per type, enforced by a filtered unique index.</summary>
    public bool IsBaseUnit { get; set; }

    /// <summary>
    /// How many base units one of these is. The base unit's own value is 1.
    /// Changing which unit is the base rescales every factor in the type, so it
    /// is refused once any item references a unit of that type.
    /// </summary>
    [Range(0.000001, 999999999999.0, ErrorMessage = "Conversion to base must be greater than zero.")]
    public decimal ConversionToBase { get; set; } = 1m;

    /// <summary>Drives quantity rounding, not just display. Grams need 3; pieces need 0.</summary>
    [Range(0, 6, ErrorMessage = "Decimal places must be between 0 and 6.")]
    public int DecimalPlaces { get; set; }

    public int DisplayOrder { get; set; }

    public bool IsSystem { get; set; }

    public bool IsActive { get; set; } = true;
}
