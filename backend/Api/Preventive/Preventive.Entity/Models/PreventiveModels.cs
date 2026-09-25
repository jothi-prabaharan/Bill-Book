using System.ComponentModel.DataAnnotations;
using Preventive.Entity.Enums;

namespace Preventive.Entity.Models;

// Requests and views for the ppm API (S7, TK-67).

public sealed record PreventiveMessage(string Message);

public sealed class SavePlanRequest
{
    [Required(ErrorMessage = "Name is required.")]
    [MaxLength(200, ErrorMessage = "Name cannot exceed 200 characters.")]
    public string Name { get; set; } = null!;

    public long? FacilityAssetId { get; set; }

    public long? SpaceId { get; set; }

    public Frequency Frequency { get; set; } = Frequency.Monthly;

    [Range(1, 100, ErrorMessage = "The interval must be between 1 and 100.")]
    public int Interval { get; set; } = 1;

    public DateOnly StartDate { get; set; }

    public DateOnly? EndDate { get; set; }

    [Range(0, 365, ErrorMessage = "Lead days must be between 0 and 365.")]
    public int LeadDays { get; set; }

    public long? DefaultAssigneeEmployeeId { get; set; }

    public bool IsActive { get; set; } = true;
}

public sealed class OccurrenceActionRequest
{
    /// <summary><c>Skipped</c> for a Scheduled occurrence, or <c>Done</c> for a Raised one.</summary>
    public OccurrenceStatus OccurrenceStatus { get; set; }
}

public sealed class PlanView
{
    public long PreventivePlanId { get; set; }

    public string Name { get; set; } = null!;

    public long? FacilityAssetId { get; set; }

    public long? SpaceId { get; set; }

    public Frequency Frequency { get; set; }

    public int Interval { get; set; }

    public DateOnly StartDate { get; set; }

    public DateOnly? EndDate { get; set; }

    public DateOnly NextDueDate { get; set; }

    public int LeadDays { get; set; }

    public long? DefaultAssigneeEmployeeId { get; set; }

    public bool IsActive { get; set; }
}

public sealed class OccurrenceView
{
    public long PreventiveOccurrenceId { get; set; }

    public long PreventivePlanId { get; set; }

    public string PlanName { get; set; } = null!;

    public DateOnly DueDate { get; set; }

    public long? WorkOrderId { get; set; }

    public string? WorkOrderNo { get; set; }

    public OccurrenceStatus OccurrenceStatus { get; set; }
}

public sealed class GenerationResult
{
    /// <summary>Occurrences added in this run.</summary>
    public int Generated { get; set; }

    /// <summary>Work orders raised in this run.</summary>
    public int Raised { get; set; }

    /// <summary>Occurrences whose work order could not be raised; the next run retries them.</summary>
    public int Failed { get; set; }
}
