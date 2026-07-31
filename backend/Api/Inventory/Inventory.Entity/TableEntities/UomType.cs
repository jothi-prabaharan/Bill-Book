using System.ComponentModel.DataAnnotations;
using Shared.Kernel.Tenancy;

namespace Inventory.Entity.TableEntities;

/// <summary>
/// A measurement family — Quantity, Weight, Volume. A table rather than an enum
/// because customers add their own, and because the base unit every conversion
/// hangs off is defined per type.
///
/// There is deliberately no BaseUomId here. The base is a flag on the unit,
/// where a filtered unique index guarantees at most one per type and the unit is
/// by construction in the right type — a pointer here could not be NOT NULL (the
/// type exists before its units do) and nothing would stop it aiming at a unit
/// of another type.
/// </summary>
public class UomType : OrgScopedEntity
{
    public long UomTypeId { get; set; }

    /// <summary>Seeded rows only: the immutable canonical name. Null for user-created types.</summary>
    [MaxLength(30, ErrorMessage = "Unit type system name cannot exceed 30 characters.")]
    public string? UomTypeSystemName { get; set; }

    /// <summary>Display name — editable even on seeded types.</summary>
    [Required(ErrorMessage = "Unit type name is required.")]
    [MaxLength(50, ErrorMessage = "Unit type name cannot exceed 50 characters.")]
    public string UomTypeName { get; set; } = null!;

    public int DisplayOrder { get; set; }

    /// <summary>Seeded types: renamable, never deletable.</summary>
    public bool IsSystem { get; set; }

    public bool IsActive { get; set; } = true;
}
