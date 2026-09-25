using System.ComponentModel.DataAnnotations;
using WorkOrder.Entity.Enums;

namespace WorkOrder.Entity.Models;

// Requests and views for the wrk API (S6, TK-66).

public sealed record WorkOrderMessage(string Message);

public sealed class SaveWorkOrderRequest
{
    [Required(ErrorMessage = "Title is required.")]
    [MaxLength(200, ErrorMessage = "Title cannot exceed 200 characters.")]
    public string Title { get; set; } = null!;

    [MaxLength(2000, ErrorMessage = "Description cannot exceed 2000 characters.")]
    public string? Description { get; set; }

    public WorkOrderSource WorkOrderSource { get; set; } = WorkOrderSource.Complaint;

    public WorkOrderPriority Priority { get; set; } = WorkOrderPriority.Medium;

    public long? FacilityAssetId { get; set; }

    public long? SpaceId { get; set; }

    public DateOnly ReportedDate { get; set; }

    public DateOnly? DueDate { get; set; }

    public List<string> Tasks { get; set; } = [];
}

public sealed class WorkOrderActionRequest
{
    public WorkOrderAction Action { get; set; }

    /// <summary>For Assign.</summary>
    public long? EmployeeId { get; set; }

    /// <summary>For Complete.</summary>
    public DateOnly? CompletedDate { get; set; }

    /// <summary>For Complete.</summary>
    [Range(typeof(decimal), "0", "100000000", ErrorMessage = "Labour cost cannot be negative.")]
    public decimal? LabourCost { get; set; }

    /// <summary>For Cancel.</summary>
    [MaxLength(500, ErrorMessage = "Reason cannot exceed 500 characters.")]
    public string? Reason { get; set; }
}

public sealed class TaskTickRequest
{
    public bool IsDone { get; set; }
}

public sealed class IssuePartRequest
{
    [Range(1, long.MaxValue, ErrorMessage = "Choose an item.")]
    public long ItemId { get; set; }

    public long? WarehouseId { get; set; }

    [Range(typeof(decimal), "0.0001", "1000000", ErrorMessage = "The quantity must be more than zero.")]
    public decimal Quantity { get; set; }

    public DateOnly IssueDate { get; set; }
}

/// <summary>What another service asks to raise (TK-67 plans, TK-68 AMC visits). Idempotent on <see cref="SourceKey"/>.</summary>
public sealed class RaiseWorkOrderRequest
{
    public Guid CustomerId { get; set; }

    public Guid OrgId { get; set; }

    [Required(ErrorMessage = "Source key is required.")]
    [MaxLength(60, ErrorMessage = "Source key cannot exceed 60 characters.")]
    public string SourceKey { get; set; } = null!;

    [Required(ErrorMessage = "Title is required.")]
    [MaxLength(200, ErrorMessage = "Title cannot exceed 200 characters.")]
    public string Title { get; set; } = null!;

    public WorkOrderSource WorkOrderSource { get; set; } = WorkOrderSource.Preventive;

    public long? FacilityAssetId { get; set; }

    public long? SpaceId { get; set; }

    public DateOnly ReportedDate { get; set; }

    public DateOnly? DueDate { get; set; }

    public long? AssignedEmployeeId { get; set; }

    public long? PreventivePlanId { get; set; }

    public long? AmcContractId { get; set; }
}

public sealed class RaiseWorkOrderResponse
{
    public long WorkOrderId { get; set; }

    public string WorkOrderNo { get; set; } = null!;

    /// <summary>False when one with that source key already existed.</summary>
    public bool Created { get; set; }
}

public sealed class WorkOrderTaskView
{
    public long WorkOrderTaskId { get; set; }

    public string Description { get; set; } = null!;

    public bool IsDone { get; set; }
}

public sealed class WorkOrderPartView
{
    public long WorkOrderPartId { get; set; }

    public long ItemId { get; set; }

    public string? ItemName { get; set; }

    public long? WarehouseId { get; set; }

    public decimal Quantity { get; set; }

    public decimal UnitCost { get; set; }
}

public sealed class WorkOrderView
{
    public long WorkOrderId { get; set; }

    public string WorkOrderNo { get; set; } = null!;

    public string Title { get; set; } = null!;

    public string? Description { get; set; }

    public WorkOrderSource WorkOrderSource { get; set; }

    public WorkOrderPriority Priority { get; set; }

    public long? FacilityAssetId { get; set; }

    public long? SpaceId { get; set; }

    public DateOnly ReportedDate { get; set; }

    public DateOnly? DueDate { get; set; }

    public long? AssignedEmployeeId { get; set; }

    public long? AmcContractId { get; set; }

    public long? PreventivePlanId { get; set; }

    public WorkOrderStatus WorkOrderStatus { get; set; }

    public DateOnly? CompletedDate { get; set; }

    public decimal LabourCost { get; set; }

    public decimal PartsCost { get; set; }

    public string? CancelReason { get; set; }

    public List<WorkOrderTaskView> Tasks { get; set; } = [];

    public List<WorkOrderPartView> Parts { get; set; } = [];
}
