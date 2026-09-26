using System.ComponentModel.DataAnnotations;
using Shared.Kernel.Tenancy;

namespace Accounting.Entity.TableEntities;

/// <summary>A piece of a project that time is logged against (TK-104).</summary>
public class ProjectTask : OrgScopedEntity
{
    public long ProjectTaskId { get; set; }

    public long ProjectId { get; set; }

    [Required(ErrorMessage = "Task name is required.")]
    [MaxLength(150, ErrorMessage = "Task name cannot exceed 150 characters.")]
    public string TaskName { get; set; } = null!;

    /// <summary>The rate when the project bills by task.</summary>
    [Range(typeof(decimal), "0", "79228162514264337593543950335", ErrorMessage = "The hourly rate cannot be negative.")]
    public decimal? HourlyRate { get; set; }

    [Range(typeof(decimal), "0", "79228162514264337593543950335", ErrorMessage = "Budget hours cannot be negative.")]
    public decimal? BudgetHours { get; set; }

    public bool IsBillable { get; set; } = true;

    public bool IsActive { get; set; } = true;
}
