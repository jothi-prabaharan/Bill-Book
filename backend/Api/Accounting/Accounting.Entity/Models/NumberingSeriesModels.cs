using System.ComponentModel.DataAnnotations;

namespace Accounting.Entity.Models;

public class NumberingSeriesListItem
{
    public long NumberingSeriesId { get; set; }

    public string? SeriesSystemName { get; set; }

    public string SeriesCode { get; set; } = null!;

    public string SeriesName { get; set; } = null!;

    /// <summary>Master or Document.</summary>
    public string SeriesFor { get; set; } = null!;

    public long? BranchId { get; set; }

    public string? Prefix { get; set; }

    public string? Suffix { get; set; }

    public string? Separator { get; set; }

    public bool IncludeFinancialYear { get; set; }

    public string FinancialYearFormat { get; set; } = null!;

    public bool IncludeBranchCode { get; set; }

    public string? BranchCode { get; set; }

    public int NumberLength { get; set; }

    public long StartNumber { get; set; }

    public long NextNumber { get; set; }

    public string ResetFrequency { get; set; } = null!;

    public DateOnly? LastResetOn { get; set; }

    public bool AllowManualOverride { get; set; }

    public bool IsDefault { get; set; }

    public bool IsSystem { get; set; }

    public bool IsActive { get; set; }

    public int DisplayOrder { get; set; }

    /// <summary>
    /// The code the next allocation would produce, composed by the same code
    /// that allocation uses. Shown on the list so a mis-set prefix or padding is
    /// visible before it reaches a document.
    /// </summary>
    public string Preview { get; set; } = null!;
}

public class SaveNumberingSeriesRequest
{
    [Required(ErrorMessage = "Series code is required.")]
    [MaxLength(30, ErrorMessage = "Series code cannot exceed 30 characters.")]
    public string SeriesCode { get; set; } = null!;

    [Required(ErrorMessage = "Series name is required.")]
    [MaxLength(50, ErrorMessage = "Series name cannot exceed 50 characters.")]
    public string SeriesName { get; set; } = null!;

    /// <summary>Master or Document. Ignored on update — a series cannot change what it numbers.</summary>
    [Required(ErrorMessage = "Series type is required.")]
    public string SeriesFor { get; set; } = "Master";

    public long? BranchId { get; set; }

    [MaxLength(15, ErrorMessage = "Prefix cannot exceed 15 characters.")]
    public string? Prefix { get; set; }

    [MaxLength(15, ErrorMessage = "Suffix cannot exceed 15 characters.")]
    public string? Suffix { get; set; }

    [MaxLength(1, ErrorMessage = "Separator cannot exceed 1 character.")]
    public string? Separator { get; set; } = "-";

    public bool IncludeFinancialYear { get; set; }

    [Required(ErrorMessage = "Financial year format is required.")]
    public string FinancialYearFormat { get; set; } = "Compact";

    public bool IncludeBranchCode { get; set; }

    [MaxLength(10, ErrorMessage = "Branch code cannot exceed 10 characters.")]
    public string? BranchCode { get; set; }

    [Range(1, 12, ErrorMessage = "Number length must be between 1 and 12.")]
    public int NumberLength { get; set; } = 5;

    [Range(0, long.MaxValue, ErrorMessage = "Start number cannot be negative.")]
    public long StartNumber { get; set; } = 1;

    [Required(ErrorMessage = "Reset frequency is required.")]
    public string ResetFrequency { get; set; } = "Never";

    public bool AllowManualOverride { get; set; }

    public bool IsActive { get; set; } = true;

    // NextNumber is not here on purpose. Moving a live counter is a separate,
    // audited action rather than a field someone edits while renaming a series.
}

public class SetNextNumberRequest
{
    [Range(0, long.MaxValue, ErrorMessage = "Next number cannot be negative.")]
    public long NextNumber { get; set; }
}

/// <summary>
/// A drag-and-drop drop, described by its neighbours rather than by a computed
/// position. The server derives the value, so two people dragging at once cannot
/// both compute the same one.
/// </summary>
public class ReorderRequest
{
    public long MovedId { get; set; }

    /// <summary>The row now above the moved row. Null means it moved to the top.</summary>
    public long? PreviousId { get; set; }

    /// <summary>The row now below the moved row. Null means it moved to the bottom.</summary>
    public long? NextId { get; set; }
}

public enum SaveSeriesOutcome
{
    Ok = 0,
    NotFound = 1,
    DuplicateName = 2,
    /// <summary>The series code identifies a seeded series and cannot be changed.</summary>
    SeriesCodeLocked = 3,
    ManualOverrideOnDocument = 4,
    BranchCodeRequired = 5,
    NextNumberBelowStart = 6,
    /// <summary>An enum value the caller sent does not exist.</summary>
    InvalidValue = 7,
    /// <summary>Deactivating this would leave its code with no active series.</summary>
    LastActiveSeries = 8,
}

public sealed record SaveSeriesResult(SaveSeriesOutcome Outcome, long? NumberingSeriesId);

/// <summary>
/// Moving a live counter. <c>Lowered</c> means the new value walks back over
/// numbers that may already be on records — allowed, because an administrator
/// sometimes must, but the caller is told so it can say so.
/// </summary>
public sealed record SetNextNumberResult(SaveSeriesOutcome Outcome, bool Lowered);
