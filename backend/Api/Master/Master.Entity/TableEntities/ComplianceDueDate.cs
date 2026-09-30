using System.ComponentModel.DataAnnotations;
using Shared.Kernel.Entities;

namespace Master.Entity.TableEntities;

public enum ComplianceFrequency
{
    Monthly = 1,
    Quarterly = 2,
    HalfYearly = 3,
    Annual = 4,
}

/// <summary>
/// Seeded and operator-editable statutory compliance filing due-date rules (TK-119).
/// Schema mst, in the master database — identical for every customer, and extended globally
/// when the government extends filing deadlines.
/// </summary>
public class ComplianceDueDate : AuditableEntity
{
    public int ComplianceDueDateId { get; set; }

    [Required(ErrorMessage = "Category is required.")]
    [MaxLength(50, ErrorMessage = "Category cannot exceed 50 characters.")]
    public string Category { get; set; } = null!;

    [Required(ErrorMessage = "Form name is required.")]
    [MaxLength(50, ErrorMessage = "Form name cannot exceed 50 characters.")]
    public string FormName { get; set; } = null!;

    [Required(ErrorMessage = "Description is required.")]
    [MaxLength(300, ErrorMessage = "Description cannot exceed 300 characters.")]
    public string Description { get; set; } = null!;

    public ComplianceFrequency Frequency { get; set; } = ComplianceFrequency.Monthly;

    [Range(1, 31, ErrorMessage = "Due day must be between 1 and 31.")]
    public int DueDay { get; set; }

    [Range(0, 12, ErrorMessage = "Month offset must be between 0 and 12.")]
    public int DueMonthOffset { get; set; } = 1;

    [Required(ErrorMessage = "Rule description is required.")]
    [MaxLength(200, ErrorMessage = "Rule description cannot exceed 200 characters.")]
    public string RuleDescription { get; set; } = null!;

    [MaxLength(50, ErrorMessage = "Applicability condition cannot exceed 50 characters.")]
    public string ApplicabilityCondition { get; set; } = "All";

    [MaxLength(20, ErrorMessage = "Specific period cannot exceed 20 characters.")]
    public string? SpecificPeriod { get; set; }

    public DateOnly? ExtendedDueDate { get; set; }

    [MaxLength(500, ErrorMessage = "Extension notice cannot exceed 500 characters.")]
    public string? ExtensionNotice { get; set; }

    public bool IsActive { get; set; } = true;
}
