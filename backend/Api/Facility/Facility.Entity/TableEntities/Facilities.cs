using System.ComponentModel.DataAnnotations;
using Facility.Entity.Enums;
using Shared.Kernel.Tenancy;

namespace Facility.Entity.TableEntities;

/// <summary>A building on the campus. Deactivated, never deleted, because spaces and assets name it.</summary>
public class Building : OrgScopedEntity
{
    public long BuildingId { get; set; }

    [Required(ErrorMessage = "Code is required.")]
    [MaxLength(20, ErrorMessage = "Code cannot exceed 20 characters.")]
    public string Code { get; set; } = null!;

    [Required(ErrorMessage = "Name is required.")]
    [MaxLength(100, ErrorMessage = "Name cannot exceed 100 characters.")]
    public string Name { get; set; } = null!;

    [Range(1, 200, ErrorMessage = "Floors must be between 1 and 200.")]
    public int Floors { get; set; } = 1;

    public bool IsActive { get; set; } = true;
}

/// <summary>A room or area in a building: a classroom, a lab, the playground.</summary>
public class Space : OrgScopedEntity
{
    public long SpaceId { get; set; }

    public long BuildingId { get; set; }

    [Required(ErrorMessage = "Code is required.")]
    [MaxLength(20, ErrorMessage = "Code cannot exceed 20 characters.")]
    public string Code { get; set; } = null!;

    [Required(ErrorMessage = "Name is required.")]
    [MaxLength(100, ErrorMessage = "Name cannot exceed 100 characters.")]
    public string Name { get; set; } = null!;

    public SpaceKind SpaceKind { get; set; } = SpaceKind.Classroom;

    /// <summary>0 is the ground floor; basements are negative.</summary>
    [Range(-10, 200, ErrorMessage = "Floor must be between -10 and 200.")]
    public int Floor { get; set; }

    [Range(1, 10000, ErrorMessage = "Capacity must be between 1 and 10000.")]
    public int? Capacity { get; set; }

    public bool IsActive { get; set; } = true;
}

/// <summary>Something that is maintained: an AC, a pump, a projector, a bench.</summary>
public class FacilityAsset : OrgScopedEntity
{
    public long FacilityAssetId { get; set; }

    [Required(ErrorMessage = "Asset tag is required.")]
    [MaxLength(30, ErrorMessage = "Asset tag cannot exceed 30 characters.")]
    public string AssetTag { get; set; } = null!;

    [Required(ErrorMessage = "Name is required.")]
    [MaxLength(200, ErrorMessage = "Name cannot exceed 200 characters.")]
    public string Name { get; set; } = null!;

    public AssetCategory AssetCategory { get; set; } = AssetCategory.Other;

    public long? SpaceId { get; set; }

    [MaxLength(100, ErrorMessage = "Make cannot exceed 100 characters.")]
    public string? Make { get; set; }

    [MaxLength(100, ErrorMessage = "Model cannot exceed 100 characters.")]
    public string? Model { get; set; }

    [MaxLength(100, ErrorMessage = "Serial number cannot exceed 100 characters.")]
    public string? SerialNo { get; set; }

    public DateOnly? PurchaseDate { get; set; }

    public DateOnly? WarrantyUntil { get; set; }

    public decimal? PurchaseCost { get; set; }

    public AssetStatus AssetStatus { get; set; } = AssetStatus.InUse;

    /// <summary>Reserved for the fixed asset register: a chair is maintained and never depreciated alone, an AC is both.</summary>
    public long? FixedAssetId { get; set; }
}
