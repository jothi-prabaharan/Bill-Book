using System.ComponentModel.DataAnnotations;
using Shared.Kernel.Tenancy;
using WorkOrder.Entity.Enums;

namespace WorkOrder.Entity.TableEntities;

/// <summary>
/// A job to fix or service something (S6, TK-66), numbered from WRK. Named
/// <c>WorkOrderDocument</c> because <c>WorkOrder</c> is this service's
/// namespace; the table is <c>wrk.WorkOrders</c>.
/// </summary>
public class WorkOrderDocument : OrgScopedEntity
{
    public long WorkOrderId { get; set; }

    [Required(ErrorMessage = "Work order number is required.")]
    [MaxLength(30, ErrorMessage = "Work order number cannot exceed 30 characters.")]
    public string WorkOrderNo { get; set; } = null!;

    [Required(ErrorMessage = "Title is required.")]
    [MaxLength(200, ErrorMessage = "Title cannot exceed 200 characters.")]
    public string Title { get; set; } = null!;

    [MaxLength(2000, ErrorMessage = "Description cannot exceed 2000 characters.")]
    public string? Description { get; set; }

    public WorkOrderSource WorkOrderSource { get; set; } = WorkOrderSource.Complaint;

    public WorkOrderPriority Priority { get; set; } = WorkOrderPriority.Medium;

    /// <summary>Unenforced: <c>fac.FacilityAssets</c>. At least one of this and <see cref="SpaceId"/>.</summary>
    public long? FacilityAssetId { get; set; }

    /// <summary>Unenforced: <c>fac.Spaces</c>.</summary>
    public long? SpaceId { get; set; }

    public DateOnly ReportedDate { get; set; }

    public DateOnly? DueDate { get; set; }

    /// <summary>Unenforced: <c>hrm.Employees</c>, checked through Hrm.</summary>
    public long? AssignedEmployeeId { get; set; }

    /// <summary>Unenforced: <c>amc.AmcContracts</c>; the vendor attends instead of staff.</summary>
    public long? AmcContractId { get; set; }

    /// <summary>Unenforced: <c>ppm.PreventivePlans</c>.</summary>
    public long? PreventivePlanId { get; set; }

    /// <summary>
    /// What raised it, when another service did: <c>PPM:12:2026-10-01</c>,
    /// <c>AMC-VISIT:5</c>. Unique per branch, which makes raising idempotent.
    /// </summary>
    [MaxLength(60, ErrorMessage = "Source key cannot exceed 60 characters.")]
    public string? SourceKey { get; set; }

    public WorkOrderStatus WorkOrderStatus { get; set; } = WorkOrderStatus.Open;

    public DateOnly? CompletedDate { get; set; }

    public decimal LabourCost { get; set; }

    [MaxLength(500, ErrorMessage = "Cancel reason cannot exceed 500 characters.")]
    public string? CancelReason { get; set; }

    public ICollection<WorkOrderTask> Tasks { get; set; } = [];

    public ICollection<WorkOrderPart> Parts { get; set; } = [];
}

public class WorkOrderTask : OrgScopedEntity
{
    public long WorkOrderTaskId { get; set; }

    public long WorkOrderId { get; set; }

    [Required(ErrorMessage = "Description is required.")]
    [MaxLength(500, ErrorMessage = "Description cannot exceed 500 characters.")]
    public string Description { get; set; } = null!;

    public bool IsDone { get; set; }

    public int SortOrder { get; set; }
}

/// <summary>A part used, issued from Inventory through its guarded decrement at the cost Inventory gives.</summary>
public class WorkOrderPart : OrgScopedEntity
{
    public long WorkOrderPartId { get; set; }

    public long WorkOrderId { get; set; }

    /// <summary>Unenforced: <c>inv.Items</c>.</summary>
    public long ItemId { get; set; }

    /// <summary>Unenforced: <c>inv.Warehouses</c>.</summary>
    public long? WarehouseId { get; set; }

    public decimal Quantity { get; set; }

    /// <summary>From Inventory at issue.</summary>
    public decimal UnitCost { get; set; }

    [MaxLength(200, ErrorMessage = "Item name cannot exceed 200 characters.")]
    public string? ItemName { get; set; }
}
