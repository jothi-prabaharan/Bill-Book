using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Accounting.Entity.Enums;

namespace Accounting.Entity.Models;

public record FixedAssetCategoryModel(
    long FixedAssetCategoryId,
    string CategoryName,
    long AssetAccountId,
    long AccumulatedDepreciationAccountId,
    long DepreciationExpenseAccountId
);

public class CreateFixedAssetRequest
{
    [Required(ErrorMessage = "Category ID is required.")]
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

    [Required(ErrorMessage = "Purchase date is required.")]
    public DateOnly PurchaseDate { get; set; }
    
    [Required(ErrorMessage = "Purchase price is required.")]
    [Range(0.01, double.MaxValue, ErrorMessage = "Purchase price must be greater than zero.")]
    public decimal PurchasePrice { get; set; }

    public long? PurchaseBillId { get; set; }
    
    public FixedAssetStatus Status { get; set; }
    
    public List<CreateDepreciationScheduleRequest> Schedules { get; set; } = new();
}

public class CreateDepreciationScheduleRequest
{
    public DepreciationScheduleType ScheduleType { get; set; }
    public DepreciationMethod DepreciationMethod { get; set; }
    public decimal Rate { get; set; }
    public int UsefulLifeYears { get; set; }
    public DateOnly DepreciationStartDate { get; set; }
    public decimal SalvageValue { get; set; }
}

public record FixedAssetModel(
    long FixedAssetId,
    long FixedAssetCategoryId,
    string AssetCode,
    string AssetName,
    string? Description,
    string? SerialNumber,
    DateOnly PurchaseDate,
    decimal PurchasePrice,
    long? PurchaseBillId,
    FixedAssetStatus Status
);

public class CapitalizeAssetRequest
{
    [Required(ErrorMessage = "Category ID is required.")]
    public long FixedAssetCategoryId { get; set; }

    [Required(ErrorMessage = "Asset code is required.")]
    [MaxLength(50, ErrorMessage = "Asset code cannot exceed 50 characters.")]
    public string AssetCode { get; set; } = null!;

    [Required(ErrorMessage = "Asset name is required.")]
    public string AssetName { get; set; } = null!;
    
    [Required(ErrorMessage = "Purchase Bill ID is required.")]
    public long PurchaseBillId { get; set; }
    
    [Required(ErrorMessage = "Purchase price is required.")]
    public decimal PurchasePrice { get; set; }
    
    [Required(ErrorMessage = "Purchase date is required.")]
    public DateOnly PurchaseDate { get; set; }
}

public class DisposeAssetRequest
{
    [Required(ErrorMessage = "Disposal date is required.")]
    public DateOnly DisposalDate { get; set; }

    public decimal SaleAmount { get; set; }
    
    public string? Notes { get; set; }
}
