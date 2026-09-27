using System.ComponentModel.DataAnnotations;

namespace Master.Entity.Models;

// ---- Custom field definitions (TK-110) -----------------------------------------

public class SaveCustomFieldRequest
{
    /// <summary><c>Contact</c>, <c>Item</c>, <c>Invoice</c> and so on. Fixed once the field exists.</summary>
    [Required(ErrorMessage = "Choose what the field belongs to.")]
    [MaxLength(30, ErrorMessage = "The record kind cannot exceed 30 characters.")]
    public string EntityKind { get; set; } = null!;

    [Required(ErrorMessage = "Key is required.")]
    [MaxLength(40, ErrorMessage = "Key cannot exceed 40 characters.")]
    public string Key { get; set; } = null!;

    [Required(ErrorMessage = "Label is required.")]
    [MaxLength(80, ErrorMessage = "Label cannot exceed 80 characters.")]
    public string Label { get; set; } = null!;

    /// <summary><c>Text</c>, <c>Dropdown</c>, <c>Date</c> and so on. Fixed once the field exists.</summary>
    [Required(ErrorMessage = "Choose the field's type.")]
    [MaxLength(20, ErrorMessage = "The field type cannot exceed 20 characters.")]
    public string DataType { get; set; } = null!;

    public List<SaveCustomFieldOptionRequest> Options { get; set; } = [];

    public bool IsRequired { get; set; }

    [MaxLength(4000, ErrorMessage = "Default value cannot exceed 4000 characters.")]
    public string? DefaultValue { get; set; }

    [Range(1, 4000, ErrorMessage = "Maximum length must be between 1 and 4000.")]
    public int? MaxLength { get; set; }

    public bool ShowInList { get; set; }

    public bool ShowOnPrint { get; set; }

    public bool IsSearchable { get; set; }

    public bool CarryForward { get; set; }

    [Range(0, 9999, ErrorMessage = "Display order must be between 0 and 9999.")]
    public int DisplayOrder { get; set; }

    public bool IsActive { get; set; } = true;
}

public class SaveCustomFieldOptionRequest
{
    [Required(ErrorMessage = "Option value is required.")]
    [MaxLength(60, ErrorMessage = "Option value cannot exceed 60 characters.")]
    public string Value { get; set; } = null!;

    [Required(ErrorMessage = "Option label is required.")]
    [MaxLength(80, ErrorMessage = "Option label cannot exceed 80 characters.")]
    public string Label { get; set; } = null!;

    public bool Active { get; set; } = true;
}

public enum CustomFieldOutcome
{
    Ok,
    NotFound,
    Refused,

    /// <summary>Another field of the same kind already has the key.</summary>
    Duplicate,

    /// <summary>The kind already has its fifty active fields.</summary>
    LimitReached,
}

public sealed record CustomFieldResult(CustomFieldOutcome Outcome, long Id = 0, string? Detail = null);
