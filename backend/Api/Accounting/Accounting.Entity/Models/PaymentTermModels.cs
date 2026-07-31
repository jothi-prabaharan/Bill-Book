using System.ComponentModel.DataAnnotations;

namespace Accounting.Entity.Models;

public class PaymentTermListItem
{
    public long PaymentTermId { get; set; }

    public string? TermSystemName { get; set; }

    public string TermName { get; set; } = null!;

    public string TermType { get; set; } = null!;

    public int DueDays { get; set; }

    public int? DueDayOfMonth { get; set; }

    public decimal DiscountPercent { get; set; }

    public int DiscountDays { get; set; }

    public bool IsSales { get; set; }

    public bool IsPurchase { get; set; }

    public bool IsDefault { get; set; }

    public bool IsSystem { get; set; }

    public bool IsActive { get; set; }

    public int DisplayOrder { get; set; }

    /// <summary>
    /// The due date this term produces for a document dated today, so the list
    /// shows what the rule means rather than only how it is configured.
    /// </summary>
    public DateOnly SampleDueDate { get; set; }
}

public class SavePaymentTermRequest
{
    [Required(ErrorMessage = "Term name is required.")]
    [MaxLength(50, ErrorMessage = "Term name cannot exceed 50 characters.")]
    public string TermName { get; set; } = null!;

    [Required(ErrorMessage = "Term type is required.")]
    public string TermType { get; set; } = "Net";

    [Range(0, 365, ErrorMessage = "Due days must be between 0 and 365.")]
    public int DueDays { get; set; }

    [Range(1, 31, ErrorMessage = "Day of month must be between 1 and 31.")]
    public int? DueDayOfMonth { get; set; }

    [Range(0, 100, ErrorMessage = "Discount percent must be between 0 and 100.")]
    public decimal DiscountPercent { get; set; }

    [Range(0, 365, ErrorMessage = "Discount days must be between 0 and 365.")]
    public int DiscountDays { get; set; }

    public bool IsSales { get; set; } = true;

    public bool IsPurchase { get; set; } = true;

    public bool IsActive { get; set; } = true;
}

/// <summary>The due date and the discount deadline for a document on a given date.</summary>
public sealed record DueDateResult(DateOnly DueDate, DateOnly? DiscountUntil, decimal DiscountPercent);

public enum SaveTermOutcome
{
    Ok = 0,
    NotFound = 1,
    DuplicateName = 2,
    NoApplicability = 3,
    DayOfMonthRequired = 4,
    DiscountWindowTooLong = 5,
    DiscountDaysRequired = 6,
    InvalidValue = 7,
    SystemTermLocked = 8,
}

public sealed record SaveTermResult(SaveTermOutcome Outcome, long? PaymentTermId);
