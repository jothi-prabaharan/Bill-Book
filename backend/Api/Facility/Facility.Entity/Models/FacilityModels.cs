using System.ComponentModel.DataAnnotations;
using Facility.Entity.Enums;

namespace Facility.Entity.Models;

// Requests and views for the fac API (S5, TK-65). One shape each way: a view is
// the request plus its id and, for a space or asset, where it is.

public sealed record FacilityMessage(string Message);

public sealed class SaveBuildingRequest
{
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

public sealed class BuildingView
{
    public long BuildingId { get; set; }

    public string Code { get; set; } = null!;

    public string Name { get; set; } = null!;

    public int Floors { get; set; }

    public bool IsActive { get; set; }

    public int Spaces { get; set; }
}

public sealed class SaveSpaceRequest
{
    [Range(1, long.MaxValue, ErrorMessage = "Choose a building.")]
    public long BuildingId { get; set; }

    [Required(ErrorMessage = "Code is required.")]
    [MaxLength(20, ErrorMessage = "Code cannot exceed 20 characters.")]
    public string Code { get; set; } = null!;

    [Required(ErrorMessage = "Name is required.")]
    [MaxLength(100, ErrorMessage = "Name cannot exceed 100 characters.")]
    public string Name { get; set; } = null!;

    public SpaceKind SpaceKind { get; set; } = SpaceKind.Classroom;

    [Range(-10, 200, ErrorMessage = "Floor must be between -10 and 200.")]
    public int Floor { get; set; }

    [Range(1, 10000, ErrorMessage = "Capacity must be between 1 and 10000.")]
    public int? Capacity { get; set; }

    public bool IsActive { get; set; } = true;
}

public sealed class SpaceView
{
    public long SpaceId { get; set; }

    public long BuildingId { get; set; }

    public string BuildingName { get; set; } = null!;

    public string Code { get; set; } = null!;

    public string Name { get; set; } = null!;

    public SpaceKind SpaceKind { get; set; }

    public int Floor { get; set; }

    public int? Capacity { get; set; }

    public bool IsActive { get; set; }
}

public sealed class SaveAssetRequest
{
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

    [Range(typeof(decimal), "0", "1000000000", ErrorMessage = "The purchase cost cannot be negative.")]
    public decimal? PurchaseCost { get; set; }

    public AssetStatus AssetStatus { get; set; } = AssetStatus.InUse;
}

public sealed class AssetView
{
    public long FacilityAssetId { get; set; }

    public string AssetTag { get; set; } = null!;

    public string Name { get; set; } = null!;

    public AssetCategory AssetCategory { get; set; }

    public long? SpaceId { get; set; }

    public string? SpaceName { get; set; }

    public string? BuildingName { get; set; }

    public string? Make { get; set; }

    public string? Model { get; set; }

    public string? SerialNo { get; set; }

    public DateOnly? PurchaseDate { get; set; }

    public DateOnly? WarrantyUntil { get; set; }

    public decimal? PurchaseCost { get; set; }

    public AssetStatus AssetStatus { get; set; }

    /// <summary>Whether the warranty runs on the day asked about.</summary>
    public bool IsUnderWarranty { get; set; }
}
