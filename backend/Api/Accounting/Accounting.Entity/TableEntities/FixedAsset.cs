using System;
using System.ComponentModel.DataAnnotations;
using Accounting.Entity.Enums;
using Shared.Kernel.Tenancy;

namespace Accounting.Entity.TableEntities;

public class FixedAsset : OrgScopedEntity
{
    public long FixedAssetId { get; set; }

    public long FixedAssetCategoryId { get; set; }

    [Required(ErrorMessage = "Asset code is required.")]
    [MaxLength(50, ErrorMessage = "Asset code cannot exceed 50 characters.")]
    public string AssetCode { get; set; } = null!;

    [Required(ErrorMessage = "Asset name is required.")]
    [MaxLength(200, ErrorMessage = "Asset name cannot exceed 200 characters.")]
    public string AssetName { get; set; } = null!;

    [MaxLength(500, ErrorMessage = "Description cannot exceed 500 characters.")]
    public string? Description { get; set; }

    [MaxLength(50, ErrorMessage = "Serial number cannot exceed 50 characters.")]
    public string? SerialNumber { get; set; }

    public DateOnly PurchaseDate { get; set; }
    
    public decimal PurchasePrice { get; set; }

    public long? PurchaseBillId { get; set; }

    public FixedAssetStatus Status { get; set; }
}
