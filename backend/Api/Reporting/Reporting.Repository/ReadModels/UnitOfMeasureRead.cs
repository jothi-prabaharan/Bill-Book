#pragma warning disable CS8618

using Shared.Kernel.Tenancy;

namespace Reporting.Repository.ReadModels;

/// <summary>
/// A unit, carrying its factor to the type's base unit. That factor is the only
/// conversion mechanism in the system: any-to-any within a type is
/// <c>qty × Factor(from) ÷ Factor(to)</c>, so there is no pairwise table and no
/// way to record an inconsistent set of conversions.
///
/// Pack sizes are units of their type, not per-item facts: a 50 kg bag is a
/// Weight unit with a factor of 50.
/// </summary>
public class UnitOfMeasureRead : OrgScopedEntity
{
    public long UomId { get; set; }

    /// <summary>Immutable once an item uses the unit — its factor is relative to this type.</summary>
    public long UomTypeId { get; set; }

    /// <summary>Seeded rows only: the immutable canonical name that seed logic keys on.</summary>

    public string? UomSystemName { get; set; }

    /// <summary>The organization's own code, typed by staff. Unique per organization.</summary>


    public string UomCode { get; set; }
    /// <summary>
    /// The notified GST unit code this reports as on GSTR-1 and the e-invoice.
    /// Separate from UomCode because carat and tola are not notified units — a
    /// jeweller needs their own code and a legal one to report under.
    /// </summary>


    public string UqcCode { get; set; }
    public string UomName { get; set; }
    /// <summary>At most one per type, enforced by a filtered unique index.</summary>
    public bool IsBaseUnit { get; set; }

    /// <summary>
    /// How many base units one of these is. The base unit's own value is 1.
    /// Changing which unit is the base rescales every factor in the type, so it
    /// is refused once any item references a unit of that type.
    /// </summary>

    public decimal ConversionToBase { get; set; }
    /// <summary>Drives quantity rounding, not just display. Grams need 3; pieces need 0.</summary>

    public int DecimalPlaces { get; set; }

    public int DisplayOrder { get; set; }

    public bool IsSystem { get; set; }

    public bool IsActive { get; set; }
}



