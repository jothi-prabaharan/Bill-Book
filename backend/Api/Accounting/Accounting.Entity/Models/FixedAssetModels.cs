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

/// <summary>
/// Taking an asset off the books (D-20). The proceeds land in one of two places,
/// and the request names at most one:
/// <list type="bullet">
/// <item><see cref="ProceedsBankAccountId"/> — the bank or cash account the money
/// was paid into; <see cref="SaleAmount"/> is what it fetched.</item>
/// <item><see cref="SalesInvoiceId"/> — a posted sales invoice raised to the
/// buyer, which already debited their receivable. What the invoice credited
/// before GST is the proceeds, and <see cref="SaleAmount"/> is ignored.</item>
/// </list>
/// Neither, with a zero sale amount, is an asset scrapped for nothing.
/// </summary>
public class DisposeAssetRequest
{
    [Required(ErrorMessage = "Disposal date is required.")]
    public DateOnly DisposalDate { get; set; }

    [Range(0, double.MaxValue, ErrorMessage = "Sale amount cannot be negative.")]
    public decimal SaleAmount { get; set; }

    public long? ProceedsBankAccountId { get; set; }

    public long? SalesInvoiceId { get; set; }

    [MaxLength(500, ErrorMessage = "Notes cannot exceed 500 characters.")]
    public string? Notes { get; set; }
}

/// <summary>
/// Depreciation schedules set on an asset already on the register — one bought
/// on a bill arrives with none, because a bill line says nothing about its life.
/// </summary>
public class SetDepreciationSchedulesRequest
{
    [Required(ErrorMessage = "Schedules are required.")]
    public List<CreateDepreciationScheduleRequest> Schedules { get; set; } = new();
}

/// <summary>
/// A posted bill's capital lines, sent by Purchase so each becomes a register
/// row (D-19, TK-12). Internal: the tenant travels in the body.
/// </summary>
public class CapitaliseBillRequest
{
    [Required(ErrorMessage = "Customer ID is required.")]
    public Guid CustomerId { get; set; }

    [Required(ErrorMessage = "Organization ID is required.")]
    public Guid OrgId { get; set; }

    [Required(ErrorMessage = "Bill ID is required.")]
    public long PurchaseBillId { get; set; }

    [Required(ErrorMessage = "Bill number is required.")]
    [MaxLength(40, ErrorMessage = "Bill number cannot exceed 40 characters.")]
    public string DocumentNo { get; set; } = null!;

    [Required(ErrorMessage = "Bill date is required.")]
    public DateOnly DocumentDate { get; set; }

    [Required(ErrorMessage = "Lines are required.")]
    public List<CapitaliseBillLine> Lines { get; set; } = new();
}

public class CapitaliseBillLine
{
    [Required(ErrorMessage = "Bill line ID is required.")]
    public long BillDetailId { get; set; }

    public int LineNumber { get; set; }

    [Required(ErrorMessage = "Category ID is required.")]
    public long FixedAssetCategoryId { get; set; }

    [Required(ErrorMessage = "Description is required.")]
    [MaxLength(200, ErrorMessage = "Description cannot exceed 200 characters.")]
    public string Description { get; set; } = null!;

    /// <summary>The line's taxable value — what the bill debited to Fixed Asset.</summary>
    [Range(0.01, double.MaxValue, ErrorMessage = "Amount must be greater than zero.")]
    public decimal Amount { get; set; }
}

public sealed record CapitaliseBillResponse(IReadOnlyList<long> FixedAssetIds, long? JournalId);

/// <summary>Why a register write was refused. Every value is something the user can act on.</summary>
public enum FixedAssetOutcome
{
    Ok = 0,

    /// <summary>No such asset in this branch — including another branch's, which the filter hides.</summary>
    NotFound = 1,

    /// <summary>The category is not one of this branch's.</summary>
    CategoryMissing = 2,

    /// <summary>Another asset in this branch already has that code.</summary>
    DuplicateCode = 3,

    /// <summary>
    /// A schedule that could never charge anything, or two schedules of one
    /// type: straight line needs a life or a rate, written-down value a rate
    /// below 100, and salvage cannot exceed cost.
    /// </summary>
    InvalidSchedule = 4,

    /// <summary>The asset is already disposed of, or never went into service.</summary>
    NotActive = 5,

    /// <summary>A disposal dated before the asset was bought.</summary>
    DisposalBeforePurchase = 6,

    /// <summary>The bill already put this asset on the register; capitalising it again would reclassify it twice.</summary>
    AlreadyCapitalised = 7,

    /// <summary>A disposal that fetched something but names nowhere for it to land, or names both places.</summary>
    ProceedsDestinationRequired = 8,

    /// <summary>The bank account is not this branch's, or has no ledger account behind it.</summary>
    ProceedsAccountMissing = 9,

    /// <summary>The sales invoice has no posted sale in the ledger — not posted yet, voided, or not this branch's.</summary>
    InvoiceNotPosted = 10,

    /// <summary>The journal was refused. <see cref="FixedAssetResult.Detail"/> says why.</summary>
    PostingRefused = 11,

    /// <summary>The books are closed for that date.</summary>
    PeriodClosed = 12,

    /// <summary>Depreciation has already been charged on the schedules, so they are history now.</summary>
    SchedulesInUse = 13,

    /// <summary>A system account the entry needs is missing from the chart — a branch seeded before it existed.</summary>
    SystemAccountMissing = 14,
}

public sealed record FixedAssetResult(
    FixedAssetOutcome Outcome, long? FixedAssetId = null, string? Detail = null, long? JournalId = null);

public enum DepreciationRunOutcome
{
    Ok = 0,

    /// <summary>The journal was refused. <see cref="DepreciationRunResult.JournalOutcome"/> says why.</summary>
    JournalRefused = 1,
}

/// <summary>
/// What one depreciation run did. <see cref="AssetsCharged"/> is zero, with no
/// journal, when every active asset had already been charged for the month —
/// a repeated run is a success that did nothing, not an error.
/// </summary>
public sealed record DepreciationRunResult(
    DepreciationRunOutcome Outcome,
    int AssetsCharged = 0,
    long? JournalId = null,
    SaveJournalOutcome? JournalOutcome = null,
    string? Detail = null);
