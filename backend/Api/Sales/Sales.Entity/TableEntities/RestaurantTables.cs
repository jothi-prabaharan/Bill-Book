using System.ComponentModel.DataAnnotations;
using Shared.Kernel.Restaurant;
using Shared.Kernel.Tenancy;

namespace Sales.Entity.TableEntities;

/// <summary>A room or area of a restaurant (TK-145): Ground floor, Terrace, AC hall.</summary>
public class Floor : OrgScopedEntity
{
    public long FloorId { get; set; }

    [Required(ErrorMessage = "Enter the floor's name.")]
    [MaxLength(50, ErrorMessage = "A floor's name can be at most 50 characters.")]
    public string Name { get; set; } = null!;

    public int DisplayOrder { get; set; }

    public bool IsActive { get; set; } = true;
}

/// <summary>
/// A table guests sit at (TK-145). Tables pushed together for a large party
/// point at the head of the group through <see cref="MergedIntoTableId"/>;
/// the group is ordered and billed as the head table.
/// </summary>
public class RestaurantTable : OrgScopedEntity
{
    public long RestaurantTableId { get; set; }

    public long FloorId { get; set; }

    [Required(ErrorMessage = "Enter the table's number or code.")]
    [MaxLength(20, ErrorMessage = "A table code can be at most 20 characters.")]
    public string Code { get; set; } = null!;

    [Range(1, 100, ErrorMessage = "A table seats between 1 and 100.")]
    public int Seats { get; set; } = 4;

    public TableStatus Status { get; set; } = TableStatus.Free;

    /// <summary>The head table of the group this one was merged into; null when it stands alone or is the head.</summary>
    public long? MergedIntoTableId { get; set; }

    public int DisplayOrder { get; set; }

    public bool IsActive { get; set; } = true;
}
