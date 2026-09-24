using System.ComponentModel.DataAnnotations;
using Hrm.Entity.Enums;

namespace Hrm.Entity.Models;

public sealed class SaveChecklistTemplateRequest
{
    [Required(ErrorMessage = "Template name is required.")]
    [MaxLength(100, ErrorMessage = "Template name cannot exceed 100 characters.")]
    public string Name { get; set; } = null!;

    public ChecklistKind Kind { get; set; }

    public List<SaveChecklistTemplateItemRequest> Items { get; set; } = [];
}

public sealed class SaveChecklistTemplateItemRequest
{
    [Required(ErrorMessage = "Title is required.")]
    [MaxLength(200, ErrorMessage = "Title cannot exceed 200 characters.")]
    public string Title { get; set; } = null!;

    public ChecklistOwnerRole OwnerRole { get; set; }

    public int SortOrder { get; set; }
}

public sealed class CreateEmployeeChecklistRequest
{
    [Range(1, long.MaxValue, ErrorMessage = "Employee ID is required.")]
    public long EmployeeId { get; set; }

    public ChecklistKind Kind { get; set; }

    public long? ChecklistTemplateId { get; set; }
}

public sealed class UpdateChecklistItemRequest
{
    public bool IsDone { get; set; }

    [MaxLength(500, ErrorMessage = "Remarks cannot exceed 500 characters.")]
    public string? Remarks { get; set; }
}

public sealed class SaveSeparationRequest
{
    [Range(1, long.MaxValue, ErrorMessage = "Employee ID is required.")]
    public long EmployeeId { get; set; }

    public SeparationKind Kind { get; set; }

    public DateOnly RequestDate { get; set; }

    public DateOnly LastWorkingDate { get; set; }

    public bool IsNoticeWaived { get; set; }

    [Required(ErrorMessage = "Reason is required.")]
    [MaxLength(500, ErrorMessage = "Reason cannot exceed 500 characters.")]
    public string Reason { get; set; } = null!;

    [MaxLength(2000, ErrorMessage = "Exit interview notes cannot exceed 2000 characters.")]
    public string? ExitInterviewNotes { get; set; }
}
