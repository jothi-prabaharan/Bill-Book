using System.ComponentModel.DataAnnotations;
using Inventory.Entity.Enums;
using Shared.Kernel.Tenancy;

namespace Inventory.Entity.TableEntities;

/// <summary>
/// Karatage and fineness — 22K, 916, 999. A small master because purity is
/// picked constantly and because it scales the metal rate: a gram of 22K is
/// worth 0.916 of a gram of pure gold, and that factor has to come from one
/// place rather than being typed per item.
/// </summary>
public class MetalPurity : OrgScopedEntity
{
    public long MetalPurityId { get; set; }

    public MetalType MetalType { get; set; } = MetalType.Gold;

    /// <summary>Seeded rows only: the immutable canonical name.</summary>
    [MaxLength(20, ErrorMessage = "Purity system name cannot exceed 20 characters.")]
    public string? PuritySystemName { get; set; }

    /// <summary>What the counter calls it: 22K, 916, 999.</summary>
    [Required(ErrorMessage = "Purity name is required.")]
    [MaxLength(20, ErrorMessage = "Purity name cannot exceed 20 characters.")]
    public string PurityName { get; set; } = null!;

    /// <summary>
    /// Fraction of pure metal — 22K is 0.9160. Multiplies the pure-metal rate to
    /// price a piece, so four decimals are the minimum that keeps a gram honest.
    /// </summary>
    [Range(0.0001, 1.0, ErrorMessage = "Purity factor must be between 0.0001 and 1.")]
    public decimal PurityFactor { get; set; }

    public int DisplayOrder { get; set; }

    public bool IsSystem { get; set; }

    public bool IsActive { get; set; } = true;
}
