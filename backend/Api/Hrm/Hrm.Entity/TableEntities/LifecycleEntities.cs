using System.ComponentModel.DataAnnotations;
using Hrm.Entity.Enums;
using Shared.Kernel.Tenancy;

namespace Hrm.Entity.TableEntities;

public class ChecklistTemplate : OrgScopedEntity
{
    public long ChecklistTemplateId { get; set; }

    [Required(ErrorMessage = "Template name is required.")]
    [MaxLength(100, ErrorMessage = "Template name cannot exceed 100 characters.")]
    public string Name { get; set; } = null!;

    public ChecklistKind Kind { get; set; }

    public ICollection<ChecklistTemplateItem> Items { get; set; } = new List<ChecklistTemplateItem>();
}

public class ChecklistTemplateItem : OrgScopedEntity
{
    public long ChecklistTemplateItemId { get; set; }

    public long ChecklistTemplateId { get; set; }

    public ChecklistTemplate Template { get; set; } = null!;

    [Required(ErrorMessage = "Title is required.")]
    [MaxLength(200, ErrorMessage = "Title cannot exceed 200 characters.")]
    public string Title { get; set; } = null!;

    public ChecklistOwnerRole OwnerRole { get; set; }

    public int SortOrder { get; set; }
}

public class EmployeeChecklist : OrgScopedEntity
{
    public long EmployeeChecklistId { get; set; }

    public long EmployeeId { get; set; }

    public ChecklistKind Kind { get; set; }

    public long? ChecklistTemplateId { get; set; }

    public ICollection<EmployeeChecklistItem> Items { get; set; } = new List<EmployeeChecklistItem>();
}

public class EmployeeChecklistItem : OrgScopedEntity
{
    public long EmployeeChecklistItemId { get; set; }

    public long EmployeeChecklistId { get; set; }

    public EmployeeChecklist Checklist { get; set; } = null!;

    [Required(ErrorMessage = "Title is required.")]
    [MaxLength(200, ErrorMessage = "Title cannot exceed 200 characters.")]
    public string Title { get; set; } = null!;

    public ChecklistOwnerRole OwnerRole { get; set; }

    public bool IsDone { get; set; }

    public DateOnly? DoneDate { get; set; }

    [MaxLength(500, ErrorMessage = "Remarks cannot exceed 500 characters.")]
    public string? Remarks { get; set; }
}

public class Separation : OrgScopedEntity
{
    public long SeparationId { get; set; }

    public long EmployeeId { get; set; }

    public SeparationKind Kind { get; set; }

    public DateOnly RequestDate { get; set; }

    public DateOnly LastWorkingDate { get; set; }

    public decimal NoticeShortfallDays { get; set; }

    public bool IsNoticeWaived { get; set; }

    [Required(ErrorMessage = "Reason is required.")]
    [MaxLength(500, ErrorMessage = "Reason cannot exceed 500 characters.")]
    public string Reason { get; set; } = null!;

    [MaxLength(2000, ErrorMessage = "Exit interview notes cannot exceed 2000 characters.")]
    public string? ExitInterviewNotes { get; set; }

    public SeparationStatus Status { get; set; }
}
